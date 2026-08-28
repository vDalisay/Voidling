using Voidling.Application.Simulation;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class LifecycleCocoonSimulationTests
{
    private static readonly GameBalanceRules BaseRules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void LifecycleEnd_ReincarnationEmitsCocoonBeforeOutcome()
    {
        var rules = BaseRules with
        {
            Reincarnation = BaseRules.Reincarnation with
            {
                AdultLifespanSeconds = 1.0f,
                MinimumHappiness = 5.0f,
                MaximumStress = 70.0f
            }
        };
        var creature = new VoidlingData
        {
            Id = "reincarnating",
            Name = "Mallow",
            Stage = LifeStage.Adult,
            AdultAgeSeconds = 0.9f,
            Needs = new CreatureNeedsState { Happiness = 20.0f, Stress = 10.0f }
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);

        var result = new AdvanceSimulationUseCase(rules).Advance(state, 0.1f);

        Assert.Equal(2, result.Events.Count);
        var cocoon = Assert.IsType<CreatureEnteredCocoonEvent>(result.Events[0]);
        Assert.True(cocoon.WillReincarnate);
        Assert.Equal(creature.Id, cocoon.CreatureId);
        Assert.IsType<CreatureReincarnatedEvent>(result.Events[1]);
    }

    [Fact]
    public void LifecycleEnd_DeathEmitsFadingCocoonBeforeOutcome()
    {
        var rules = BaseRules with
        {
            Reincarnation = BaseRules.Reincarnation with
            {
                AdultLifespanSeconds = 1.0f,
                MinimumHappiness = 5.0f,
                MaximumStress = 70.0f
            }
        };
        var creature = new VoidlingData
        {
            Id = "departing",
            Name = "Pip",
            Stage = LifeStage.Adult,
            AdultAgeSeconds = 0.9f,
            Needs = new CreatureNeedsState { Happiness = 0.0f, Stress = 10.0f }
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);

        var result = new AdvanceSimulationUseCase(rules).Advance(state, 0.1f);

        Assert.Equal(2, result.Events.Count);
        var cocoon = Assert.IsType<CreatureEnteredCocoonEvent>(result.Events[0]);
        Assert.False(cocoon.WillReincarnate);
        Assert.Equal(creature.Id, cocoon.CreatureId);
        Assert.IsType<CreatureDiedEvent>(result.Events[1]);
    }
}
