using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Garden;
using Voidling.Application.Persistence;
using Voidling.Application.Training;
using Voidling.Domain.Garden;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// Biome tiles, Cow-Evolution style: buy one-star tiles, put one on plain ground, stack a matching
/// tile (same biome, same stars) on top for the next star up to four, where Water becomes a Swamp and
/// Dry a Volcano. Tiles below four stars can be picked back up with their stars.
/// </summary>
public sealed class BiomeTileTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void BuyingATile_PutsAOneStarTileInTheInventory()
    {
        var (tiles, state) = CreateIsland(coins: 100);

        Assert.True(tiles.BuyBiomeTile(state, BiomeCatalog.Mountain).Succeeded);

        Assert.Equal(100 - Rules.GardenModules.BiomeTilePrice, state.Coins);
        Assert.Equal(1, BiomeTileUseCase.OwnedCount(state, BiomeCatalog.Mountain, 1));
        Assert.Equal(BiomeTileFailure.UnknownBiome, tiles.BuyBiomeTile(state, BiomeCatalog.Swamp).Failure);
    }

    [Fact]
    public void ATileOnPlainGround_TurnsTheHexIntoThatBiome()
    {
        var (tiles, state) = CreateIsland(coins: 100);
        tiles.BuyBiomeTile(state, BiomeCatalog.Grove);

        var placed = tiles.PlaceBiomeTile(state, TrainingUseCase.StarterHexId, BiomeCatalog.Grove, 1);

        var hex = Starter(state);
        Assert.True(placed.Succeeded);
        Assert.False(placed.Stacked);
        Assert.Equal(BiomeCatalog.Grove, hex.BiomeId);
        Assert.Equal("stamina", hex.StatId);
        Assert.Equal(1, hex.Level);
        Assert.Empty(state.BiomeTiles);
    }

    [Fact]
    public void OnlyAMatchingTileStacks()
    {
        var (tiles, state) = CreateIsland(coins: 500);
        tiles.BuildBiome(state, TrainingUseCase.StarterHexId, BiomeCatalog.Water);
        tiles.BuyBiomeTile(state, BiomeCatalog.Dry);
        state.BiomeTiles.Add(new BiomeTileStackData { BiomeId = BiomeCatalog.Water, Stars = 2, Count = 1 });

        Assert.Equal(BiomeTileFailure.TileDoesNotMatch,
            tiles.PlaceBiomeTile(state, TrainingUseCase.StarterHexId, BiomeCatalog.Dry, 1).Failure);
        Assert.Equal(BiomeTileFailure.TileDoesNotMatch,
            tiles.PlaceBiomeTile(state, TrainingUseCase.StarterHexId, BiomeCatalog.Water, 2).Failure);
        Assert.Equal(BiomeTileFailure.NoTileOwned,
            tiles.PlaceBiomeTile(state, TrainingUseCase.StarterHexId, BiomeCatalog.Water, 1).Failure);
        Assert.Equal(1, Starter(state).Level);
    }

    [Theory]
    [InlineData(BiomeCatalog.Water, BiomeCatalog.Swamp)]
    [InlineData(BiomeCatalog.Dry, BiomeCatalog.Volcano)]
    [InlineData(BiomeCatalog.Plains, BiomeCatalog.Plains)]
    public void StackingToFourStars_MakesTheSpecialEnvironment(string biomeId, string fourStarId)
    {
        var (tiles, state) = CreateIsland(coins: 0);
        var hex = Starter(state);
        hex.BiomeId = biomeId;
        hex.StatId = BiomeCatalog.StatOf(biomeId);
        hex.Level = 3;
        state.BiomeTiles.Add(new BiomeTileStackData { BiomeId = biomeId, Stars = 3, Count = 1 });

        var stacked = tiles.PlaceBiomeTile(state, TrainingUseCase.StarterHexId, biomeId, 3);

        Assert.True(stacked.Stacked);
        Assert.Equal(4, hex.Level);
        Assert.Equal(fourStarId, hex.BiomeId);
        Assert.Equal(BiomeCatalog.StatOf(biomeId), hex.StatId);
        Assert.Equal(BiomeTileFailure.TileIsPermanent, tiles.PickUpBiomeTile(state, TrainingUseCase.StarterHexId).Failure);
    }

    [Fact]
    public void PickingUpATile_KeepsItsStarsSoItCanStackElsewhere()
    {
        var (tiles, state) = CreateIsland(coins: 1000);
        var second = AddPlainHex(state, "second", 1, 0);
        var trainee = new VoidlingData { Id = "trainee", Name = "Trainee" };
        state.Voidlings.Add(trainee);

        // Two two-star Water tiles…
        foreach (var hexId in new[] { TrainingUseCase.StarterHexId, second.Id })
        {
            tiles.BuildBiome(state, hexId, BiomeCatalog.Water);
            tiles.BuyBiomeTile(state, BiomeCatalog.Water);
            tiles.PlaceBiomeTile(state, hexId, BiomeCatalog.Water, 1);
        }
        new TrainingUseCase(Rules).SetPassiveTrainingLand(state, trainee.Id, second.Id);

        // …one picked up keeps its two stars and frees the hex and its trainee…
        var picked = tiles.PickUpBiomeTile(state, second.Id);
        Assert.True(picked.Succeeded);
        Assert.Equal(2, picked.Stars);
        Assert.Equal(string.Empty, second.BiomeId);
        Assert.Equal(string.Empty, trainee.PassiveTrainingModuleId);
        Assert.Equal(1, BiomeTileUseCase.OwnedCount(state, BiomeCatalog.Water, 2));

        // …and stacks on the other for three stars.
        Assert.True(tiles.PlaceBiomeTile(state, TrainingUseCase.StarterHexId, BiomeCatalog.Water, 2).Stacked);
        Assert.Equal(3, Starter(state).Level);
        Assert.Empty(state.BiomeTiles);
    }

    [Fact]
    public void OlderSaves_GetTheBiomeThatTrainsTheirStat()
    {
        var state = new GameStateData { SaveVersion = 22 };
        state.GardenModules.Add(new GardenModuleData { Id = "old-swim", StatId = "swim", Level = 2, Placed = true });
        state.GardenModules.Add(new GardenModuleData { Id = "old-stamina", StatId = "stamina", Level = 1, Placed = true, HexQ = 1 });
        state.BiomeTiles.Add(new BiomeTileStackData { BiomeId = "lava", Stars = 1, Count = 2 });
        state.BiomeTiles.Add(new BiomeTileStackData { BiomeId = BiomeCatalog.Water, Stars = 1, Count = 1 });
        state.BiomeTiles.Add(new BiomeTileStackData { BiomeId = BiomeCatalog.Water, Stars = 1, Count = 2 });

        new GameStateMigrationService(Rules).Normalize(state);

        var swim = state.GardenModules.Single(module => module.Id == "old-swim");
        var stamina = state.GardenModules.Single(module => module.Id == "old-stamina");
        Assert.Equal(BiomeCatalog.Water, swim.BiomeId);
        Assert.Equal(2, swim.Level);
        Assert.Equal(BiomeCatalog.Grove, stamina.BiomeId);
        var stack = Assert.Single(state.BiomeTiles);
        Assert.Equal((BiomeCatalog.Water, 1, 3), (stack.BiomeId, stack.Stars, stack.Count));
    }

    private static (BiomeTileUseCase Tiles, GameStateData State) CreateIsland(int coins)
    {
        var state = new GameStateData { Coins = coins };
        TrainingUseCase.EnsureStarterHex(state);
        return (new BiomeTileUseCase(Rules), state);
    }

    private static GardenModuleData Starter(GameStateData state)
        => state.GardenModules.Single(module => module.Id == TrainingUseCase.StarterHexId);

    private static GardenModuleData AddPlainHex(GameStateData state, string id, int q, int r)
    {
        var hex = new GardenModuleData { Id = id, Placed = true, HexQ = q, HexR = r };
        state.GardenModules.Add(hex);
        return hex;
    }
}
