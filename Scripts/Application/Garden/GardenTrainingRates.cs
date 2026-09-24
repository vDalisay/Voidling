using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Garden;

/// <summary>
/// Keeps the passive training cached on Voidlings in step with the hex they train on. The hex is
/// the source of truth for stat and rate; creatures only carry a copy for the simulation.
/// </summary>
internal static class GardenTrainingRates
{
    public static float RateFor(GardenModuleData module, GardenModuleRules rules)
        => module.Placed && module.StatId.Length > 0
            ? rules.PointsPerMinuteForLevel(module.Level)
            : 0.0f;

    /// <summary>Refreshes stat and rate for every Voidling assigned to a hex after the hex changed.</summary>
    public static void RefreshAssignedCreatures(GameStateData state, GardenModuleData module, GardenModuleRules rules)
    {
        var rate = RateFor(module, rules);
        foreach (var creature in state.Voidlings)
        {
            if (!string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal))
                continue;

            creature.PassiveTrainingStatId = module.StatId;
            creature.PassiveTrainingPointsPerMinute = rate;
            if (rate <= 0.0f)
                creature.PassiveTrainingPointRemainder = 0.0;
        }
    }

    /// <summary>Stops every Voidling training on a hex, keeping the progress they already have.</summary>
    public static void UnassignCreatures(GameStateData state, string moduleId)
    {
        foreach (var creature in state.Voidlings)
        {
            if (!string.Equals(creature.PassiveTrainingModuleId, moduleId, StringComparison.Ordinal))
                continue;

            creature.PassiveTrainingStatId = string.Empty;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
        }
    }
}
