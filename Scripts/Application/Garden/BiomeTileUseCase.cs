using System;
using System.Linq;
using Voidling.Domain.Garden;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Garden;

public enum BiomeTileFailure
{
    None,
    UnknownBiome,
    ModuleNotFound,
    NotPlaced,
    NotEnoughCurrency,
    NoTileOwned,
    /// <summary>The hex holds another biome or another star; only a matching tile stacks.</summary>
    TileDoesNotMatch,
    /// <summary>The hex is already at the top star.</summary>
    MaxStars,
    /// <summary>Plain ground: there is no biome tile to pick up.</summary>
    NotBiome,
    /// <summary>A four-star tile (Swamp, Volcano, …) stays where it is.</summary>
    TileIsPermanent
}

public readonly record struct BiomeTileResult(
    BiomeTileFailure Failure,
    bool Changed,
    int CoinsSpent = 0,
    bool Stacked = false,
    string BiomeId = "",
    int Stars = 0)
{
    public bool Succeeded => Failure == BiomeTileFailure.None;
}

/// <summary>
/// Biome tiles, Cow-Evolution style. The shop sells one-star tiles. A tile placed on plain ground
/// turns the hex into that biome; a tile placed on a hex of the same biome and star merges with it
/// into one tile a star higher, up to four stars, where Water becomes a Swamp and Dry a Volcano.
/// A tile below four stars can be picked back up, with its stars, to stack elsewhere.
/// </summary>
public sealed class BiomeTileUseCase
{
    private readonly GameBalanceRules _rules;

    public BiomeTileUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public int MaxStars => Math.Min(BiomeCatalog.MaxStars, _rules.GardenModules.MaxLevel);

    public BiomeTileResult BuyBiomeTile(GameStateData state, string biomeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (BiomeCatalog.FindBase(biomeId) == null)
            return new BiomeTileResult(BiomeTileFailure.UnknownBiome, false);

        var price = Math.Max(0, _rules.GardenModules.BiomeTilePrice);
        if (state.Coins < price)
            return new BiomeTileResult(BiomeTileFailure.NotEnoughCurrency, false);

        state.Coins -= price;
        AddTile(state, biomeId, 1);
        return new BiomeTileResult(BiomeTileFailure.None, true, price, BiomeId: biomeId, Stars: 1);
    }

    /// <summary>Buys a one-star tile and puts it straight onto a plain hex, as one action.</summary>
    public BiomeTileResult BuildBiome(GameStateData state, string moduleId, string biomeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var module = FindPlaced(state, moduleId, out var failure);
        if (module == null)
            return new BiomeTileResult(failure, false);
        if (BiomeCatalog.FindBase(biomeId) == null)
            return new BiomeTileResult(BiomeTileFailure.UnknownBiome, false);
        if (module.BiomeId.Length > 0)
            return new BiomeTileResult(BiomeTileFailure.TileDoesNotMatch, false);

        var bought = BuyBiomeTile(state, biomeId);
        if (!bought.Succeeded)
            return bought;

        var placed = PlaceBiomeTile(state, moduleId, biomeId, 1);
        return placed with { CoinsSpent = bought.CoinsSpent };
    }

    public BiomeTileFailure ValidatePlacement(GameStateData state, string moduleId, string biomeId, int stars)
    {
        ArgumentNullException.ThrowIfNull(state);
        var module = FindPlaced(state, moduleId, out var failure);
        if (module == null)
            return failure;
        if (BiomeCatalog.FindBase(biomeId) == null || stars < 1 || stars >= MaxStars + 1)
            return BiomeTileFailure.UnknownBiome;
        if (OwnedCount(state, biomeId, stars) <= 0)
            return BiomeTileFailure.NoTileOwned;
        if (module.BiomeId.Length == 0)
            return BiomeTileFailure.None;
        if (!string.Equals(BiomeCatalog.BaseOf(module.BiomeId), biomeId, StringComparison.Ordinal) || module.Level != stars)
            return BiomeTileFailure.TileDoesNotMatch;
        return module.Level >= MaxStars ? BiomeTileFailure.MaxStars : BiomeTileFailure.None;
    }

