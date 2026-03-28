using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Полиция: преследует игрока по NavMesh, только пока игрок несёт хотя бы один предмет (<see cref="ItemCarrier"/>).
/// Радиуса «агро» нет — условие одно: предмет в руках (и игрок не в укрытии).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PoliceChaseAI : MonoBehaviour
{
    [Header("Преследование")]
    [Tooltip("Скорость NavMesh Agent при преследовании (юниты/с).")]
    [SerializeField] [Min(0.01f)] private float chaseSpeed = 4.5f;

    [Tooltip("High — штатное поведение Unity (плавнее повороты к пути). No — резче к цели, но сильнее «занос».")]
    [SerializeField] private ObstacleAvoidanceType obstacleAvoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

    [SerializeField] [Min(1f)] private float chaseAcceleration = 28f;

    [SerializeField] [Min(1f)] private float chaseAngularSpeed = 540f;

    [Tooltip("Считается догон (конец эпизода).")]
    public float catchDistance = 0.65f;

    public float chaseStoppingDistance = 0.35f;

    [Header("После догона или укрытия")]
    public VillagerPursuitEndAction pursuitEndAction = VillagerPursuitEndAction.ReturnToSpawn;

    [Tooltip("Точка возврата после погони. Пусто — позиция при старте сцены.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Отладка")]
    [SerializeField] private bool debugLog;

    public bool IsChasing => _phase == PursuitPhase.Chasing;

    private enum PursuitPhase
    {
        Idle,
        Chasing
    }

    private Transform _player;
    private ItemCarrier _playerCarrier;
    private NavMeshAgent _agent;
    private Vector3 _spawnPosition;
    private PursuitPhase _phase = PursuitPhase.Idle;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
            _playerCarrier = playerObj.GetComponent<ItemCarrier>();
        }

        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.speed = chaseSpeed;
        _agent.acceleration = chaseAcceleration;
        _agent.angularSpeed = chaseAngularSpeed;
        _agent.obstacleAvoidanceType = obstacleAvoidance;

        _spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;

        if (debugLog)
        {
            if (_player == null)
                LogWarning("Старт: объект с тегом Player не найден.");
            else if (_playerCarrier == null)
                LogWarning("Старт: у игрока нет ItemCarrier — условие «несёт предмет» не сработает.");
            else
                Log($"Старт: игрок и ItemCarrier найдены, спавн {_spawnPosition}");
        }
    }

    private void Update()
    {
        if (_agent == null)
            return;

        if (_phase == PursuitPhase.Idle)
            UpdateIdle();
        else
            UpdateChasing();
    }

    private void UpdateIdle()
    {
        _agent.isStopped = true;

        if (_player == null || !PlayerOutOfShelter())
            return;

        if (!HasLoot())
            return;

        BeginChase();
    }

    private void UpdateChasing()
    {
        if (_player == null)
        {
            EndPursuit("игрок пропал (null)");
            return;
        }

        if (PlayerShelterState.IsInsidePlayerHome)
        {
            EndPursuit("игрок в укрытии (дом)");
            return;
        }

        if (!HasLoot())
        {
            EndPursuit("игрок перестал нести предмет");
            return;
        }

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist <= catchDistance)
        {
            NpcStealCarriedItem2D.StripAll(_playerCarrier);
            EndPursuit($"догон: расстояние {dist:F2} ≤ catchDistance ({catchDistance})");
            return;
        }

        _agent.isStopped = false;
        _agent.stoppingDistance = Mathf.Max(0f, chaseStoppingDistance);
        _agent.SetDestination(_player.position);
    }

    // В руках есть хотя бы один предмет (ItemCarrier).
    private bool HasLoot()
    {
        return _playerCarrier != null && _playerCarrier.CarriedItems.Count > 0;
    }

    private void BeginChase()
    {
        float dist = _player != null ? Vector3.Distance(transform.position, _player.position) : -1f;
        Log($"Начало погони: игрок с предметом, дистанция до цели {dist:F2}");

        _phase = PursuitPhase.Chasing;
        _agent.isStopped = false;
    }

    private void EndPursuit(string reason)
    {
        Log($"Конец погони: {reason}");

        if (pursuitEndAction == VillagerPursuitEndAction.DestroySelf)
        {
            Log("Действие: DestroySelf");
            Destroy(gameObject);
            return;
        }

        Log($"Действие: ReturnToSpawn → {_spawnPosition}");
        TeleportToSpawn();
        _phase = PursuitPhase.Idle;
        _agent.isStopped = true;
    }

    private void TeleportToSpawn()
    {
        Vector3 pos = _spawnPosition;

        var rb2d = GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.position = new Vector2(pos.x, pos.y);
            rb2d.linearVelocity = Vector2.zero;
        }

        _agent.Warp(pos);
    }

    private void Log(string message)
    {
        if (debugLog)
            Debug.Log($"[PoliceChaseAI] {name}: {message}", this);
    }

    private void LogWarning(string message)
    {
        if (debugLog)
            Debug.LogWarning($"[PoliceChaseAI] {name}: {message}", this);
    }

    // Игрок найден и не в укрытии — можно детектить и преследовать.
    private bool PlayerOutOfShelter()
    {
        return _player != null && !PlayerShelterState.IsInsidePlayerHome;
    }
}
