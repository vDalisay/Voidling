using System.Linq;
using Voidling.Application.Simulation;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class SimulationArchitectureTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Advance_TransitionsChildAndClampsBreedingCooldown()
    {
        var child = new VoidlingData
        {
            Id = "child",
            Name = "Bud",
            Stage = LifeStage.Child,
            AgeSeconds = Rules.Lifecycle.ChildToAdultSeconds - 0.25f,
            BreedCooldownSeconds = 0.1f
        };
        var state = new GameStateData();
        state.Voidlings.Add(child);

        var result = new AdvanceSimulationUseCase(Rules).Advance(state, 0.5f);

        Assert.True(result.Changed);
        Assert.Equal(LifeStage.Adult, child.Stage);
        Assert.Equal(0.0f, child.BreedCooldownSeconds);
        var transition = Assert.Single(result.Events);
        var adult = Assert.IsType<CreatureBecameAdultEvent>(transition);
        Assert.Equal("child", adult.CreatureId);
    }

    /// <summary>
    /// An adult must survive a long play session. Voidlings are bred and raised across sittings, so
    /// a lifespan short enough to expire inside one loses lineages the player was working on.
    /// </summary>
    [Fact]
    public void Advance_AdultSurvivesSixHoursOfOpenGameTimeBeforeItsLifeEnds()
    {
        const float sixHours = 6.0f * 60.0f * 60.0f;
        Assert.True(
            Rules.Reincarnation.AdultLifespanSeconds >= sixHours,
            $"Adult lifespan is {Rules.Reincarnation.AdultLifespanSeconds}s, below the six-hour floor.");

        var adult = NewAdult("elder");
        var state = new GameStateData();
        state.Voidlings.Add(adult);
        var simulation = new AdvanceSimulationUseCase(Rules);

        // One minute short of six hours the creature is still in the Garden and still an adult.
        simulation.Advance(state, sixHours - 60.0f);

        Assert.Contains(adult, state.Voidlings);
        Assert.Empty(state.DepartedVoidlings);
        Assert.Equal(LifeStage.Adult, adult.Stage);
    }

    /// <summary>
    /// Reincarnation is gated on hidden care, so an uncared-for creature reaching the end of its
    /// life departs permanently. This pins the current rule so a balance change to it is deliberate.
    /// </summary>
    [Fact]
    public void Advance_UncaredForAdultDepartsPermanentlyAtTheEndOfItsLife()
    {
        var adult = NewAdult("neglected");
        adult.AdultAgeSeconds = Rules.Reincarnation.AdultLifespanSeconds - 1.0f;
        var state = new GameStateData();
        state.Voidlings.Add(adult);

        var result = new AdvanceSimulationUseCase(Rules).Advance(state, 2.0f);

        Assert.Single(result.Events.OfType<CreatureDiedEvent>());
        Assert.Empty(result.Events.OfType<CreatureReincarnatedEvent>());
        Assert.DoesNotContain(adult, state.Voidlings);
        Assert.Contains(adult, state.DepartedVoidlings);
    }

    private static VoidlingData NewAdult(string id) => new()
    {
        Id = id,
        Name = id,
        Stage = LifeStage.Adult,
        AgeSeconds = Rules.Lifecycle.ChildToAdultSeconds
    };

    [Fact]
    public void Advance_AdultTransitionIsEmittedExactlyOnce()
    {
        var child = new VoidlingData
        {
            Id = "child",
            Name = "Bud",
            Stage = LifeStage.Child,
            AgeSeconds = Rules.Lifecycle.ChildToAdultSeconds - 0.1f
        };
        var state = new GameStateData();
        state.Voidlings.Add(child);
        var simulation = new AdvanceSimulationUseCase(Rules);

        var first = simulation.Advance(state, 0.2f);
        var second = simulation.Advance(state, 5.0f);

        Assert.Single(first.Events.OfType<CreatureBecameAdultEvent>());
        Assert.Empty(second.Events.OfType<CreatureBecameAdultEvent>());
        Assert.Equal(LifeStage.Adult, child.Stage);
    }

    [Fact]
    public void Advance_ViableReadyEggHatchesWithoutRerollingGenome()
    {
        var genome = new GenomeFactory(Rules.Genetics).CreateRandom(123UL);
        var egg = new EggData
        {
            Id = "egg-1",
            Genome = genome,
            IsViable = true,
            FailureResolved = true,
            RequiredIncubationSeconds = 1.0f,
            ParentAId = "a",
            ParentBId = "b",
            TintHex = "#ABCDEF"
        };
        var state = new GameStateData();
        state.OwnedEggs.Add(egg);

        var result = new AdvanceSimulationUseCase(Rules).Advance(state, 1.0f);

        Assert.True(result.Changed);
        Assert.Empty(state.OwnedEggs);
        var creature = Assert.Single(state.Voidlings);
        Assert.Equal("egg-1", creature.Id);
        Assert.Same(genome, creature.Genome);
        Assert.Equal("a", creature.ParentAId);
        Assert.Equal("b", creature.ParentBId);
        Assert.All(Rules.Genetics.StatIds, statId => Assert.Equal(0, creature.TrainingPoints[statId]));
        Assert.IsType<CreatureHatchedEvent>(Assert.Single(result.Events));
    }

    [Fact]
    public void Advance_HatchTransitionIsEmittedExactlyOnce()
    {
        var egg = new EggData
        {
            Id = "egg-once",
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(555UL),
            IsViable = true,
            FailureResolved = true,
            RequiredIncubationSeconds = 0.1f
        };
        var state = new GameStateData();
        state.OwnedEggs.Add(egg);
        var simulation = new AdvanceSimulationUseCase(Rules);

        var first = simulation.Advance(state, 0.2f);
        var second = simulation.Advance(state, 10.0f);

        Assert.Single(first.Events.OfType<CreatureHatchedEvent>());
        Assert.Empty(second.Events.OfType<CreatureHatchedEvent>());
        Assert.Single(state.Voidlings);
        Assert.Empty(state.OwnedEggs);
    }

    [Fact]
    public void Advance_NonViableReadyEggFailsAndRemainsPersisted()
    {
        var egg = new EggData
        {
            Id = "failed-egg",
            IsViable = false,
            FailureResolved = true,
            RequiredIncubationSeconds = 0.25f
        };
        var state = new GameStateData();
        state.OwnedEggs.Add(egg);

        var result = new AdvanceSimulationUseCase(Rules).Advance(state, 0.5f);

        Assert.True(result.Changed);
        Assert.Empty(state.Voidlings);
        Assert.Same(egg, Assert.Single(state.OwnedEggs));
        Assert.Equal(EggState.Failed, egg.State);
        Assert.True(egg.FailureResolved);
        Assert.IsType<EggFailedEvent>(Assert.Single(result.Events));
    }

    [Fact]
    public void Advance_FailedEggTransitionIsEmittedExactlyOnce()
    {
        var egg = new EggData
        {
            Id = "failed-once",
            IsViable = false,
            FailureResolved = true,
            RequiredIncubationSeconds = 0.1f
        };
        var state = new GameStateData();
        state.OwnedEggs.Add(egg);
        var simulation = new AdvanceSimulationUseCase(Rules);

        var first = simulation.Advance(state, 0.2f);
        var second = simulation.Advance(state, 10.0f);

        Assert.Single(first.Events.OfType<EggFailedEvent>());
        Assert.Empty(second.Events.OfType<EggFailedEvent>());
        Assert.Equal(EggState.Failed, egg.State);
        Assert.Same(egg, Assert.Single(state.OwnedEggs));
    }

    [Fact]
    public void Advance_EquivalentElapsedChunksReachSameLifecycleState()
    {
        var firstState = CreateChunkingState();
        var secondState = CreateChunkingState();
        var simulation = new AdvanceSimulationUseCase(Rules);

        simulation.Advance(firstState, 2.0f);
        simulation.Advance(secondState, 0.5f);
        simulation.Advance(secondState, 0.75f);
        simulation.Advance(secondState, 0.75f);

        var firstCreature = Assert.Single(firstState.Voidlings);
        var secondCreature = Assert.Single(secondState.Voidlings);
        Assert.Equal(firstCreature.Stage, secondCreature.Stage);
        Assert.Equal(firstCreature.AgeSeconds, secondCreature.AgeSeconds, 4);
        Assert.Equal(firstCreature.BreedCooldownSeconds, secondCreature.BreedCooldownSeconds, 4);
        Assert.Equal(firstState.OwnedEggs[0].IncubationSeconds, secondState.OwnedEggs[0].IncubationSeconds, 4);
        Assert.Equal(firstState.OwnedEggs[0].State, secondState.OwnedEggs[0].State);
    }

    [Fact]
    public void Advance_ZeroElapsedTimeIsAStableNoOp()
    {
        var state = new GameStateData();
        var child = new VoidlingData { Stage = LifeStage.Child, AgeSeconds = 5.0f };
        state.Voidlings.Add(child);

        var result = new AdvanceSimulationUseCase(Rules).Advance(state, 0.0f);

        Assert.False(result.Changed);
        Assert.Empty(result.Events);
        Assert.Equal(5.0f, child.AgeSeconds);
    }

    private static GameStateData CreateChunkingState()
    {
        var state = new GameStateData();
        state.Voidlings.Add(new VoidlingData
        {
            Id = "growing",
            Stage = LifeStage.Child,
            AgeSeconds = 3.0f,
            BreedCooldownSeconds = 3.0f
        });
        state.OwnedEggs.Add(new EggData
        {
            Id = "incubating",
            RequiredIncubationSeconds = 10.0f,
            IncubationSeconds = 1.0f,
            IsViable = true,
            FailureResolved = true
        });
        return state;
    }
}
