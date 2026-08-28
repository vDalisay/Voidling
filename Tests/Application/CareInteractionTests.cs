using Voidling.Application.Care;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class CareInteractionTests
{
    private static readonly CareInteractionRules Rules = CareInteractionRules.DemoDefaults;

    [Fact]
    public void Pet_ExistingCreatureImprovesOnlyCurrentCareState()
    {
        var creature = new VoidlingData
        {
            Id = "pet-me",
            Needs = new CreatureNeedsState
            {
                Hunger = 41.0f,
                Energy = 63.0f,
                Stress = 10.0f,
                Boredom = 7.0f,
                Loneliness = 6.0f,
                Happiness = 9.0f
            }
        };
        creature.TrainingPoints["run"] = 12;
        var state = new GameStateData();
        state.Voidlings.Add(creature);

        var result = new CareUseCase(Rules).Pet(state, creature.Id);

        Assert.True(result.Succeeded);
        Assert.True(result.Changed);
        Assert.Equal(11.0f, creature.Needs.Happiness, 3);
        Assert.Equal(6.0f, creature.Needs.Stress, 3);
        Assert.Equal(2.0f, creature.Needs.Boredom, 3);
        Assert.Equal(0.0f, creature.Needs.Loneliness, 3);
        Assert.Equal(41.0f, creature.Needs.Hunger);
        Assert.Equal(63.0f, creature.Needs.Energy);
        Assert.Equal(12, creature.TrainingPoints["run"]);
    }

    [Fact]
    public void Pet_ClampsCareValuesToNormalizedRange()
    {
        var creature = new VoidlingData
        {
            Id = "near-limits",
            Needs = new CreatureNeedsState
            {
                Happiness = 99.5f,
                Stress = 1.0f,
                Boredom = 2.0f,
                Loneliness = 3.0f
            }
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);

        var result = new CareUseCase(Rules).Pet(state, creature.Id);

        Assert.True(result.Succeeded);
        Assert.True(result.Changed);
        Assert.Equal(100.0f, creature.Needs.Happiness);
        Assert.Equal(0.0f, creature.Needs.Stress);
        Assert.Equal(0.0f, creature.Needs.Boredom);
        Assert.Equal(0.0f, creature.Needs.Loneliness);
    }

    [Fact]
    public void Pet_MissingCreatureReturnsTypedFailureWithoutMutation()
    {
        var state = new GameStateData();

        var result = new CareUseCase(Rules).Pet(state, "missing");

        Assert.False(result.Succeeded);
        Assert.False(result.Changed);
        Assert.Equal(CareInteractionFailure.CreatureNotFound, result.Failure);
        Assert.Empty(state.Voidlings);
    }
}
