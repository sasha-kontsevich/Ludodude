using UnityEngine;

/// <summary>
/// Интерьер здания: точка появления игрока внутри, триггер выхода телепортирует наружу.
/// Связь с <see cref="Building"/> через <see cref="Bind"/>.
/// </summary>
public class Interior : MonoBehaviour
{
    [SerializeField] private Collider2D exitDoorTrigger;

    [Tooltip("Куда ставить игрока при входе с улицы (из Building). Пусто — корень этого объекта.")]
    [SerializeField] private Transform playerSpawnPoint;

    private Transform _exteriorSpawnPoint;

    private void Reset()
    {
        if (exitDoorTrigger != null)
            exitDoorTrigger.isTrigger = true;
    }

    private void Awake()
    {
        if (exitDoorTrigger == null)
            exitDoorTrigger = GetComponent<Collider2D>();

        if (exitDoorTrigger != null && exitDoorTrigger.gameObject != gameObject)
        {
            var relay = exitDoorTrigger.GetComponent<InteriorExitRelay>();
            if (relay == null)
                relay = exitDoorTrigger.gameObject.AddComponent<InteriorExitRelay>();
            relay.Init(this);
        }
    }

    /// <summary>Точка телепорта при входе в здание с карты.</summary>
    public Transform GetPlayerSpawnPoint() => playerSpawnPoint != null ? playerSpawnPoint : transform;

    /// <summary>
    /// Вызывается из <see cref="Building"/> после Instantiate префаба интерьера.
    /// </summary>
    public void Bind(Transform exteriorSpawnPoint)
    {
        _exteriorSpawnPoint = exteriorSpawnPoint;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (exitDoorTrigger == null || exitDoorTrigger.gameObject != gameObject)
            return;

        TryTeleportOut(other);
    }

    internal void TryTeleportOut(Collider2D other)
    {
        if (_exteriorSpawnPoint == null)
        {
            Debug.LogWarning("Interior: не вызван Bind (нет точки спавна снаружи).", this);
            return;
        }

        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null)
            return;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.position = _exteriorSpawnPoint.position;
        rb.linearVelocity = Vector2.zero;
    }
}

/// <summary>
/// Вешается на объект с коллайдером выхода, если Interior стоит на родителе.
/// </summary>
public class InteriorExitRelay : MonoBehaviour
{
    private Interior _interior;

    public void Init(Interior interior) => _interior = interior;

    private void OnTriggerEnter2D(Collider2D other) => _interior?.TryTeleportOut(other);
}
