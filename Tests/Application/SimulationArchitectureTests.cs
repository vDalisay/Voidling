using System.Linq;
using Voidling.Application.Simulation;
using Voidling.Domain.Care;
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
        Assert.Equal(0.25f, child.AdultAgeSeconds, 4);
        Assert.Equal(0.0f, child.BreedCooldownSeconds);
        var transition = Assert.Single(result.Events);
        var adult = Assert.IsType<CreatureBecameAdultEvent>(transition);
        Assert.Equal("child", adult.CreatureId);
    }

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
    public void Advance_EligibleAdultReincarnatesExactlyOnce()
    {
        var rules = Rules with
        {
            Reincarnation = Rules.Reincarnation with
            {
                AdultLifespanSeconds = 1.0f,
                MinimumHappiness = 5.0f,
                MaximumStress = 70.0f,
                RetainedTrainingFraction = 0.10f
            }
        };
        var adult = new VoidlingData
        {
            Id = "reincarnate",
            Name = "Mallow",
            Stage = LifeStage.Adult,
            AdultAgeSeconds = 0.9f,
            Needs = new CreatureNeedsState { Happiness = 20.0f, Stress = 10.0f }
        };
        adult.TrainingPoints["run"] = 50;
        var state = new GameStateData();
        state.Voidlings.Add(adult);
        var simulation = new AdvanceSimulationUseCase(rules);

        var first = simulation.Advance(state, 0.2f);
        var second = simulation.Advance(state, 0.2f);

        var reincarnated = Assert.IsType<CreatureReincarnatedEvent>(
            Assert.Single(first.Events.OfType<CreatureReincarnatedEvent>()));
        Assert.Equal("reincarnate", reincarnated.CreatureId);
        Assert.Equal(1, reincarnated.ReincarnationCount);
        Assert.Equal(LifeStage.Child, adult.Stage);
        Assert.Equal(1, adult.ReincarnationCount);
        Assert.Equal(0.0f, adult.AdultAgeSeconds);
        Assert.Equal(5, adult.TrainingPoints["run"]);
        Assert.Equal(CreatureDepartureReason.None, adult.DepartureReason);
        Assert.Same(adult, Assert.Single(state.Voidlings));
        Assert.Empty(state.DepartedVoidlings);
        Assert.Empty(second.Events.OfType<CreatureReincarnatedEvent>());
    }

    [Fact]
    public void Advance_IneligibleAdultDiesAndMovesToLineageExactlyOnce()
    {
        var rules = Rules with
        {
            Reincarnation = Rules.Reincarnation with
            {
                AdultLifespanSeconds = 1.0f,
                MinimumHappiness = 5.0f,
                MaximumStress = 70.0f
            }
        };
        var adult = new VoidlingData
        {
            Id = "death",
            Name = "Pip",
            Stage = LifeStage.Adult,
            AdultAgeSeconds = 0.9f,
            Needs = new CreatureNeedsState { Happiness = 0.0f, Stress = 10.0f }
        };
        var state = new GameStateData();
        state.Voidlings.Add(adult);
        var simulation = new AdvanceSimulationUseCase(rules);

        var first = simulation.Advance(state, 0.2f);
        var second = simulation.Advance(state, 5.0f);

        var died = Assert.IsType<CreatureDiedEvent>(Assert.Single(first.Events.OfType<CreatureDiedEvent>()));
        Assert.Equal("death", died.CreatureId);
        Assert.Empty(state.Voidlings);
        Assert.Same(adult, Assert.Single(state.DepartedVoidlings));
        Assert.Equal(CreatureDepartureReason.Death, adult.DepartureReason);
        Assert.Empty(second.Events.OfType<CreatureDiedEvent>());
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
        Assert.Equal(firstCreature.AdultAgeSeconds, secondCreature.AdultAgeSeconds, 4);
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
