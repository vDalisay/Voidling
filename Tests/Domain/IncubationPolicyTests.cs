using System.Collections.Generic;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

/// <summary>Stronger and rarer eggs take longer: each S-rank stat, rare trait and special variant adds time.</summary>
public sealed class IncubationPolicyTests
{
    private static readonly HatchingRules Rules = GameBalanceRules.DemoDefaults.Hatching;

    [Fact]
    public void OrdinaryEgg_TakesTheBaseTime()
    {
        Assert.Equal(Rules.IncubationSeconds, IncubationPolicy.RequiredSeconds(Genome(sRanks: 0), null, false, Rules));
    }

    [Fact]
    public void EverySRankStat_AddsTime()
    {
        var one = IncubationPolicy.RequiredSeconds(Genome(sRanks: 1), null, false, Rules);
        var three = IncubationPolicy.RequiredSeconds(Genome(sRanks: 3), null, false, Rules);

        Assert.Equal(Rules.IncubationSeconds + Rules.SecondsPerSRankStat, one);
        Assert.Equal(Rules.IncubationSeconds + 3 * Rules.SecondsPerSRankStat, three);
    }

    [Fact]
    public void RareTraitsAndSpecialVariants_AddExtraTime()
    {
        var traits = new List<RareTraitData> { new() { TraitId = "Aurora" } };

        var rare = IncubationPolicy.RequiredSeconds(Genome(sRanks: 0), traits, false, Rules);
        var special = IncubationPolicy.RequiredSeconds(Genome(sRanks: 1), null, true, Rules);

        Assert.Equal(Rules.IncubationSeconds + Rules.SecondsPerRareTrait, rare);
        Assert.Equal(Rules.IncubationSeconds + Rules.SecondsPerSRankStat + Rules.SpecialVariantSeconds, special);
    }

    [Fact]
    public void OnlyTheExpressedRankCounts()
    {
        var genome = Genome(sRanks: 0);
        genome.AbilityGenes["run"] = new GenePairData { AlleleA = 5, AlleleB = 2, ExpressedAlleleIndex = 1 };

        Assert.Equal(Rules.IncubationSeconds, IncubationPolicy.RequiredSeconds(genome, null, false, Rules));
    }

    private static GenomeData Genome(int sRanks)
    {
        var genome = new GenomeData();
        var statIds = new[] { "run", "swim", "fly", "power", "stamina" };
        for (var i = 0; i < statIds.Length; i++)
        {
            var rank = i < sRanks ? 5 : 2;
            genome.AbilityGenes[statIds[i]] = new GenePairData { AlleleA = rank, AlleleB = rank };
        }

        return genome;
    }
}
