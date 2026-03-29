using System;
using UnityEngine;

[DisallowMultipleComponent]
public class GamblingMachineController : MonoBehaviour
{
    [SerializeField] private SlotMachineConfig config;
    [SerializeField] private bool logSpinsToConsole;
    [SerializeField] private int currentLevel;

    private GamblingMachineEngine _engine;

    public event Action<SpinResult> OnSpinCompleted;

    public SlotMachineConfig Config => config;
    public int CurrentLevel => Mathf.Max(0, currentLevel);
    public float CurrentMinBet => config == null ? 0f : config.GetMinBetForLevel(CurrentLevel);
    public float CurrentSpinCost => Mathf.Max(0f, GameManager.Instance != null ? GameManager.Instance.GoalDeposit : 0f);

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogWarning("GamblingMachineController: SlotMachineConfig is not assigned.", this);
            return;
        }

        _engine = new GamblingMachineEngine(config);
    }

    public float GetBetAmountByNormalized(float normalized)
    {
        if (config == null)
            return 0f;
        return config.GetBetAmountByNormalized(normalized, CurrentLevel);
    }

    public SpinResult TrySpin(float betAmount, int? forcedSeed = null)
    {
        var gm = GameManager.Instance;
        float balanceBefore = gm != null ? gm.CasinoDeposit : 0f;
        float requiredSpinCost = CurrentSpinCost;

        SpinResult failResult = ValidateSpinRequest(gm, requiredSpinCost, balanceBefore);
        if (failResult != null)
        {
            RaiseSpinCompleted(failResult);
            return failResult;
        }

        gm.CasinoDeposit -= requiredSpinCost;

        var request = new SpinRequest
        {
            BetAmount = requiredSpinCost,
            ReelCount = config.ReelCount,
            RowCount = config.RowCount,
            ForcedSeed = forcedSeed
        };

        var result = _engine.Spin(request);
        gm.Level += 1;
        result.BetAmount = requiredSpinCost;
        result.BalanceBefore = balanceBefore;
        result.LevelBefore = CurrentLevel;
        result.MinBetForLevel = requiredSpinCost;

        if (result.IsSuccess && result.PayoutAmount > 0f)
            gm.CasinoDeposit += result.PayoutAmount;

        if (result.IsSuccess)
            currentLevel = CurrentLevel + 1;

        result.BalanceAfter = gm.CasinoDeposit;
        result.LevelAfter = CurrentLevel;
        RaiseSpinCompleted(result);
        return result;
    }

    private SpinResult ValidateSpinRequest(GameManager gm, float requiredSpinCost, float balanceBefore)
    {
        if (gm == null)
        {
            return CreateFailedResult(SpinFailureReason.ConfigurationError, requiredSpinCost, balanceBefore);
        }

        if (_engine == null || config == null || !config.HasValidWeights())
        {
            return CreateFailedResult(SpinFailureReason.ConfigurationError, requiredSpinCost, balanceBefore);
        }

        if (requiredSpinCost <= 0f)
            return CreateFailedResult(SpinFailureReason.InvalidBet, requiredSpinCost, balanceBefore);

        if (gm.CasinoDeposit < requiredSpinCost)
            return CreateFailedResult(SpinFailureReason.InsufficientFunds, requiredSpinCost, balanceBefore);

        return null;
    }

    private SpinResult CreateFailedResult(SpinFailureReason reason, float spinCost, float balance)
    {
        return new SpinResult
        {
            IsSuccess = false,
            IsWin = false,
            FailureReason = reason,
            BetAmount = Mathf.Max(0f, spinCost),
            BalanceBefore = balance,
            BalanceAfter = balance,
            PayoutAmount = 0f,
            PayoutMultiplier = 0f,
            LevelBefore = CurrentLevel,
            LevelAfter = CurrentLevel,
            MinBetForLevel = Mathf.Max(0f, spinCost)
        };
    }

    private void RaiseSpinCompleted(SpinResult result)
    {
        OnSpinCompleted?.Invoke(result);
        if (!logSpinsToConsole || result == null)
            return;

        Debug.Log(
            $"[GamblingMachine] success={result.IsSuccess}, win={result.IsWin}, reason={result.FailureReason}, " +
            $"bet={result.BetAmount:0.##}, minBet={result.MinBetForLevel:0.##}, level={result.LevelBefore}->{result.LevelAfter}, " +
            $"payout={result.PayoutAmount:0.##}, before={result.BalanceBefore:0.##}, after={result.BalanceAfter:0.##}",
            this);
    }
}
