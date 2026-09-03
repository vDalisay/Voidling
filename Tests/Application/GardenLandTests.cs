using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Persistence;
using Voidling.Application.Garden;
using Voidling.Application.Training;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// Buy a hex land tile, grow the island with it, then drop a Voidling on it to train there.
/// </summary>
public sealed class GardenLandTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void BoughtLandStartsInInventoryAndTrainsNobodyUntilItIsPlaced()
    {
        var (training, state, creature) = CreateGarden(coins: 500);

        Assert.True(training.BuyGardenModule(state, "tile", "run").Succeeded);
        var tile = Assert.Single(state.GardenModules);
        Assert.False(tile.Placed);
        Assert.Equal(500 - Rules.GardenModules.PurchaseCost, state.Coins);

        var beforePlacement = training.SetPassiveTrainingLand(state, creature.Id, "tile");
        Assert.Equal(PassiveTrainingFailure.LandNotPlaced, beforePlacement.Failure);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);
    }

    [Fact]
    public void LandOnlyFitsWhereTheIslandCanGrowAndOneTileOwnsOneHex()
    {
        var (training, state, _) = CreateGarden(coins: 500);
        training.BuyGardenModule(state, "first", "run");
        training.BuyGardenModule(state, "second", "swim");

        Assert.True(training.PlaceGardenModule(state, "first", 0, 0).Succeeded);
        Assert.Equal(GardenModuleFailure.AlreadyPlaced, training.PlaceGardenModule(state, "first", 1, 0).Failure);
        Assert.Equal(GardenModuleFailure.DoesNotFit, training.PlaceGardenModule(state, "second", 0, 0).Failure);

        // Far out at sea, with nothing to touch, is not a place the island can reach.
        Assert.Equal(GardenModuleFailure.DoesNotFit, training.PlaceGardenModule(state, "second", 99, 99).Failure);
        Assert.True(training.PlaceGardenModule(state, "second", 1, 0).Succeeded);
    }

    [Fact]
    public void DroppingAVoidlingOnATileTrainsItsStatAtTheTileRate()
    {
        var (training, state, creature) = CreateGarden(coins: 500);
        training.BuyGardenModule(state, "tile", "swim");
        training.PlaceGardenModule(state, "tile", 0, 0);

        var assignment = training.SetPassiveTrainingLand(state, creature.Id, "tile");

        Assert.True(assignment.Succeeded);
        Assert.True(assignment.Changed);
        Assert.Equal("swim", creature.PassiveTrainingStatId);
        Assert.Equal("tile", creature.PassiveTrainingModuleId);
        Assert.Equal(Rules.GardenModules.PointsPerMinuteForLevel(1), creature.PassiveTrainingPointsPerMinute);

        // Upgrading the ground the creature trains on speeds it up without re-dropping it.
        Assert.True(training.UpgradeGardenModule(state, "tile").Succeeded);
        Assert.Equal(Rules.GardenModules.PointsPerMinuteForLevel(2), creature.PassiveTrainingPointsPerMinute);

        Assert.True(training.StopPassiveTraining(state, creature.Id).Changed);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);
        Assert.Equal(0.0f, creature.PassiveTrainingPointsPerMinute);
    }

    [Fact]
    public void PreHexSavesKeepTheirPlacedModulesOnTheIsland()
    {
        var state = new GameStateData
        {
            SaveVersion = 20,
            GardenModules = new List<GardenModuleData>
            {
                new() { Id = "stored", StatId = "run", Level = 1, SlotIndex = -1 },
                new() { Id = "slot0", StatId = "swim", Level = 2, SlotIndex = 0 },
                new() { Id = "slot1", StatId = "fly", Level = 1, SlotIndex = 1 }
            }
        };

        new GameStateMigrationService(Rules).Normalize(state);

        var byId = state.GardenModules.ToDictionary(module => module.Id);
        Assert.False(byId["stored"].Placed);
        Assert.True(byId["slot0"].Placed);
        Assert.True(byId["slot1"].Placed);
        Assert.Equal(2, byId["slot0"].Level);

        // Migrated tiles land on distinct, connected hexes rather than stacking on one.
        var placed = state.GardenModules.Where(module => module.Placed).Select(module => (module.HexQ, module.HexR)).ToList();
        Assert.Equal(placed.Count, placed.Distinct().Count());
        Assert.All(placed, hex => Assert.True(Rules.GardenModules.Hex.IsBaseIsland(hex.HexQ, hex.HexR)));
        Assert.All(state.GardenModules, module => Assert.Equal(-1, module.SlotIndex));
    }

    [Fact]
    public void OneVoidlingTrainsPerTileAndItsOwnTileAlwaysTakesItBack()
    {
        var (training, state, first) = CreateGarden(coins: 500);
        var second = CreateAdult("second", 9UL);
        state.Voidlings.Add(second);
        training.BuyGardenModule(state, "tile", "run");
        training.PlaceGardenModule(state, "tile", 0, 0);

        Assert.True(training.SetPassiveTrainingLand(state, first.Id, "tile").Succeeded);
        Assert.False(training.HasRoomFor(state, "tile", second.Id));

        var turnedAway = training.SetPassiveTrainingLand(state, second.Id, "tile");
        Assert.Equal(PassiveTrainingFailure.LandFull, turnedAway.Failure);
        Assert.Equal(string.Empty, second.PassiveTrainingStatId);
        Assert.Equal("run", first.PassiveTrainingStatId);

        // The resident is never counted against itself, so putting it back down still works.
        Assert.True(training.HasRoomFor(state, "tile", first.Id));
        Assert.True(training.SetPassiveTrainingLand(state, first.Id, "tile").Succeeded);

        // Carrying the resident off frees the ground for someone else.
        Assert.True(training.StopPassiveTraining(state, first.Id).Changed);
        Assert.True(training.SetPassiveTrainingLand(state, second.Id, "tile").Succeeded);
        Assert.Equal("run", second.PassiveTrainingStatId);
    }

    private static (TrainingUseCase Training, GameStateData State, VoidlingData Creature) CreateGarden(int coins)
    {
        var creature = CreateAdult("trainee", 7UL);
        var state = new GameStateData { Coins = coins };
        state.Voidlings.Add(creature);
        return (new TrainingUseCase(Rules), state, creature);
    }

    private static VoidlingData CreateAdult(string id, ulong seed)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed)
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }
}
