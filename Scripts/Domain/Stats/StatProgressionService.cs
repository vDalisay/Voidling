using System;
using System.Collections.Generic;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Stats;

public readonly record struct StatProgressResult(
    int ProgressApplied,
    int LevelsGained,
    int PointsGained,
    bool ReachedMaxLevel)
{
    public bool Changed => ProgressApplied > 0 || LevelsGained > 0;
}

/// <summary>
/// Chao Garden stat growth. Training fills a bar; a full bar is a level-up worth
/// <c>PerRank × rank + Base + random(1..RandomMax)</c> stat points, capped. Every rank can reach
/// the level cap; the rank only changes the points per level. The level-up roll is seeded by the
/// creature, stat, life and level, so the same creature always gains the same points.
/// </summary>
public sealed class StatProgressionService
{
    public StatProgressResult AddProgress(VoidlingData creature, string statId, int progress, StatGrowthRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);
        var stat = GetOrCreate(creature, statId);
        Normalize(stat, rules);
        if (progress <= 0 || stat.Level >= rules.MaxLevel)
            return new StatProgressResult(0, 0, 0, false);

        var levelsGained = 0;
        var pointsBefore = stat.Points;
        stat.Progress += progress;
        while (stat.Progress >= rules.ProgressPerLevel && stat.Level < rules.MaxLevel)
        {
            stat.Progress -= rules.ProgressPerLevel;
            stat.Level++;
            levelsGained++;
            stat.Points = Math.Min(rules.PointCap, stat.Points + RollLevelUpPoints(creature, statId, stat.Level, rules));
        }

        var reachedMax = stat.Level >= rules.MaxLevel;
        if (reachedMax)
            stat.Progress = 0;

        return new StatProgressResult(progress, levelsGained, stat.Points - pointsBefore, reachedMax && levelsGained > 0);
    }

    /// <summary>
    /// Stat points one level-up is worth. Uses the rank expressed right now, as Chao Garden does,
    /// so a rank promoted at adulthood makes later level-ups worth more.
    /// </summary>
    public int RollLevelUpPoints(VoidlingData creature, string statId, int newLevel, StatGrowthRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);
        var rank = Math.Clamp(StatCalculator.GetGene(creature, statId).ExpressedValue, 0, 5);
        var randomMax = Math.Max(1, rules.PointsPerLevelRandomMax);
        var roll = StableRandom
            .Create(0UL, $"stat-level:{creature.Id}:{statId}:{Math.Max(0, creature.ReincarnationCount)}:{newLevel}")
            .Next(1, randomMax + 1);
        return Math.Max(0, rules.PointsPerLevelPerRank * rank + rules.PointsPerLevelBase + roll);
    }

    /// <summary>Reincarnation, as in Chao Garden: every stat back to the start level, keeping a share of its points.</summary>
    public void ApplyReincarnation(VoidlingData creature, float retainedPointFraction, StatGrowthRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);
        var fraction = float.IsFinite(retainedPointFraction) ? Math.Clamp(retainedPointFraction, 0.0f, 1.0f) : 0.0f;
        foreach (var stat in creature.Stats.Values)
        {
            Normalize(stat, rules);
            stat.Level = Math.Clamp(rules.ReincarnationLevel, 0, rules.MaxLevel);
            stat.Progress = 0;
            stat.Points = (int)Math.Floor(stat.Points * fraction);
        }
    }

    /// <summary>
    /// Trains a stat from scratch up to a level, rolling every level-up on the way. Used for
    /// generated opponents, so they are built by the same rule a player's Voidling grows by.
    /// </summary>
    public void TrainToLevel(VoidlingData creature, string statId, int level, StatGrowthRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);
        creature.Stats[statId] = new StatProgressData();
        var target = Math.Clamp(level, 0, rules.MaxLevel);
        if (target > 0)
            AddProgress(creature, statId, target * rules.ProgressPerLevel, rules);
    }

    /// <summary>Gives a creature an entry for every stat, keeping existing progress in range.</summary>
    public static void EnsureStats(VoidlingData creature, IEnumerable<string> statIds, StatGrowthRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(statIds);
        ArgumentNullException.ThrowIfNull(rules);
        creature.Stats ??= new Dictionary<string, StatProgressData>(StringComparer.Ordinal);
        foreach (var statId in statIds)
            Normalize(GetOrCreate(creature, statId), rules);
    }

    /// <summary>A fresh set of stats at level 0, as a Voidling has when it hatches.</summary>
    public static Dictionary<string, StatProgressData> CreateNewborn(IEnumerable<string> statIds)
    {
        ArgumentNullException.ThrowIfNull(statIds);
        var stats = new Dictionary<string, StatProgressData>(StringComparer.Ordinal);
        foreach (var statId in statIds)
            stats[statId] = new StatProgressData();
        return stats;
    }

    private static StatProgressData GetOrCreate(VoidlingData creature, string statId)
    {
        creature.Stats ??= new Dictionary<string, StatProgressData>(StringComparer.Ordinal);
        if (!creature.Stats.TryGetValue(statId, out var stat) || stat == null)
        {
            stat = new StatProgressData();
            creature.Stats[statId] = stat;
        }

        return stat;
    }

    private static void Normalize(StatProgressData stat, StatGrowthRules rules)
    {
        stat.Level = Math.Clamp(stat.Level, 0, rules.MaxLevel);
        stat.Points = Math.Clamp(stat.Points, 0, rules.PointCap);
        stat.Progress = stat.Level >= rules.MaxLevel ? 0 : Math.Clamp(stat.Progress, 0, rules.ProgressPerLevel - 1);
    }
}
