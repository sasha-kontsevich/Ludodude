using UnityEngine;

/// <summary>
/// Вешается на подбираемый объект. Размер при переноске задаётся на ItemCarrier.
/// На объекте обычно нужен Collider2D (можно trigger) для обнаружения носителем.
/// </summary>
[DisallowMultipleComponent]
public class Item : MonoBehaviour
{
    [SerializeField] private int cost = 1;

    [Header("Подсветка подбора")]
    [SerializeField] private Color pickupOutlineColor = new Color(0.95f, 0.85f, 0.2f, 1f);

    [SerializeField] private float pickupOutlineWidth = 0.04f;

    [Tooltip("Запас за пределами bounds спрайтов (в мировых единицах).")]
    [SerializeField] private float pickupOutlinePadding = 0.02f;

    private LineRenderer _pickupOutline;
    private bool _pickupHighlight;
    private Vector3 _localScaleBeforeCarry = Vector3.one;

    public int Cost => cost;

    public bool IsCarried { get; private set; }

    public ItemCarrier Carrier { get; private set; }

    private void Awake()
    {
        if (GetComponentsInChildren<Collider2D>(true).Length == 0)
        {
            Debug.LogError(
                "Item: нет Collider2D на этом объекте и детях — триггер подбора не пересекается с предметом, подбор и обводка не работают. Добавьте BoxCollider2D или PolygonCollider2D.",
                this);
        }
    }

    private void LateUpdate()
    {
        if (_pickupHighlight && _pickupOutline != null && _pickupOutline.enabled)
            UpdatePickupOutlineGeometry();
    }

    /// <summary>Обводка «можно подобрать» — выставляет ItemCarrier в LateUpdate.</summary>
    public void SetPickupHighlight(bool enabled)
    {
        if (_pickupHighlight == enabled)
            return;

        _pickupHighlight = enabled;

        if (enabled)
            EnsurePickupOutline();
        else if (_pickupOutline != null)
            _pickupOutline.enabled = false;
    }

    private void EnsurePickupOutline()
    {
        if (_pickupOutline == null)
        {
            var go = new GameObject("PickupOutline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            _pickupOutline = go.AddComponent<LineRenderer>();
            _pickupOutline.loop = true;
            _pickupOutline.positionCount = 5;
            _pickupOutline.useWorldSpace = true;
            _pickupOutline.textureMode = LineTextureMode.Stretch;
            _pickupOutline.numCornerVertices = 0;
            _pickupOutline.numCapVertices = 0;
            _pickupOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pickupOutline.receiveShadows = false;

            Shader lineShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (lineShader != null)
                _pickupOutline.material = new Material(lineShader);
        }

        _pickupOutline.startWidth = pickupOutlineWidth;
        _pickupOutline.endWidth = pickupOutlineWidth;
        _pickupOutline.startColor = pickupOutlineColor;
        _pickupOutline.endColor = pickupOutlineColor;

        var srs = GetComponentsInChildren<SpriteRenderer>(false);
        if (srs.Length > 0)
        {
            int maxOrder = int.MinValue;
            int layerId = srs[0].sortingLayerID;
            foreach (var sr in srs)
            {
                maxOrder = Mathf.Max(maxOrder, sr.sortingOrder);
                layerId = sr.sortingLayerID;
            }

            _pickupOutline.sortingLayerID = layerId;
            _pickupOutline.sortingOrder = maxOrder + 1;
        }

        UpdatePickupOutlineGeometry();
        _pickupOutline.enabled = true;
    }

    private void UpdatePickupOutlineGeometry()
    {
        if (_pickupOutline == null)
            return;

        Bounds b = GetWorldVisualBounds();
        b.Expand(pickupOutlinePadding * 2f);

        float z = transform.position.z;
        Vector3 p0 = new Vector3(b.min.x, b.min.y, z);
        Vector3 p1 = new Vector3(b.max.x, b.min.y, z);
        Vector3 p2 = new Vector3(b.max.x, b.max.y, z);
        Vector3 p3 = new Vector3(b.min.x, b.max.y, z);

        _pickupOutline.SetPosition(0, p0);
        _pickupOutline.SetPosition(1, p1);
        _pickupOutline.SetPosition(2, p2);
        _pickupOutline.SetPosition(3, p3);
        _pickupOutline.SetPosition(4, p0);
    }

    private Bounds GetWorldVisualBounds()
    {
        var srs = GetComponentsInChildren<SpriteRenderer>(false);
        if (srs.Length == 0)
        {
            var col = GetComponentInChildren<Collider2D>();
            if (col != null)
                return col.bounds;
            return new Bounds(transform.position, Vector3.one * 0.5f);
        }

        Bounds b = srs[0].bounds;
        for (int i = 1; i < srs.Length; i++)
            b.Encapsulate(srs[i].bounds);
        return b;
    }

    internal void SetCarried(ItemCarrier carrier, bool carried)
    {
        Carrier = carried ? carrier : null;
        IsCarried = carried;

        if (carried)
        {
            SetPickupHighlight(false);
            _localScaleBeforeCarry = transform.localScale;
            if (carrier != null)
            {
                float h = GetWorldVisualBounds().size.y;
                if (h > 1e-4f)
                {
                    float mult = carrier.CarriedItemWorldHeight / h;
                    Vector3 ls = _localScaleBeforeCarry;
                    transform.localScale = new Vector3(ls.x * mult, ls.y * mult, ls.z);
                }
            }
        }
        else
            transform.localScale = _localScaleBeforeCarry;

        var cols = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
            c.enabled = !carried;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (carried)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }
}
