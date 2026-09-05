using System;
using System.Linq;
using Voidling.Application.Garden;
using Voidling.Domain.Care;
using Voidling.Domain.Evolution;
using Voidling.Domain.Garden;
using Voidling.Domain.Preferences;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Training;

public enum TrainingFailure
{
    None,
    UnknownStat,
    CreatureNotFound,
    NotEnoughCurrency,
    NoItemOwned,
    StatAtCap
}

public enum PassiveTrainingFailure
{
    None,
    UnknownStat,
    CreatureNotFound,
    LandNotPlaced,
    LandNotTrainingGround,
    LandFull
}

public enum GardenModuleFailure
{
    None,
    UnknownStat,
    UnknownShape,
    DuplicateModuleId,
    ModuleNotFound,
    AlreadyPlaced,
    NotPlaced,
    NotTrainingGround,
    AlreadyTrainingGround,
    DoesNotFit,
    NotEnoughCurrency,
    MaxLevel
}

public readonly record struct TrainingPurchaseResult(TrainingFailure Failure)
{
    public bool Succeeded => Failure == TrainingFailure.None;
}

public readonly record struct TrainingApplicationResult(
    TrainingFailure Failure,
    int Gain,
    bool WasFavoriteFood = false,
    bool FavoriteFoodDiscoveredNow = false)
{
    public bool Succeeded => Failure == TrainingFailure.None;
}

public readonly record struct PassiveTrainingAssignmentResult(
    PassiveTrainingFailure Failure,
    string StatId,
    bool Changed)
{
    public bool Succeeded => Failure == PassiveTrainingFailure.None;
}

public readonly record struct GardenModuleMutationResult(
    GardenModuleFailure Failure,
    bool Changed,
    int CoinsSpent = 0)
{
    public bool Succeeded => Failure == GardenModuleFailure.None;
}

/// <summary>
/// Coordinates active training inventory and Garden-backed passive training without UI,
/// persistence or Godot APIs. Both training paths share the same DNA-rank hard ceiling.
/// </summary>
public sealed class TrainingUseCase
{
    private readonly GameBalanceRules _rules;
    private readonly StatCalculator _stats;
    private readonly CreatureNeedsService _needs = new();
    private readonly FavoriteFoodPreferenceService _favoriteFood = new();

