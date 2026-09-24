using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Stats;

/// <summary>
/// Read-only view over a creature's Chao-style stat progress. Levels run 0-99 for every rank;
/// stat points (capped) are what races read, turned into a 0-100 race value.
/// </summary>
public sealed class StatCalculator
{
    private readonly StatGrowthRules _rules;

    public StatCalculator(StatGrowthRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public int GetLevel(VoidlingData data, string statId)
        => Math.Clamp(Find(data, statId)?.Level ?? 0, 0, _rules.MaxLevel);

    public int GetPoints(VoidlingData data, string statId)
        => Math.Clamp(Find(data, statId)?.Points ?? 0, 0, _rules.PointCap);

    public bool IsAtMaxLevel(VoidlingData data, string statId)
        => GetLevel(data, statId) >= _rules.MaxLevel;

    /// <summary>How full the bar toward the next level is, 0..1; a maxed stat reads as full.</summary>
    public float GetLevelProgress(VoidlingData data, string statId)
    {
        if (IsAtMaxLevel(data, statId))
            return 1.0f;

        var progress = Math.Clamp(Find(data, statId)?.Progress ?? 0, 0, _rules.ProgressPerLevel - 1);
        return progress / (float)_rules.ProgressPerLevel;
    }

    /// <summary>The 0-100 value the race simulation reads: stat points against the point cap.</summary>
    public float GetEffectiveStat(VoidlingData data, string statId)
        => Math.Clamp(GetPoints(data, statId) * 100.0f / Math.Max(1, _rules.PointCap), 0.0f, 100.0f);

    public static GenePairData GetGene(VoidlingData data, string statId)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Genome?.AbilityGenes != null &&
            data.Genome.AbilityGenes.TryGetValue(statId, out var gene) &&
            gene != null)
        {
            return gene;
        }

        return new GenePairData();
    }

    private static StatProgressData? Find(VoidlingData data, string statId)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Stats != null && data.Stats.TryGetValue(statId, out var progress) ? progress : null;
    }
}
