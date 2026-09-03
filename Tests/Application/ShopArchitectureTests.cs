using System;
using Voidling.Application.Shop;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class ShopArchitectureTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void StoreEggFactory_SameIdentitySeedProducesSameLockedGenetics()
    {
        var factory = new StoreEggFactory(Rules);
        var first = factory.Create("egg", 12345UL);
        var second = factory.Create("egg", 12345UL);

        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(first.TintHex, second.TintHex);
        foreach (var statId in Rules.Genetics.StatIds)
        {
            var firstGene = first.Genome.AbilityGenes[statId];
            var secondGene = second.Genome.AbilityGenes[statId];
            Assert.Equal(firstGene.AlleleA, secondGene.AlleleA);
            Assert.Equal(firstGene.AlleleB, secondGene.AlleleB);
            Assert.Equal(firstGene.ExpressedAlleleIndex, secondGene.ExpressedAlleleIndex);
        }
    }

    [Fact]
    public void Purchase_MovesExactPreRolledEggToInventoryAndLeavesTheSlotEmpty()
    {
        var shop = new ShopUseCase(Rules);
        var listing = shop.CreateStoreInventoryEgg("store-1", 111UL);
        var originalGenome = listing.Genome;
        var state = new GameStateData { Coins = Rules.Shop.StoreEggPrice };
        state.StoreEggs.Add(listing);

        var result = shop.BuyStoreEgg(state, "store-1");

        Assert.True(result.Succeeded);
        Assert.Same(listing, result.PurchasedEgg);
        Assert.Same(originalGenome, result.PurchasedEgg!.Genome);
        Assert.Equal(111UL, result.PurchasedEgg.Seed);
        Assert.Same(listing, Assert.Single(state.OwnedEggs));
        Assert.Equal(0, state.Coins);
        Assert.Empty(state.StoreEggs);
    }

    [Fact]
    public void Purchase_StoresTheEggUnplacedSoIncubationHasNotStarted()
    {
        var shop = new ShopUseCase(Rules);
        var state = new GameStateData { Coins = Rules.Shop.StoreEggPrice };
        state.StoreEggs.Add(shop.CreateStoreInventoryEgg("store-1", 111UL));

        var egg = shop.BuyStoreEgg(state, "store-1").PurchasedEgg!;

        Assert.Equal(EggState.Stored, egg.State);
        Assert.Equal(0.0f, egg.IncubationSeconds);
        Assert.Equal(0.0f, egg.WorldX);
        Assert.Equal(0.0f, egg.WorldY);
    }

    [Fact]
    public void PlacingAStoredEgg_StartsIncubationAtTheChosenGardenPosition()
    {
        var shop = new ShopUseCase(Rules);
        var state = new GameStateData { Coins = Rules.Shop.StoreEggPrice };
        state.StoreEggs.Add(shop.CreateStoreInventoryEgg("store-1", 111UL));
        var egg = shop.BuyStoreEgg(state, "store-1").PurchasedEgg!;

        Assert.Equal(ShopFailure.None, shop.PlaceStoredEgg(state, "store-1", 12.0f, 34.0f));

        Assert.Equal(EggState.Incubating, egg.State);
        Assert.Equal(12.0f, egg.WorldX);
        Assert.Equal(34.0f, egg.WorldY);

        // An egg already in the Garden cannot be re-placed, so the timer cannot be reset.
        Assert.Equal(ShopFailure.EggNotFound, shop.PlaceStoredEgg(state, "store-1", 99.0f, 99.0f));
        Assert.Equal(12.0f, egg.WorldX);
    }

    [Fact]
    public void RefillingSlots_TopsUpToTheSlotCountAndIsIdempotent()
    {
        var shop = new ShopUseCase(Rules);
        var state = new GameStateData();
        var created = 0;

        Assert.True(shop.RefillStoreEggSlots(state, () => shop.CreateStoreInventoryEgg($"refill-{created++}", (ulong)created)));
        Assert.Equal(Rules.Shop.StoreEggSlotCount, state.StoreEggs.Count);

        Assert.False(shop.RefillStoreEggSlots(state, () => throw new InvalidOperationException("must not create")));
        Assert.Equal(Rules.Shop.StoreEggSlotCount, state.StoreEggs.Count);
    }

    [Fact]
    public void Purchase_InsufficientCurrencyDoesNotMoveOrEmptyTheListing()
    {
        var shop = new ShopUseCase(Rules);
        var listing = shop.CreateStoreInventoryEgg("store-1", 111UL);
        var state = new GameStateData { Coins = Rules.Shop.StoreEggPrice - 1 };
        state.StoreEggs.Add(listing);

        var result = shop.BuyStoreEgg(state, "store-1");

        Assert.Equal(ShopFailure.NotEnoughCurrency, result.Failure);
        Assert.Empty(state.OwnedEggs);
        Assert.Same(listing, Assert.Single(state.StoreEggs));
        Assert.Equal(Rules.Shop.StoreEggPrice - 1, state.Coins);
    }
}
