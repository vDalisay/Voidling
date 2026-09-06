using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// The piece's own footprint, scaled to fit and coloured by the ground it carries, so two
    /// pieces are told apart at a glance instead of by their text alone. The holder is a
    /// TextureRect with no texture: the sized, mouse-transparent control every slot and card here
    /// already expects.
    /// </summary>
    public static TextureRect CreateShapeArt(string shapeId, Color tint, float boxWidth = 40.0f, float boxHeight = 26.0f)
    {
        const float ratio = 1.7f;
        var shape = GardenTileShape.Find(shapeId) ?? GardenTileShape.Single;

        var units = shape.Cells.Select(cell => new Vector2(1.5f * cell.Q, cell.R + cell.Q * 0.5f)).ToArray();
        var spanX = units.Max(unit => unit.X) - units.Min(unit => unit.X) + 2.0f;
        var spanY = units.Max(unit => unit.Y) - units.Min(unit => unit.Y) + 1.0f;
        var topEdge = Mathf.Min(boxWidth / spanX, boxHeight / (ratio * spanY));
        var height = topEdge * ratio;

        var centers = units.Select(unit => new Vector2(unit.X * topEdge, unit.Y * height)).ToArray();
        var middle = new Vector2(
            (centers.Max(center => center.X) + centers.Min(center => center.X)) * 0.5f,
            (centers.Max(center => center.Y) + centers.Min(center => center.Y)) * 0.5f);
        var origin = new Vector2(boxWidth * 0.5f, boxHeight * 0.5f) - middle;

        var holder = new TextureRect
        {
            CustomMinimumSize = new Vector2(boxWidth, boxHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        foreach (var center in centers)
        {
            var polygon = HexShape.Corners(topEdge, height);
            for (var i = 0; i < polygon.Length; i++) polygon[i] += origin + center;
            var outline = HexShape.Outline(topEdge, height);
            for (var i = 0; i < outline.Length; i++) outline[i] += origin + center;
            holder.AddChild(new Polygon2D { Polygon = polygon, Color = tint });
            holder.AddChild(new Line2D
            {
                Points = outline, DefaultColor = tint.Darkened(0.45f), Width = 1.0f, JointMode = Line2D.LineJointMode.Round
            });
        }
        return holder;
    }

    private static readonly Color PlainGroundColor = Color.FromHtml("#8FC57E");
}
