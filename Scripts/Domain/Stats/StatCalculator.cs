using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Stats;

public sealed class StatCalculator
{
    private readonly StatGrowthRules _rules;

    public StatCalculator(StatGrowthRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public int GetTrainingPoints(VoidlingData data, string statId)
        => data.TrainingPoints.TryGetValue(statId, out var points) ? points : 0;

    public int GetTrainingPointCap(VoidlingData data, string statId)
    {
        var rank = GetGene(data, statId).ExpressedValue;
        return Math.Clamp(_rules.RankCaps.ForRank(rank), 0, _rules.MaxTrainingPoints);
    }

    public int GetEffectiveTrainingPoints(VoidlingData data, string statId)
        => Math.Clamp(GetTrainingPoints(data, statId), 0, GetTrainingPointCap(data, statId));

    public int GetLevel(VoidlingData data, string statId)
        => Math.Clamp(1 + GetEffectiveTrainingPoints(data, statId) / _rules.TrainingPointsPerLevel, 1, _rules.MaxLevel);

    public float GetLevelProgress(VoidlingData data, string statId)
    {
        if (GetLevel(data, statId) >= _rules.MaxLevel)
            return 1.0f;

        return (GetEffectiveTrainingPoints(data, statId) % _rules.TrainingPointsPerLevel)
               / (float)_rules.TrainingPointsPerLevel;
    }

    public float GetEffectiveStat(VoidlingData data, string statId)
    {
        var grade = GetGene(data, statId).ExpressedValue;
        var training = GetEffectiveTrainingPoints(data, statId);
        return Math.Clamp(12.0f + grade * 13.0f + training * 0.55f, 0.0f, 100.0f);
    }

    public static GenePairData GetGene(VoidlingData data, string statId)
        => data.Genome.AbilityGenes.TryGetValue(statId, out var gene) ? gene : new GenePairData();
}
