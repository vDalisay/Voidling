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
}
