using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Binds a shared slot UI presenter to this machine when player enters trigger zone.
/// </summary>
[DisallowMultipleComponent]
public class GamblingMachineInteractable : MonoBehaviour
{
    [SerializeField] private GamblingMachineController machineController;
    [SerializeField] private Collider2D zoneTrigger;
    [SerializeField] private SlotMachinePanelPresenter panelPresenter;
    [SerializeField] private bool hidePanelOnExit = true;
    [SerializeField] private bool openByInteract = true;
    [SerializeField] private bool togglePanelByInteract = true;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Tooltip")]
    [SerializeField] private bool showTooltip = true;
    [SerializeField] private string interactTooltipText = "Играть в слот";
    [SerializeField] private string interactFallbackKeyLabel = "E";

    private TopDownPlayerController _playerInZone;
    private bool _tooltipShown;
    private bool _warnedAboutMissingPresenter;
    private InputActionMap _playerMap;
    private InputAction _interactAction;
    private bool _interactPressedThisFrame;

    private void Reset()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<Collider2D>();

        if (zoneTrigger != null)
            zoneTrigger.isTrigger = true;
    }

    private void Awake()
    {
        if (machineController == null)
            machineController = GetComponent<GamblingMachineController>();

        if (zoneTrigger == null)
            zoneTrigger = GetComponent<Collider2D>();

        if (inputActions != null)
        {
            _playerMap = inputActions.FindActionMap("Player");
            _interactAction = _playerMap?.FindAction("Interact");
        }

        if (zoneTrigger != null && zoneTrigger.gameObject != gameObject)
        {
            var relay = zoneTrigger.GetComponent<GamblingMachineTriggerRelay>();
            if (relay == null)
                relay = zoneTrigger.gameObject.AddComponent<GamblingMachineTriggerRelay>();
            relay.Init(this);
        }

        if (openByInteract && _interactAction == null)
            Debug.LogWarning("GamblingMachineInteractable: assign Input Actions asset with Player/Interact action.", this);

        ResolvePresenterIfNeeded();
    }

    private void OnEnable()
    {
        _playerMap?.Enable();
        if (_interactAction != null)
            _interactAction.started += OnInteractStarted;
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleTriggerEnter(other);

    private void OnTriggerExit2D(Collider2D other) => HandleTriggerExit(other);

    internal void HandleTriggerEnter(Collider2D other)
    {
        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null)
            return;

        _playerInZone = player;
        ResolvePresenterIfNeeded();
    }

    internal void HandleTriggerExit(Collider2D other)
    {
        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null || player != _playerInZone)
            return;

        _playerInZone = null;
        ResolvePresenterIfNeeded();
        if (panelPresenter == null || machineController == null)
            return;

        if (panelPresenter.IsBoundTo(machineController))
            panelPresenter.UnbindMachine();

        if (hidePanelOnExit)
            panelPresenter.HidePanel();

        HideTooltip();
    }

    private void Update()
    {
        if (_playerInZone == null)
        {
            HideTooltip();
            // Prevent stale Interact press outside the zone from auto-opening on enter.
            _interactPressedThisFrame = false;
            return;
        }

        ResolvePresenterIfNeeded();
        bool panelOpenForThisMachine = panelPresenter != null && panelPresenter.IsBoundTo(machineController);

        if (showTooltip && !panelOpenForThisMachine)
            ShowTooltip();
        else
            HideTooltip();

        if (!openByInteract || !_interactPressedThisFrame)
            return;
        _interactPressedThisFrame = false;

        if (panelPresenter == null || machineController == null)
        {
            if (panelPresenter == null && !_warnedAboutMissingPresenter)
            {
                Debug.LogWarning("GamblingMachineInteractable: SlotMachinePanelPresenter not found in loaded scene objects.", this);
                _warnedAboutMissingPresenter = true;
            }
            return;
        }

        if (togglePanelByInteract && panelPresenter.IsBoundTo(machineController))
        {
            panelPresenter.UnbindMachine();
            panelPresenter.HidePanel();
            return;
        }

        panelPresenter.BindMachine(machineController);
        panelPresenter.ShowPanel();
    }

    private void OnDisable()
    {
        if (_interactAction != null)
            _interactAction.started -= OnInteractStarted;
        _playerMap?.Disable();
        _interactPressedThisFrame = false;
        HideTooltip();
    }

    private void OnInteractStarted(InputAction.CallbackContext _)
    {
        _interactPressedThisFrame = true;
    }

    private void ResolvePresenterIfNeeded()
    {
        if (panelPresenter != null)
        {
            _warnedAboutMissingPresenter = false;
            return;
        }

        panelPresenter = SlotMachinePanelPresenter.Instance;
        if (panelPresenter != null)
        {
            _warnedAboutMissingPresenter = false;
            return;
        }

        var presenters = Object.FindObjectsByType<SlotMachinePanelPresenter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (presenters != null && presenters.Length > 0)
        {
            panelPresenter = presenters[0];
            _warnedAboutMissingPresenter = false;
            return;
        }
    }

    private void ShowTooltip()
    {
        if (_tooltipShown)
            return;

        var tm = TooltipManager.Instance;
        if (tm == null)
            return;

        string hint = AppendActionHint(interactTooltipText, interactFallbackKeyLabel);
        tm.Show(hint);
        _tooltipShown = true;
    }

    private void HideTooltip()
    {
        if (!_tooltipShown)
            return;

        TooltipManager.Instance?.Hide();
        _tooltipShown = false;
    }

    private static string AppendActionHint(string baseText, string actionLabel)
    {
        if (string.IsNullOrWhiteSpace(actionLabel))
            return baseText;
        if (string.IsNullOrWhiteSpace(baseText))
            return $"[{actionLabel}]";
        if (baseText.Contains($"[{actionLabel}]") || baseText.Contains($"({actionLabel})"))
            return baseText;
        return $"{baseText} [{actionLabel}]";
    }
}

public class GamblingMachineTriggerRelay : MonoBehaviour
{
    private GamblingMachineInteractable _interactable;

    public void Init(GamblingMachineInteractable interactable) => _interactable = interactable;

    private void OnTriggerEnter2D(Collider2D other) => _interactable?.HandleTriggerEnter(other);

    private void OnTriggerExit2D(Collider2D other) => _interactable?.HandleTriggerExit(other);
}
