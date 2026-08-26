using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Evolution;

public enum EvolutionSpecialization
{
    None,
    Generalist,
    Run,
    Swim,
    Fly,
    Power
}

public readonly record struct EvolutionResult(
    EvolutionSpecialization Specialization,
    string PromotedStatId,
    int PreviousRank,
    int NewRank)
{
    public bool Promoted => NewRank > PreviousRank;
}

/// <summary>
/// Pure deterministic first-evolution rules. Child training changes hidden raising influence while
/// inherited ability ranks remain untouched until the Child -> Adult transition explicitly
/// promotes the currently expressed allele. Presentation art is deliberately not part of this
/// service so production evolution sprites can be added later through the visual pipeline.
/// </summary>
public static class EvolutionService
{
    public static void ApplyTrainingInfluence(
        VoidlingData creature,
        string statId,
        int appliedTrainingPoints,
        StatGrowthRules statRules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(statRules);
        if (creature.Stage != LifeStage.Child || appliedTrainingPoints <= 0)
            return;

        var delta = appliedTrainingPoints / (float)Math.Max(1, statRules.MaxTrainingPoints);
        switch (statId)
        {
            case "swim":
                creature.SwimFlyInfluence = Math.Clamp(creature.SwimFlyInfluence - delta, -1.0f, 1.0f);
                break;
            case "fly":
                creature.SwimFlyInfluence = Math.Clamp(creature.SwimFlyInfluence + delta, -1.0f, 1.0f);
                break;
            case "run":
                creature.RunPowerInfluence = Math.Clamp(creature.RunPowerInfluence - delta, -1.0f, 1.0f);
                break;
            case "power":
                creature.RunPowerInfluence = Math.Clamp(creature.RunPowerInfluence + delta, -1.0f, 1.0f);
                break;
        }
    }

    public static EvolutionResult ResolveFirstEvolution(VoidlingData creature, GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);

        // Existing/loaded adults may already have a resolved semantic form. Never promote twice.
        if (creature.EvolutionSpecialization != EvolutionSpecialization.None)
            return new EvolutionResult(creature.EvolutionSpecialization, string.Empty, 0, 0);

        var candidates = new[]
        {
            (Specialization: EvolutionSpecialization.Run, StatId: "run", Magnitude: Math.Max(0.0f, -creature.RunPowerInfluence)),
            (Specialization: EvolutionSpecialization.Swim, StatId: "swim", Magnitude: Math.Max(0.0f, -creature.SwimFlyInfluence)),
            (Specialization: EvolutionSpecialization.Fly, StatId: "fly", Magnitude: Math.Max(0.0f, creature.SwimFlyInfluence)),
            (Specialization: EvolutionSpecialization.Power, StatId: "power", Magnitude: Math.Max(0.0f, creature.RunPowerInfluence))
        };

        var selected = candidates[0];
        for (var i = 1; i < candidates.Length; i++)
        {
            // Strictly greater keeps the array order as a stable deterministic tie-break.
            if (candidates[i].Magnitude > selected.Magnitude)
                selected = candidates[i];
        }

        var threshold = Math.Clamp(rules.Evolution.SpecializationThreshold, 0.0f, 1.0f);
        var specialization = selected.Magnitude >= threshold
            ? selected.Specialization
            : EvolutionSpecialization.Generalist;
        var promotedStatId = specialization == EvolutionSpecialization.Generalist
            ? "stamina"
            : selected.StatId;

        creature.EvolutionSpecialization = specialization;
        creature.EvolutionMagnitude = selected.Magnitude;

        return PromoteExpressedAllele(creature, promotedStatId, rules, specialization);
    }

    private static EvolutionResult PromoteExpressedAllele(
        VoidlingData creature,
        string statId,
        GameBalanceRules rules,
        EvolutionSpecialization specialization)
    {
        if (!creature.Genome.AbilityGenes.TryGetValue(statId, out var gene))
            return new EvolutionResult(specialization, statId, 0, 0);

        var maxRank = Math.Max(0, rules.Genetics.GradeWeights.Count - 1);
        var previous = Math.Clamp(gene.ExpressedValue, 0, maxRank);
        var promoted = Math.Min(maxRank, previous + 1);

        if (gene.ExpressedAlleleIndex == 0)
            gene.AlleleA = promoted;
        else
            gene.AlleleB = promoted;

        return new EvolutionResult(specialization, statId, previous, promoted);
    }
}
