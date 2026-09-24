using System;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Domain.Training;

public readonly record struct PassiveTrainingStepResult(
    bool Changed,
    string StatId,
    int ProgressGained,
    bool ReachedCap);

/// <summary>
/// Advances one semantic passive-training assignment from explicit open-game elapsed time. The
/// rate is training progress per minute; whole steps fill the stat's level bar. Legacy
/// assignments use the global base rate; module-backed assignments use the cached rate refreshed
/// by Application/migration from the authoritative module level and placement.
/// </summary>
public sealed class PassiveTrainingService
{
    private readonly StatProgressionService _progression = new();

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
        var changed = false;
        if (stats.IsAtMaxLevel(creature, statId))
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

        var wholeSteps = Math.Floor(total);
        var gain = (int)Math.Min(wholeSteps, int.MaxValue);
        var result = _progression.AddProgress(creature, statId, gain, rules.Stats);
        changed |= result.Changed;

        var nextRemainder = stats.IsAtMaxLevel(creature, statId) ? 0.0 : total - wholeSteps;
        if (!creature.PassiveTrainingPointRemainder.Equals(nextRemainder))
        {
            creature.PassiveTrainingPointRemainder = nextRemainder;
            changed = true;
        }

        return new PassiveTrainingStepResult(changed, statId, result.ProgressApplied, result.ReachedMaxLevel);
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
