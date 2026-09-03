using System;
using System.Collections.Generic;

namespace Voidling.Domain.Garden;

/// <summary>
/// Flat-top axial hex grid laid over the authored Garden island. Placement rules and rendering
/// share this geometry so "does this tile fit" and "where is it drawn" can never disagree.
/// A tile is sized the way the art is authored: <paramref name="TopEdgeWidth"/> is the flat top
/// edge, the tile is twice that wide, and <paramref name="Height"/> is its total height. Keeping
/// height independent lets a squashed sprite tile drop in without changing the grid maths.
/// </summary>
public sealed record GardenHexLayout(
    float TopEdgeWidth,
    float Height,
    float OriginX,
    float OriginY,
    float IslandLeft,
    float IslandTop,
    float IslandRight,
    float IslandBottom)
{
    /// <summary>Total tile width, corner to corner.</summary>
    public float Width => TopEdgeWidth * 2.0f;

    /// <summary>Largest circle that fits inside a tile, used to keep a trainee on its own ground.</summary>
    public float InnerRadius => MathF.Min(
        Height * 0.5f,
        TopEdgeWidth * Height / MathF.Sqrt(Height * Height + TopEdgeWidth * TopEdgeWidth));

    private static readonly (int Q, int R)[] NeighbourOffsets =
        { (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1) };

    public (float X, float Y) CenterOf(int q, int r)
        => (OriginX + TopEdgeWidth * 1.5f * q,
            OriginY + Height * (r + q * 0.5f));

    public (int Q, int R) At(float x, float y)
    {
        // Normalize to a regular-hex frame first so the standard axial rounding applies to a
        // squashed tile exactly as it does to a regular one.
        var px = (x - OriginX) / TopEdgeWidth;
        var py = (y - OriginY) * MathF.Sqrt(3.0f) / Height;
        return RoundAxial(px * 2.0f / 3.0f, (-px + MathF.Sqrt(3.0f) * py) / 3.0f);
    }

    /// <summary>True when the tile sits on the island the Garden scene already authors.</summary>
    public bool IsBaseIsland(int q, int r)
    {
        var (x, y) = CenterOf(q, r);
        return x >= IslandLeft && x <= IslandRight && y >= IslandTop && y <= IslandBottom;
    }

    public static IReadOnlyList<(int Q, int R)> NeighboursOf(int q, int r)
    {
        var neighbours = new (int Q, int R)[NeighbourOffsets.Length];
        for (var i = 0; i < NeighbourOffsets.Length; i++)
            neighbours[i] = (q + NeighbourOffsets[i].Q, r + NeighbourOffsets[i].R);
        return neighbours;
    }

    /// <summary>
    /// Catan-style fit: a tile may not overlap one that is already down, and must either sit on
    /// the authored island or touch land that is already there.
    /// </summary>
    public bool CanPlace(int q, int r, Func<int, int, bool> isOccupied)
    {
        ArgumentNullException.ThrowIfNull(isOccupied);
        if (isOccupied(q, r))
            return false;
        if (IsBaseIsland(q, r))
            return true;

        foreach (var (neighbourQ, neighbourR) in NeighboursOf(q, r))
        {
            if (isOccupied(neighbourQ, neighbourR) || IsBaseIsland(neighbourQ, neighbourR))
                return true;
        }

        return false;
    }

    private static (int Q, int R) RoundAxial(float q, float r)
    {
        var y = -q - r;
        var roundedQ = MathF.Round(q);
        var roundedY = MathF.Round(y);
        var roundedR = MathF.Round(r);
        var deltaQ = MathF.Abs(roundedQ - q);
        var deltaY = MathF.Abs(roundedY - y);
        var deltaR = MathF.Abs(roundedR - r);

        if (deltaQ > deltaY && deltaQ > deltaR)
            roundedQ = -roundedY - roundedR;
        else if (deltaY <= deltaR)
            roundedR = -roundedQ - roundedY;

        return ((int)roundedQ, (int)roundedR);
    }
}
