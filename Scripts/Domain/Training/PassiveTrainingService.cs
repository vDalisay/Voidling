using System;
using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Domain.Training;

public readonly record struct PassiveTrainingStepResult(
    bool Changed,
    string StatId,
    int PointsGained,
    bool ReachedCap);

/// <summary>
/// Advances one semantic passive-training assignment from explicit open-game elapsed time.
/// Legacy assignments use the global base rate; module-backed assignments use the cached rate
/// refreshed by Application/migration from the authoritative module level and placement.
/// </summary>
public sealed class PassiveTrainingService
{
    public PassiveTrainingStepResult Advance(VoidlingData creature, float elapsedSeconds, GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);

        var statId = creature.PassiveTrainingStatId ?? string.Empty;
        var pointsPerMinute = string.IsNullOrEmpty(creature.PassiveTrainingModuleId)
            ? rules.PassiveTraining.PointsPerMinute
            : creature.PassiveTrainingPointsPerMinute;
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0.0f ||
            string.IsNullOrEmpty(statId) || !ContainsStat(rules, statId) ||
            !float.IsFinite(pointsPerMinute) || pointsPerMinute <= 0.0f)
        {
            return new PassiveTrainingStepResult(false, statId, 0, false);
        }

        var stats = new StatCalculator(rules.Stats);
        var cap = stats.GetTrainingPointCap(creature, statId);
        var stored = stats.GetTrainingPoints(creature, statId);
        var current = Math.Clamp(stored, 0, cap);
        var changed = current != stored;
        if (changed)
            creature.TrainingPoints[statId] = current;

        if (current >= cap)
        {
            if (creature.PassiveTrainingPointRemainder != 0.0)
            {
                creature.PassiveTrainingPointRemainder = 0.0;
                changed = true;
            }

            return new PassiveTrainingStepResult(changed, statId, 0, false);
        }

        var remainder = NormalizeRemainder(creature.PassiveTrainingPointRemainder);
        var total = remainder + elapsedSeconds * (double)pointsPerMinute / 60.0;
        if (!double.IsFinite(total) || total < 0.0)
            return new PassiveTrainingStepResult(changed, statId, 0, false);

        var availableWholePoints = Math.Floor(total);
        var gain = (int)Math.Min(availableWholePoints, cap - current);
        var reachedCap = current + gain >= cap;
        var nextRemainder = reachedCap ? 0.0 : total - availableWholePoints;

        if (gain > 0)
        {
            creature.TrainingPoints[statId] = current + gain;
            EvolutionService.ApplyTrainingInfluence(creature, statId, gain, rules.Stats);
            changed = true;
        }

        if (!creature.PassiveTrainingPointRemainder.Equals(nextRemainder))
        {
            creature.PassiveTrainingPointRemainder = nextRemainder;
            changed = true;
        }

        return new PassiveTrainingStepResult(changed, statId, gain, reachedCap && gain > 0);
    }

    private static bool ContainsStat(GameBalanceRules rules, string statId)
    {
        foreach (var candidate in rules.Genetics.StatIds)
        {
            if (string.Equals(candidate, statId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static double NormalizeRemainder(double value)
        => double.IsFinite(value) && value >= 0.0 && value < 1.0 ? value : 0.0;
}
