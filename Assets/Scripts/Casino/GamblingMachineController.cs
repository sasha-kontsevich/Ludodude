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
        float minBetForLevel = CurrentMinBet;

        SpinResult failResult = ValidateSpinRequest(gm, betAmount, balanceBefore, minBetForLevel);
        if (failResult != null)
        {
            RaiseSpinCompleted(failResult);
            return failResult;
        }

        gm.CasinoDeposit -= betAmount;

        var request = new SpinRequest
        {
            BetAmount = betAmount,
            ReelCount = config.ReelCount,
            RowCount = config.RowCount,
            ForcedSeed = forcedSeed
        };

        var result = _engine.Spin(request);
        result.BetAmount = betAmount;
        result.BalanceBefore = balanceBefore;
        result.LevelBefore = CurrentLevel;
        result.MinBetForLevel = minBetForLevel;

        if (result.IsSuccess && result.PayoutAmount > 0f)
            gm.CasinoDeposit += result.PayoutAmount;

        if (result.IsSuccess)
            currentLevel = CurrentLevel + 1;

        result.BalanceAfter = gm.CasinoDeposit;
        result.LevelAfter = CurrentLevel;
        RaiseSpinCompleted(result);
        return result;
    }

    private SpinResult ValidateSpinRequest(GameManager gm, float betAmount, float balanceBefore, float minBetForLevel)
    {
        if (gm == null)
        {
            return CreateFailedResult(SpinFailureReason.ConfigurationError, betAmount, balanceBefore, minBetForLevel);
        }

        if (_engine == null || config == null || !config.HasValidWeights())
        {
            return CreateFailedResult(SpinFailureReason.ConfigurationError, betAmount, balanceBefore, minBetForLevel);
        }

        float clampedBet = Mathf.Max(0f, betAmount);
        float maxBetForLevel = Mathf.Max(minBetForLevel, config.MaxBet);
        if (clampedBet < minBetForLevel || clampedBet > maxBetForLevel)
            return CreateFailedResult(SpinFailureReason.InvalidBet, betAmount, balanceBefore, minBetForLevel);

        if (gm.CasinoDeposit < clampedBet)
            return CreateFailedResult(SpinFailureReason.InsufficientFunds, betAmount, balanceBefore, minBetForLevel);

        return null;
    }

    private SpinResult CreateFailedResult(SpinFailureReason reason, float betAmount, float balance, float minBetForLevel)
    {
        return new SpinResult
        {
            IsSuccess = false,
            IsWin = false,
            FailureReason = reason,
            BetAmount = Mathf.Max(0f, betAmount),
            BalanceBefore = balance,
            BalanceAfter = balance,
            PayoutAmount = 0f,
            PayoutMultiplier = 0f,
            LevelBefore = CurrentLevel,
            LevelAfter = CurrentLevel,
            MinBetForLevel = minBetForLevel
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
