using System;
using UnityEngine;

public sealed class GamblingMachineEngine
{
    private readonly SlotMachineConfig _config;

    public GamblingMachineEngine(SlotMachineConfig config)
    {
        _config = config;
    }

    public SpinResult Spin(SpinRequest request)
    {
        var result = new SpinResult
        {
            IsSuccess = false,
            IsWin = false,
            FailureReason = SpinFailureReason.None,
            BetAmount = Mathf.Max(0f, request.BetAmount),
            PayoutAmount = 0f,
            PayoutMultiplier = 0f,
            WinningRows = Array.Empty<int>()
        };

        if (_config == null || !_config.HasValidWeights())
        {
            result.FailureReason = SpinFailureReason.ConfigurationError;
            return result;
        }

        int reels = Mathf.Max(1, request.ReelCount > 0 ? request.ReelCount : _config.ReelCount);
        int rows = Mathf.Max(1, request.RowCount > 0 ? request.RowCount : _config.RowCount);
        result.VisibleSymbols = new SlotSymbolId[reels * rows];

        var random = request.ForcedSeed.HasValue
            ? new System.Random(request.ForcedSeed.Value)
            : new System.Random(unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode());

        bool canAttemptWin = result.BetAmount >= GameManager.WinUnlockBet;
        bool shouldWin = canAttemptWin && random.NextDouble() <= _config.VictoryChance;
        FillGrid(result.VisibleSymbols, reels, rows, random);

        int centerRow = rows / 2;
        if (shouldWin)
            ForceCenterLineWin(result.VisibleSymbols, reels, rows, centerRow, random);
        else
            EnsureCenterLineLoss(result.VisibleSymbols, reels, rows, centerRow, random);

        SlotSymbolId lineSymbol = result.VisibleSymbols[centerRow * reels];
        bool isLineEqual = true;
        for (int reel = 1; reel < reels; reel++)
        {
            if (result.VisibleSymbols[centerRow * reels + reel] == lineSymbol)
                continue;
            isLineEqual = false;
            break;
        }

        result.IsSuccess = true;
        result.IsWin = isLineEqual;
        if (!result.IsWin || !_config.TryGetPayoutMultiplier(lineSymbol, out float mult))
            return result;

        result.PayoutMultiplier = mult;
        result.PayoutAmount = result.BetAmount * mult;
        result.WinningRows = new[] { centerRow };
        return result;
    }

    private void FillGrid(SlotSymbolId[] target, int reels, int rows, System.Random random)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int reel = 0; reel < reels; reel++)
                target[row * reels + reel] = RollWeightedSymbol(random);
        }
    }

    private void ForceCenterLineWin(SlotSymbolId[] target, int reels, int rows, int centerRow, System.Random random)
    {
        SlotSymbolId winner = RollWeightedSymbol(random);
        for (int reel = 0; reel < reels; reel++)
            target[centerRow * reels + reel] = winner;
    }

    private void EnsureCenterLineLoss(SlotSymbolId[] target, int reels, int rows, int centerRow, System.Random random)
    {
        int maxAttempts = 32;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            SlotSymbolId first = target[centerRow * reels];
            bool allSame = true;
            for (int reel = 1; reel < reels; reel++)
            {
                if (target[centerRow * reels + reel] == first)
                    continue;
                allSame = false;
                break;
            }

            if (!allSame)
                return;

            int reelToChange = reels <= 1 ? 0 : random.Next(1, reels);
            SlotSymbolId replacement = first;
            int rerolls = 0;
            while (replacement == first && rerolls < 32)
            {
                replacement = RollWeightedSymbol(random);
                rerolls++;
            }

            target[centerRow * reels + reelToChange] = replacement;
        }
    }

    private SlotSymbolId RollWeightedSymbol(System.Random random)
    {
        var weights = _config.SymbolWeights;
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
            total += Mathf.Max(0f, weights[i].Weight);

        if (total <= 0f)
            return SlotSymbolId.Cherry;

        double roll = random.NextDouble() * total;
        float cumulative = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            float w = Mathf.Max(0f, weights[i].Weight);
            cumulative += w;
            if (roll <= cumulative)
                return weights[i].Symbol;
        }

        return weights[weights.Count - 1].Symbol;
    }
}
