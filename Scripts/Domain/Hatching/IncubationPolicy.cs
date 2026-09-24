using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Hatching;

/// <summary>
/// How long an egg incubates, worked out once when the egg is created and then fixed like its
/// genes: a base time, plus time for every stat whose expressed rank is S, plus time for every
/// rare trait, plus time for a special variant such as the Swamp guy.
/// </summary>
public static class IncubationPolicy
{
    private const int SRank = 5;

    public static float RequiredSeconds(
        GenomeData genome,
        IReadOnlyCollection<RareTraitData>? rareTraits,
        bool isSpecialVariant,
        HatchingRules rules)
    {
        ArgumentNullException.ThrowIfNull(genome);
        ArgumentNullException.ThrowIfNull(rules);

        var sRankStats = genome.AbilityGenes?.Values.Count(gene => gene != null && gene.ExpressedValue >= SRank) ?? 0;
        var traits = rareTraits?.Count(trait => trait != null && !string.IsNullOrWhiteSpace(trait.TraitId)) ?? 0;
        var seconds = rules.IncubationSeconds +
                      sRankStats * rules.SecondsPerSRankStat +
                      traits * rules.SecondsPerRareTrait +
                      (isSpecialVariant ? rules.SpecialVariantSeconds : 0.0f);
        return float.IsFinite(seconds) ? Math.Max(0.1f, seconds) : Math.Max(0.1f, rules.IncubationSeconds);
    }
}
