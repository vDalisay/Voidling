using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Player-facing names and identity colors for Garden biomes and their four-star environments.
/// Gameplay owns the biome IDs; this only names and tints them. Biome ground art slots in here when
/// it arrives, so the island, the Build screen, the shop and the inventory stay in step.
/// </summary>
public static class BiomePresentationCatalog
{
    private sealed record BiomeLook(string NameKey, Color Color);

    private static readonly IReadOnlyDictionary<string, BiomeLook> Looks =
        new Dictionary<string, BiomeLook>(StringComparer.Ordinal)
        {
            ["plains"] = new("BIOME_PLAINS", Color.FromHtml("#A8D46F")),
            ["water"] = new("BIOME_WATER", Color.FromHtml("#6FB3E3")),
            ["mountain"] = new("BIOME_MOUNTAIN", Color.FromHtml("#A9A3CC")),
            ["dry"] = new("BIOME_DRY", Color.FromHtml("#E6C47C")),
            ["grove"] = new("BIOME_GROVE", Color.FromHtml("#5FA062")),
            ["swamp"] = new("BIOME_SWAMP", Color.FromHtml("#6F8F4C")),
            ["volcano"] = new("BIOME_VOLCANO", Color.FromHtml("#C9563F"))
        };

    /// <summary>Plain ground keeps the grass of the island.</summary>
    public static readonly Color PlainGroundColor = Color.FromHtml("#8FC57E");

    public static string NameFor(string? biomeId)
        => biomeId != null && Looks.TryGetValue(biomeId, out var look)
            ? TranslationServer.Translate(look.NameKey)
            : TranslationServer.Translate("UI_LAND_PLAIN_GROUND");

    public static Color ColorFor(string? biomeId)
        => biomeId != null && Looks.TryGetValue(biomeId, out var look) ? look.Color : PlainGroundColor;
}
