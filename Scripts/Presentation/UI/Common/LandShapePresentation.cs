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

    /// <summary>Green for plain ground, the stat's own colour for ground that already trains.</summary>
    public static Color TintFor(string statId)
        => statId.Length == 0 ? PlainGroundColor : StatPresentationCatalog.ColorFor(statId);

    /// <summary>
    /// What an inventory row says about a piece: its shape and size, plus the ground it carries, so
    /// two stored pieces are never just two identical lines.
    /// </summary>
    public static string DescribeStoredPiece(string shapeId, string statId, int level)
    {
        var shape = string.Format(
            TranslationServer.Translate("UI_INVENTORY_LAND_TILE"),
            NameFor(shapeId),
            HexCountOf(shapeId));
        return statId.Length == 0
            ? string.Format(TranslationServer.Translate("UI_INVENTORY_LAND_PLAIN"), shape)
            : string.Format(
                TranslationServer.Translate("UI_INVENTORY_LAND_TRAINED"),
                StatPresentationCatalog.NameFor(statId).ToUpperInvariant(),
                level,
                shape);
    }

    private static readonly Color PlainGroundColor = Color.FromHtml("#8FC57E");
}
