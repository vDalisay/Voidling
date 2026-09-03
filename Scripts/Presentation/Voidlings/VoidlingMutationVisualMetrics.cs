using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Voidlings;

public enum AngelHaloPixelTone
{
    Back,
    Gold,
    Shine
}

public readonly record struct AngelHaloPixel(Rect2 Rect, AngelHaloPixelTone Tone);

public readonly record struct AngelHaloVisual(
    Vector2 Center,
    float CellSize,
    bool Compact);

/// <summary>
/// Canonical Angel-mutation presentation. Garden actors, races and UI portraits all consume this
/// exact pixel layout. Art-dependent anchors resolve from the selected visual-family definition so
/// Water/Power/Flying silhouettes can tune mutation placement independently without consumer edits.
/// </summary>
public static class VoidlingMutationVisualMetrics
{
    private static readonly string[] AdultPattern =
    {
        "  #####  ",
        "##     ##",
        "#       #",
        "##     ##",
        "  #####  "
    };

    private static readonly string[] CompactPattern =
    {
        " ### ",
        "#   #",
        " ### "
    };

    public static readonly Color BackColor = Color.FromHtml("#C99B37");
    public static readonly Color GoldColor = Color.FromHtml("#F1CE55");
    public static readonly Color ShineColor = Color.FromHtml("#FFF2A8");

    public static AngelHaloVisual ForSpriteTarget(float spriteScale, string? visualTypeId = null)
    {
        var definition = VoidlingVisualFactory.ResolveDefinition(visualTypeId);
        var referenceScale = Mathf.Max(0.01f, definition.AdultWorldScale);
        var ratio = Mathf.Max(0.25f, spriteScale / referenceScale);
        var compact = spriteScale < definition.MutationCompactScaleThreshold;
        var localCenterY = compact
            ? definition.MutationCompactCenterYOffset
            : definition.MutationAdultCenterYOffset * ratio;
        return new AngelHaloVisual(new Vector2(0, localCenterY), 1.0f, compact);
    }

    public static AngelHaloVisual ForPortrait(
        float nominalSpritePixels,
        Vector2 controlSize,
        string? visualTypeId = null)
    {
        var definition = VoidlingVisualFactory.ResolveDefinition(visualTypeId);
        var referencePixels = Mathf.Max(1.0f, definition.FrameHeight);
        var spriteScale = Mathf.Max(0.20f, nominalSpritePixels / referencePixels);
        var compact = nominalSpritePixels < definition.PortraitMutationCompactPixelThreshold;
        var referenceScale = Mathf.Max(0.01f, definition.AdultWorldScale);
        var localCenterY = compact
            ? definition.MutationCompactCenterYOffset
            : definition.MutationAdultCenterYOffset *
              Mathf.Max(0.65f, spriteScale / referenceScale);
        var cellSize = Mathf.Max(1.0f, Mathf.Round(nominalSpritePixels / referencePixels));
        return new AngelHaloVisual(
            new Vector2(controlSize.X * 0.5f, controlSize.Y * 0.5f + localCenterY),
            cellSize,
            compact);
    }

    public static IReadOnlyList<AngelHaloPixel> BuildPixels(AngelHaloVisual halo)
    {
        var pattern = halo.Compact ? CompactPattern : AdultPattern;
        var pixels = new List<AngelHaloPixel>(32);
        var width = pattern[0].Length;
        var height = pattern.Length;
        var origin = halo.Center - new Vector2(
            (width - 1) * halo.CellSize * 0.5f,
            (height - 1) * halo.CellSize * 0.5f);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < pattern[y].Length; x++)
            {
                if (pattern[y][x] != '#')
                    continue;

                var tone = y < height / 2
                    ? AngelHaloPixelTone.Back
                    : y == height / 2
                        ? AngelHaloPixelTone.Gold
                        : AngelHaloPixelTone.Shine;
                pixels.Add(new AngelHaloPixel(
                    new Rect2(origin + new Vector2(x, y) * halo.CellSize, Vector2.One * halo.CellSize),
                    tone));
            }
        }

        return pixels;
    }

    public static Color ColorFor(AngelHaloPixelTone tone)
        => tone switch
        {
            AngelHaloPixelTone.Back => BackColor,
            AngelHaloPixelTone.Shine => ShineColor,
            _ => GoldColor
        };
}
