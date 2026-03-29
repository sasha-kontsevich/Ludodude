using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Подбирает предметы с компонентом Item, складывает их над головой (один над другим).
/// Лимит стоимости берётся из CharacterStats.MaxCarryCost (или из legacy-поля для старых префабов).
/// </summary>
public class ItemCarrier : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Родитель для стопки: разместите дочерний объект над головой персонажа. Если пусто — создаётся точка над корнем.")]
    [SerializeField] private Transform carryAnchor;

    [Tooltip("Смещение якоря ношения от корня, если carryAnchor не задан.")]
    [SerializeField] private Vector2 carryAnchorOffset = new Vector2(0f, 0.85f);

    [FormerlySerializedAs("maxCarryCost")]
    [Tooltip("Устаревшее поле для миграции. Используется, только если на объекте нет CharacterStats.")]
    [SerializeField] private int legacyMaxCarryCost = 10;

    [Tooltip("Коллайдер-триггер зоны подбора (дочерний CircleCollider2D и т.д.). Пусто — первый триггер на этом объекте или на детях.")]
    [SerializeField] private Collider2D pickupTrigger;

    [Tooltip("Смещение от якоря стопки (CarryStack / carryAnchor), куда ставится предмет при дропе.")]
    [SerializeField] private Vector2 dropOffset = new Vector2(0f, 0f);

    [Tooltip("Высота предмета в мировых единицах, к которой масштабируется модель, пока её несут (равномерный scale по X и Y).")]
    [SerializeField] private float carriedItemWorldHeight = 0.45f;

    [Tooltip("Расстояние между центрами предметов в стопке как доля высоты (меньше — плотнее).")]
    [SerializeField] [Range(0.2f, 1f)] private float stackSpacingFactor = 0.52f;

    [Header("Анимация подбора")]
    [Tooltip("Длительность полёта предмета по дуге в слот стопки.")]
    [SerializeField] private float pickupArcDuration = 0.35f;

    [Tooltip("Высота дуги в мировых единицах (пик посередине пути).")]
    [SerializeField] private float pickupArcHeight = 0.35f;

    [Header("Покачивание стопки при ходьбе")]
    [SerializeField] private float wobbleFrequency = 11f;

    [SerializeField] private float wobblePositionAmplitude = 0.038f;

    [Tooltip("Скорость носителя, при которой покачивание максимальное (м/с).")]
    [SerializeField] private float wobbleFullSpeed = 2.2f;

    [Header("Подсказка при подборе")]
    [SerializeField] private bool pickupTooltipEnabled = true;

    [Tooltip("{0} — имя объекта, {1} — стоимость (Item.Cost).")]
    [SerializeField] private string pickupTooltipTemplate = "Подобрать: {0} (стоимость: {1})";
    [SerializeField] private string pickupFallbackKeyLabel = "F";
    [Tooltip("{0} — количество предметов в стопке.")]
    [SerializeField] private string dropTooltipTemplate = "Бросить предмет ({0} шт.)";
    [SerializeField] private string dropFallbackKeyLabel = "Q";

    private readonly List<Item> _carried = new List<Item>(8);
    private readonly HashSet<Item> _inRange = new HashSet<Item>();

    private InputActionMap _playerMap;
    private InputAction _interactAction;
    private InputAction _pickupAction;
    private InputAction _dropAction;
    private Transform _stackRoot;
    private Item _pickupOutlineTarget;
    private Rigidbody2D _rb;
    private TopDownPlayerController _player;
    private CharacterStats _stats;

    private readonly Dictionary<Item, PickupAnimState> _pickupAnim = new Dictionary<Item, PickupAnimState>(8);

    private struct PickupAnimState
    {
        public float StartTime;
        public float Duration;
        public Vector3 WorldStart;
        public int StackIndex;
    }

    public IReadOnlyList<Item> CarriedItems => _carried;

    public int CurrentCarriedCost { get; private set; }

    public float CarriedItemWorldHeight => Mathf.Max(0.01f, carriedItemWorldHeight);

    private void Awake()
    {
        if (pickupTrigger == null)
            pickupTrigger = GetPickupTrigger();

        if (pickupTrigger != null && !pickupTrigger.isTrigger)
            Debug.LogWarning("ItemCarrier: коллайдер подбора лучше сделать Is Trigger = true.", pickupTrigger);
        else if (pickupTrigger == null)
            Debug.LogWarning("ItemCarrier: не найден триггер-коллайдер (дочерний CircleCollider2D с Is Trigger). Подбор по зоне не заработает.", this);

        EnsureStackRoot();

        _rb = GetComponent<Rigidbody2D>();
        _player = GetComponent<TopDownPlayerController>();
        _stats = GetComponent<CharacterStats>();

        if (inputActions != null)
        {
            _playerMap = inputActions.FindActionMap("Player");
            _interactAction = _playerMap?.FindAction("Interact");
            _pickupAction = _playerMap?.FindAction("Crouch");
            _dropAction = _playerMap?.FindAction("Attack");
        }
    }

    private Collider2D GetPickupTrigger()
    {
        var colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in colliders)
        {
            if (c.isTrigger)
                return c;
        }

        return null;
    }

    private void EnsureStackRoot()
    {
        if (carryAnchor != null)
        {
            _stackRoot = carryAnchor;
            return;
        }

        var go = new GameObject("CarryStack");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = carryAnchorOffset;
        _stackRoot = go.transform;
    }

    private void OnEnable()
    {
        _playerMap?.Enable();
        // Interact в проекте с интеракцией Hold: при коротком нажатии есть started, а performed — только после удержания.
        if (_interactAction != null)
            _interactAction.started += OnInteractStarted;
        if (_pickupAction != null)
            _pickupAction.started += OnPickupStarted;
        if (_dropAction != null)
            _dropAction.started += OnDropStarted;
    }

    private void OnDisable()
    {
        if (_interactAction != null)
            _interactAction.started -= OnInteractStarted;
        if (_pickupAction != null)
            _pickupAction.started -= OnPickupStarted;
        if (_dropAction != null)
            _dropAction.started -= OnDropStarted;
        _playerMap?.Disable();
        ClearPickupOutline();
        if (pickupTooltipEnabled && TooltipManager.Instance != null)
            TooltipManager.Instance.Hide();
    }

    private void LateUpdate()
    {
        Item best = FindBestPickupableInRange();
        if (best != _pickupOutlineTarget)
        {
            _pickupOutlineTarget?.SetPickupHighlight(false);
            _pickupOutlineTarget = best;
            _pickupOutlineTarget?.SetPickupHighlight(true);
        }

        SyncPickupTooltip(best);

        UpdatePickupArcAnimations();
        if (_carried.Count > 0)
            RefreshStackLayout();
    }

    private void SyncPickupTooltip(Item best)
    {
        var tm = TooltipManager.Instance;
        if (tm == null)
            return;

        if (!pickupTooltipEnabled)
        {
            if (best == null)
                tm.Hide();
            return;
        }

        if (_carried.Count > 0)
        {
            string msg = string.Format(dropTooltipTemplate, _carried.Count);
            msg = AppendActionHint(msg, dropFallbackKeyLabel);
            tm.Show(msg);
            return;
        }

        if (best != null)
        {
            string msg = string.Format(pickupTooltipTemplate, best.gameObject.name, best.Cost);
            msg = AppendActionHint(msg, pickupFallbackKeyLabel);
            tm.Show(msg);
        }
        else
            tm.Hide();
    }

    private static string AppendActionHint(string baseText, string actionLabel)
    {
        if (string.IsNullOrWhiteSpace(actionLabel))
            return baseText;
        if (string.IsNullOrWhiteSpace(baseText))
            return $"[{actionLabel}]";
        if (baseText.Contains($"[{actionLabel}]") || baseText.Contains($"({actionLabel})"))
            return baseText;
        return $"{baseText} [{actionLabel}]";
    }

    private void ClearPickupOutline()
    {
        _pickupOutlineTarget?.SetPickupHighlight(false);
        _pickupOutlineTarget = null;
    }

    private void OnInteractStarted(InputAction.CallbackContext _)
    {
        if (DepositMachine.TryDeposit(this))
            return;
    }

    private void OnPickupStarted(InputAction.CallbackContext _)
    {
        Item best = FindBestPickupableInRange();
        if (best != null)
            PickUp(best);
    }

    private void OnDropStarted(InputAction.CallbackContext _)
    {
        DropLastCarriedItem();
    }

    /// <summary>После скрытия подсказки депозита — восстановить подсказку подбора, если рядом есть предмет.</summary>
    public void RefreshTooltipAfterDepositZone()
    {
        SyncPickupTooltip(FindBestPickupableInRange());
    }

    /// <summary>Снимает все предметы со стопки для сдачи в депозит (без дропа на землю).</summary>
    public void ReleaseAllItemsForDeposit(List<Item> into)
    {
        if (into == null)
            return;

        into.Clear();
        for (int i = 0; i < _carried.Count; i++)
        {
            var item = _carried[i];
            if (item != null)
                into.Add(item);
        }

        _carried.Clear();
        CurrentCarriedCost = 0;

        foreach (var item in into)
        {
            if (item == null)
                continue;
            if (item == _pickupOutlineTarget)
                _pickupOutlineTarget = null;
            _pickupAnim.Remove(item);
            item.transform.SetParent(null, true);
            item.DetachFromCarrierForDeposit();
        }
    }

    /// <summary>Снимает верхний предмет в стопке и кладёт рядом с носителем.</summary>
    public bool DropLastCarriedItem()
    {
        if (_carried.Count == 0)
            return false;

        int idx = _carried.Count - 1;
        Item item = _carried[idx];
        if (item == null)
        {
            _carried.RemoveAt(idx);
            return false;
        }

        _carried.RemoveAt(idx);
        CurrentCarriedCost -= item.Cost;

        if (item == _pickupOutlineTarget)
            _pickupOutlineTarget = null;

        _pickupAnim.Remove(item);

        item.transform.SetParent(null, true);
        Vector3 pos = _stackRoot.position + (Vector3)dropOffset;
        pos.z = _stackRoot.position.z;
        item.transform.position = pos;

        var rb = item.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        item.SetCarried(null, false);
        RefreshStackLayout();
        return true;
    }

    /// <summary>Ближайший предмет в зоне, который ещё можно подобрать по лимиту стоимости (как при Interact).</summary>
    public Item FindBestPickupableInRange()
    {
        Item best = null;
        float bestSqr = float.MaxValue;
        Vector2 p = transform.position;

        foreach (var item in _inRange)
        {
            if (item == null || item.IsCarried)
                continue;
            if (!CanAddCost(item.Cost))
                continue;

            float sqr = ((Vector2)item.transform.position - p).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = item;
            }
        }

        return best;
    }

    public bool CanAddCost(int itemCost)
    {
        int maxCarryCost = _stats != null ? _stats.MaxCarryCost : legacyMaxCarryCost;
        if (maxCarryCost <= 0)
            return true;
        return CurrentCarriedCost + itemCost <= maxCarryCost;
    }

    public float GetSellPriceMultiplier()
    {
        if (_stats == null)
            return 1f;

        return Mathf.Max(0f, _stats.SellPriceMultiplier);
    }

    public bool PickUp(Item item)
    {
        if (item == null || item.IsCarried)
            return false;
        if (!CanAddCost(item.Cost))
            return false;

        if (item == _pickupOutlineTarget)
            _pickupOutlineTarget = null;

        item.transform.SetParent(_stackRoot, true);
        _carried.Add(item);
        CurrentCarriedCost += item.Cost;
        item.SetCarried(this, true);

        int stackIndex = _carried.Count - 1;
        float duration = Mathf.Max(0.01f, pickupArcDuration);
        if (pickupArcHeight > 0.0001f && duration > 0.0001f)
        {
            _pickupAnim[item] = new PickupAnimState
            {
                StartTime = Time.time,
                Duration = duration,
                WorldStart = item.transform.position,
                StackIndex = stackIndex
            };
        }

        RefreshStackLayout();
        return true;
    }

    private Vector3 GetStackSlotBaseLocal(int index)
    {
        float step = CarriedItemWorldHeight * stackSpacingFactor;
        float y = 0f;
        for (int i = 0; i < _carried.Count; i++)
        {
            float half = step * 0.5f;
            y += half;
            if (i == index)
                return new Vector3(0f, y, 0f);
            y += half;
        }

        return Vector3.zero;
    }

    private void UpdatePickupArcAnimations()
    {
        if (_pickupAnim.Count == 0)
            return;

        var done = new List<Item>(4);
        foreach (var kv in _pickupAnim)
        {
            var item = kv.Key;
            var st = kv.Value;
            if (item == null || !item.IsCarried || item.Carrier != this)
            {
                done.Add(kv.Key);
                continue;
            }

            float elapsed = Time.time - st.StartTime;
            float u = Mathf.Clamp01(elapsed / Mathf.Max(1e-5f, st.Duration));
            float s = u * u * (3f - 2f * u);
            Vector3 worldEnd = _stackRoot.TransformPoint(GetStackSlotBaseLocal(st.StackIndex));
            Vector3 pos = Vector3.Lerp(st.WorldStart, worldEnd, s);
            pos += Vector3.up * (pickupArcHeight * 4f * u * (1f - u));
            item.transform.position = pos;

            if (u >= 1f)
                done.Add(kv.Key);
        }

        for (int i = 0; i < done.Count; i++)
            _pickupAnim.Remove(done[i]);
    }

    private void RefreshStackLayout()
    {
        float moveBlend = GetCarryMoveBlend();
        float t = Time.time * wobbleFrequency;

        for (int i = 0; i < _carried.Count; i++)
        {
            var it = _carried[i];
            if (it == null)
                continue;

            if (_pickupAnim.ContainsKey(it))
                continue;

            Vector3 basePos = GetStackSlotBaseLocal(i);
            it.transform.localPosition = basePos + GetStackWobbleOffset(i, t, moveBlend);
        }
    }

    private float GetCarryMoveBlend()
    {
        float blend = 0f;
        if (_rb != null)
            blend = Mathf.Clamp01(_rb.linearVelocity.magnitude / Mathf.Max(0.05f, wobbleFullSpeed));
        if (blend < 0.08f && _player != null)
            blend = Mathf.Max(blend, Mathf.Clamp01(_player.MoveInput.magnitude));
        return blend;
    }

    private Vector3 GetStackWobbleOffset(int index, float t, float moveBlend)
    {
        if (moveBlend < 0.001f)
            return Vector3.zero;

        float ix = index * 0.71f;
        float iy = index * 0.37f;
        float a = wobblePositionAmplitude * moveBlend;
        float x = Mathf.Sin(t + ix) * a;
        float y = Mathf.Sin(t * 1.13f + iy) * a * 0.62f;
        return new Vector3(x, y, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var item = other.GetComponentInParent<Item>();
        if (item != null && !item.IsCarried)
            _inRange.Add(item);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var item = other.GetComponentInParent<Item>();
        if (item != null)
            _inRange.Remove(item);
    }
}
