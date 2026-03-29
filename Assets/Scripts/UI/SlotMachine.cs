using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
public class PanelSpriteDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private Image targetImage;
    private RectTransform panel;
    public Sprite[] sprites;

    [Range(0f, 1f)]
    public float threshold = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float maxPullWhenInsufficientFunds = 0.78f;
    private float percent = 0f; 
    private bool reachedBottom = false;
    private bool dragging = false;

    [Header("Spin trigger")]
    [SerializeField] private SlotMachinePanelPresenter slotPanelPresenter;

    [Header("Feedback effects")]
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private float shakeDuration = 0.18f;
    [SerializeField] private float shakeMagnitude = 8f;
    [SerializeField] private float flashDuration = 0.14f;
    [SerializeField] private Color flashColor = new Color(1f, 0.92f, 0.45f, 1f);
    [SerializeField] private ParticleSystem[] pullParticles;
    [SerializeField] private bool autoFindParticlesInPanel = true;
    [SerializeField] private bool createRuntimeParticlesIfMissing = true;

    private ParticleSystem runtimeParticles;

    void Start()
    {   
        targetImage = GetComponent<Image>();
        panel = GetComponent<RectTransform>();
        if (slotPanelPresenter == null)
            slotPanelPresenter = GetComponentInParent<SlotMachinePanelPresenter>();

        if (shakeTarget == null)
            shakeTarget = slotPanelPresenter != null
                ? slotPanelPresenter.GetComponent<RectTransform>()
                : panel;

        TryResolveParticles();

        if (sprites.Length > 0)
            targetImage.sprite = sprites[0];
            
    }

    private float GetPercent(PointerEventData eventData)
    {
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panel,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        float height = panel.rect.height;
        float y = (height / 2 - localPoint.y) / height;

        return Mathf.Clamp01(y);
    }
    private void UpdateSprite(float percent)
    {
        if (sprites == null || sprites.Length == 0) return;

        int index = Mathf.FloorToInt(percent * sprites.Length);

        // чтобы не выйти за массив
        index = Mathf.Clamp(index, 0, sprites.Length - 1);

        targetImage.sprite = sprites[index];
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        if(reachedBottom) return;

        float cur_percent = GetPercent(eventData);

        if (Mathf.Abs( percent-cur_percent) < threshold)
        {
            Debug.Log("Клик рядом с рукой 10%");
            dragging = true;
        }
        else
        {
            dragging = false;
        }

        UpdateSprite(percent);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(!dragging || reachedBottom) return;
        float requestedPercent = GetPercent(eventData);
        bool canAffordSpin = CanAffordSpin();
        float blockedLimit = Mathf.Clamp01(Mathf.Min(maxPullWhenInsufficientFunds, 1f - threshold - 0.01f));
        percent = canAffordSpin ? requestedPercent : Mathf.Min(requestedPercent, blockedLimit);
        UpdateSprite(percent);

        if (!canAffordSpin)
            return;

        if (!reachedBottom && percent >= 1f-threshold)
        {
            Debug.Log("Достигли последних 10%");
            reachedBottom = true;
            dragging = false;

            TriggerSpin();
            PlayPullEffects();
            returnHand();
        }
    }

    [SerializeField] private float frameDelay = 0.05f;

    private Coroutine returnCoroutine;

    private void returnHand()
    {
        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        returnCoroutine = StartCoroutine(ReturnAnimation());
    }

    private IEnumerator ReturnAnimation()
    {
        for (; percent >= 0; percent -= 0.1f)
        {
            UpdateSprite(percent);
            yield return new WaitForSeconds(frameDelay);
        }
        if (percent < 0)
            percent = 0f;

        reachedBottom = false;
    }

    private void TriggerSpin()
    {
        if (slotPanelPresenter == null)
            slotPanelPresenter = SlotMachinePanelPresenter.Instance;

        if (slotPanelPresenter == null)
        {
            Debug.LogWarning("PanelSpriteDrag: SlotMachinePanelPresenter is not assigned/found.");
            return;
        }

        slotPanelPresenter.SpinFromUi();
    }

    private void PlayPullEffects()
    {
        StartCoroutine(ShakeRoutine());
        StartCoroutine(FlashRoutine());
        PlayParticles();
    }

    private IEnumerator ShakeRoutine()
    {
        if (shakeTarget == null)
            yield break;

        Vector2 basePos = shakeTarget.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float damper = 1f - (elapsed / Mathf.Max(0.0001f, shakeDuration));
            Vector2 offset = Random.insideUnitCircle * (shakeMagnitude * damper);
            shakeTarget.anchoredPosition = basePos + offset;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        shakeTarget.anchoredPosition = basePos;
    }

    private IEnumerator FlashRoutine()
    {
        if (targetImage == null)
            yield break;

        Color baseColor = targetImage.color;
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            float t = elapsed / Mathf.Max(0.0001f, flashDuration);
            float blend = 1f - Mathf.Abs(2f * t - 1f);
            targetImage.color = Color.Lerp(baseColor, flashColor, blend);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        targetImage.color = baseColor;
    }

    private bool CanAffordSpin()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return false;

        return gm.CasinoDeposit >= gm.GoalDeposit;
    }

    private void PlayParticles()
    {
        if (pullParticles == null || pullParticles.Length == 0)
            return;

        for (int i = 0; i < pullParticles.Length; i++)
        {
            ParticleSystem ps = pullParticles[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }
    }

    private void TryResolveParticles()
    {
        if (pullParticles != null && pullParticles.Length > 0)
            return;

        if (autoFindParticlesInPanel)
        {
            Transform searchRoot = shakeTarget != null ? shakeTarget : transform.root;
            ParticleSystem[] found = searchRoot.GetComponentsInChildren<ParticleSystem>(true);
            if (found != null && found.Length > 0)
            {
                pullParticles = found;
                return;
            }
        }

        if (!createRuntimeParticlesIfMissing)
            return;

        runtimeParticles = CreateRuntimeParticles();
        if (runtimeParticles != null)
            pullParticles = new[] { runtimeParticles };
    }

    private ParticleSystem CreateRuntimeParticles()
    {
        Transform parent = shakeTarget != null ? shakeTarget : transform;
        var go = new GameObject("SpinBurstParticles");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.45f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.35f;
        main.startSpeed = 140f;
        main.startSize = 7f;
        main.startColor = new Color(1f, 0.92f, 0.45f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 65f;
        shape.radius = 40f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var alpha = new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0f, 1f)
        };
        var color = new GradientColorKey[]
        {
            new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
            new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f)
        };
        var gradient = new Gradient();
        gradient.SetKeys(color, alpha);
        colorOverLifetime.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 500;

        return ps;
    }


}