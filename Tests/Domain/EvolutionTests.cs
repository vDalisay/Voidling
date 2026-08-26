using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class EvolutionTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void ChildTraining_TracksOpposingRaisingInfluenceWithoutMutatingGenes()
    {
        var creature = CreateCreature();
        var runBefore = creature.Genome.AbilityGenes["run"].ExpressedValue;

        EvolutionService.ApplyTrainingInfluence(creature, "run", 30, Rules.Stats);
        EvolutionService.ApplyTrainingInfluence(creature, "power", 12, Rules.Stats);
        EvolutionService.ApplyTrainingInfluence(creature, "swim", 24, Rules.Stats);
        EvolutionService.ApplyTrainingInfluence(creature, "fly", 12, Rules.Stats);

        Assert.Equal(-0.15f, creature.RunPowerInfluence, 3);
        Assert.Equal(-0.10f, creature.SwimFlyInfluence, 3);
        Assert.Equal(runBefore, creature.Genome.AbilityGenes["run"].ExpressedValue);
    }

    [Fact]
    public void AdultTraining_DoesNotChangeFirstEvolutionInfluence()
    {
        var creature = CreateCreature();
        creature.Stage = LifeStage.Adult;

        EvolutionService.ApplyTrainingInfluence(creature, "run", 60, Rules.Stats);

        Assert.Equal(0.0f, creature.RunPowerInfluence);
    }

    [Fact]
    public void RunEvolution_PromotesOnlyTheExpressedAllele()
    {
        var creature = CreateCreature();
        creature.RunPowerInfluence = -0.80f;
        creature.Genome.AbilityGenes["run"] = new GenePairData
        {
            AlleleA = 3,
            AlleleB = 4,
            ExpressedAlleleIndex = 1
        };

        var result = EvolutionService.ResolveFirstEvolution(creature, Rules);
        var gene = creature.Genome.AbilityGenes["run"];

        Assert.Equal(EvolutionSpecialization.Run, result.Specialization);
        Assert.Equal("run", result.PromotedStatId);
        Assert.Equal(4, result.PreviousRank);
        Assert.Equal(5, result.NewRank);
        Assert.Equal(3, gene.AlleleA);
        Assert.Equal(5, gene.AlleleB);
        Assert.Equal(EvolutionSpecialization.Run, creature.EvolutionSpecialization);
        Assert.Equal(0.80f, creature.EvolutionMagnitude, 3);
    }

    [Fact]
    public void GeneralistEvolution_PromotesStamina()
    {
        var creature = CreateCreature();
        creature.RunPowerInfluence = 0.20f;
        creature.SwimFlyInfluence = -0.10f;
        creature.Genome.AbilityGenes["stamina"] = new GenePairData
        {
            AlleleA = 2,
            AlleleB = 4,
            ExpressedAlleleIndex = 0
        };

        var result = EvolutionService.ResolveFirstEvolution(creature, Rules);
        var gene = creature.Genome.AbilityGenes["stamina"];

        Assert.Equal(EvolutionSpecialization.Generalist, result.Specialization);
        Assert.Equal("stamina", result.PromotedStatId);
        Assert.Equal(2, result.PreviousRank);
        Assert.Equal(3, result.NewRank);
        Assert.Equal(3, gene.AlleleA);
        Assert.Equal(4, gene.AlleleB);
    }

    [Fact]
    public void EvolutionPromotion_IsCappedAtS()
    {
        var creature = CreateCreature();
        creature.SwimFlyInfluence = 1.0f;
        creature.Genome.AbilityGenes["fly"] = new GenePairData
        {
            AlleleA = 5,
            AlleleB = 1,
            ExpressedAlleleIndex = 0
        };

        var result = EvolutionService.ResolveFirstEvolution(creature, Rules);

        Assert.Equal(EvolutionSpecialization.Fly, result.Specialization);
        Assert.Equal(5, result.PreviousRank);
        Assert.Equal(5, result.NewRank);
        Assert.Equal(5, creature.Genome.AbilityGenes["fly"].AlleleA);
    }

    [Fact]
    public void ResolveFirstEvolution_IsIdempotent()
    {
        var creature = CreateCreature();
        creature.RunPowerInfluence = -1.0f;
        var first = EvolutionService.ResolveFirstEvolution(creature, Rules);
        var rankAfterFirst = creature.Genome.AbilityGenes["run"].ExpressedValue;

        var second = EvolutionService.ResolveFirstEvolution(creature, Rules);

        Assert.True(first.Promoted);
        Assert.False(second.Promoted);
        Assert.Equal(rankAfterFirst, creature.Genome.AbilityGenes["run"].ExpressedValue);
    }

    private static VoidlingData CreateCreature()
    {
        var creature = new VoidlingData
        {
            Id = "evolution-test",
            Stage = LifeStage.Child
        };

        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 2,
                AlleleB = 3,
                ExpressedAlleleIndex = 0
            };
        }

        return creature;
    }
}
