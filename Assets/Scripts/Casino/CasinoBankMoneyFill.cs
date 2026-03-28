using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Заполняет <see cref="moneysRoot"/> купюрами по <see cref="GameManager.CasinoDeposit"/> (лог-шкала).
/// Область — <see cref="fillAreaCollider"/>; куча растёт снизу вверх: при малом числе купюр занят только низ,
/// при count → maxMoneyInstances вершина доходит до верха коллайдера. Горка — сужение по X к верху кучи.
/// </summary>
public class CasinoBankMoneyFill : MonoBehaviour
{
    [SerializeField] private Transform moneysRoot;

    [SerializeField] private GameObject moneyPrefab;

    [Tooltip("Axis-aligned bounds этого коллайдера задают область кучи (низ → верх по Y).")]
    [SerializeField] private Collider2D fillAreaCollider;

    [Tooltip("Максимум купюр при полной шкале.")]
    [SerializeField] private int maxMoneyInstances = 24;

    [Tooltip("При таком депозите лог-шкала даёт полное заполнение.")]
    [SerializeField] private float depositForFullVisual = 500f;

    [Tooltip("Если депозит > 0, показать хотя бы одну купюру.")]
    [SerializeField] private bool showAtLeastOneIfAnyDeposit = true;

    [SerializeField] private bool clearExistingChildrenOnAwake = true;

    [Header("Горка")]
    [Tooltip("Насколько уже «верхушка» относительно низа: 0.3 ≈ узкая кромка сверху.")]
    [SerializeField] [Range(0.05f, 1f)] private float moundTopWidthFactor = 0.35f;

    [Tooltip("Случайный сдвиг по X (мир), сильнее у основания.")]
    [SerializeField] private float horizontalJitter = 0.035f;

    [SerializeField] private Vector2 rotationRangeDeg = new Vector2(-52f, 52f);

    private readonly List<GameObject> _spawned = new List<GameObject>(32);

    private int _lastTargetCount = -1;

    private void Awake()
    {
        if (moneysRoot == null)
        {
            var t = transform.Find("Visuals/Moneys");
            if (t != null)
                moneysRoot = t;
        }

        if (fillAreaCollider == null)
            Debug.LogWarning("CasinoBankMoneyFill: назначьте fillAreaCollider (область кучи).", this);

        if (moneysRoot == null || moneyPrefab == null)
            return;

        if (clearExistingChildrenOnAwake)
        {
            for (int i = moneysRoot.childCount - 1; i >= 0; i--)
                Destroy(moneysRoot.GetChild(i).gameObject);
        }
    }

    private void LateUpdate()
    {
        if (moneysRoot == null || moneyPrefab == null || fillAreaCollider == null)
            return;

        var gm = GameManager.Instance;
        if (gm == null)
            return;

        int target = ComputeVisibleCount(gm.CasinoDeposit);
        if (target == _lastTargetCount)
            return;

        _lastTargetCount = target;
        SyncSpawnedCount(target);
    }

    private int ComputeVisibleCount(float deposit)
    {
        deposit = Mathf.Max(0f, deposit);
        float full = Mathf.Max(1f, depositForFullVisual);
        float t = Mathf.Log(1f + deposit) / Mathf.Log(1f + full);
        t = Mathf.Clamp01(t);
        int n = Mathf.RoundToInt(t * maxMoneyInstances);

        if (showAtLeastOneIfAnyDeposit && deposit > 0f)
            n = Mathf.Max(1, n);

        return Mathf.Clamp(n, 0, maxMoneyInstances);
    }

    private void SyncSpawnedCount(int target)
    {
        while (_spawned.Count < target)
        {
            var go = Instantiate(moneyPrefab, moneysRoot);
            _spawned.Add(go);
        }

        while (_spawned.Count > target)
        {
            int last = _spawned.Count - 1;
            var go = _spawned[last];
            _spawned.RemoveAt(last);
            if (go != null)
                Destroy(go);
        }

        int n = _spawned.Count;
        for (int i = 0; i < n; i++)
        {
            if (_spawned[i] != null)
                ApplyLayout(i, n, _spawned[i].transform);
        }
    }

    private void ApplyLayout(int index, int count, Transform t)
    {
        Bounds b = fillAreaCollider.bounds;

        float fullH = b.max.y - b.min.y;
        float pileFill = Mathf.Min(1f, (float)count / Mathf.Max(1, maxMoneyInstances));
        float pileTop = b.min.y + fullH * pileFill;
        float pileH = Mathf.Max(1e-5f, pileTop - b.min.y);

        // Слои снизу вверх только внутри [дно … текущая вершина кучи], а не на всю высоту коллайдера.
        float y = b.min.y + (index + 0.5f) * pileH / Mathf.Max(count, 1);

        float tInPile = Mathf.Clamp01((y - b.min.y) / pileH);
        float centerX = (b.min.x + b.max.x) * 0.5f;
        float halfWidth = (b.max.x - b.min.x) * 0.5f;
        float widthFactor = Mathf.Lerp(1f, moundTopWidthFactor, tInPile);
        float halfSpan = halfWidth * widthFactor;
        float ux = Hash01(index, 11);
        float x = centerX + Mathf.Lerp(-halfSpan, halfSpan, ux);
        float jitter = horizontalJitter * (1f - tInPile * 0.65f);
        x += (Hash01(index, 41) - 0.5f) * 2f * jitter;

        float uz = Hash01(index, 19);
        float z = Mathf.Lerp(b.min.z, b.max.z, uz);

        t.position = new Vector3(x, y, z);

        float zDeg = Mathf.Lerp(rotationRangeDeg.x, rotationRangeDeg.y, Hash01(index, 23));
        var euler = t.localEulerAngles;
        euler.z = zDeg;
        t.localEulerAngles = euler;
    }

    private static float Hash01(int index, int salt)
    {
        float v = Mathf.Sin(index * 12.9898f + salt * 78.233f + salt * 0.001f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }
}
