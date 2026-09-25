using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// Premium sprites the Garden's water, weather and wildlife are built from, sliced once into
/// standalone textures (an AtlasTexture region cannot repeat or be UV-mapped on its own), plus the
/// few small pixel textures the effects generate themselves.
/// </summary>
internal static class GardenAtmosphereAssets
{
    internal const string ShaderRoot = "res://Resources/Presentation/Garden/";
    internal const string Premium = "res://Assets/Sprout Lands - Sprites - premium pack/";
    internal const string EarlyAccess = "res://Assets/Sprout Sorry pack/Early Access/";

    private static readonly Dictionary<string, Image> SheetCache = new(StringComparer.Ordinal);

    internal static Texture2D Load(string path)
        => GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Garden art '{path}' is missing.");

    /// <summary>One region of a sheet as its own texture.</summary>
    internal static Texture2D Slice(string path, int x, int y, int width, int height)
        => ImageTexture.CreateFromImage(Sheet(path).GetRegion(new Rect2I(x, y, width, height)));

    /// <summary>A horizontal strip of equal frames from a sheet, as separate textures.</summary>
    internal static Texture2D[] Strip(string path, int x, int y, int frameWidth, int frameHeight, int count)
    {
        var frames = new Texture2D[count];
        for (var i = 0; i < count; i++)
            frames[i] = Slice(path, x + i * frameWidth, y, frameWidth, frameHeight);
        return frames;
    }

    internal static SpriteFrames Frames(string animation, IReadOnlyList<Texture2D> textures, double fps, bool loop = true)
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        frames.AddAnimation(animation);
        frames.SetAnimationSpeed(animation, fps);
        frames.SetAnimationLoop(animation, loop);
        foreach (var texture in textures)
            frames.AddFrame(animation, texture);
        return frames;
    }

    internal static Image Sheet(string path)
    {
        if (SheetCache.TryGetValue(path, out var image))
            return image;

        image = Load(path).GetImage()
            ?? throw new InvalidOperationException($"Garden art '{path}' has no readable image.");
        if (image.IsCompressed())
            image.Decompress();
        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);
        SheetCache[path] = image;
        return image;
    }

    /// <summary>Tiny textures drawn from a pattern of 'X' (white) and '.' (clear) rows.</summary>
    internal static Texture2D Pixels(params string[] rows)
    {
        var image = Image.CreateEmpty(rows[0].Length, rows.Length, false, Image.Format.Rgba8);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var alpha = rows[y][x] switch
                {
                    'X' => 1.0f,
                    'x' => 0.55f,
                    ':' => 0.25f,
                    _ => 0.0f
                };
                image.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// A round light falloff stepped into a few flat bands with a dithered edge between them, so a
    /// light pool reads as pixel art rather than a smooth airbrush.
    /// </summary>
    internal static Texture2D BandedLight(int size, int bands)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var center = (size - 1) * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / (size * 0.5f);
                var falloff = Mathf.Clamp(1.0f - distance, 0.0f, 1.0f);
                falloff *= falloff;
                var level = falloff * bands;
                var step = Mathf.Floor(level);
                // Ordered dither across each band edge.
                var threshold = ((x & 1) * 2 + (y & 1)) / 4.0f + 0.125f;
                if (level - step > threshold)
                    step += 1.0f;
                var value = Mathf.Clamp(step / bands, 0.0f, 1.0f);
                image.SetPixel(x, y, new Color(value, value, value, value));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
