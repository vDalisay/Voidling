using System.Linq;
using Voidling.Application.Simulation;
using Voidling.Domain.Care;
using Voidling.Domain.Lifecycle;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// Life stages count hours of open-game time and hidden happiness alone decides reincarnation:
/// 70 or more reincarnates, anything less dies, and stress plays no part.
/// </summary>
public sealed class LifecycleCareTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Theory]
    [InlineData(69.9f, LifecycleEndOutcome.Die)]
    [InlineData(70.0f, LifecycleEndOutcome.Reincarnate)]
    [InlineData(100.0f, LifecycleEndOutcome.Reincarnate)]
    public void Decide_UsesHappinessSeventyAsTheOnlyThreshold(float happiness, LifecycleEndOutcome expected)
    {
        var creature = new VoidlingData { Needs = new CreatureNeedsState { Happiness = happiness, Stress = 100.0f } };

        var decision = new ReincarnationService().Decide(creature, Rules.Reincarnation);

        Assert.Equal(expected, decision.Outcome);
    }

    [Fact]
    public void LifeStages_AreMeasuredInHoursOfOpenGameTime()
    {
        Assert.True(Rules.Lifecycle.ChildToAdultSeconds >= 3600.0f, "A baby should stay a baby for at least an hour.");
        Assert.True(Rules.Reincarnation.AdultLifespanSeconds >= 6.0f * 3600.0f, "An adult should live at least six hours.");
        Assert.True(Rules.Reincarnation.AdultLifespanSeconds <= 24.0f * 3600.0f, "Lifespans are hours, not days.");
    }

    [Fact]
    public void CareRiskWarning_FiresBelowItsOwnThresholdNotTheReincarnationOne()
    {
        var creature = NewAdult("worried");
        creature.Needs.Happiness = Rules.Reincarnation.CareRiskHappiness + 0.5f;
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        var simulation = new AdvanceSimulationUseCase(Rules);

        // Well below 70 but still above the care-risk line: no warning yet.
        Assert.True(creature.Needs.Happiness < Rules.Reincarnation.MinimumHappiness);
        Assert.Empty(simulation.Advance(state, 1.0f).Events.OfType<CreatureCareRiskEvent>());

        // Ten minutes of drain crosses the care-risk line once.
        var crossing = simulation.Advance(state, 600.0f);
        Assert.Single(crossing.Events.OfType<CreatureCareRiskEvent>());
    }

    [Fact]
    public void CaredForAdult_ReincarnatesWhileANeglectedOneDies()
    {
        var cared = NewAdult("cared");
        var neglected = NewAdult("neglected");
        var state = new GameStateData();
        state.Voidlings.Add(cared);
        state.Voidlings.Add(neglected);
        var simulation = new AdvanceSimulationUseCase(Rules);
        var care = new CareInteractionService();
        var lifespan = Rules.Reincarnation.AdultLifespanSeconds;

        // A cozy routine: every two hours the player spends a moment petting one Voidling.
        var died = false;
        var reincarnated = false;
        for (var elapsed = 0.0f; elapsed < lifespan + 60.0f; elapsed += 1800.0f)
        {
            if (elapsed % 7200.0f < 1.0f)
            {
                for (var pet = 0; pet < 20; pet++)
                    care.Pet(cared.Needs, Rules.CareInteractions);
            }

            var step = simulation.Advance(state, 1800.0f);
            reincarnated |= step.Events.OfType<CreatureReincarnatedEvent>().Any(e => e.CreatureId == cared.Id);
            died |= step.Events.OfType<CreatureDiedEvent>().Any(e => e.CreatureId == neglected.Id);
        }

        Assert.True(reincarnated, "A Voidling petted every couple of hours should reincarnate.");
        Assert.True(died, "A neglected Voidling should die at the end of its life.");
        Assert.Contains(cared, state.Voidlings);
        Assert.Equal(LifeStage.Child, cared.Stage);
        Assert.Contains(neglected, state.DepartedVoidlings);
    }

    private static VoidlingData NewAdult(string id) => new()
    {
        Id = id,
        Name = id,
        Stage = LifeStage.Adult,
        AgeSeconds = Rules.Lifecycle.ChildToAdultSeconds
    };
}
