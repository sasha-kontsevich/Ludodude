using UnityEngine;

/// <summary>
/// Здание: спавнит префаб интерьера далеко от карты и телепортирует игрока при входе в коллайдер двери.
/// Коллайдер двери — на этом же объекте или на дочернем (назначьте ссылку).
/// </summary>
public class Building : MonoBehaviour
{
    [SerializeField] private Collider2D doorTrigger;
    [SerializeField] private GameObject interiorPrefab;

    [Tooltip("Если задано — позиция интерьера вручную. Иначе слот выдаётся автоматически (сетка далеко от центра карты).")]
    [SerializeField] private bool useManualInteriorPosition;

    [SerializeField] private Vector2 manualInteriorSpawnPosition;

    [Tooltip("Используется только для первого вызова InteriorSpawnLayout.Configure в этой сессии (если в сцене нет InteriorSpawnSettings). Задайте одинаковые значения на всех зданиях или повесьте InteriorSpawnSettings.")]
    [SerializeField] private Vector2 fallbackRegionAnchor = new Vector2(50_000f, 50_000f);

    [SerializeField] private int fallbackGridColumns = 32;
    [SerializeField] private float fallbackCellSize = 2048f;

    [Tooltip("Имя дочернего объекта в префабе — куда телепортировать игрока. Пусто = корень префаба.")]
    [SerializeField] private string interiorEntranceChildName;

    private GameObject _interiorInstance;
    private Transform _teleportDestination;

    private void Reset()
    {
        if (doorTrigger != null)
            doorTrigger.isTrigger = true;
    }

    private void Awake()
    {
        if (doorTrigger == null)
            doorTrigger = GetComponent<Collider2D>();

        if (doorTrigger != null && doorTrigger.gameObject != gameObject)
        {
            var relay = doorTrigger.GetComponent<BuildingDoorRelay>();
            if (relay == null)
                relay = doorTrigger.gameObject.AddComponent<BuildingDoorRelay>();
            relay.Init(this);
        }
    }

    private void Start()
    {
        if (doorTrigger == null)
        {
            Debug.LogError("Building: назначьте коллайдер двери (doorTrigger) или повесьте Collider2D на этот объект.", this);
            return;
        }

        if (interiorPrefab == null)
        {
            Debug.LogError("Building: не назначен interiorPrefab.", this);
            return;
        }

        Vector2 spawnPos;
        if (useManualInteriorPosition)
        {
            spawnPos = manualInteriorSpawnPosition;
        }
        else
        {
            InteriorSpawnLayout.Configure(fallbackRegionAnchor, fallbackGridColumns, fallbackCellSize);
            spawnPos = InteriorSpawnLayout.AllocateNext();
        }

        _interiorInstance = Instantiate(interiorPrefab, spawnPos, Quaternion.identity);
        _teleportDestination = ResolveEntrance(_interiorInstance.transform);
    }

    private void OnDestroy()
    {
        if (_interiorInstance != null)
            Destroy(_interiorInstance);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (doorTrigger == null || doorTrigger.gameObject != gameObject)
            return;

        TryTeleport(other);
    }

    internal void TryTeleport(Collider2D other)
    {
        if (_teleportDestination == null)
            return;

        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null)
            return;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.position = _teleportDestination.position;
        rb.linearVelocity = Vector2.zero;
    }

    private Transform ResolveEntrance(Transform interiorRoot)
    {
        if (string.IsNullOrEmpty(interiorEntranceChildName))
            return interiorRoot;

        Transform found = FindChildByName(interiorRoot, interiorEntranceChildName);
        if (found == null)
        {
            Debug.LogWarning($"Building: в префабе не найден объект «{interiorEntranceChildName}», используется корень.", this);
            return interiorRoot;
        }

        return found;
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform found = FindChildByName(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}

/// <summary>
/// Вешается на объект с коллайдером двери, если Building стоит на родителе. Не добавляйте вручную.
/// </summary>
public class BuildingDoorRelay : MonoBehaviour
{
    private Building _building;

    public void Init(Building building) => _building = building;

    private void OnTriggerEnter2D(Collider2D other) => _building?.TryTeleport(other);
}
