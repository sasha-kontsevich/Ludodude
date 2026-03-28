using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Зона сдачи предметов: подсказка при входе с полной стопкой, Interact снимает всё и зачисляет сумму в <see cref="GameManager.CasinoDeposit"/>.
/// </summary>
[DefaultExecutionOrder(100)]
public class DepositMachine : MonoBehaviour
{
    public static DepositMachine ActiveForPlayer { get; private set; }

    [SerializeField] private Collider2D zoneTrigger;

    [Tooltip("Куда летят предметы. Пусто — этот transform.")]
    [SerializeField] private Transform attractor;

    [Tooltip("{0} — количество предметов, {1} — сумма стоимости.")]
    [SerializeField] private string depositTooltipTemplate = "Сдать предметы ({0} шт., сумма: {1})";

    [Tooltip("Задержка между стартом полёта следующего предмета (предыдущий может ещё лететь).")]
    [SerializeField] private float delayBetweenItems = 0.12f;

    [Tooltip("Длительность полёта одного предмета к аттрактору.")]
    [SerializeField] private float flyDuration = 0.4f;

    [Tooltip("Высота дуги «подкидывания» в мировых единицах (пик по середине пути).")]
    [SerializeField] private float tossArcHeight = 0.4f;

    private ItemCarrier _carrierInZone;
    private bool _depositTooltipShown;

    private readonly List<Item> _depositBuffer = new List<Item>(16);

    private void Reset()
    {
        if (zoneTrigger != null)
            zoneTrigger.isTrigger = true;
    }

    private void Awake()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<Collider2D>();

        if (zoneTrigger != null && zoneTrigger.gameObject != gameObject)
        {
            var relay = zoneTrigger.GetComponent<DepositMachineTriggerRelay>();
            if (relay == null)
                relay = zoneTrigger.gameObject.AddComponent<DepositMachineTriggerRelay>();
            relay.Init(this);
        }
    }

    private void OnDestroy()
    {
        if (ActiveForPlayer == this)
            ActiveForPlayer = null;
    }

    private void LateUpdate()
    {
        var tm = TooltipManager.Instance;
        bool wantDepositHint = _carrierInZone != null && _carrierInZone.CarriedItems.Count > 0;

        if (wantDepositHint)
        {
            if (tm != null)
            {
                int count = _carrierInZone.CarriedItems.Count;
                int sum = 0;
                foreach (var i in _carrierInZone.CarriedItems)
                {
                    if (i != null)
                        sum += i.Cost;
                }

                tm.Show(string.Format(depositTooltipTemplate, count, sum));
            }

            _depositTooltipShown = true;
        }
        else if (_depositTooltipShown)
        {
            tm?.Hide();
            _carrierInZone?.RefreshTooltipAfterDepositZone();
            _depositTooltipShown = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleTriggerEnter(other);

    private void OnTriggerExit2D(Collider2D other) => HandleTriggerExit(other);

    internal void HandleTriggerEnter(Collider2D other)
    {
        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null)
            return;

        var carrier = player.GetComponent<ItemCarrier>();
        if (carrier == null)
            return;

        ActiveForPlayer = this;
        _carrierInZone = carrier;
    }

    internal void HandleTriggerExit(Collider2D other)
    {
        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null)
            return;

        var carrier = player.GetComponent<ItemCarrier>();
        if (carrier != _carrierInZone)
            return;

        var carrierLeaving = _carrierInZone;
        _carrierInZone = null;
        if (ActiveForPlayer == this)
            ActiveForPlayer = null;

        if (_depositTooltipShown && TooltipManager.Instance != null)
        {
            TooltipManager.Instance.Hide();
            carrierLeaving?.RefreshTooltipAfterDepositZone();
            _depositTooltipShown = false;
        }
    }

    /// <summary>Возвращает true, если депозит выполнен (Interact не должен обрабатываться дальше).</summary>
    public static bool TryDeposit(ItemCarrier carrier)
    {
        if (carrier == null || ActiveForPlayer == null || ActiveForPlayer._carrierInZone != carrier)
            return false;
        if (carrier.CarriedItems.Count == 0)
            return false;

        var gm = GameManager.Instance;
        if (gm == null)
            return false;

        int sum = 0;
        foreach (var i in carrier.CarriedItems)
        {
            if (i != null)
                sum += i.Cost;
        }

        gm.CasinoDeposit += sum;

        var machine = ActiveForPlayer;
        machine._depositBuffer.Clear();
        carrier.ReleaseAllItemsForDeposit(machine._depositBuffer);

        if (machine._depositBuffer.Count == 0)
            return true;

        var flying = new List<Item>(machine._depositBuffer);
        machine._depositBuffer.Clear();
        machine.StartCoroutine(machine.FlyAndDestroyItems(flying));
        return true;
    }

    private IEnumerator FlyAndDestroyItems(List<Item> items)
    {
        Vector3 end = attractor != null ? attractor.position : transform.position;
        float gap = Mathf.Max(0f, delayBetweenItems);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null)
                StartCoroutine(FlyOneItemAndDestroy(item, end));

            if (i < items.Count - 1)
                yield return new WaitForSeconds(gap);
        }
    }

    private IEnumerator FlyOneItemAndDestroy(Item item, Vector3 end)
    {
        if (item == null)
            yield break;

        float duration = Mathf.Max(0.05f, flyDuration);
        Vector3 start = item.transform.position;
        float t = 0f;
        while (t < duration)
        {
            if (item == null)
                yield break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float s = u * u * (3f - 2f * u);
            Vector3 basePos = Vector3.Lerp(start, end, s);
            float arc = 4f * tossArcHeight * u * (1f - u);
            item.transform.position = basePos + Vector3.up * arc;
            yield return null;
        }

        if (item != null)
            Destroy(item.gameObject);
    }
}

/// <summary>Вешается на объект с коллайдером зоны, если <see cref="DepositMachine"/> на родителе.</summary>
public class DepositMachineTriggerRelay : MonoBehaviour
{
    private DepositMachine _machine;

    public void Init(DepositMachine machine) => _machine = machine;

    private void OnTriggerEnter2D(Collider2D other) => _machine?.HandleTriggerEnter(other);

    private void OnTriggerExit2D(Collider2D other) => _machine?.HandleTriggerExit(other);
}
