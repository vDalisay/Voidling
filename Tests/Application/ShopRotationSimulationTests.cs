using System.Linq;
using System.Text.Json;
using Voidling.Application.Persistence;
using Voidling.Application.Simulation;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class ShopRotationSimulationTests
{
    [Fact]
    public void Advance_BeforeIntervalKeepsCurrentStoreEggsAndPersistsCountdownProgress()
    {
        var rules = RotationRules(10.0f);
        var state = CreateState();
        var originalIds = state.StoreEggs.Select(egg => egg.Id).ToArray();

        var result = new AdvanceSimulationUseCase(rules).Advance(state, 9.0f);

        Assert.True(result.Changed);
        Assert.Equal(9.0, state.ShopEggRotationElapsedSeconds, 5);
        Assert.Equal(originalIds, state.StoreEggs.Select(egg => egg.Id).ToArray());
        Assert.Equal(100, state.SeedCounter);
    }

    [Fact]
    public void Advance_AtIntervalRefreshesOnlyStoreEggSlots()
    {
        var rules = RotationRules(10.0f);
        var state = CreateState();
        var owned = new EggData
        {
            Id = "owned",
            State = EggState.Failed,
            IsViable = false,
            FailureResolved = true
        };
        state.OwnedEggs.Add(owned);

        new AdvanceSimulationUseCase(rules).Advance(state, 10.0f);

        Assert.Equal(0.0, state.ShopEggRotationElapsedSeconds, 5);
        Assert.Equal(103, state.SeedCounter);
        Assert.Equal(3, state.StoreEggs.Count);
        Assert.All(state.StoreEggs, egg =>
        {
            Assert.StartsWith("shop-", egg.Id);
            Assert.Equal(EggSource.Store, egg.Source);
        });
        Assert.Same(owned, Assert.Single(state.OwnedEggs));
    }

    [Fact]
    public void Advance_MultipleRotationsIsChunkInvariant()
    {
        var rules = RotationRules(10.0f);
        var oneChunk = CreateState();
        var severalChunks = CreateState();
        var simulation = new AdvanceSimulationUseCase(rules);

        simulation.Advance(oneChunk, 25.0f);
        simulation.Advance(severalChunks, 5.0f);
        simulation.Advance(severalChunks, 7.0f);
        simulation.Advance(severalChunks, 13.0f);

        Assert.Equal(5.0, oneChunk.ShopEggRotationElapsedSeconds, 5);
        Assert.Equal(oneChunk.ShopEggRotationElapsedSeconds, severalChunks.ShopEggRotationElapsedSeconds, 5);
        Assert.Equal(106, oneChunk.SeedCounter);
        Assert.Equal(oneChunk.SeedCounter, severalChunks.SeedCounter);
        Assert.Equal(
            JsonSerializer.Serialize(oneChunk.StoreEggs),
            JsonSerializer.Serialize(severalChunks.StoreEggs));
    }

    [Fact]
    public void Migration_NormalizesInvalidRotationRemainder()
    {
        var rules = RotationRules(10.0f);
        var state = CreateState();
        state.SaveVersion = 17;
        state.ShopEggRotationElapsedSeconds = double.PositiveInfinity;

        new GameStateMigrationService(rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal(0.0, state.ShopEggRotationElapsedSeconds);
    }

    private static GameStateData CreateState()
    {
        var state = new GameStateData
        {
            SeedCounter = 100,
            ShopEggRotationElapsedSeconds = 0.0
        };
        state.StoreEggs.Add(new EggData { Id = "old-a", Source = EggSource.Store });
        state.StoreEggs.Add(new EggData { Id = "old-b", Source = EggSource.Store });
        state.StoreEggs.Add(new EggData { Id = "old-c", Source = EggSource.Store });
        return state;
    }

    private static GameBalanceRules RotationRules(float intervalSeconds)
    {
        var defaults = GameBalanceRules.DemoDefaults;
        return defaults with
        {
            Economy = defaults.Economy with { GardenCoinsPerMinute = 0.0f },
            Shop = defaults.Shop with { EggRotationIntervalSeconds = intervalSeconds }
        };
    }
}
