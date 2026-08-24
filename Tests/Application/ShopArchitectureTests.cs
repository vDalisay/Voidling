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
    public void Purchase_MovesExactPreRolledEggAndCreatesNextLockedListing()
    {
        var shop = new ShopUseCase(Rules);
        var listing = shop.CreateStoreInventoryEgg("store-1", 111UL);
        var originalGenome = listing.Genome;
        var state = new GameStateData { Coins = Rules.Shop.StoreEggPrice };
        state.StoreEggs.Add(listing);

        var result = shop.BuyStoreEgg(state, "store-1", "store-2", 222UL, 12.0f, 34.0f);

        Assert.True(result.Succeeded);
        Assert.Same(listing, result.PurchasedEgg);
        Assert.Same(originalGenome, result.PurchasedEgg!.Genome);
        Assert.Equal(111UL, result.PurchasedEgg.Seed);
        Assert.Equal(12.0f, result.PurchasedEgg.WorldX);
        Assert.Equal(34.0f, result.PurchasedEgg.WorldY);
        Assert.Same(listing, Assert.Single(state.OwnedEggs));
        Assert.Equal(0, state.Coins);

        var replacement = Assert.Single(state.StoreEggs);
        Assert.Equal("store-2", replacement.Id);
        Assert.Equal(222UL, replacement.Seed);
        Assert.Same(replacement, result.ReplacementEgg);
    }

    [Fact]
    public void Purchase_InsufficientCurrencyDoesNotMoveOrReplaceListing()
    {
        var shop = new ShopUseCase(Rules);
        var listing = shop.CreateStoreInventoryEgg("store-1", 111UL);
        var state = new GameStateData { Coins = Rules.Shop.StoreEggPrice - 1 };
        state.StoreEggs.Add(listing);

        var result = shop.BuyStoreEgg(state, "store-1", "unused", 999UL, 12.0f, 34.0f);

        Assert.Equal(ShopFailure.NotEnoughCurrency, result.Failure);
        Assert.Empty(state.OwnedEggs);
        Assert.Same(listing, Assert.Single(state.StoreEggs));
        Assert.Equal(Rules.Shop.StoreEggPrice - 1, state.Coins);
    }
}
