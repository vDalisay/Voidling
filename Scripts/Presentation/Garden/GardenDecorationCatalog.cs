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

    private static readonly GardenDecorationDefinition[] Definitions =
    {
        new("tree", "Tree", new Rect2(0, 0, 32, 48), 1.00f),
        new("small_tree", "Small Tree", new Rect2(0, 0, 32, 48), 0.78f),
        new("large_tree", "Large Tree", new Rect2(0, 0, 32, 48), 1.22f)
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
