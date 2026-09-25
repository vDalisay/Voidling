using System;

namespace Voidling.Presentation.Lighting;

/// <summary>
/// Builds a normal map for pixel-art sprites that were never drawn with one.
///
/// Each sprite is treated as one soft, rounded shape made from its silhouette alone: a pixel's
/// height rises with its distance from the edge along a quarter circle and levels off
/// <see cref="RoundingPixels"/> in, so the outline rounds away and the middle faces the viewer. The
/// artwork's own colours play no part: shading drawn into the art stays exactly as drawn instead of
/// being embossed into bumps, which is what makes generated maps look like 3D clay rather than lit
/// pixel art. Detail a single pixel wide stays flat, since both of its neighbours are empty.
///
/// A sheet is processed one frame cell at a time, so a sprite never rounds against its neighbour's
/// pixels. The output follows the OpenGL convention Godot's 2D lighting expects (green points up).
/// Plain C# over RGBA8 bytes, so it is tested without the engine.
/// </summary>
public static class SpriteNormalMapBuilder
{
    /// <summary>How many pixels in from the edge the shape keeps rounding before it faces forward.</summary>
    public const float RoundingPixels = 7.0f;

    /// <summary>How steeply height turns into tilt. The outermost pixel leans about 55 degrees.</summary>
    public const float Strength = 4.0f;

    private const float Diagonal = 1.41421356f;

    /// <summary>
    /// Returns an RGBA8 normal map the same size as <paramref name="rgba"/>. Transparent pixels get
    /// the flat normal. <paramref name="cellWidth"/> and <paramref name="cellHeight"/> are the frame
    /// size; pass the whole image size for a single sprite.
    /// </summary>
    public static byte[] Build(byte[] rgba, int width, int height, int cellWidth, int cellHeight)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0 || rgba.Length < width * height * 4)
            throw new ArgumentException("Image data does not match its size.", nameof(rgba));
        if (cellWidth <= 0 || cellHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellWidth), "Cell size must be positive.");

        var heights = Heights(rgba, width, height, cellWidth, cellHeight);
        var normals = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                if (heights[i] <= 0.0f)
                {
                    Write(normals, i, 0.0f, 0.0f, 1.0f);
                    continue;
                }

                // Neighbours outside the frame count as empty, like transparent pixels.
                var left = SameCell(x - 1, y, x, y, cellWidth, cellHeight, width, height) ? heights[i - 1] : 0.0f;
                var right = SameCell(x + 1, y, x, y, cellWidth, cellHeight, width, height) ? heights[i + 1] : 0.0f;
                var up = SameCell(x, y - 1, x, y, cellWidth, cellHeight, width, height) ? heights[i - width] : 0.0f;
                var down = SameCell(x, y + 1, x, y, cellWidth, cellHeight, width, height) ? heights[i + width] : 0.0f;

                // Screen space: x to the right, y down. The surface faces away from its uphill side.
                var nx = -(right - left) * 0.5f * Strength;
                var ny = -(down - up) * 0.5f * Strength;
                var length = MathF.Sqrt(nx * nx + ny * ny + 1.0f);
                Write(normals, i, nx / length, ny / length, 1.0f / length);
            }
        }

        return normals;
    }

    /// <summary>0 for transparent pixels, rising along a quarter circle to 1 inside the body.</summary>
    private static float[] Heights(byte[] rgba, int width, int height, int cellWidth, int cellHeight)
    {
        var distance = new float[width * height];
        var far = RoundingPixels + 2.0f;
        for (var i = 0; i < distance.Length; i++)
            distance[i] = rgba[i * 4 + 3] > 127 ? far : 0.0f;

        // Two-pass chamfer distance to the nearest empty pixel, where the frame edge counts as empty.
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                Relax(distance, x, y, width, height, cellWidth, cellHeight, forward: true);
        }
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = width - 1; x >= 0; x--)
                Relax(distance, x, y, width, height, cellWidth, cellHeight, forward: false);
        }

        var heights = new float[distance.Length];
        for (var i = 0; i < heights.Length; i++)
        {
            if (distance[i] <= 0.0f)
                continue;

            var inward = 1.0f - Math.Clamp(distance[i] / RoundingPixels, 0.0f, 1.0f);
            heights[i] = MathF.Sqrt(1.0f - inward * inward);
        }

        return heights;
    }

    private static void Relax(float[] distance, int x, int y, int width, int height, int cellWidth, int cellHeight, bool forward)
    {
        var i = y * width + x;
        if (distance[i] <= 0.0f)
            return;

        var step = forward ? -1 : 1;
        var best = distance[i];
        best = MathF.Min(best, Neighbour(distance, x + step, y, x, y, width, height, cellWidth, cellHeight) + 1.0f);
        best = MathF.Min(best, Neighbour(distance, x, y + step, x, y, width, height, cellWidth, cellHeight) + 1.0f);
        best = MathF.Min(best, Neighbour(distance, x + step, y + step, x, y, width, height, cellWidth, cellHeight) + Diagonal);
        best = MathF.Min(best, Neighbour(distance, x - step, y + step, x, y, width, height, cellWidth, cellHeight) + Diagonal);
        distance[i] = best;
    }

    private static float Neighbour(float[] distance, int nx, int ny, int x, int y, int width, int height, int cellWidth, int cellHeight)
        => SameCell(nx, ny, x, y, cellWidth, cellHeight, width, height) ? distance[ny * width + nx] : 0.0f;

    private static bool SameCell(int nx, int ny, int x, int y, int cellWidth, int cellHeight, int width, int height)
        => nx >= 0 && ny >= 0 && nx < width && ny < height &&
           nx / cellWidth == x / cellWidth && ny / cellHeight == y / cellHeight;

    /// <summary>Encodes a screen-space normal (y down) as OpenGL-style RGB (green up).</summary>
    private static void Write(byte[] normals, int index, float nx, float ny, float nz)
    {
        normals[index * 4] = Encode(nx);
        normals[index * 4 + 1] = Encode(-ny);
        normals[index * 4 + 2] = Encode(nz);
        normals[index * 4 + 3] = 255;
    }

    private static byte Encode(float component)
        => (byte)Math.Clamp((int)MathF.Round((component * 0.5f + 0.5f) * 255.0f), 0, 255);
}
