using System;
using System.Collections.Generic;

namespace Voidling.Domain.Garden;

/// <summary>
/// Flat-top axial hex grid the whole island is built from. Placement rules and rendering share
/// this geometry so "does this piece fit" and "where is it drawn" can never disagree.
/// A tile is sized the way the art is authored: <paramref name="TopEdgeWidth"/> is the flat top
/// edge, the tile is twice that wide, and <paramref name="Height"/> is its total height. Keeping
/// height independent lets a squashed sprite tile drop in without changing the grid maths.
/// There is no authored base island any more: the player's starting hex is a real placed tile and
/// everything else must grow off land that is already down.
/// </summary>
public sealed record GardenHexLayout(
    float TopEdgeWidth,
    float Height,
    float OriginX,
    float OriginY)
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

    public static IReadOnlyList<(int Q, int R)> NeighboursOf(int q, int r)
    {
        var neighbours = new (int Q, int R)[NeighbourOffsets.Length];
        for (var i = 0; i < NeighbourOffsets.Length; i++)
            neighbours[i] = (q + NeighbourOffsets[i].Q, r + NeighbourOffsets[i].R);
        return neighbours;
    }

    /// <summary>Corner-to-corner edge count of a tile; the coastline is drawn per edge.</summary>
    public static int EdgeCount => NeighbourOffsets.Length;

    /// <summary>
    /// Catan-style fit for a whole piece: no cell may overlap land that is already down, and at
    /// least one cell has to touch it, so the island always stays one connected landmass.
    /// </summary>
    public static bool CanPlaceShape(
        IReadOnlyList<(int Q, int R)> cells,
        int anchorQ,
        int anchorR,
        Func<int, int, bool> isOccupied)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(isOccupied);
        if (cells.Count == 0)
            return false;

        var touchesLand = false;
        foreach (var (offsetQ, offsetR) in cells)
        {
            var q = anchorQ + offsetQ;
            var r = anchorR + offsetR;
            if (isOccupied(q, r))
                return false;

            foreach (var (neighbourQ, neighbourR) in NeighboursOf(q, r))
                touchesLand |= isOccupied(neighbourQ, neighbourR);
        }

        return touchesLand;
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
