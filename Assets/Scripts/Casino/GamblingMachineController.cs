using System;
using UnityEngine;

[DisallowMultipleComponent]
public class GamblingMachineController : MonoBehaviour
{
    private enum StatBuffType
    {
        MaxCarryCost,
        SpeedMultiplier,
        SellPriceMultiplier
    }

    [SerializeField] private SlotMachineConfig config;
    [SerializeField] private bool logSpinsToConsole;
    [SerializeField] private int currentLevel;
    [Header("Win reward")]
    [SerializeField] private Item winRewardItemPrefab;
    [SerializeField] private Transform winRewardSpawnPoint;
    [SerializeField] private bool spawnRewardOnlyOnce = true;
    [Header("Defeat bonuses")]
    [SerializeField] private bool buffPlayerOnDefeat = true;
    [SerializeField] private Vector2Int maxCarryCostBuffRange = new Vector2Int(8, 25);
    [SerializeField] private Vector2 speedMultiplierBuffRange = new Vector2(0.25f, 0.9f);
    [SerializeField] private Vector2 sellMultiplierBuffRange = new Vector2(1.5f, 4f);
    [SerializeField] [Min(1f)] private float defeatBuffGrowthBase = 1.25f;
    [SerializeField] private float defeatBuffTooltipDuration = 2.6f;
    [SerializeField] private string defeatBuffTooltipFormat = "Поражение в слоте #{3}: +{0} к {1} (текущее: {2})";

    private GamblingMachineEngine _engine;
    private bool _rewardSpawned;
    private CharacterStats _cachedPlayerStats;
    private int _defeatCount;

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

        TryApplyDefeatBuff(result);
        TrySpawnWinReward(result);

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

    private void TrySpawnWinReward(SpinResult result)
    {
        if (result == null || !result.IsSuccess || !result.IsWin)
            return;
        if (spawnRewardOnlyOnce && _rewardSpawned)
            return;
        if (winRewardItemPrefab == null)
            return;

        Vector3 spawnPos = winRewardSpawnPoint != null
            ? winRewardSpawnPoint.position
            : transform.position + Vector3.up * 1.25f;

        var spawned = Instantiate(winRewardItemPrefab, spawnPos, Quaternion.identity);
        if (spawned != null && spawned.GetComponent<WinGoalItem>() == null)
            spawned.gameObject.AddComponent<WinGoalItem>();

        _rewardSpawned = true;
    }

    private void TryApplyDefeatBuff(SpinResult result)
    {
        if (!buffPlayerOnDefeat || result == null || !result.IsSuccess || result.IsWin)
            return;

        _defeatCount++;

        CharacterStats stats = ResolvePlayerStats();
        if (stats == null)
            return;

        StatBuffType selected = (StatBuffType)UnityEngine.Random.Range(0, 3);
        string deltaText;
        string statName;
        string currentValueText;
        float growthFactor = Mathf.Pow(Mathf.Max(1f, defeatBuffGrowthBase), Mathf.Max(0, _defeatCount - 1));

        switch (selected)
        {
            case StatBuffType.MaxCarryCost:
            {
                int min = Mathf.Min(maxCarryCostBuffRange.x, maxCarryCostBuffRange.y);
                int max = Mathf.Max(maxCarryCostBuffRange.x, maxCarryCostBuffRange.y);
                int baseDelta = UnityEngine.Random.Range(min, max + 1);
                int delta = Mathf.Max(1, Mathf.RoundToInt(baseDelta * growthFactor));
                int value = stats.AddMaxCarryCost(delta);
                deltaText = delta.ToString();
                statName = "грузоподъемности";
                currentValueText = value.ToString();
                break;
            }
            case StatBuffType.SpeedMultiplier:
            {
                float min = Mathf.Min(speedMultiplierBuffRange.x, speedMultiplierBuffRange.y);
                float max = Mathf.Max(speedMultiplierBuffRange.x, speedMultiplierBuffRange.y);
                float baseDelta = UnityEngine.Random.Range(min, max);
                float delta = baseDelta;
                float value = stats.AddSpeedMultiplier(delta);
                deltaText = delta.ToString("0.##");
                statName = "скорости";
                currentValueText = value.ToString("0.##");
                break;
            }
            default:
            {
                float min = Mathf.Min(sellMultiplierBuffRange.x, sellMultiplierBuffRange.y);
                float max = Mathf.Max(sellMultiplierBuffRange.x, sellMultiplierBuffRange.y);
                float baseDelta = UnityEngine.Random.Range(min, max);
                float delta = baseDelta * growthFactor;
                float value = stats.AddSellPriceMultiplier(delta);
                deltaText = delta.ToString("0.##");
                statName = "множителя продажи";
                currentValueText = value.ToString("0.##");
                break;
            }
        }

        TooltipManager.Instance?.Show(
            string.Format(defeatBuffTooltipFormat, deltaText, statName, currentValueText, _defeatCount),
            defeatBuffTooltipDuration);
    }

    private CharacterStats ResolvePlayerStats()
    {
        if (_cachedPlayerStats != null)
            return _cachedPlayerStats;

        TopDownPlayerController player = FindFirstObjectByType<TopDownPlayerController>(FindObjectsInactive.Exclude);
        if (player != null)
        {
            _cachedPlayerStats = player.GetComponent<CharacterStats>();
            if (_cachedPlayerStats != null)
                return _cachedPlayerStats;
        }

        _cachedPlayerStats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Exclude);
        return _cachedPlayerStats;
    }
}