    /// <summary>
    /// Puts an owned tile onto a hex. On plain ground the hex becomes that biome at the tile's stars;
    /// on a matching tile both merge into one tile a star higher.
    /// </summary>
    public BiomeTileResult PlaceBiomeTile(GameStateData state, string moduleId, string biomeId, int stars)
    {
        var failure = ValidatePlacement(state, moduleId, biomeId, stars);
        if (failure != BiomeTileFailure.None)
            return new BiomeTileResult(failure, false);

        var module = state.GardenModules.First(candidate => candidate.Placed && candidate.Id == moduleId);
        var stacked = module.BiomeId.Length > 0;
        var newStars = stacked ? module.Level + 1 : stars;
        RemoveTile(state, biomeId, stars);
        module.Level = newStars;
        module.BiomeId = BiomeCatalog.IdAtStars(biomeId, newStars);
        module.StatId = BiomeCatalog.StatOf(biomeId);
        GardenTrainingRates.RefreshAssignedCreatures(state, module, _rules.GardenModules);
        return new BiomeTileResult(BiomeTileFailure.None, true, Stacked: stacked, BiomeId: module.BiomeId, Stars: newStars);
    }

    /// <summary>Lifts a biome tile off a hex into the inventory with its stars; the hex is plain ground again.</summary>
    public BiomeTileResult PickUpBiomeTile(GameStateData state, string moduleId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var module = FindPlaced(state, moduleId, out var failure);
        if (module == null)
            return new BiomeTileResult(failure, false);
        if (module.BiomeId.Length == 0)
            return new BiomeTileResult(BiomeTileFailure.NotBiome, false);
        if (module.Level >= MaxStars)
            return new BiomeTileResult(BiomeTileFailure.TileIsPermanent, false);

        var baseBiome = BiomeCatalog.BaseOf(module.BiomeId);
        var stars = Math.Clamp(module.Level, 1, MaxStars);
        AddTile(state, baseBiome, stars);
        module.BiomeId = string.Empty;
        module.StatId = string.Empty;
        module.Level = 1;
        GardenTrainingRates.UnassignCreatures(state, module.Id);
        return new BiomeTileResult(BiomeTileFailure.None, true, BiomeId: baseBiome, Stars: stars);
    }

    public static int OwnedCount(GameStateData state, string biomeId, int stars)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.BiomeTiles
            .Where(stack => string.Equals(stack.BiomeId, biomeId, StringComparison.Ordinal) && stack.Stars == stars)
            .Sum(stack => Math.Max(0, stack.Count));
    }

    private static void AddTile(GameStateData state, string biomeId, int stars)
    {
        var stack = state.BiomeTiles.FirstOrDefault(candidate =>
            string.Equals(candidate.BiomeId, biomeId, StringComparison.Ordinal) && candidate.Stars == stars);
        if (stack == null)
        {
            stack = new BiomeTileStackData { BiomeId = biomeId, Stars = stars };
            state.BiomeTiles.Add(stack);
        }

        stack.Count = stack.Count == int.MaxValue ? int.MaxValue : stack.Count + 1;
    }

    private static void RemoveTile(GameStateData state, string biomeId, int stars)
    {
        var stack = state.BiomeTiles.First(candidate =>
            string.Equals(candidate.BiomeId, biomeId, StringComparison.Ordinal) &&
            candidate.Stars == stars &&
            candidate.Count > 0);
        stack.Count--;
        if (stack.Count <= 0)
            state.BiomeTiles.Remove(stack);
    }

    private static GardenModuleData? FindPlaced(GameStateData state, string moduleId, out BiomeTileFailure failure)
    {
        var module = state.GardenModules.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, moduleId, StringComparison.Ordinal));
        failure = module == null
            ? BiomeTileFailure.ModuleNotFound
            : module.Placed ? BiomeTileFailure.None : BiomeTileFailure.NotPlaced;
        return failure == BiomeTileFailure.None ? module : null;
    }
}
