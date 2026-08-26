using System;
using Voidling.Application.Persistence;
using Voidling.Application.Simulation;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class GardenIncomeSimulationTests
{
    [Fact]
    public void OpenGameElapsedTime_AwardsPassiveGardenIncome()
    {
        var rules = GameBalanceRules.DemoDefaults with
        {
            Economy = new EconomyRules(GardenCoinsPerMinute: 60.0f)
        };
        var simulation = new AdvanceSimulationUseCase(rules);
        var state = new GameStateData { Coins = 10 };

        var first = simulation.Advance(state, 0.5f);
        var second = simulation.Advance(state, 0.5f);

        Assert.False(first.Changed);
        Assert.True(second.Changed);
        Assert.Equal(11, state.Coins);
        Assert.Equal(0.0, state.GardenIncomeCoinRemainder, 8);
    }

    [Fact]
    public void PassiveIncome_IsDeterministicAcrossElapsedChunking()
    {
        var rules = GameBalanceRules.DemoDefaults with
        {
            Economy = new EconomyRules(GardenCoinsPerMinute: 2.5f)
        };
        var fine = new GameStateData { Coins = 0 };
        var coarse = new GameStateData { Coins = 0 };
        var fineSimulation = new AdvanceSimulationUseCase(rules);
        var coarseSimulation = new AdvanceSimulationUseCase(rules);

        for (var i = 0; i < 240; i++)
            fineSimulation.Advance(fine, 0.5f);
        coarseSimulation.Advance(coarse, 120.0f);

        Assert.Equal(coarse.Coins, fine.Coins);
        Assert.Equal(5, fine.Coins);
        Assert.Equal(coarse.GardenIncomeCoinRemainder, fine.GardenIncomeCoinRemainder, 8);
    }

    [Fact]
    public void PassiveIncome_HasNoDailyCapAndDoesNotUseWallClock()
    {
        var rules = GameBalanceRules.DemoDefaults with
        {
            Economy = new EconomyRules(GardenCoinsPerMinute: 1.0f)
        };
        var state = new GameStateData { Coins = 0 };
        var simulation = new AdvanceSimulationUseCase(rules);

        simulation.Advance(state, 24.0f * 60.0f * 60.0f);

        Assert.Equal(1440, state.Coins);
        Assert.Equal(0.0, state.GardenIncomeCoinRemainder, 8);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidOrNonPositiveElapsed_DoesNotAdvanceIncome(float elapsed)
    {
        var state = new GameStateData { Coins = 7, GardenIncomeCoinRemainder = 0.25 };
        var simulation = new AdvanceSimulationUseCase(GameBalanceRules.DemoDefaults);

        var result = simulation.Advance(state, elapsed);

        Assert.False(result.Changed);
        Assert.Equal(7, state.Coins);
        Assert.Equal(0.25, state.GardenIncomeCoinRemainder, 8);
    }

    [Fact]
    public void SaveV10Migration_NormalizesOnlyInvalidIncomeRemainder()
    {
        var migration = new GameStateMigrationService(GameBalanceRules.DemoDefaults);
        var valid = new GameStateData { SaveVersion = 9, GardenIncomeCoinRemainder = 0.75 };
        var invalid = new GameStateData { SaveVersion = 9, GardenIncomeCoinRemainder = double.NaN };

        migration.Normalize(valid);
        migration.Normalize(invalid);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, valid.SaveVersion);
        Assert.Equal(0.75, valid.GardenIncomeCoinRemainder, 8);
        Assert.Equal(0.0, invalid.GardenIncomeCoinRemainder, 8);
    }
}
