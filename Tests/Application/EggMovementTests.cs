using Voidling.Application.Shop;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>Eggs in the Garden can be carried somewhere else, like Voidlings; incubation carries on.</summary>
public sealed class EggMovementTests
{
    private static readonly ShopUseCase Shop = new(GameBalanceRules.DemoDefaults);

    [Theory]
    [InlineData(EggState.Incubating)]
    [InlineData(EggState.WaitingForSpace)]
    [InlineData(EggState.Failed)]
    public void PlacedEggs_CanBeMovedWithoutResettingIncubation(EggState eggState)
    {
        var egg = new EggData { Id = "egg", State = eggState, WorldX = 10, WorldY = 20, IncubationSeconds = 7 };
        var state = new GameStateData();
        state.OwnedEggs.Add(egg);

        Assert.Equal(ShopFailure.None, Shop.MovePlacedEgg(state, "egg", 300, 150));

        Assert.Equal((300f, 150f), (egg.WorldX, egg.WorldY));
        Assert.Equal(7f, egg.IncubationSeconds);
        Assert.Equal(eggState, egg.State);
    }

    [Fact]
    public void EggsInTheSatchel_AreNotMovedThisWay()
    {
        var stored = new EggData { Id = "stored", State = EggState.Stored };
        var stowed = new EggData { Id = "stowed", State = EggState.Failed, Stowed = true };
        var state = new GameStateData();
        state.OwnedEggs.Add(stored);
        state.OwnedEggs.Add(stowed);

        Assert.Equal(ShopFailure.EggNotFound, Shop.MovePlacedEgg(state, "stored", 1, 1));
        Assert.Equal(ShopFailure.EggNotFound, Shop.MovePlacedEgg(state, "stowed", 1, 1));
        Assert.Equal(ShopFailure.EggNotFound, Shop.MovePlacedEgg(state, "missing", 1, 1));
    }
}
