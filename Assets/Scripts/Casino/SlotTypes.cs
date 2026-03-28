using System;

[Serializable]
public enum SlotSymbolId
{
    Cherry = 0,
    Lemon = 1,
    Bell = 2,
    Seven = 3,
    Diamond = 4
}

[Serializable]
public enum SpinFailureReason
{
    None = 0,
    InvalidBet = 1,
    InsufficientFunds = 2,
    ConfigurationError = 3
}

[Serializable]
public struct SpinRequest
{
    public float BetAmount;
    public int ReelCount;
    public int RowCount;
    public int? ForcedSeed;
}

[Serializable]
public sealed class SpinResult
{
    public bool IsSuccess;
    public bool IsWin;
    public SpinFailureReason FailureReason;

    // Flattened matrix in row-major order: index = row * ReelCount + reel.
    public SlotSymbolId[] VisibleSymbols = Array.Empty<SlotSymbolId>();

    // Currently one payline for baseline version (center line).
    public int[] WinningRows = Array.Empty<int>();

    public float BetAmount;
    public float PayoutMultiplier;
    public float PayoutAmount;
    public float BalanceBefore;
    public float BalanceAfter;
    public int LevelBefore;
    public int LevelAfter;
    public float MinBetForLevel;
}
