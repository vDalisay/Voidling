using Voidling.Application.Simulation;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class EggShellArchitectureTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void SuccessfulHatch_RetainsOneSellableShellWithEggIdentityAndAppearance()
    {
        var egg = new EggData
        {
            Id = "egg-shell-source",
            Source = EggSource.Bred,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(777UL),
            IsViable = true,
            FailureResolved = true,
            RequiredIncubationSeconds = 0.1f,
            TintHex = "#ABCDEF"
        };
        var state = new GameStateData();
        state.OwnedEggs.Add(egg);

        var first = new AdvanceSimulationUseCase(Rules).Advance(state, 0.2f);

        Assert.True(first.Changed);
        Assert.Empty(state.OwnedEggs);
        Assert.Single(state.Voidlings);
        var shell = Assert.Single(state.EggShells);
        Assert.Equal(egg.Id, shell.Id);
        Assert.Equal(EggSource.Bred, shell.Source);
        Assert.Equal("#ABCDEF", shell.TintHex);

        new AdvanceSimulationUseCase(Rules).Advance(state, 1.0f);
        Assert.Single(state.EggShells);
    }

    [Fact]
    public void FailedHatch_DoesNotCreateShell()
    {
        var egg = new EggData
        {
            Id = "failed",
            IsViable = false,
            FailureResolved = true,
            RequiredIncubationSeconds = 0.1f
        };
        var state = new GameStateData();
        state.OwnedEggs.Add(egg);

        new AdvanceSimulationUseCase(Rules).Advance(state, 0.2f);

        Assert.Empty(state.EggShells);
        Assert.Equal(EggState.Failed, egg.State);
    }
}
