using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class SlotMachinePanelPresenter : MonoBehaviour
{
    [SerializeField] private GamblingMachineController machineController;
    [SerializeField] private UIDragging betDrag;

    [Header("Optional UI labels")]
    [SerializeField] private TMP_Text betLabel;
    [SerializeField] private TMP_Text outcomeLabel;
    [SerializeField] private TMP_Text symbolsLabel;
    [SerializeField] private TMP_Text balanceLabel;

    [SerializeField] private string betFormat = "Bet: {0:0.##}";
    [SerializeField] private string balanceFormat = "Balance: {0:0.##}";

    private readonly StringBuilder _symbolsBuilder = new StringBuilder(64);

    private void Awake()
    {
        if (machineController == null)
            machineController = FindObjectOfType<GamblingMachineController>();
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

    private void Update()
    {
        if (machineController == null)
            return;

        float bet = GetCurrentBet();
        if (betLabel != null)
            betLabel.text = string.Format(betFormat, bet);

        if (balanceLabel != null && GameManager.Instance != null)
            balanceLabel.text = string.Format(balanceFormat, GameManager.Instance.CasinoDeposit);
    }

    // Hook this to a button onClick in UI.
    public void SpinFromUi()
    {
        if (machineController == null)
            return;

        machineController.TrySpin(GetCurrentBet());
    }

    public float GetCurrentBet()
    {
        float normalized = betDrag != null ? betDrag.value : 0f;
        return machineController != null ? machineController.GetBetAmountByNormalized(normalized) : 0f;
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
}
