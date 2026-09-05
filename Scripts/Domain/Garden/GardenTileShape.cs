using System;
using System.Collections.Generic;
using System.Linq;

namespace Voidling.Domain.Garden;

/// <summary>
/// A buyable piece of land: one to three connected hexes. Offsets are axial and the first cell is
/// always (0, 0) — the hex the player is aiming at — so rotating about that anchor keeps the piece
/// under the cursor. Price and training capacity both follow <see cref="HexCount"/>: every hex of a
/// piece is its own tile once it is down, holding one Voidling when it is turned into training
/// ground.
/// </summary>
public sealed record GardenTileShape(string Id, IReadOnlyList<(int Q, int R)> Cells)
{
    public int HexCount => Cells.Count;

    /// <summary>Rotations that produce a distinct footprint; the rest repeat one of these.</summary>
    public int RotationCount => HexCount == 1 ? 1 : 6;

    public static readonly GardenTileShape Single = new("single", Offsets((0, 0)));
    public static readonly GardenTileShape Pair = new("pair", Offsets((0, 0), (1, 0)));
    public static readonly GardenTileShape Line = new("line", Offsets((0, 0), (1, 0), (2, 0)));
    public static readonly GardenTileShape Bend = new("bend", Offsets((0, 0), (1, 0), (1, 1)));
    public static readonly GardenTileShape Cluster = new("cluster", Offsets((0, 0), (1, 0), (0, 1)));

    public static IReadOnlyList<GardenTileShape> Catalog { get; } =
        Array.AsReadOnly(new[] { Single, Pair, Line, Bend, Cluster });

    public static GardenTileShape? Find(string id)
        => Catalog.FirstOrDefault(shape => string.Equals(shape.Id, id, StringComparison.Ordinal));

    /// <summary>The footprint after <paramref name="steps"/> 60° clockwise turns about the anchor.</summary>
    public IReadOnlyList<(int Q, int R)> CellsRotated(int steps)
        => Cells.Select(cell => Rotate(cell, steps)).ToArray();

    /// <summary>Axial 60° clockwise rotation about (0, 0), which is why the anchor never moves.</summary>
    public static (int Q, int R) Rotate((int Q, int R) cell, int steps)
    {
        var (q, r) = cell;
        for (var turn = 0; turn < ((steps % 6) + 6) % 6; turn++)
            (q, r) = (-r, q + r);
        return (q, r);
    }

    private static IReadOnlyList<(int Q, int R)> Offsets(params (int Q, int R)[] cells) => Array.AsReadOnly(cells);
}
