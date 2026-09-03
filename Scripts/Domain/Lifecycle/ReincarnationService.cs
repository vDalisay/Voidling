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
    float Happiness,
    float Stress);

/// <summary>
/// Pure lifecycle-end policy and reincarnation mutation. Eligibility is based on the confirmed
/// hidden-care inputs; the exact threshold and retention percentage are authorable prototype
/// balance. Reincarnation deliberately does not promote a DNA rank yet because the exact
/// reincarnation rank-promotion rule remains a separate unresolved product decision.
/// </summary>
public sealed class ReincarnationService
{
    public LifecycleEndDecision Decide(VoidlingData creature, ReincarnationRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);

        var happiness = Math.Clamp(creature.Needs?.Happiness ?? 0.0f, 0.0f, 100.0f);
        var stress = Math.Clamp(creature.Needs?.Stress ?? 0.0f, 0.0f, 100.0f);
        var eligible = happiness >= rules.MinimumHappiness && stress <= rules.MaximumStress;
        return new LifecycleEndDecision(
            eligible ? LifecycleEndOutcome.Reincarnate : LifecycleEndOutcome.Die,
            happiness,
            stress);
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