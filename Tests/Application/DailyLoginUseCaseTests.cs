using System;
using Voidling.Application.Daily;
using Voidling.Application.Persistence;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class DailyLoginUseCaseTests
{
    private static readonly int[] Rewards = { 10, 20, 30 };

    [Fact]
    public void Claim_FirstDayIsIdempotentAndExposesNextReward()
    {
        var state = new GameStateData { Coins = 0 };
        var daily = new DailyLoginUseCase();

        var first = daily.Claim(state, 100, Rewards);
        var duplicate = daily.Claim(state, 100, Rewards);
        var status = daily.GetStatus(state, 100, Rewards);

        Assert.True(first.Claimed);
        Assert.Equal(10, first.CoinsAwarded);
        Assert.Equal(10, state.Coins);
        Assert.Equal(1, state.DailyLogin.Streak);
        Assert.Equal(100, state.DailyLogin.LastClaimDayNumber);
        Assert.False(duplicate.Claimed);
        Assert.Equal(0, duplicate.CoinsAwarded);
        Assert.False(status.CanClaim);
        Assert.Equal(1, status.CurrentStreak);
        Assert.Equal(20, status.NextReward);
    }

    [Fact]
    public void Claim_ConsecutiveDaysAdvanceStreakAndMissedDayResetsIt()
    {
        var state = new GameStateData { Coins = 0 };
        var daily = new DailyLoginUseCase();

        var first = daily.Claim(state, 200, Rewards);
        var second = daily.Claim(state, 201, Rewards);
        var reset = daily.Claim(state, 203, Rewards);

        Assert.Equal(10, first.CoinsAwarded);
        Assert.Equal(20, second.CoinsAwarded);
        Assert.Equal(10, reset.CoinsAwarded);
        Assert.Equal(40, state.Coins);
        Assert.Equal(1, state.DailyLogin.Streak);
        Assert.Equal(203, state.DailyLogin.LastClaimDayNumber);
    }

    [Fact]
    public void Claim_RewardCycleWrapsPredictably()
    {
        var state = new GameStateData { Coins = 0 };
        var daily = new DailyLoginUseCase();

        Assert.Equal(10, daily.Claim(state, 300, Rewards).CoinsAwarded);
        Assert.Equal(20, daily.Claim(state, 301, Rewards).CoinsAwarded);
        Assert.Equal(30, daily.Claim(state, 302, Rewards).CoinsAwarded);
        Assert.Equal(10, daily.Claim(state, 303, Rewards).CoinsAwarded);
        Assert.Equal(4, state.DailyLogin.Streak);
    }

    [Fact]
    public void Migration_InitializesLegacyDailyLoginStateAndAdvancesSaveVersion()
    {
        var state = new GameStateData { SaveVersion = 15 };
        state.DailyLogin = null!;

        new GameStateMigrationService(GameBalanceRules.DemoDefaults).Normalize(state);

        Assert.NotNull(state.DailyLogin);
        Assert.Equal(0, state.DailyLogin.LastClaimDayNumber);
        Assert.Equal(0, state.DailyLogin.Streak);
        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
    }
}
