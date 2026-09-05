using System;
using System.Collections.Generic;
using Godot;
using Voidling.Domain.Garden;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Player-facing identity for the land pieces the shop sells. The shapes themselves and their
/// prices stay domain-owned; this only maps a shape ID to the name on the card.
/// </summary>
public static class LandShapePresentation
{
    private static readonly IReadOnlyDictionary<string, string> NameKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GardenTileShape.Single.Id] = "UI_LAND_SHAPE_SINGLE",
            [GardenTileShape.Pair.Id] = "UI_LAND_SHAPE_PAIR",
            [GardenTileShape.Line.Id] = "UI_LAND_SHAPE_LINE",
            [GardenTileShape.Bend.Id] = "UI_LAND_SHAPE_BEND",
            [GardenTileShape.Cluster.Id] = "UI_LAND_SHAPE_CLUSTER"
        };

    public static string NameFor(string shapeId)
        => NameKeys.TryGetValue(shapeId, out var key)
            ? TranslationServer.Translate(key)
            : TranslationServer.Translate("UI_LAND_SHAPE_SINGLE");

    /// <summary>How many hexes a piece covers, for labels that count capacity.</summary>
    public static int HexCountOf(string shapeId) => GardenTileShape.Find(shapeId)?.HexCount ?? 1;
}
