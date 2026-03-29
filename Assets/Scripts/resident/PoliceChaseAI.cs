using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Полиция: преследует игрока по NavMesh, только пока игрок несёт хотя бы один предмет (<see cref="ItemCarrier"/>).
/// Радиуса «агро» нет — условие одно: предмет в руках (и игрок не в укрытии).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PoliceChaseAI : MonoBehaviour
{

    [Header("Патруль")]
    [Tooltip("Пустой объект с точками за которыми следуем (по порядку)")]
    [SerializeField] private Transform pointsObject;
    private Transform[] points;
    private int currentPoint = 0;


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

    [Header("Игрок сам сбросил добычу (вдали)")]
    [Tooltip("За это время скорость падает с chaseSpeed до конечной — без похода к точке сброса.")]
    [SerializeField] [Min(0.05f)] private float slowDownAfterDropDuration = 1.1f;

    [Tooltip("Скорость в конце замедления (до остановки в Idle).")]
    [SerializeField] [Min(0f)] private float slowDownAfterDropEndSpeed = 0.45f;

    [Tooltip("Ускорение в конце замедления (мягче тормозит).")]
    [SerializeField] [Min(0.01f)] private float slowDownAfterDropEndAccel = 10f;

    [Tooltip("Если до цели на пути осталось меньше — можно завершить охлаждение раньше по времени.")]
    [SerializeField] private float coolingArriveDistanceSlack = 0.35f;

    [Header("После догона или укрытия")]
    public VillagerPursuitEndAction pursuitEndAction = VillagerPursuitEndAction.ReturnToSpawn;

    [Tooltip("Точка возврата после погони. Пусто — позиция при старте сцены.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Если добыча пропала, а офицер ближе этого расстояния до игрока — контакт (кража триггером / догон) и телепорт на спавн.")]
    [SerializeField] private float teleportIfLootLostWithinDistance = 2.5f;

    [Header("Отладка")]
    [SerializeField] private bool debugLog;


    [Header("Радиус обнаружения")]
    [SerializeField] private float aggroRadius = 5f;
    public bool IsChasing => _phase == PursuitPhase.Chasing;

    private enum PursuitPhase
    {
        Patrool,
        Idle,
        Chasing,
        CoolingDownAfterDrop
    }

    private Transform _player;
    private ItemCarrier _playerCarrier;
    private NavMeshAgent _agent;
    private Vector3 _spawnPosition;
    private PursuitPhase _phase = PursuitPhase.Patrool;

    private Vector3 _coolingDestination;
    private float _coolingElapsed;

    private bool IsPlayerInRange()
    {
        if (_player == null) return false;

        float dist = Vector3.Distance(transform.position, _player.position);
        return dist <= aggroRadius;
    }
    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
            _playerCarrier = playerObj.GetComponent<ItemCarrier>();
        }

        if (pointsObject != null)
        {
            int count = pointsObject.childCount;
            points = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                points[i] = pointsObject.GetChild(i);
            }
        }
        else
        {
            Debug.Log("PointsObject не задан!", this);
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
        else if (_phase == PursuitPhase.Chasing)
            UpdateChasing();
        else if(_phase == PursuitPhase.Patrool)
            UpdatePatrool();
        else
            UpdateCoolingDownAfterDrop();
    }

    private void UpdateIdle()
    {
        _agent.isStopped = true;

        if (_player == null || !PlayerOutOfShelter())
            return;

        if (!HasLoot())
            return;

        if (!IsPlayerInRange())
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

        if (!IsPlayerInRange())
        {
            EndPursuit("игрок вышел из радиуса");
            return;
        }

        if (!HasLoot())
        {
            float distToPlayer = Vector3.Distance(transform.position, _player.position);
            if (distToPlayer <= teleportIfLootLostWithinDistance)
            {
                EndPursuit($"добыча снята вблизи офицера (дистанция {distToPlayer:F2} ≤ {teleportIfLootLostWithinDistance})");
                return;
            }

            Log("Игрок сбросил добычу вдали — замедление");
            _coolingDestination = _agent.hasPath ? _agent.destination : transform.position;
            _coolingElapsed = 0f;
            _phase = PursuitPhase.CoolingDownAfterDrop;
            _agent.isStopped = false;
            _agent.stoppingDistance = Mathf.Max(0f, chaseStoppingDistance);
            _agent.SetDestination(_coolingDestination);
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

    private void UpdatePatrool()
    {
        if (points == null || points.Length == 0)
            return;

        // если вдруг игрок появился с предметом → сразу в погоню
        if (_player != null && PlayerOutOfShelter() && HasLoot() && IsPlayerInRange())
        {
            BeginChase();
            return;
        }

        _agent.isStopped = false;
        _agent.stoppingDistance = 0.1f;

        Transform target = points[currentPoint];
        _agent.SetDestination(target.position);

        // дошёл до точки
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            currentPoint = (currentPoint + 1) % points.Length;
        }
    }
    private void UpdateCoolingDownAfterDrop()
    {
        if (_player != null && PlayerOutOfShelter() && HasLoot())
        {
            Log("Игрок снова с предметом — продолжаю погоню");
            BeginChase();
            return;
        }

        if (_player == null)
        {
            EndPursuit("игрок пропал при замедлении");
            return;
        }

        if (PlayerShelterState.IsInsidePlayerHome)
        {
            EndPursuit("игрок в укрытии при замедлении");
            return;
        }

        _coolingElapsed += Time.deltaTime;
        float u = Mathf.Clamp01(_coolingElapsed / slowDownAfterDropDuration);
        _agent.speed = Mathf.Lerp(chaseSpeed, slowDownAfterDropEndSpeed, u);
        _agent.acceleration = Mathf.Lerp(chaseAcceleration, slowDownAfterDropEndAccel, u);
        _agent.isStopped = false;
        _agent.SetDestination(_coolingDestination);

        bool arrived = !_agent.pathPending &&
            _agent.hasPath &&
            _agent.remainingDistance <= _agent.stoppingDistance + coolingArriveDistanceSlack;

        if (u >= 1f || arrived)
        {
            Log("Замедление завершено — патруль/ожидание");
            _phase = PursuitPhase.Patrool;
            _agent.isStopped = true;
            _agent.speed = chaseSpeed;
            _agent.acceleration = chaseAcceleration;
        }
    }

    private bool HasLoot()
    {
        return _playerCarrier != null && _playerCarrier.CarriedItems.Count > 0;
    }

    private void BeginChase()
    {
        float dist = _player != null ? Vector3.Distance(transform.position, _player.position) : -1f;
        Log($"Начало погони: игрок с предметом, дистанция до цели {dist:F2}");

        _phase = PursuitPhase.Chasing;
        _agent.speed = chaseSpeed;
        _agent.acceleration = chaseAcceleration;
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

        Log($"Действие: Stop");
        //TeleportToSpawn();
        _phase = PursuitPhase.Patrool;
        _agent.isStopped = true;
        _agent.speed = chaseSpeed;
        _agent.acceleration = chaseAcceleration;
    }

    // private void TeleportToSpawn()
    // {
    //     Vector3 pos = _spawnPosition;

    //     var rb2d = GetComponent<Rigidbody2D>();
    //     if (rb2d != null)
    //     {
    //         rb2d.position = new Vector2(pos.x, pos.y);
    //         rb2d.linearVelocity = Vector2.zero;
    //     }

    //     _agent.Warp(pos);
    // }

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

    private bool PlayerOutOfShelter()
    {
        return _player != null && !PlayerShelterState.IsInsidePlayerHome;
    }
}