    public TrainingUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _stats = new StatCalculator(_rules.Stats);
    }

    public TrainingPurchaseResult BuyTrainingItem(GameStateData state, string statId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!_rules.Genetics.StatIds.Contains(statId))
            return new TrainingPurchaseResult(TrainingFailure.UnknownStat);
        if (state.Coins < _rules.Shop.TrainingItemPrice)
            return new TrainingPurchaseResult(TrainingFailure.NotEnoughCurrency);

        state.Coins -= _rules.Shop.TrainingItemPrice;
        state.TrainingItems.TryGetValue(statId, out var count);
        state.TrainingItems[statId] = count + 1;
        return new TrainingPurchaseResult(TrainingFailure.None);
    }

    public TrainingFailure ValidateTrainingItem(GameStateData state, string creatureId, string statId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_rules.Genetics.StatIds.Contains(statId))
            return TrainingFailure.UnknownStat;

        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        if (creature == null)
            return TrainingFailure.CreatureNotFound;
        if (_stats.GetTrainingPoints(creature, statId) >= _stats.GetTrainingPointCap(creature, statId))
            return TrainingFailure.StatAtCap;

        state.TrainingItems.TryGetValue(statId, out var count);
        return count > 0 ? TrainingFailure.None : TrainingFailure.NoItemOwned;
    }

    public TrainingApplicationResult ApplyTrainingItem(GameStateData state, string creatureId, string statId, ulong seed)
    {
        var failure = ValidateTrainingItem(state, creatureId, statId);
        if (failure != TrainingFailure.None)
            return new TrainingApplicationResult(failure, 0);

        var creature = state.Voidlings.First(v => v.Id == creatureId);
        _favoriteFood.Normalize(creature, _rules.Genetics.StatIds);
        var wasFavoriteFood = string.Equals(creature.FavoriteFoodId, statId, StringComparison.Ordinal);
        var favoriteFoodDiscoveredNow = wasFavoriteFood && !creature.FavoriteFoodDiscovered;
        if (favoriteFoodDiscoveredNow)
            creature.FavoriteFoodDiscovered = true;

        state.TrainingItems[statId]--;

        var rolledGain = StableRandom.Create(seed, $"training:{creatureId}:{statId}").Next(5, 10);
        var favoriteBonus = wasFavoriteFood ? Math.Max(0, _rules.FavoriteFood.BonusTrainingPoints) : 0;
        var current = _stats.GetTrainingPoints(creature, statId);
        var cap = _stats.GetTrainingPointCap(creature, statId);
        var updated = Math.Min(cap, current + rolledGain + favoriteBonus);
        var appliedGain = Math.Max(0, updated - current);
        creature.TrainingPoints[statId] = updated;

        EvolutionService.ApplyTrainingInfluence(creature, statId, appliedGain, _rules.Stats);
        _needs.ApplyTrainingTreat(creature.Needs, _rules.Needs);
        return new TrainingApplicationResult(
            TrainingFailure.None,
            appliedGain,
            wasFavoriteFood,
            favoriteFoodDiscoveredNow);
    }

    /// <summary>
    /// Buys a piece of plain ground from the shop. It waits in the inventory as one item and only
    /// becomes hexes when the player puts it down, so an unplaced piece trains nobody.
    /// </summary>
    public GardenModuleMutationResult BuyLandShape(GameStateData state, string moduleId, string shapeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var shape = GardenTileShape.Find(shapeId);
        if (shape == null)
            return new GardenModuleMutationResult(GardenModuleFailure.UnknownShape, false);
        if (string.IsNullOrWhiteSpace(moduleId) || state.GardenModules.Any(module => module.Id == moduleId))
            return new GardenModuleMutationResult(GardenModuleFailure.DuplicateModuleId, false);

        var cost = PriceOf(shape);
        if (state.Coins < cost)
            return new GardenModuleMutationResult(GardenModuleFailure.NotEnoughCurrency, false);

        state.Coins -= cost;
        state.GardenModules.Add(new GardenModuleData
        {
            Id = moduleId,
            ShapeId = shape.Id,
            StatId = string.Empty,
            Level = 1,
            Placed = false
        });
        return new GardenModuleMutationResult(GardenModuleFailure.None, true, cost);
    }

    /// <summary>Shop price of a piece: plain ground, charged per hex it covers.</summary>
    public int PriceOf(GardenTileShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return Math.Max(0, _rules.GardenModules.EmptyHexCost) * shape.HexCount;
    }

    /// <summary>
    /// Puts an owned piece down, anchored on one hex and turned <paramref name="rotationSteps"/>
    /// sixths of a turn. Every hex it covers becomes its own tile, so a three-hex piece can end up
    /// training three Voidlings.
    /// </summary>
    public GardenModuleMutationResult PlaceGardenModule(
        GameStateData state,
        string moduleId,
        int hexQ,
        int hexR,
        int rotationSteps = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        var module = state.GardenModules.FirstOrDefault(candidate => candidate.Id == moduleId);
        if (module == null)
            return new GardenModuleMutationResult(GardenModuleFailure.ModuleNotFound, false);
        if (module.Placed)
            return new GardenModuleMutationResult(GardenModuleFailure.AlreadyPlaced, false);

        var shape = GardenTileShape.Find(module.ShapeId) ?? GardenTileShape.Single;
        var cells = shape.CellsRotated(rotationSteps);
        if (!GardenHexLayout.CanPlaceShape(cells, hexQ, hexR, (q, r) => IsHexOccupied(state, q, r)))
            return new GardenModuleMutationResult(GardenModuleFailure.DoesNotFit, false);

        // The anchor cell keeps the bought item's ID so inventory, toasts and saves stay stable;
        // the rest of the piece becomes sibling hexes derived from it.
        module.HexQ = hexQ + cells[0].Q;
        module.HexR = hexR + cells[0].R;
        module.ShapeId = GardenTileShape.Single.Id;
        module.Placed = true;
        for (var index = 1; index < cells.Count; index++)
        {
            state.GardenModules.Add(new GardenModuleData
            {
                Id = SiblingHexId(state, module.Id, index),
                ShapeId = GardenTileShape.Single.Id,
                StatId = string.Empty,
                Level = 1,
                Placed = true,
                HexQ = hexQ + cells[index].Q,
                HexR = hexR + cells[index].R
            });
        }

        RefreshAssignedCreatureRates(state, module);
        return new GardenModuleMutationResult(GardenModuleFailure.None, true);
    }

    /// <summary>Turns one placed empty hex into training ground for a stat, for coins.</summary>
    public GardenModuleMutationResult ConvertHexToTrainingGround(GameStateData state, string moduleId, string statId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_rules.Genetics.StatIds.Contains(statId))
            return new GardenModuleMutationResult(GardenModuleFailure.UnknownStat, false);

        var module = state.GardenModules.FirstOrDefault(candidate => candidate.Id == moduleId);
        if (module == null)
            return new GardenModuleMutationResult(GardenModuleFailure.ModuleNotFound, false);
        if (!module.Placed)
            return new GardenModuleMutationResult(GardenModuleFailure.NotPlaced, false);
        if (module.StatId.Length > 0)
            return new GardenModuleMutationResult(GardenModuleFailure.AlreadyTrainingGround, false);

        var cost = _rules.GardenModules.TrainingConversionCost;
        if (state.Coins < cost)
            return new GardenModuleMutationResult(GardenModuleFailure.NotEnoughCurrency, false);

        state.Coins -= cost;
        module.StatId = statId;
        module.Level = 1;
        return new GardenModuleMutationResult(GardenModuleFailure.None, true, cost);
    }

    /// <summary>
    /// Every island starts as one free hex of plain ground, so there is always something for the
    /// next piece to connect to and somewhere for the starter Voidlings to stand.
    /// </summary>
    public static void EnsureStarterHex(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.GardenModules.Any(module => module.Placed))
            return;

        state.GardenModules.Add(new GardenModuleData
        {
            Id = StarterHexId,
            ShapeId = GardenTileShape.Single.Id,
            StatId = string.Empty,
            Level = 1,
            Placed = true,
            HexQ = 0,
            HexR = 0
        });
    }

    public const string StarterHexId = "starter-hex";

    private static string SiblingHexId(GameStateData state, string anchorId, int index)
    {
        var candidate = $"{anchorId}-{index}";
        var suffix = index;
        while (state.GardenModules.Any(module => string.Equals(module.Id, candidate, StringComparison.Ordinal)))
            candidate = $"{anchorId}-{++suffix}";
        return candidate;
    }

    public static bool IsHexOccupied(GameStateData state, int hexQ, int hexR)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.GardenModules.Any(module => module.Placed && module.HexQ == hexQ && module.HexR == hexR);
    }

    public GardenModuleMutationResult UpgradeGardenModule(GameStateData state, string moduleId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var module = state.GardenModules.FirstOrDefault(candidate => candidate.Id == moduleId);
        if (module == null)
            return new GardenModuleMutationResult(GardenModuleFailure.ModuleNotFound, false);
        if (module.StatId.Length == 0)
            return new GardenModuleMutationResult(GardenModuleFailure.NotTrainingGround, false);

        var cost = _rules.GardenModules.UpgradeCostForLevel(module.Level);
        if (cost < 0)
            return new GardenModuleMutationResult(GardenModuleFailure.MaxLevel, false);
        if (state.Coins < cost)
            return new GardenModuleMutationResult(GardenModuleFailure.NotEnoughCurrency, false);

        state.Coins -= cost;
        module.Level = Math.Min(_rules.GardenModules.MaxLevel, module.Level + 1);
        RefreshAssignedCreatureRates(state, module);
        return new GardenModuleMutationResult(GardenModuleFailure.None, true, cost);
    }

    /// <summary>
    /// Assigns passive training by dropping a Voidling onto a placed land tile. The tile is the
    /// source of truth for the stat and rate; the creature is free to wander off it afterwards.
    /// </summary>
    public PassiveTrainingAssignmentResult SetPassiveTrainingLand(GameStateData state, string creatureId, string moduleId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        if (creature == null)
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.CreatureNotFound, string.Empty, false);

        var module = state.GardenModules.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, moduleId, StringComparison.Ordinal));
        if (module == null || !module.Placed)
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.LandNotPlaced, string.Empty, false);
        if (module.StatId.Length == 0)
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.LandNotTrainingGround, string.Empty, false);
        if (!_rules.Genetics.StatIds.Contains(module.StatId))
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.UnknownStat, string.Empty, false);
        if (!HasRoomFor(state, module.Id, creatureId))
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.LandFull, module.StatId, false);

        var rate = RateFor(module);
        var changed = !string.Equals(creature.PassiveTrainingStatId, module.StatId, StringComparison.Ordinal) ||
                      !string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal) ||
                      creature.PassiveTrainingPointRemainder != 0.0 ||
                      !creature.PassiveTrainingPointsPerMinute.Equals(rate);
        if (changed)
        {
            creature.PassiveTrainingStatId = module.StatId;
            creature.PassiveTrainingModuleId = module.Id;
            creature.PassiveTrainingPointsPerMinute = rate;
            creature.PassiveTrainingPointRemainder = 0.0;
        }

        return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.None, module.StatId, changed);
    }

    /// <summary>
    /// True when one more Voidling still fits on a tile. A creature already training there is not
    /// counted against itself, so putting it back down on its own ground always works.
    /// </summary>
    public bool HasRoomFor(GameStateData state, string moduleId, string creatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var residents = state.Voidlings.Count(creature =>
            string.Equals(creature.PassiveTrainingModuleId, moduleId, StringComparison.Ordinal) &&
            !string.Equals(creature.Id, creatureId, StringComparison.Ordinal));
        return residents < Math.Max(1, _rules.GardenModules.VoidlingsPerTile);
    }

    public PassiveTrainingAssignmentResult StopPassiveTraining(GameStateData state, string creatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        return creature == null
            ? new PassiveTrainingAssignmentResult(PassiveTrainingFailure.CreatureNotFound, string.Empty, false)
            : ClearPassiveTraining(creature);
    }

    private PassiveTrainingAssignmentResult ClearPassiveTraining(VoidlingData creature)
    {
        var changed = creature.PassiveTrainingStatId.Length > 0 ||
                      creature.PassiveTrainingModuleId.Length > 0 ||
                      creature.PassiveTrainingPointsPerMinute != 0.0f ||
                      creature.PassiveTrainingPointRemainder != 0.0;
        if (changed)
        {
            creature.PassiveTrainingStatId = string.Empty;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
        }

        return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.None, string.Empty, changed);
    }

    private void RefreshAssignedCreatureRates(GameStateData state, GardenModuleData module)
    {
        var rate = RateFor(module);
        foreach (var creature in state.Voidlings)
        {
            if (!string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal))
                continue;

            creature.PassiveTrainingStatId = module.StatId;
            creature.PassiveTrainingPointsPerMinute = rate;
            if (rate <= 0.0f)
                creature.PassiveTrainingPointRemainder = 0.0;
        }
    }

    private float RateFor(GardenModuleData module)
        => module.Placed && module.StatId.Length > 0
            ? _rules.GardenModules.PointsPerMinuteForLevel(module.Level)
            : 0.0f;
}
