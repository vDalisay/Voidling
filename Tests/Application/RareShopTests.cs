using System.Text.Json;
using Voidling.Application.Shop;
using Voidling.Application.Simulation;
using Voidling.Domain.Shop;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class RareShopTests
{
    [Fact]
    public void Resolver_IsDeterministicAndHonorsChanceBounds()
    {
        Assert.Equal(string.Empty, RareShopOfferResolver.Resolve(123UL, 0.0));
        Assert.Equal(ShopItemIds.FullIncubationSkip, RareShopOfferResolver.Resolve(123UL, 1.0));
        Assert.Equal(
            RareShopOfferResolver.Resolve(987654UL, 0.35),
            RareShopOfferResolver.Resolve(987654UL, 0.35));
    }

    [Fact]
    public void BuyRareOffer_ConsumesOfferAndAddsPersistentUtilityInventory()
    {
        var defaults = GameBalanceRules.DemoDefaults;
        var rules = defaults with
        {
            Shop = defaults.Shop with { FullIncubationSkipPrice = 45 }
        };
        var shop = new ShopUseCase(rules);
        var state = new GameStateData
        {
            Coins = 50,
            ShopRareOfferItemId = ShopItemIds.FullIncubationSkip
        };

        var result = shop.BuyRareOffer(state, ShopItemIds.FullIncubationSkip);

        Assert.Equal(ShopFailure.None, result);
        Assert.Equal(5, state.Coins);
        Assert.Equal(string.Empty, state.ShopRareOfferItemId);
        Assert.Equal(1, state.UtilityItems[ShopItemIds.FullIncubationSkip]);

        var duplicate = shop.BuyRareOffer(state, ShopItemIds.FullIncubationSkip);
        Assert.Equal(ShopFailure.RareOfferNotFound, duplicate);
        Assert.Equal(5, state.Coins);
        Assert.Equal(1, state.UtilityItems[ShopItemIds.FullIncubationSkip]);
    }

    [Fact]
    public void UseIncubationSkip_MakesEggReadyButLeavesHatchingToSimulation()
    {
        var rules = GameBalanceRules.DemoDefaults;
        var shop = new ShopUseCase(rules);
        var state = new GameStateData();
        state.UtilityItems[ShopItemIds.FullIncubationSkip] = 1;
        var egg = new EggData
        {
            Id = "egg",
            State = EggState.Incubating,
            IsViable = true,
            IncubationSeconds = 2.0f,
            RequiredIncubationSeconds = 20.0f,
            Genome = new GenomeData()
        };
        state.OwnedEggs.Add(egg);

        var result = shop.UseFullIncubationSkip(state, egg.Id);

        Assert.Equal(ShopFailure.None, result);
        Assert.Equal(20.0f, egg.IncubationSeconds);
        Assert.Equal(0, state.UtilityItems[ShopItemIds.FullIncubationSkip]);
        Assert.Same(egg, Assert.Single(state.OwnedEggs));
        Assert.Empty(state.Voidlings);

        new AdvanceSimulationUseCase(rules).Advance(state, 0.01f);

        Assert.Empty(state.OwnedEggs);
        Assert.Single(state.Voidlings);
    }

    [Fact]
    public void RareOfferRotation_IsChunkInvariantWithEggInventory()
    {
        var defaults = GameBalanceRules.DemoDefaults;
        var rules = defaults with
        {
            Economy = defaults.Economy with { GardenCoinsPerMinute = 0.0f },
            Shop = defaults.Shop with
            {
                EggRotationIntervalSeconds = 10.0f,
                StoreEggSlotCount = 3,
                RareOfferAppearanceChance = 0.37
            }
        };
        var oneChunk = CreateRotationState();
        var severalChunks = CreateRotationState();
        var simulation = new AdvanceSimulationUseCase(rules);

        simulation.Advance(oneChunk, 35.0f);
        simulation.Advance(severalChunks, 7.0f);
        simulation.Advance(severalChunks, 8.0f);
        simulation.Advance(severalChunks, 20.0f);

        Assert.Equal(oneChunk.SeedCounter, severalChunks.SeedCounter);
        Assert.Equal(oneChunk.ShopEggRotationElapsedSeconds, severalChunks.ShopEggRotationElapsedSeconds, 5);
        Assert.Equal(oneChunk.ShopRareOfferItemId, severalChunks.ShopRareOfferItemId);
        Assert.Equal(
            JsonSerializer.Serialize(oneChunk.StoreEggs),
            JsonSerializer.Serialize(severalChunks.StoreEggs));
    }

    private static GameStateData CreateRotationState()
    {
        var state = new GameStateData { SeedCounter = 100 };
        state.StoreEggs.Add(new EggData { Id = "old-a", Source = EggSource.Store });
        state.StoreEggs.Add(new EggData { Id = "old-b", Source = EggSource.Store });
        state.StoreEggs.Add(new EggData { Id = "old-c", Source = EggSource.Store });
        return state;
    }
}
