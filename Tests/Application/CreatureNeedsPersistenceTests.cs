using Voidling.Application.Persistence;
using Voidling.Application.Simulation;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class CreatureNeedsPersistenceTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void AdvanceSimulation_ChangesOnlyCurrentRosterNeeds()
    {
        var active = new VoidlingData { Id = "active", Stage = LifeStage.Adult };
        var departed = new VoidlingData { Id = "departed", Stage = LifeStage.Adult };
        var state = new GameStateData();
        state.Voidlings.Add(active);
        state.DepartedVoidlings.Add(departed);

        new AdvanceSimulationUseCase(Rules).Advance(state, 60.0f);

        Assert.Equal(0.75f, active.Needs.Hunger, 3);
        Assert.Equal(99.55f, active.Needs.Energy, 3);
        Assert.Equal(0.0f, departed.Needs.Hunger, 3);
        Assert.Equal(100.0f, departed.Needs.Energy, 3);
    }

    [Fact]
    public void Hatch_InitializesNeutralCurrentCareState()
    {
        var state = new GameStateData();
        state.OwnedEggs.Add(new EggData
        {
            Id = "care-egg",
            IsViable = true,
            FailureResolved = true,
            RequiredIncubationSeconds = 0.1f
        });

        new AdvanceSimulationUseCase(Rules).Advance(state, 0.2f);

        var creature = Assert.Single(state.Voidlings);
        Assert.Equal(0.0f, creature.Needs.Hunger);
        Assert.Equal(100.0f, creature.Needs.Energy);
        Assert.Equal(0.0f, creature.Needs.Fatigue);
        Assert.Equal(0.0f, creature.Needs.Stress);
        Assert.Equal(100.0f, creature.Needs.Nourishment);
        Assert.Equal(100.0f, creature.Needs.Condition);
        Assert.Equal(0.0f, creature.Needs.Happiness);
    }

    [Fact]
    public void Migration_V11CreatesAndBoundsCareStateWithoutRerollingAnythingElse()
    {
        var missing = new VoidlingData { Id = "missing", Needs = null! };
        var malformed = new VoidlingData
        {
            Id = "malformed",
            Needs = new CreatureNeedsState
            {
                Hunger = 140.0f,
                Energy = -5.0f,
                Fatigue = float.PositiveInfinity,
                Stress = 44.0f,
                Boredom = -20.0f,
                Loneliness = 110.0f,
                Nourishment = 80.0f,
                Condition = float.NaN,
                Happiness = 101.0f
            }
        };
        var state = new GameStateData { SaveVersion = 10, SeedCounter = 9123 };
        state.Voidlings.Add(missing);
        state.Voidlings.Add(malformed);

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal(9123, state.SeedCounter);
        Assert.NotNull(missing.Needs);
        Assert.Equal(0.0f, missing.Needs.Hunger);
        Assert.Equal(100.0f, missing.Needs.Energy);
        Assert.Equal(0.0f, missing.Needs.Happiness);
        Assert.Equal(100.0f, malformed.Needs.Hunger);
        Assert.Equal(0.0f, malformed.Needs.Energy);
        Assert.Equal(0.0f, malformed.Needs.Fatigue);
        Assert.Equal(44.0f, malformed.Needs.Stress);
        Assert.Equal(0.0f, malformed.Needs.Boredom);
        Assert.Equal(100.0f, malformed.Needs.Loneliness);
        Assert.Equal(80.0f, malformed.Needs.Nourishment);
        Assert.Equal(0.0f, malformed.Needs.Condition);
        Assert.Equal(100.0f, malformed.Needs.Happiness);
    }
}