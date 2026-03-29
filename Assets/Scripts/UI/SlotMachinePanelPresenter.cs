using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class SlotMachinePanelPresenter : MonoBehaviour
{
    public static SlotMachinePanelPresenter Instance { get; private set; }

    [SerializeField] private GamblingMachineController machineController;
    [SerializeField] private UIDragging betDrag;
    [SerializeField] private GameObject panelRoot;

    [Header("Optional UI labels")]
    [SerializeField] private TMP_Text betLabel;
    [SerializeField] private TMP_Text outcomeLabel;
    [SerializeField] private TMP_Text symbolsLabel;
    [SerializeField] private TMP_Text balanceLabel;
    [SerializeField] private TMP_Text levelLabel;

    [SerializeField] private string betFormat = "Bet: {0:0.##}";
    [SerializeField] private string balanceFormat = "Balance: {0:0.##}";

    private readonly StringBuilder _symbolsBuilder = new StringBuilder(64);

    public GamblingMachineController CurrentMachine => machineController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple SlotMachinePanelPresenter instances found. Keeping the first one.", this);
            return;
        }

        Instance = this;

        if (panelRoot == null)
            panelRoot = gameObject;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (machineController != null)
            machineController.OnSpinCompleted += OnSpinCompleted;
    }

    private void OnDisable()
    {
        if (machineController != null)
            machineController.OnSpinCompleted -= OnSpinCompleted;
    }

    public void BindMachine(GamblingMachineController machine, bool clearResult = true)
    {
        if (machineController == machine)
            return;

        if (machineController != null)
            machineController.OnSpinCompleted -= OnSpinCompleted;

        machineController = machine;

        if (machineController != null && isActiveAndEnabled)
            machineController.OnSpinCompleted += OnSpinCompleted;

        if (clearResult)
            ClearResultLabels();
    }

    public void UnbindMachine(bool clearResult = true)
    {
        if (machineController != null)
            machineController.OnSpinCompleted -= OnSpinCompleted;

        machineController = null;

        if (clearResult)
            ClearResultLabels();
    }

    public bool IsBoundTo(GamblingMachineController machine)
    {
        return machineController == machine;
    }

    public void ShowPanel()
    {
        EnsurePanelRoot();
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void HidePanel()
    {
        EnsurePanelRoot();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (machineController == null)
            return;

        float bet = GetCurrentBet();
        if (betLabel != null)
            betLabel.text = string.Format(betFormat, bet);

        if (balanceLabel != null && GameManager.Instance != null)
            balanceLabel.text = string.Format(balanceFormat, GameManager.Instance.CasinoDeposit);

        if (levelLabel != null)
            levelLabel.text = $"Level: {machineController.CurrentLevel} | Min bet: {machineController.CurrentMinBet:0.##}";
    }

    // Hook this to a button onClick in UI.
    public void SpinFromUi()
    {
        if (machineController == null)
            return;

        machineController.TrySpin(machineController.CurrentSpinCost);
    }

    public float GetCurrentBet()
    {
        if (machineController == null)
            return 0f;

        return machineController.CurrentSpinCost;
    }

    private void OnSpinCompleted(SpinResult result)
    {
        if (outcomeLabel != null)
            outcomeLabel.text = BuildOutcomeText(result);

        if (symbolsLabel != null)
            symbolsLabel.text = BuildSymbolsText(result);
    }

    private string BuildOutcomeText(SpinResult result)
    {
        if (result == null)
            return "No result";

        if (!result.IsSuccess)
            return $"Spin failed: {result.FailureReason}";

        return result.IsWin
            ? $"WIN +{result.PayoutAmount:0.##}"
            : "LOSE";
    }

    private string BuildSymbolsText(SpinResult result)
    {
        if (result == null || result.VisibleSymbols == null || result.VisibleSymbols.Length == 0)
            return string.Empty;

        _symbolsBuilder.Clear();
        int reels = machineController != null && machineController.Config != null
            ? machineController.Config.ReelCount
            : 3;

        for (int i = 0; i < result.VisibleSymbols.Length; i++)
        {
            _symbolsBuilder.Append(result.VisibleSymbols[i]);
            bool endOfRow = reels > 0 && ((i + 1) % reels == 0);
            _symbolsBuilder.Append(endOfRow ? '\n' : " | ");
        }

        return _symbolsBuilder.ToString();
    }

    private void ClearResultLabels()
    {
        if (outcomeLabel != null)
            outcomeLabel.text = string.Empty;

        if (symbolsLabel != null)
            symbolsLabel.text = string.Empty;
    }

    private void EnsurePanelRoot()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
    }
}
