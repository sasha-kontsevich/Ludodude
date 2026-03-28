using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Подбирает предметы с компонентом Item, складывает их над головой (один над другим).
/// Суммарная стоимость не превышает maxCarryCost (если maxCarryCost &gt; 0).
/// </summary>
public class ItemCarrier : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Родитель для стопки: разместите дочерний объект над головой персонажа. Если пусто — создаётся точка над корнем.")]
    [SerializeField] private Transform carryAnchor;

    [Tooltip("Смещение якоря ношения от корня, если carryAnchor не задан.")]
    [SerializeField] private Vector2 carryAnchorOffset = new Vector2(0f, 0.85f);

    [Tooltip("Максимальная суммарная стоимость предметов. 0 или меньше — без лимита по стоимости.")]
    [SerializeField] private int maxCarryCost = 10;

    [Tooltip("Коллайдер-триггер зоны подбора (дочерний CircleCollider2D и т.д.). Пусто — первый триггер на этом объекте или на детях.")]
    [SerializeField] private Collider2D pickupTrigger;

    [Tooltip("Смещение от якоря стопки (CarryStack / carryAnchor), куда ставится предмет при дропе.")]
    [SerializeField] private Vector2 dropOffset = new Vector2(0f, 0f);

    [Tooltip("Высота предмета в мировых единицах, к которой масштабируется модель, пока её несут (равномерный scale по X и Y).")]
    [SerializeField] private float carriedItemWorldHeight = 0.45f;

    private readonly List<Item> _carried = new List<Item>(8);
    private readonly HashSet<Item> _inRange = new HashSet<Item>();

    private InputActionMap _playerMap;
    private InputAction _interactAction;
    private Transform _stackRoot;
    private Item _pickupOutlineTarget;

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

        if (inputActions != null)
        {
            _playerMap = inputActions.FindActionMap("Player");
            _interactAction = _playerMap?.FindAction("Interact");
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
    }

    private void OnDisable()
    {
        if (_interactAction != null)
            _interactAction.started -= OnInteractStarted;
        _playerMap?.Disable();
        ClearPickupOutline();
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
    }

    private void ClearPickupOutline()
    {
        _pickupOutlineTarget?.SetPickupHighlight(false);
        _pickupOutlineTarget = null;
    }

    private void OnInteractStarted(InputAction.CallbackContext _)
    {
        Item best = FindBestPickupableInRange();
        if (best != null)
        {
            PickUp(best);
            return;
        }

        DropLastCarriedItem();
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
        if (maxCarryCost <= 0)
            return true;
        return CurrentCarriedCost + itemCost <= maxCarryCost;
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
        RefreshStackLayout();
        return true;
    }

    private void RefreshStackLayout()
    {
        float y = 0f;
        for (int i = 0; i < _carried.Count; i++)
        {
            var it = _carried[i];
            if (it == null)
                continue;

            float half = carriedItemWorldHeight * 0.5f;
            y += half;
            it.transform.localPosition = new Vector3(0f, y, 0f);
            y += half;
        }
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
