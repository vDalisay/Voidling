using System;
using System.Linq;
using Voidling.Application.Garden;
using Voidling.Domain.Creatures;
using Voidling.Domain.Garden;
using VoidlingGame;

namespace Voidling.Application.Collection;

/// <summary>
/// Keeps each special variant's one-at-a-time life cycle: the one-time breeding spawn, an egg that
/// only incubates on an unused special environment, the hex it hatches on becoming used, and a
/// departure (death or Goodbye) opening the respawn egg. Pure state bookkeeping; no randomness.
/// </summary>
public static class SpecialVariantTracker
{
    public static SpecialVariantStateData? Find(GameStateData state, string variantId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.SpecialVariants.FirstOrDefault(entry =>
            string.Equals(entry.VariantId, variantId, StringComparison.Ordinal));
    }

    public static SpecialVariantStatus StatusOf(GameStateData state, string variantId)
        => Find(state, variantId)?.Status ?? SpecialVariantStatus.NotSpawned;

    /// <summary>A placed hex of the variant's environment that has never hatched one.</summary>
    public static bool HasUnusedEnvironment(GameStateData state, SpecialVariantDefinition variant)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(variant);
        return state.GardenModules.Any(module => IsUnusedEnvironment(module, variant));
    }

    /// <summary>
    /// The variant these parents' egg becomes, if any: the first breeding of two qualifying adults
    /// while the player owns an unused environment and no such variant exists yet.
    /// </summary>
    public static SpecialVariantDefinition? BreedingSpawnFor(GameStateData state, VoidlingData parentA, VoidlingData parentB)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var variant in SpecialVariantCatalog.All)
        {
            var entry = Find(state, variant.Id);
            if (entry is { BreedingSpawnUsed: true } || (entry?.Status ?? SpecialVariantStatus.NotSpawned) != SpecialVariantStatus.NotSpawned)
                continue;
            if (SpecialVariantCatalog.ParentsQualify(variant, parentA, parentB) && HasUnusedEnvironment(state, variant))
                return variant;
        }

        return null;
    }

    public static void RecordEgg(GameStateData state, SpecialVariantDefinition variant, string eggId, bool fromBreeding)
    {
        var entry = GetOrCreate(state, variant.Id);
        entry.Status = SpecialVariantStatus.Egg;
        entry.CreatureId = eggId;
        entry.BreedingSpawnUsed |= fromBreeding;
    }

    /// <summary>
    /// Whether an egg can incubate where it lies. Ordinary eggs always can; a special variant's egg
    /// only on an unused hex of its environment.
    /// </summary>
    public static bool CanIncubate(GameStateData state, EggData egg, GardenHexLayout hex)
        => string.IsNullOrEmpty(egg.SpecialVariantId) || UnusedEnvironmentUnder(state, egg, hex) != null;

    /// <summary>The hatch: the hex it hatched on is used from now on, and the variant is alive.</summary>
    public static void RecordHatch(GameStateData state, EggData egg, VoidlingData creature, GardenHexLayout hex)
    {
        var variant = SpecialVariantCatalog.Find(egg.SpecialVariantId);
        if (variant == null)
            return;

        var module = UnusedEnvironmentUnder(state, egg, hex);
        if (module != null)
            module.SpecialVariantHatched = true;

        var entry = GetOrCreate(state, variant.Id);
        entry.Status = SpecialVariantStatus.Alive;
        entry.CreatureId = creature.Id;
    }

    /// <summary>Death and Goodbye both end a special variant's life and open its respawn egg.</summary>
    public static void RecordDeparture(GameStateData state, VoidlingData creature)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(creature);
        var variant = SpecialVariantCatalog.Find(creature.SpecialVariantId);
        if (variant == null)
            return;

        var entry = GetOrCreate(state, variant.Id);
        if (string.Equals(entry.CreatureId, creature.Id, StringComparison.Ordinal))
            entry.Status = SpecialVariantStatus.Departed;
    }

    public static bool RespawnAvailable(GameStateData state, SpecialVariantDefinition variant)
        => StatusOf(state, variant.Id) == SpecialVariantStatus.Departed;

    private static GardenModuleData? UnusedEnvironmentUnder(GameStateData state, EggData egg, GardenHexLayout hex)
    {
        var variant = SpecialVariantCatalog.Find(egg.SpecialVariantId);
        if (variant == null)
            return null;

        var (q, r) = hex.At(egg.WorldX, egg.WorldY);
        return state.GardenModules.FirstOrDefault(module =>
            module.Placed && module.HexQ == q && module.HexR == r && IsUnusedEnvironment(module, variant));
    }

    private static bool IsUnusedEnvironment(GardenModuleData module, SpecialVariantDefinition variant)
        => module.Placed &&
           !module.SpecialVariantHatched &&
           string.Equals(module.BiomeId, variant.Environment, StringComparison.Ordinal);

    private static SpecialVariantStateData GetOrCreate(GameStateData state, string variantId)
    {
        var entry = Find(state, variantId);
        if (entry != null)
            return entry;

        entry = new SpecialVariantStateData { VariantId = variantId };
        state.SpecialVariants.Add(entry);
        return entry;
    }
}
