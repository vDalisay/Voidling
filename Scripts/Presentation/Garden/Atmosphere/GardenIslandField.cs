using System;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// A coarse raster of the island: which cells are land and how far every cell is from the shore.
///
/// It is the one picture of the coastline every Garden effect shares. The sea shader reads it as a
/// texture for its shallows and shore waves; the CPU side asks it where rain lands on grass and
/// whether a Voidling stands close enough to the water to be reflected. It is rebuilt only when
/// land is placed, and it is plain C# so the distance transform can be tested without the engine.
///
/// Distances are signed and in world pixels: positive over water (distance to the nearest land),
/// negative on land (distance to the nearest water), clamped to <see cref="MaxDistance"/>.
/// </summary>
public sealed class GardenIslandField
{
    private const float Diagonal = 1.41421356f;

    private readonly float[] _signed;

    private GardenIslandField(float originX, float originY, float cellSize, int columns, int rows, float maxDistance, float[] signed)
    {
        OriginX = originX;
        OriginY = originY;
        CellSize = cellSize;
        Columns = columns;
        Rows = rows;
        MaxDistance = maxDistance;
        _signed = signed;
    }

    public float OriginX { get; }
    public float OriginY { get; }
    public float CellSize { get; }
    public int Columns { get; }
    public int Rows { get; }
    public float MaxDistance { get; }
    public float Width => Columns * CellSize;
    public float Height => Rows * CellSize;

    /// <summary>An island with no land at all: open water everywhere.</summary>
    public static GardenIslandField Empty { get; } =
        new(0.0f, 0.0f, 1.0f, 1, 1, 64.0f, new[] { 64.0f });

    /// <summary>
    /// Rasterises <paramref name="isLand"/> over the given world rectangle, sampling each cell at its
    /// centre, then runs a two-pass chamfer distance transform outwards from the coast in both
    /// directions.
    /// </summary>
    public static GardenIslandField Build(
        Func<float, float, bool> isLand,
        float minX,
        float minY,
        float maxX,
        float maxY,
        float cellSize,
        float maxDistance)
    {
        ArgumentNullException.ThrowIfNull(isLand);
        if (cellSize <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (maxX <= minX || maxY <= minY)
            throw new ArgumentException("Field bounds must have a positive area.");

        var columns = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cellSize));
        var rows = Math.Max(1, (int)Math.Ceiling((maxY - minY) / cellSize));
        var land = new bool[columns * rows];
        for (var row = 0; row < rows; row++)
        {
            var y = minY + (row + 0.5f) * cellSize;
            for (var column = 0; column < columns; column++)
                land[row * columns + column] = isLand(minX + (column + 0.5f) * cellSize, y);
        }

        var toLand = Chamfer(land, columns, rows, target: true, maxDistance / cellSize);
        var toWater = Chamfer(land, columns, rows, target: false, maxDistance / cellSize);
        var signed = new float[land.Length];
        for (var i = 0; i < signed.Length; i++)
        {
            // Measured to the boundary between cells rather than to the neighbouring cell's centre,
            // so the coast itself sits at zero.
            var distance = land[i] ? -(toWater[i] - 0.5f) : toLand[i] - 0.5f;
            signed[i] = Math.Clamp(distance * cellSize, -maxDistance, maxDistance);
        }

        return new GardenIslandField(minX, minY, cellSize, columns, rows, maxDistance, signed);
    }

    /// <summary>Signed distance at a world position; open water beyond the field's edge.</summary>
    public float SignedDistance(float x, float y)
    {
        var column = (int)Math.Floor((x - OriginX) / CellSize);
        var row = (int)Math.Floor((y - OriginY) / CellSize);
        if (column < 0 || row < 0 || column >= Columns || row >= Rows)
            return MaxDistance;

        return _signed[row * Columns + column];
    }

    public bool IsLand(float x, float y) => SignedDistance(x, y) < 0.0f;

    /// <summary>
    /// The field encoded one byte per cell for a texture: 0.5 is the coast, brighter is further out
    /// to sea and darker is further inland, scaled so <see cref="MaxDistance"/> reaches the ends.
    /// </summary>
    public byte[] EncodeBytes()
    {
        var bytes = new byte[_signed.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var normalized = 0.5f + _signed[i] / (2.0f * MaxDistance);
            bytes[i] = (byte)Math.Clamp((int)Math.Round(normalized * 255.0f), 0, 255);
        }

        return bytes;
    }

    /// <summary>
    /// Distance, in cells, from every cell to the nearest cell whose land flag equals
    /// <paramref name="target"/>. Cells that already match are zero.
    /// </summary>
    private static float[] Chamfer(bool[] land, int columns, int rows, bool target, float cap)
    {
        var far = cap + 2.0f;
        var distance = new float[land.Length];
        for (var i = 0; i < distance.Length; i++)
            distance[i] = land[i] == target ? 0.0f : far;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var i = row * columns + column;
                var best = distance[i];
                if (best == 0.0f)
                    continue;
                if (column > 0) best = Math.Min(best, distance[i - 1] + 1.0f);
                if (row > 0)
                {
                    best = Math.Min(best, distance[i - columns] + 1.0f);
                    if (column > 0) best = Math.Min(best, distance[i - columns - 1] + Diagonal);
                    if (column < columns - 1) best = Math.Min(best, distance[i - columns + 1] + Diagonal);
                }
                distance[i] = best;
            }
        }

        for (var row = rows - 1; row >= 0; row--)
        {
            for (var column = columns - 1; column >= 0; column--)
            {
                var i = row * columns + column;
                var best = distance[i];
                if (best == 0.0f)
                    continue;
                if (column < columns - 1) best = Math.Min(best, distance[i + 1] + 1.0f);
                if (row < rows - 1)
                {
                    best = Math.Min(best, distance[i + columns] + 1.0f);
                    if (column < columns - 1) best = Math.Min(best, distance[i + columns + 1] + Diagonal);
                    if (column > 0) best = Math.Min(best, distance[i + columns - 1] + Diagonal);
                }
                distance[i] = best;
            }
        }

        return distance;
    }
}
