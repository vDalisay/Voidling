using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Persistence;
using Voidling.Application.Garden;
using Voidling.Application.Training;
using Voidling.Domain.Garden;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// Buy a piece of ground, grow the island with it, build training ground on one of its hexes, then
/// drop a Voidling there to train.
/// </summary>
public sealed class GardenLandTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void EveryIslandStartsAsOneFreeHexOfPlainGround()
    {
        var (_, state, _) = CreateGarden(coins: 500);

        var starter = Assert.Single(state.GardenModules);
        Assert.True(starter.Placed);
        Assert.Equal((0, 0), (starter.HexQ, starter.HexR));
        Assert.Equal(string.Empty, starter.StatId);
        Assert.Equal(500, state.Coins);
    }

    [Fact]
    public void BoughtLandWaitsInTheInventoryAsOnePieceUntilItIsPlaced()
    {
        var (training, state, creature) = CreateGarden(coins: 500);

        Assert.True(training.BuyLandShape(state, "piece", GardenTileShape.Line.Id).Succeeded);
        var piece = state.GardenModules.Single(module => module.Id == "piece");
        Assert.False(piece.Placed);
        Assert.Equal(GardenTileShape.Line.Id, piece.ShapeId);
        Assert.Equal(500 - Rules.GardenModules.EmptyHexCost * 3, state.Coins);

        var beforePlacement = training.SetPassiveTrainingLand(state, creature.Id, "piece");
        Assert.Equal(PassiveTrainingFailure.LandNotPlaced, beforePlacement.Failure);
    }

    [Fact]
    public void APieceBecomesOneTileForEveryHexItCovers()
    {
        var (training, state, _) = CreateGarden(coins: 500);
        training.BuyLandShape(state, "piece", GardenTileShape.Bend.Id);

        Assert.True(training.PlaceGardenModule(state, "piece", 1, 0).Succeeded);

        var placed = state.GardenModules.Where(module => module.Placed).ToList();
        Assert.Equal(4, placed.Count);
        Assert.Equal(placed.Count, placed.Select(module => (module.HexQ, module.HexR)).Distinct().Count());
        Assert.All(placed, module => Assert.Equal(string.Empty, module.StatId));

        // Three hexes of plain ground: capacity comes later, one Voidling per hex that is built on.
        Assert.Equal(3, placed.Count(module => module.Id.StartsWith("piece")));
    }

    [Fact]
    public void LandOnlyFitsConnectedToTheIslandAndNeverOnTopOfIt()
    {
        var (training, state, _) = CreateGarden(coins: 500);
        training.BuyLandShape(state, "first", GardenTileShape.Single.Id);
        training.BuyLandShape(state, "second", GardenTileShape.Single.Id);

        Assert.Equal(GardenModuleFailure.DoesNotFit, training.PlaceGardenModule(state, "first", 0, 0).Failure);
        Assert.Equal(GardenModuleFailure.DoesNotFit, training.PlaceGardenModule(state, "first", 99, 99).Failure);
        Assert.True(training.PlaceGardenModule(state, "first", 1, 0).Succeeded);
        Assert.Equal(GardenModuleFailure.AlreadyPlaced, training.PlaceGardenModule(state, "first", 2, 0).Failure);
        Assert.True(training.PlaceGardenModule(state, "second", 2, 0).Succeeded);
    }

    [Fact]
    public void RotatingAPieceChangesWhereItsHexesLand()
    {
        var (training, state, _) = CreateGarden(coins: 500);
        training.BuyLandShape(state, "piece", GardenTileShape.Line.Id);

        Assert.True(training.PlaceGardenModule(state, "piece", 1, 0, rotationSteps: 1).Succeeded);

        var placed = state.GardenModules
            .Where(module => module.Placed)
            .Select(module => (module.HexQ, module.HexR))
            .OrderBy(hex => hex.HexQ).ThenBy(hex => hex.HexR)
            .ToList();
        Assert.Equal(new[] { (0, 0), (1, 0), (1, 1), (1, 2) }, placed);
    }

    [Fact]
    public void PlainGroundTrainsNobodyUntilTrainingGroundIsBuiltOnIt()
    {
        var (training, state, creature) = CreateGarden(coins: 500);

        var refused = training.SetPassiveTrainingLand(state, creature.Id, TrainingUseCase.StarterHexId);
        Assert.Equal(PassiveTrainingFailure.LandNotTrainingGround, refused.Failure);

        var built = training.ConvertHexToTrainingGround(state, TrainingUseCase.StarterHexId, "swim");
        Assert.True(built.Succeeded);
        Assert.Equal(500 - Rules.GardenModules.TrainingConversionCost, state.Coins);
        Assert.Equal(
            GardenModuleFailure.AlreadyTrainingGround,
            training.ConvertHexToTrainingGround(state, TrainingUseCase.StarterHexId, "run").Failure);

        var assignment = training.SetPassiveTrainingLand(state, creature.Id, TrainingUseCase.StarterHexId);
        Assert.True(assignment.Succeeded);
        Assert.Equal("swim", creature.PassiveTrainingStatId);
        Assert.Equal(Rules.GardenModules.PointsPerMinuteForLevel(1), creature.PassiveTrainingPointsPerMinute);

        // Upgrading the ground the creature trains on speeds it up without re-dropping it.
        Assert.True(training.UpgradeGardenModule(state, TrainingUseCase.StarterHexId).Succeeded);
        Assert.Equal(Rules.GardenModules.PointsPerMinuteForLevel(2), creature.PassiveTrainingPointsPerMinute);

        Assert.True(training.StopPassiveTraining(state, creature.Id).Changed);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);
        Assert.Equal(0.0f, creature.PassiveTrainingPointsPerMinute);
    }

    [Fact]
    public void PlainGroundCannotBeUpgraded()
    {
        var (training, state, _) = CreateGarden(coins: 500);

        Assert.Equal(
            GardenModuleFailure.NotTrainingGround,
            training.UpgradeGardenModule(state, TrainingUseCase.StarterHexId).Failure);
        Assert.Equal(500, state.Coins);
    }

    [Fact]
    public void OneVoidlingTrainsPerHexAndItsOwnHexAlwaysTakesItBack()
    {
        var (training, state, first) = CreateGarden(coins: 500);
        var second = CreateAdult("second", 9UL);
        state.Voidlings.Add(second);
        training.ConvertHexToTrainingGround(state, TrainingUseCase.StarterHexId, "run");

        Assert.True(training.SetPassiveTrainingLand(state, first.Id, TrainingUseCase.StarterHexId).Succeeded);
        Assert.False(training.HasRoomFor(state, TrainingUseCase.StarterHexId, second.Id));

        var turnedAway = training.SetPassiveTrainingLand(state, second.Id, TrainingUseCase.StarterHexId);
        Assert.Equal(PassiveTrainingFailure.LandFull, turnedAway.Failure);
        Assert.Equal(string.Empty, second.PassiveTrainingStatId);
        Assert.Equal("run", first.PassiveTrainingStatId);

        // The resident is never counted against itself, so putting it back down still works.
        Assert.True(training.HasRoomFor(state, TrainingUseCase.StarterHexId, first.Id));
        Assert.True(training.SetPassiveTrainingLand(state, first.Id, TrainingUseCase.StarterHexId).Succeeded);

        // Carrying the resident off frees the ground for someone else.
        Assert.True(training.StopPassiveTraining(state, first.Id).Changed);
        Assert.True(training.SetPassiveTrainingLand(state, second.Id, TrainingUseCase.StarterHexId).Succeeded);
        Assert.Equal("run", second.PassiveTrainingStatId);
    }

    /// <summary>
    /// Hexes are three times the size they were, so old placements no longer describe the island.
    /// Tiles stay owned at their stat and level and go back to the inventory to be re-placed.
    /// </summary>
    [Fact]
    public void OldSavesKeepTheirTilesAndGetThemBackInTheInventory()
    {
        var creature = CreateAdult("trainee", 7UL);
        creature.PassiveTrainingStatId = "swim";
        creature.PassiveTrainingModuleId = "slot0";
        creature.PassiveTrainingPointsPerMinute = 1.0f;
        var state = new GameStateData
        {
            SaveVersion = 21,
            GardenModules = new List<GardenModuleData>
            {
                new() { Id = "stored", StatId = "run", Level = 1 },
                new() { Id = "slot0", StatId = "swim", Level = 2, Placed = true, HexQ = 1, HexR = 0 }
            }
        };
        state.Voidlings.Add(creature);

        new GameStateMigrationService(Rules).Normalize(state);

        var byId = state.GardenModules.ToDictionary(module => module.Id);
        Assert.False(byId["stored"].Placed);
        Assert.False(byId["slot0"].Placed);
        Assert.Equal(2, byId["slot0"].Level);
        Assert.Equal("swim", byId["slot0"].StatId);
        Assert.All(state.GardenModules.Where(module => !module.Placed),
            module => Assert.Equal(GardenTileShape.Single.Id, module.ShapeId));

        // The free starting hex is added so the island is never empty, and the trainee comes home.
        var starter = state.GardenModules.Single(module => module.Placed);
        Assert.Equal(TrainingUseCase.StarterHexId, starter.Id);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);
        Assert.Equal(0.0f, creature.PassiveTrainingPointsPerMinute);
    }

    private static (TrainingUseCase Training, GameStateData State, VoidlingData Creature) CreateGarden(int coins)
    {
        var creature = CreateAdult("trainee", 7UL);
        var state = new GameStateData { Coins = coins };
        state.Voidlings.Add(creature);
        TrainingUseCase.EnsureStarterHex(state);
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
