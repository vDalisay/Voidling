using System;
using System.Collections.Generic;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Domain.Evolution;

/// <summary>
/// The adult form a baby grows into. <see cref="Generalist"/> is the Neutral form; the name is kept
/// because the value is persisted.
/// </summary>
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
    int NewRank,
    string DecidingStatId = "",
    int DecidingLevel = 0)
{
    public bool Promoted => NewRank > PreviousRank;
}

/// <summary>
/// Pure deterministic adulthood rule. At the baby → adult transition the stat with the highest
/// level decides the form, provided it reached the minimum level; stamina on top, or no stat at the
/// minimum, makes a Neutral adult. Stats tied for highest are an equal random pick, seeded by the
/// creature and life so it is reproducible. The winning stat's expressed allele is promoted one rank
/// (Neutral promotes stamina) and the form sets the semantic visual type.
/// </summary>
public static class EvolutionService
{
    public const string BabyVisualTypeId = VoidlingAppearanceData.DefaultVisualTypeId;
    public const string NeutralVisualTypeId = "neutral";

    private static readonly (string StatId, EvolutionSpecialization Form)[] FormStats =
    {
        ("run", EvolutionSpecialization.Run),
        ("swim", EvolutionSpecialization.Swim),
        ("fly", EvolutionSpecialization.Fly),
        ("power", EvolutionSpecialization.Power),
        ("stamina", EvolutionSpecialization.Generalist)
    };

    public static EvolutionResult ResolveFirstEvolution(VoidlingData creature, GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(rules);

        // Existing/loaded adults may already have a resolved form. Never promote twice.
        if (creature.EvolutionSpecialization != EvolutionSpecialization.None)
            return new EvolutionResult(creature.EvolutionSpecialization, string.Empty, 0, 0);

        var stats = new StatCalculator(rules.Stats);
        var topLevel = -1;
        var tied = new List<(string StatId, EvolutionSpecialization Form)>();
        foreach (var candidate in FormStats)
        {
            var level = stats.GetLevel(creature, candidate.StatId);
            if (level > topLevel)
            {
                topLevel = level;
                tied.Clear();
            }

            if (level == topLevel)
                tied.Add(candidate);
        }

        var chosen = tied.Count == 1
            ? tied[0]
            : tied[StableRandom
                .Create(0UL, $"adult-form:{creature.Id}:{Math.Max(0, creature.ReincarnationCount)}")
                .Next(tied.Count)];
        var specialization = topLevel >= rules.Evolution.MinimumFormLevel
            ? chosen.Form
            : EvolutionSpecialization.Generalist;
        var promotedStatId = specialization == EvolutionSpecialization.Generalist ? "stamina" : chosen.StatId;

        creature.EvolutionSpecialization = specialization;
        creature.Appearance ??= new VoidlingAppearanceData();
        creature.Appearance.VisualTypeId = VisualTypeFor(specialization);

        var promotion = PromoteExpressedAllele(creature, promotedStatId, rules, specialization);
        return promotion with { DecidingStatId = chosen.StatId, DecidingLevel = Math.Max(0, topLevel) };
    }

    /// <summary>The semantic visual type of a form; the art catalog decides what it looks like.</summary>
    public static string VisualTypeFor(EvolutionSpecialization specialization) => specialization switch
    {
        EvolutionSpecialization.Run => "run",
        EvolutionSpecialization.Swim => "water",
        EvolutionSpecialization.Fly => "fly",
        EvolutionSpecialization.Power => "power",
        EvolutionSpecialization.Generalist => NeutralVisualTypeId,
        _ => BabyVisualTypeId
    };

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
