using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Voidling.Presentation.Garden;

public readonly record struct GardenDecorationDefinition(
    string TypeId,
    string DisplayName,
    Rect2 AtlasRegion,
    float Scale);

/// <summary>
/// Presentation catalog for cosmetic Garden objects. Content is semantic and authorable here;
/// persisted saves store only the stable TypeId and position.
/// </summary>
public static class GardenDecorationCatalog
{
    public const string TexturePath = "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Basic Grass Biom things 1.png";

    /// <summary>
    /// The sheet's big round tree: exactly its 32x32 cell, so no neighbouring sprite (the small tree
    /// to its left, the cherries below) is cut in with it. The three sizes are one tree at three
    /// scales.
    /// </summary>
    public static readonly Rect2 RoundTreeRegion = new(16, 0, 32, 32);

    private static readonly GardenDecorationDefinition[] Definitions =
    {
        new("tree", "Tree", RoundTreeRegion, 1.00f),
        new("small_tree", "Small Tree", RoundTreeRegion, 0.78f),
        new("large_tree", "Large Tree", RoundTreeRegion, 1.22f)
    };

    public static IReadOnlyList<GardenDecorationDefinition> All => Definitions;

    public static bool TryGet(string typeId, out GardenDecorationDefinition definition)
    {
        foreach (var candidate in Definitions)
        {
            if (string.Equals(candidate.TypeId, typeId, StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        definition = default;
        return false;
    }

    public static string NameFor(string typeId)
        => TryGet(typeId, out var definition) ? definition.DisplayName : typeId;
}
