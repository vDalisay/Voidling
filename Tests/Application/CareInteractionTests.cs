using Voidling.Application.Care;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class CareInteractionTests
{
    [Fact]
    public void Mistreat_ActiveCreatureReducesHiddenHappinessAndRaisesStress()
    {
        var creature = new VoidlingData
        {
            Id = "thrown",
            Needs = new CreatureNeedsState
            {
                Happiness = 20.0f,
                Stress = 15.0f
            }
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);

        var result = new CareUseCase(CareInteractionRules.DemoDefaults).Mistreat(state, creature.Id);

        Assert.True(result.Succeeded);
        Assert.True(result.Changed);
        Assert.Equal(17.0f, creature.Needs.Happiness, 3);
        Assert.Equal(27.0f, creature.Needs.Stress, 3);
    }

    [Fact]
    public void Mistreat_ClampsCareStateAtValidBounds()
    {
        var creature = new VoidlingData
        {
            Id = "bounds",
            Needs = new CreatureNeedsState
            {
                Happiness = 1.0f,
                Stress = 96.0f
            }
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        var care = new CareUseCase(CareInteractionRules.DemoDefaults);

        var first = care.Mistreat(state, creature.Id);
        var second = care.Mistreat(state, creature.Id);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(0.0f, creature.Needs.Happiness);
        Assert.Equal(100.0f, creature.Needs.Stress);
    }

    [Fact]
    public void Mistreat_MissingCreatureDoesNotMutateState()
    {
        var state = new GameStateData();

        var result = new CareUseCase(CareInteractionRules.DemoDefaults).Mistreat(state, "missing");

        Assert.False(result.Succeeded);
        Assert.False(result.Changed);
        Assert.Equal(CareInteractionFailure.CreatureNotFound, result.Failure);
        Assert.Empty(state.Voidlings);
    }
}
