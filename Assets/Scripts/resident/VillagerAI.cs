using UnityEngine;
using UnityEngine.AI;

public enum VillagerPursuitEndAction
{
    DestroySelf,
    ReturnToSpawn
}

[RequireComponent(typeof(NavMeshAgent))]
public class VillagerAI : MonoBehaviour
{
    [Header("Обнаружение")]
    [Tooltip("Житель начинает преследование, только если игрок подошёл не ближе этого.")]
    public float detectionDistance = 5f;

    [Header("Преследование")]
    [Tooltip("Скорость NavMesh Agent при преследовании (юниты/с).")]
    [SerializeField] [Min(0.01f)] private float chaseSpeed = 4f;

    [Tooltip("High — плавнее к касательной пути. No — резче, сильнее занос.")]
    [SerializeField] private ObstacleAvoidanceType obstacleAvoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

    [SerializeField] [Min(1f)] private float chaseAcceleration = 24f;

    [SerializeField] [Min(1f)] private float chaseAngularSpeed = 540f;

    [Tooltip("Считается, что житель догнал игрока (конец эпизода преследования).")]
    public float catchDistance = 0.65f;

    public float chaseStoppingDistance = 0.35f;

    [Header("После догона или укрытия игрока")]
    public VillagerPursuitEndAction pursuitEndAction = VillagerPursuitEndAction.ReturnToSpawn;

    [Tooltip("Точка «дома» жителя: туда его телепортирует после догона / укрытия игрока. Пусто — позиция при старте сцены.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Отладка")]
    [Tooltip("Сообщения в Console: старт, обнаружение, конец преследования и причина.")]
    [SerializeField] private bool debugLog = true;

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
            if (_player != null)
                Log($"Старт: игрок найден (тег Player), спавн {_spawnPosition}");
            else
                LogWarning("Старт: объект с тегом Player не найден — преследование не начнётся.");
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

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist <= detectionDistance)
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

    private void BeginChase()
    {
        float dist = _player != null ? Vector3.Distance(transform.position, _player.position) : -1f;
        Log($"Начало преследования: расстояние до игрока {dist:F2}, detectionDistance={detectionDistance}");

        _phase = PursuitPhase.Chasing;
        _agent.isStopped = false;
    }

    private void EndPursuit(string reason)
    {
        Log($"Конец преследования: {reason}");

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
            Debug.Log($"[VillagerAI] {name}: {message}", this);
    }

    private void LogWarning(string message)
    {
        if (debugLog)
            Debug.LogWarning($"[VillagerAI] {name}: {message}", this);
    }

    // Игрок найден и не в укрытии — можно детектить и преследовать.
    private bool PlayerOutOfShelter()
    {
        return _player != null && !PlayerShelterState.IsInsidePlayerHome;
    }
}
