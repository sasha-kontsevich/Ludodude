using System;
using UnityEngine;

[DisallowMultipleComponent]
public class GamblingMachineController : MonoBehaviour
{
    [SerializeField] private SlotMachineConfig config;
    [SerializeField] private bool logSpinsToConsole;

    private GamblingMachineEngine _engine;

    public event Action<SpinResult> OnSpinCompleted;

    public SlotMachineConfig Config => config;

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
        return config.GetBetAmountByNormalized(normalized);
    }

    public SpinResult TrySpin(float betAmount, int? forcedSeed = null)
    {
        var gm = GameManager.Instance;
        float balanceBefore = gm != null ? gm.CasinoDeposit : 0f;

        SpinResult failResult = ValidateSpinRequest(gm, betAmount, balanceBefore);
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

        if (result.IsSuccess && result.PayoutAmount > 0f)
            gm.CasinoDeposit += result.PayoutAmount;

        result.BalanceAfter = gm.CasinoDeposit;
        RaiseSpinCompleted(result);
        return result;
    }

    private SpinResult ValidateSpinRequest(GameManager gm, float betAmount, float balanceBefore)
    {
        if (gm == null)
        {
            return CreateFailedResult(SpinFailureReason.ConfigurationError, betAmount, balanceBefore);
        }

        if (_engine == null || config == null || !config.HasValidWeights())
        {
            return CreateFailedResult(SpinFailureReason.ConfigurationError, betAmount, balanceBefore);
        }

        float clampedBet = Mathf.Max(0f, betAmount);
        if (clampedBet < config.MinBet || clampedBet > config.MaxBet)
            return CreateFailedResult(SpinFailureReason.InvalidBet, betAmount, balanceBefore);

        if (gm.CasinoDeposit < clampedBet)
            return CreateFailedResult(SpinFailureReason.InsufficientFunds, betAmount, balanceBefore);

        return null;
    }

    private SpinResult CreateFailedResult(SpinFailureReason reason, float betAmount, float balance)
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
            PayoutMultiplier = 0f
        };
    }

    private void RaiseSpinCompleted(SpinResult result)
    {
        OnSpinCompleted?.Invoke(result);
        if (!logSpinsToConsole || result == null)
            return;

        Debug.Log(
            $"[GamblingMachine] success={result.IsSuccess}, win={result.IsWin}, reason={result.FailureReason}, " +
            $"bet={result.BetAmount:0.##}, payout={result.PayoutAmount:0.##}, before={result.BalanceBefore:0.##}, after={result.BalanceAfter:0.##}",
            this);
    }
}
