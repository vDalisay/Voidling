using System.Linq;
using Voidling.Application.Simulation;
using Voidling.Application.Training;
using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class EvolutionProgressionTests
{
    [Fact]
    public void TrainingItem_ChangesChildInfluenceByAppliedTrainingGain()
    {
        var rules = GameBalanceRules.DemoDefaults;
        var state = new GameStateData();
        var creature = CreateChild("child");
        state.Voidlings.Add(creature);
        state.TrainingItems["run"] = 1;

        var result = new TrainingUseCase(rules).ApplyTrainingItem(state, creature.Id, "run", 123UL);

        Assert.True(result.Succeeded);
        Assert.InRange(result.Gain, 5, 9);
        Assert.Equal(-result.Gain / (float)rules.Stats.MaxTrainingPoints, creature.RunPowerInfluence, 5);
        Assert.Equal(2, creature.Genome.AbilityGenes["run"].ExpressedValue);
    }

    [Fact]
    public void ChildToAdult_ResolvesEvolutionAndPromotesExactlyOnce()
    {
        var rules = GameBalanceRules.DemoDefaults with
        {
            Lifecycle = GameBalanceRules.DemoDefaults.Lifecycle with { ChildToAdultSeconds = 1.0f }
        };
        var state = new GameStateData();
        var creature = CreateChild("child");
        creature.RunPowerInfluence = -1.0f;
        creature.Genome.AbilityGenes["run"] = new GenePairData
        {
            AlleleA = 3,
            AlleleB = 4,
            ExpressedAlleleIndex = 1
        };
        state.Voidlings.Add(creature);

        var simulation = new AdvanceSimulationUseCase(rules);
        var first = simulation.Advance(state, 1.0f);
        var rankAfterEvolution = creature.Genome.AbilityGenes["run"].ExpressedValue;
        var second = simulation.Advance(state, 10.0f);

        var adultEvent = Assert.Single(first.Events.OfType<CreatureBecameAdultEvent>());
        Assert.Equal(EvolutionSpecialization.Run, adultEvent.Specialization);
        Assert.Equal("run", adultEvent.PromotedStatId);
        Assert.Equal(4, adultEvent.PreviousRank);
        Assert.Equal(5, adultEvent.NewRank);
        Assert.Equal(LifeStage.Adult, creature.Stage);
        Assert.Equal(EvolutionSpecialization.Run, creature.EvolutionSpecialization);
        Assert.Equal(5, rankAfterEvolution);
        Assert.Empty(second.Events.OfType<CreatureBecameAdultEvent>());
        Assert.Equal(rankAfterEvolution, creature.Genome.AbilityGenes["run"].ExpressedValue);
    }

    private static VoidlingData CreateChild(string id)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Child
        };

        foreach (var statId in GameBalanceRules.DemoDefaults.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 2,
                AlleleB = 3,
                ExpressedAlleleIndex = 0
            };
            creature.TrainingPoints[statId] = 0;
        }

        return creature;
    }
}
