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
    [SerializeField] private bool showPanelOnEnter = false;
    [SerializeField] private bool hidePanelOnExit = true;
    [SerializeField] private bool openByInteract = true;
    [SerializeField] private bool togglePanelByInteract = true;

    [Header("Tooltip")]
    [SerializeField] private bool showTooltip = true;
    [SerializeField] private string interactTooltipText = "Играть в слот (E)";

    private TopDownPlayerController _playerInZone;
    private bool _tooltipShown;
    private bool _warnedAboutMissingPresenter;

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

        if (zoneTrigger != null && zoneTrigger.gameObject != gameObject)
        {
            var relay = zoneTrigger.GetComponent<GamblingMachineTriggerRelay>();
            if (relay == null)
                relay = zoneTrigger.gameObject.AddComponent<GamblingMachineTriggerRelay>();
            relay.Init(this);
        }

        ResolvePresenterIfNeeded();
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
        if (panelPresenter == null || machineController == null || !showPanelOnEnter)
            return;

        panelPresenter.BindMachine(machineController);
        panelPresenter.ShowPanel();
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
            return;
        }

        ResolvePresenterIfNeeded();
        bool panelOpenForThisMachine = panelPresenter != null && panelPresenter.IsBoundTo(machineController);

        if (showTooltip && !panelOpenForThisMachine)
            ShowTooltip();
        else
            HideTooltip();

        if (!openByInteract || !WasInteractPressed())
            return;

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
        HideTooltip();
    }

    private static bool WasInteractPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
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

        var presenters = Object.FindObjectsByType<SlotMachinePanelPresenter>(FindObjectsSortMode.None);
        if (presenters != null && presenters.Length > 0)
        {
            panelPresenter = presenters[0];
            _warnedAboutMissingPresenter = false;
            return;
        }

        // Includes inactive objects as well. Filter out prefab assets.
        var allPresenters = Resources.FindObjectsOfTypeAll<SlotMachinePanelPresenter>();
        for (int i = 0; i < allPresenters.Length; i++)
        {
            var p = allPresenters[i];
            if (p == null)
                continue;
            if (!p.gameObject.scene.IsValid())
                continue;
            panelPresenter = p;
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

        tm.Show(interactTooltipText);
        _tooltipShown = true;
    }

    private void HideTooltip()
    {
        if (!_tooltipShown)
            return;

        TooltipManager.Instance?.Hide();
        _tooltipShown = false;
    }
}

public class GamblingMachineTriggerRelay : MonoBehaviour
{
    private GamblingMachineInteractable _interactable;

    public void Init(GamblingMachineInteractable interactable) => _interactable = interactable;

    private void OnTriggerEnter2D(Collider2D other) => _interactable?.HandleTriggerEnter(other);

    private void OnTriggerExit2D(Collider2D other) => _interactable?.HandleTriggerExit(other);
}
