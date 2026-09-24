using System;
using System.Collections.Generic;
using System.Linq;

namespace Voidling.Domain.Garden;

/// <summary>
/// A biome a training hex can be: it trains one stat, and at the top star some biomes become a
/// special environment (Water 4★ is a Swamp) that keeps training the same stat.
/// </summary>
public sealed record BiomeDefinition(string Id, string StatId, string MaxStarEnvironmentId);

/// <summary>
/// Stable semantic biome IDs for Garden training hexes. Saves store these IDs, never art. A hex's
/// stars run 1..<see cref="MaxStars"/>; stacking a matching tile on top raises it one star.
/// </summary>
public static class BiomeCatalog
{
    public const int MaxStars = 4;

    public const string Plains = "plains";
    public const string Water = "water";
    public const string Mountain = "mountain";
    public const string Dry = "dry";
    public const string Grove = "grove";

    public const string Swamp = "swamp";
    public const string Volcano = "volcano";

    public static IReadOnlyList<BiomeDefinition> Biomes { get; } = Array.AsReadOnly(new[]
    {
        new BiomeDefinition(Plains, "run", string.Empty),
        new BiomeDefinition(Water, "swim", Swamp),
        new BiomeDefinition(Mountain, "fly", string.Empty),
        new BiomeDefinition(Dry, "power", Volcano),
        new BiomeDefinition(Grove, "stamina", string.Empty)
    });

    /// <summary>A base biome (not a special environment) by ID.</summary>
    public static BiomeDefinition? FindBase(string? biomeId)
        => Biomes.FirstOrDefault(biome => string.Equals(biome.Id, biomeId, StringComparison.Ordinal));

    /// <summary>The base biome of a biome or special environment: Swamp is Water. Unknown IDs give "".</summary>
    public static string BaseOf(string? biomeOrEnvironmentId)
    {
        if (FindBase(biomeOrEnvironmentId) is { } biome)
            return biome.Id;
        return Biomes.FirstOrDefault(candidate =>
                candidate.MaxStarEnvironmentId.Length > 0 &&
                string.Equals(candidate.MaxStarEnvironmentId, biomeOrEnvironmentId, StringComparison.Ordinal))?.Id
            ?? string.Empty;
    }

    public static bool IsKnown(string? biomeOrEnvironmentId) => BaseOf(biomeOrEnvironmentId).Length > 0;

    /// <summary>The stat a biome or special environment trains; "" when unknown.</summary>
    public static string StatOf(string? biomeOrEnvironmentId)
        => FindBase(BaseOf(biomeOrEnvironmentId))?.StatId ?? string.Empty;

    /// <summary>The base biome that trains a stat; "" when no biome does.</summary>
    public static string BiomeForStat(string? statId)
        => Biomes.FirstOrDefault(biome => string.Equals(biome.StatId, statId, StringComparison.Ordinal))?.Id
            ?? string.Empty;

    /// <summary>What a base biome is called at a star count: its special environment at the top star, if it has one.</summary>
    public static string IdAtStars(string baseBiomeId, int stars)
    {
        var biome = FindBase(baseBiomeId);
        if (biome == null)
            return string.Empty;
        return stars >= MaxStars && biome.MaxStarEnvironmentId.Length > 0 ? biome.MaxStarEnvironmentId : biome.Id;
    }
}
