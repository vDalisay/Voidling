using Voidling.Application.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class RaceResultArchitectureTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 20)]
    [InlineData(3, 10)]
    [InlineData(4, 5)]
    [InlineData(9, 5)]
    public void AwardPlacement_PreservesMvpRewardTable(int place, int expectedReward)
    {
        var state = new GameStateData { Coins = 100 };
        var result = new RaceResultUseCase(Rules).AwardPlacement(state, place);

        Assert.Equal(expectedReward, result.Reward);
        Assert.Equal(100 + expectedReward, state.Coins);
    }
}
