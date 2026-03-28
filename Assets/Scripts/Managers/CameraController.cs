using System.Collections;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/// <summary>
/// Глобальная точка доступа к основной Cinemachine-камере: сдвиг виртуалки и смена Confiner 2D.
/// Повесьте на объект в сцене и назначьте <see cref="CinemachineBrain"/> (обычно на Main Camera).
/// </summary>
[DefaultExecutionOrder(-50)]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [SerializeField] private CinemachineBrain brain;

    [Tooltip("Confiner активной виртуалки (например оверворд). Нужен для смены границы при входе/выходе из интерьера.")]
    [SerializeField] private CinemachineConfiner2D defaultConfiner;

    [SerializeField] private bool persistBetweenScenes;

    [Tooltip("Если включено — Default Blend у Brain = Cut (мгновенное переключение VC). Иначе остаётся как в сцене.")]
    [SerializeField] private bool setBrainDefaultBlendToCutOnAwake;

    [Tooltip("Если true — при телепорте в интерьер не вызывается OnTargetObjectWarped (только снап). Пробуй, если всё ещё дёргается.")]
    [SerializeField] private bool skipWarpOnInteriorTeleport;

    [Tooltip("Сколько кадров подряд удерживать hard-snap после телепорта.")]
    [SerializeField] private int hardSnapFramesAfterTeleport = 3;

    Collider2D savedConfinerBoundsBeforeInterior;
    float savedConfinerDamping = -1f;
    float savedConfinerSlowingDistance = -1f;

    bool savedFollowPositionDampingPending;
    Vector3 savedFollowPositionDamping;

    bool savedComposerDampingPending;
    Vector3 savedComposerDamping;

    Coroutine restoreDampingCoroutine;

    public CinemachineBrain Brain => brain;

    /// <summary>Unity Camera с Brain (тот же GameObject, что и Brain).</summary>
    public Camera UnityCamera => brain != null ? brain.GetComponent<Camera>() : null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (brain == null)
            brain = FindFirstObjectByType<CinemachineBrain>();

        if (brain != null && setBrainDefaultBlendToCutOnAwake)
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Активная виртуальная камера Brain (если это <see cref="CinemachineCamera"/>).</summary>
    public CinemachineCamera TryGetActiveCinemachineCamera()
    {
        if (brain == null)
            return null;
        return brain.ActiveVirtualCamera as CinemachineCamera;
    }

    /// <summary>
    /// Перемещает <b>активную</b> виртуальную камеру (учитывает пайплайн Cinemachine).
    /// </summary>
    public void ForceActiveCameraPosition(Vector3 worldPosition, Quaternion? worldRotation = null)
    {
        var vcam = TryGetActiveCinemachineCamera();
        if (vcam == null)
            return;

        var rot = worldRotation ?? vcam.transform.rotation;
        vcam.ForceCameraPosition(worldPosition, rot);
    }

    /// <summary>Перемещает указанную виртуальную камеру.</summary>
    public void ForceCameraPosition(CinemachineCamera virtualCamera, Vector3 worldPosition, Quaternion? worldRotation = null)
    {
        if (virtualCamera == null)
            return;

        var rot = worldRotation ?? virtualCamera.transform.rotation;
        virtualCamera.ForceCameraPosition(worldPosition, rot);
    }

    /// <summary>Меняет границу Confiner 2D и обновляет кэш (обязательно после смены коллайдера).</summary>
    public void SetConfinerBoundingShape(CinemachineConfiner2D confiner, Collider2D boundingShape)
    {
        if (confiner == null)
            return;

        confiner.BoundingShape2D = boundingShape;
        confiner.InvalidateBoundingShapeCache();
    }

    /// <summary>Использует <see cref="defaultConfiner"/> из инспектора.</summary>
    public void SetConfinerBoundingShape(Collider2D boundingShape)
    {
        if (defaultConfiner == null)
        {
            Debug.LogWarning("CameraController: не назначен defaultConfiner — укажите в инспекторе или вызовите SetConfinerBoundingShape(confiner, shape).", this);
            return;
        }

        SetConfinerBoundingShape(defaultConfiner, boundingShape);
    }

    /// <summary>Включает приоритет и задаёт значение (выше — чаще становится Live).</summary>
    public void SetVirtualCameraPriority(CinemachineCamera virtualCamera, int priority)
    {
        if (virtualCamera == null)
            return;

        virtualCamera.Priority = priority;
    }

    /// <summary>
    /// Телепорт без смены интерьера: синхронизация Transform, warp цели и жёсткий снап VC.
    /// </summary>
    public void NotifyPlayerTeleported(Transform player, Vector3 worldPositionDelta)
    {
        Physics2D.SyncTransforms();

        var vcam = TryGetActiveCinemachineCamera();
        if (vcam == null || player == null)
            return;

        vcam.OnTargetObjectWarped(player, worldPositionDelta);
        SnapCameraHard(vcam, player);
        ScheduleRestoreAllDampings(vcam, player);
    }

    /// <summary>Вход в интерьер: Warp → Snap → Confiner; демпфирование Follow/Composer/Confiner на кадр снимается.</summary>
    public void EnterInteriorCamera(Transform player, Vector3 warpDelta, Collider2D interiorConfinerBounds)
    {
        Physics2D.SyncTransforms();

        if (defaultConfiner == null)
            Debug.LogWarning("CameraController: не назначен defaultConfiner.", this);

        savedConfinerBoundsBeforeInterior = defaultConfiner != null ? defaultConfiner.BoundingShape2D : null;

        if (defaultConfiner != null)
        {
            savedConfinerDamping = defaultConfiner.Damping;
            savedConfinerSlowingDistance = defaultConfiner.SlowingDistance;
            defaultConfiner.Damping = 0f;
            defaultConfiner.SlowingDistance = 0f;
        }
        else
        {
            savedConfinerDamping = -1f;
            savedConfinerSlowingDistance = -1f;
        }

        var vcam = TryGetActiveCinemachineCamera();
        if (vcam != null && player != null)
        {
            if (!skipWarpOnInteriorTeleport)
                vcam.OnTargetObjectWarped(player, warpDelta);
            SnapCameraHard(vcam, player);
        }

        if (defaultConfiner != null && interiorConfinerBounds != null)
            SetConfinerBoundingShape(defaultConfiner, interiorConfinerBounds);

        ScheduleRestoreAllDampings(vcam, player);
    }

    /// <summary>Выход из интерьера: восстановление границы, warp и снап.</summary>
    public void ExitInteriorCamera(Transform player, Vector3 warpDelta)
    {
        Physics2D.SyncTransforms();

        if (defaultConfiner != null)
        {
            savedConfinerDamping = defaultConfiner.Damping;
            savedConfinerSlowingDistance = defaultConfiner.SlowingDistance;
            defaultConfiner.Damping = 0f;
            defaultConfiner.SlowingDistance = 0f;
        }
        else
        {
            savedConfinerDamping = -1f;
            savedConfinerSlowingDistance = -1f;
        }

        var vcam = TryGetActiveCinemachineCamera();
        if (vcam != null && player != null)
        {
            if (!skipWarpOnInteriorTeleport)
                vcam.OnTargetObjectWarped(player, warpDelta);
            SnapCameraHard(vcam, player);
        }

        if (defaultConfiner != null && savedConfinerBoundsBeforeInterior != null)
            SetConfinerBoundingShape(defaultConfiner, savedConfinerBoundsBeforeInterior);

        savedConfinerBoundsBeforeInterior = null;

        ScheduleRestoreAllDampings(vcam, player);
    }

    void ScheduleRestoreAllDampings(CinemachineCamera vcam, Transform player)
    {
        if (restoreDampingCoroutine != null)
        {
            StopCoroutine(restoreDampingCoroutine);
            restoreDampingCoroutine = null;
        }

        var follow = vcam != null ? vcam.GetComponent<CinemachineFollow>() : null;
        var composer = vcam != null ? vcam.GetComponent<CinemachinePositionComposer>() : null;

        restoreDampingCoroutine = StartCoroutine(RestoreDampingAfterHardSnapFrames(vcam, player, follow, composer, defaultConfiner));
    }

    IEnumerator RestoreDampingAfterHardSnapFrames(
        CinemachineCamera vcam,
        Transform player,
        CinemachineFollow follow,
        CinemachinePositionComposer composer,
        CinemachineConfiner2D confiner)
    {
        int frames = Mathf.Max(1, hardSnapFramesAfterTeleport);
        for (int i = 0; i < frames; i++)
        {
            if (vcam != null && player != null)
            {
                Physics2D.SyncTransforms();
                SnapVirtualCameraToTrackedTarget(vcam, player);
                vcam.PreviousStateIsValid = false;
            }
            yield return new WaitForEndOfFrame();
        }

        if (follow != null && savedFollowPositionDampingPending)
        {
            var ts = follow.TrackerSettings;
            ts.PositionDamping = savedFollowPositionDamping;
            follow.TrackerSettings = ts;
            savedFollowPositionDampingPending = false;
        }

        if (composer != null && savedComposerDampingPending)
        {
            composer.Damping = savedComposerDamping;
            savedComposerDampingPending = false;
        }

        if (confiner != null && savedConfinerDamping >= 0f)
        {
            confiner.Damping = savedConfinerDamping;
            savedConfinerDamping = -1f;
        }
        if (confiner != null && savedConfinerSlowingDistance >= 0f)
        {
            confiner.SlowingDistance = savedConfinerSlowingDistance;
            savedConfinerSlowingDistance = -1f;
        }

        restoreDampingCoroutine = null;
    }

    /// <summary>
    /// Временно обнуляет Position Damping у Follow и Damping у Composer, снапает VC, восстановление — в <see cref="ScheduleRestoreAllDampings"/> через кадр.
    /// </summary>
    void SnapCameraHard(CinemachineCamera vcam, Transform player)
    {
        var follow = vcam.GetComponent<CinemachineFollow>();
        if (follow != null)
        {
            savedFollowPositionDamping = follow.TrackerSettings.PositionDamping;
            savedFollowPositionDampingPending = true;
            var ts = follow.TrackerSettings;
            ts.PositionDamping = Vector3.zero;
            follow.TrackerSettings = ts;
        }

        var composer = vcam.GetComponent<CinemachinePositionComposer>();
        if (composer != null)
        {
            savedComposerDamping = composer.Damping;
            savedComposerDampingPending = true;
            composer.Damping = Vector3.zero;
        }

        SnapVirtualCameraToTrackedTarget(vcam, player);
        // Важно: сбрасываем внутреннюю "память" прошлого кадра, иначе возможен отложенный доворот/доцентрирование.
        vcam.PreviousStateIsValid = false;
    }

    /// <summary>
    /// Целевая позиция VC по Cinemachine Follow или Position Composer.
    /// </summary>
    void SnapVirtualCameraToTrackedTarget(CinemachineCamera vcam, Transform player)
    {
        var follow = vcam.Follow;
        if (follow == null)
            return;

        var rot = vcam.transform.rotation;

        // Важно: если есть PositionComposer, он определяет желаемое экранное положение цели.
        // Снап нужно считать под него, иначе спустя время камера "доцентрируется".
        var composer = vcam.GetComponent<CinemachinePositionComposer>();
        if (composer != null && composer.enabled && composer.IsValid)
        {
            var tracked = follow.position + follow.rotation * composer.TargetOffset;
            var screen = composer.Composition.ScreenPosition; // 0..1, где (0.5,0.5) — центр

            float aspect = 1f;
            if (UnityCamera != null && UnityCamera.pixelHeight > 0)
                aspect = (float)UnityCamera.pixelWidth / UnityCamera.pixelHeight;

            // world-units смещение от центра кадра к желаемой точке композиции
            Vector2 halfSpan;
            if (vcam.Lens.Orthographic)
            {
                halfSpan = new Vector2(vcam.Lens.OrthographicSize * aspect, vcam.Lens.OrthographicSize);
            }
            else
            {
                float dist = Mathf.Max(0.001f, composer.CameraDistance);
                float halfY = Mathf.Tan(vcam.Lens.FieldOfView * Mathf.Deg2Rad * 0.5f) * dist;
                halfSpan = new Vector2(halfY * aspect, halfY);
            }

            // Экранный оффсет цели: +X вправо, +Y вверх.
            Vector2 targetOffsetFromCenter = new Vector2(
                (screen.x - 0.5f) * 2f * halfSpan.x,
                (screen.y - 0.5f) * 2f * halfSpan.y);

            var right = rot * Vector3.right;
            var up = rot * Vector3.up;

            // Если цель должна быть смещена на +offset на экране, камера смещается в обратную сторону.
            var pos = tracked
                      - right * targetOffsetFromCenter.x
                      - up * targetOffsetFromCenter.y
                      + rot * Vector3.back * composer.CameraDistance;

            vcam.ForceCameraPosition(pos, rot);
            return;
        }

        var followComp = vcam.GetComponent<CinemachineFollow>();
        if (followComp != null && followComp.enabled && followComp.IsValid)
        {
            if (followComp.TrackerSettings.BindingMode == BindingMode.WorldSpace)
            {
                vcam.ForceCameraPosition(follow.position + followComp.FollowOffset, rot);
                return;
            }
        }

        var p = follow.position;
        var z = vcam.transform.position.z;
        vcam.ForceCameraPosition(new Vector3(p.x, p.y, z), rot);
    }
}
