using System.Linq;
using Voidling.Application.Simulation;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class CareRiskSimulationTests
{
    [Fact]
    public void Advance_WhenCareCrossesReincarnationThresholdEmitsRiskOnce()
    {
        var rules = GameBalanceRules.DemoDefaults with
        {
            Needs = GameBalanceRules.DemoDefaults.Needs with { HappinessLossPerMinute = 2.0f }
        };
        var creature = new VoidlingData
        {
            Id = "care-risk",
            Name = "Pip",
            Stage = LifeStage.Adult,
            Needs = new CreatureNeedsState
            {
                Happiness = 11.0f,
                Stress = 0.0f
            }
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        var simulation = new AdvanceSimulationUseCase(rules);

        var crossing = simulation.Advance(state, 60.0f);
        var stillAtRisk = simulation.Advance(state, 1.0f);

        var risk = Assert.Single(crossing.Events.OfType<CreatureCareRiskEvent>());
        Assert.Equal(creature.Id, risk.CreatureId);
        Assert.Equal(creature.Name, risk.Name);
        Assert.Empty(stillAtRisk.Events.OfType<CreatureCareRiskEvent>());
    }

    [Fact]
    public void Advance_WhenCreatureAlreadyAtRiskDoesNotSpamRiskEvent()
    {
        var creature = new VoidlingData
        {
            Id = "already-risky",
            Stage = LifeStage.Adult,
            Needs = new CreatureNeedsState
            {
                Happiness = 0.0f,
                Stress = 100.0f
            }
        };
        var state = new GameStateData();
        state.Voidlings.Add(creature);

        var result = new AdvanceSimulationUseCase(GameBalanceRules.DemoDefaults).Advance(state, 5.0f);

        Assert.Empty(result.Events.OfType<CreatureCareRiskEvent>());
    }
}
