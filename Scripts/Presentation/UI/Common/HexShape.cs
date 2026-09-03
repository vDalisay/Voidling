using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// The one flat-top hexagon outline every Garden/land visual is drawn from. A tile is described by
/// its flat top edge and its total height, which is how the art is authored: at a top edge of 5 the
/// tile is 10 wide, so the same numbers scale straight from a sprite sheet to the world grid.
/// </summary>
public static class HexShape
{
    /// <summary>Total width of a tile whose flat top edge is <paramref name="topEdgeWidth"/>.</summary>
    public static float WidthFor(float topEdgeWidth) => topEdgeWidth * 2.0f;

    /// <summary>Corners in draw order, centred on the origin.</summary>
    public static Vector2[] Corners(float topEdgeWidth, float height)
    {
        var halfTop = topEdgeWidth * 0.5f;
        var halfHeight = height * 0.5f;
        return new[]
        {
            new Vector2(-halfTop, -halfHeight),
            new Vector2(halfTop, -halfHeight),
            new Vector2(topEdgeWidth, 0.0f),
            new Vector2(halfTop, halfHeight),
            new Vector2(-halfTop, halfHeight),
            new Vector2(-topEdgeWidth, 0.0f)
        };
    }

    /// <summary>Closed ring for outlining a tile with a <see cref="Line2D"/>.</summary>
    public static Vector2[] Outline(float topEdgeWidth, float height)
    {
        var corners = Corners(topEdgeWidth, height);
        var ring = new Vector2[corners.Length + 1];
        corners.CopyTo(ring, 0);
        ring[^1] = corners[0];
        return ring;
    }
}
