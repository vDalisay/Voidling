using System;
using System.Linq;
using Voidling.Domain.Care;
using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Lifecycle;

public enum LifecycleEndOutcome
{
    Reincarnate,
    Die
}

public readonly record struct LifecycleEndDecision(
    LifecycleEndOutcome Outcome,
    float Happiness);

/// <summary>
/// Pure lifecycle-end policy and reincarnation mutation. Hidden happiness is the only condition:
/// at or above the authored threshold a Voidling reincarnates, below it the Voidling dies.
/// Reincarnation deliberately does not promote a DNA rank; that remains a separate product rule.
/// </summary>
public sealed class ReincarnationService
{
    public LifecycleEndDecision Decide(VoidlingData creature, ReincarnationRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);

        var happiness = Math.Clamp(creature.Needs?.Happiness ?? 0.0f, 0.0f, 100.0f);
        return new LifecycleEndDecision(
            happiness >= rules.MinimumHappiness ? LifecycleEndOutcome.Reincarnate : LifecycleEndOutcome.Die,
            happiness);
    }

    public void ApplyReincarnation(VoidlingData creature, ReincarnationRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);

        var retainedFraction = Math.Clamp(rules.RetainedTrainingFraction, 0.0f, 1.0f);
        foreach (var statId in creature.TrainingPoints.Keys.ToArray())
        {
            var current = Math.Max(0, creature.TrainingPoints[statId]);
            creature.TrainingPoints[statId] = (int)Math.Floor(current * retainedFraction);
        }

        creature.Stage = LifeStage.Child;
        creature.AgeSeconds = 0.0f;
        creature.AdultAgeSeconds = 0.0f;
        creature.BreedCooldownSeconds = 0.0f;
        creature.ReincarnationCount = Math.Max(0, creature.ReincarnationCount) + 1;
        creature.DepartureReason = CreatureDepartureReason.None;
        creature.SwimFlyInfluence = 0.0f;
        creature.RunPowerInfluence = 0.0f;
        creature.EvolutionSpecialization = EvolutionSpecialization.None;
        creature.EvolutionMagnitude = 0.0f;
        creature.Needs = new CreatureNeedsState();
    }
}