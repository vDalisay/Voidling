using System;
using Voidling.Domain.Care;
using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
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
    private readonly StatProgressionService _stats = new();

    public LifecycleEndDecision Decide(VoidlingData creature, ReincarnationRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);

        var happiness = Math.Clamp(creature.Needs?.Happiness ?? 0.0f, 0.0f, 100.0f);
        return new LifecycleEndDecision(
            happiness >= rules.MinimumHappiness ? LifecycleEndOutcome.Reincarnate : LifecycleEndOutcome.Die,
            happiness);
    }

    /// <summary>
    /// Starts a new life: a baby again, every stat back to level 1 keeping a share of its points
    /// (Chao Garden), and hidden care state reset.
    /// </summary>
    public void ApplyReincarnation(VoidlingData creature, ReincarnationRules rules, StatGrowthRules statRules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(statRules);

        _stats.ApplyReincarnation(creature, rules.RetainedPointFraction, statRules);

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
        // A baby again: the adult form is chosen afresh at the next adulthood. A special variant
        // keeps its own look through every life.
        creature.Appearance ??= new VoidlingAppearanceData();
        if (string.IsNullOrEmpty(creature.SpecialVariantId))
            creature.Appearance.VisualTypeId = EvolutionService.BabyVisualTypeId;
        creature.Needs = new CreatureNeedsState();
    }
}