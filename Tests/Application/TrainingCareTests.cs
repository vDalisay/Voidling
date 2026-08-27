using Voidling.Application.Training;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class TrainingCareTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void ApplyTrainingItem_SuccessConsumesTreatAndImprovesHiddenCareStateOnce()
    {
        var creature = CreateCreature();
        creature.Needs = new CreatureNeedsState
        {
            Hunger = 50.0f,
            Energy = 50.0f,
            Nourishment = 50.0f,
            Happiness = 0.0f
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        state.TrainingItems["run"] = 1;

        var result = new TrainingUseCase(Rules).ApplyTrainingItem(state, creature.Id, "run", 123UL);

        Assert.True(result.Succeeded);
        Assert.InRange(result.Gain, 5, 9);
        Assert.Equal(0, state.TrainingItems["run"]);
        Assert.Equal(38.0f, creature.Needs.Hunger, 3);
        Assert.Equal(52.0f, creature.Needs.Energy, 3);
        Assert.Equal(58.0f, creature.Needs.Nourishment, 3);
        Assert.Equal(2.0f, creature.Needs.Happiness, 3);
    }

    [Fact]
    public void ApplyTrainingItem_AtDnaCapDoesNotConsumeTreatOrChangeCare()
    {
        var creature = CreateCreature();
        creature.TrainingPoints["run"] = Rules.Stats.RankCaps.E;
        creature.Needs = new CreatureNeedsState
        {
            Hunger = 50.0f,
            Energy = 50.0f,
            Nourishment = 50.0f,
            Happiness = 7.0f
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        state.TrainingItems["run"] = 1;

        var result = new TrainingUseCase(Rules).ApplyTrainingItem(state, creature.Id, "run", 456UL);

        Assert.False(result.Succeeded);
        Assert.Equal(TrainingFailure.StatAtCap, result.Failure);
        Assert.Equal(1, state.TrainingItems["run"]);
        Assert.Equal(50.0f, creature.Needs.Hunger);
        Assert.Equal(50.0f, creature.Needs.Energy);
        Assert.Equal(50.0f, creature.Needs.Nourishment);
        Assert.Equal(7.0f, creature.Needs.Happiness);
    }

    [Fact]
    public void ApplyTrainingItem_WithoutOwnedTreatDoesNotChangeCare()
    {
        var creature = CreateCreature();
        creature.Needs.Happiness = 9.0f;
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        state.TrainingItems["run"] = 0;

        var result = new TrainingUseCase(Rules).ApplyTrainingItem(state, creature.Id, "run", 789UL);

        Assert.False(result.Succeeded);
        Assert.Equal(TrainingFailure.NoItemOwned, result.Failure);
        Assert.Equal(9.0f, creature.Needs.Happiness);
        Assert.Equal(0.0f, creature.Needs.Hunger);
    }

    private static VoidlingData CreateCreature()
    {
        var creature = new VoidlingData
        {
            Id = "care-training",
            Stage = LifeStage.Child,
            Genome = new GenomeData()
        };
        creature.Genome.AbilityGenes["run"] = new GenePairData
        {
            AlleleA = 0,
            AlleleB = 0,
            ExpressedAlleleIndex = 0
        };
        creature.TrainingPoints["run"] = 0;
        return creature;
    }
}