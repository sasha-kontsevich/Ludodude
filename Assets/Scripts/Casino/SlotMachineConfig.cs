using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SlotMachineConfig", menuName = "Casino/Slot Machine Config")]
public class SlotMachineConfig : ScriptableObject
{
    [Header("Layout")]
    [SerializeField] private int reelCount = 3;
    [SerializeField] private int rowCount = 3;

    [Header("Bet")]
    [SerializeField] private float minBet = 5f;
    [SerializeField] private float maxBet = 100f;

    [Header("Win tuning")]
    [Tooltip("Chance that a spin is generated as a winning one. 0 = always lose, 1 = always win.")]
    [SerializeField] [Range(0f, 1f)] private float victoryChance = 0.2f;

    [Header("Symbols")]
    [SerializeField] private List<SymbolWeightEntry> symbolWeights = new List<SymbolWeightEntry>
    {
        new SymbolWeightEntry { Symbol = SlotSymbolId.Cherry, Weight = 40f },
        new SymbolWeightEntry { Symbol = SlotSymbolId.Lemon, Weight = 30f },
        new SymbolWeightEntry { Symbol = SlotSymbolId.Bell, Weight = 16f },
        new SymbolWeightEntry { Symbol = SlotSymbolId.Seven, Weight = 10f },
        new SymbolWeightEntry { Symbol = SlotSymbolId.Diamond, Weight = 4f },
    };

    [Header("Payouts for 3 in center line")]
    [SerializeField] private List<PayoutEntry> payoutTable = new List<PayoutEntry>
    {
        new PayoutEntry { Symbol = SlotSymbolId.Cherry, Multiplier = 1.5f },
        new PayoutEntry { Symbol = SlotSymbolId.Lemon, Multiplier = 2f },
        new PayoutEntry { Symbol = SlotSymbolId.Bell, Multiplier = 3f },
        new PayoutEntry { Symbol = SlotSymbolId.Seven, Multiplier = 6f },
        new PayoutEntry { Symbol = SlotSymbolId.Diamond, Multiplier = 12f },
    };

    public int ReelCount
    {
        get => Mathf.Max(1, reelCount);
        set => reelCount = Mathf.Max(1, value);
    }

    public int RowCount
    {
        get => Mathf.Max(1, rowCount);
        set => rowCount = Mathf.Max(1, value);
    }

    public float MinBet
    {
        get => Mathf.Max(0.01f, minBet);
        set => minBet = Mathf.Max(0.01f, value);
    }

    public float MaxBet
    {
        get => Mathf.Max(MinBet, maxBet);
        set => maxBet = Mathf.Max(MinBet, value);
    }

    public float VictoryChance
    {
        get => Mathf.Clamp01(victoryChance);
        set => victoryChance = Mathf.Clamp01(value);
    }

    public IReadOnlyList<SymbolWeightEntry> SymbolWeights => symbolWeights;
    public IReadOnlyList<PayoutEntry> PayoutTable => payoutTable;

    public float GetBetAmountByNormalized(float normalized)
    {
        float t = Mathf.Clamp01(normalized);
        return Mathf.Lerp(MinBet, MaxBet, t);
    }

    public bool TryGetPayoutMultiplier(SlotSymbolId symbol, out float multiplier)
    {
        for (int i = 0; i < payoutTable.Count; i++)
        {
            var row = payoutTable[i];
            if (row.Symbol != symbol)
                continue;

            multiplier = Mathf.Max(0f, row.Multiplier);
            return true;
        }

        multiplier = 0f;
        return false;
    }

    public bool HasValidWeights()
    {
        if (symbolWeights == null || symbolWeights.Count == 0)
            return false;

        float sum = 0f;
        for (int i = 0; i < symbolWeights.Count; i++)
            sum += Mathf.Max(0f, symbolWeights[i].Weight);

        return sum > 0f;
    }
}

[Serializable]
public struct SymbolWeightEntry
{
    public SlotSymbolId Symbol;
    public float Weight;
}

[Serializable]
public struct PayoutEntry
{
    public SlotSymbolId Symbol;
    public float Multiplier;
}
