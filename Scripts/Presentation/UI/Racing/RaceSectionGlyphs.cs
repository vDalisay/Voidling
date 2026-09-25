using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Racing;

/// <summary>
/// Small pixel glyphs for the four kinds of race stretch (run, swim, climb, fly). The premium icon
/// sheet has no swimming or climbing icon, so these are drawn here at the pack's pixel scale and
/// shared by the course select screen and the signposts on the track, so a stretch looks the same
/// on the board as on the course.
/// </summary>
public static class RaceSectionGlyphs
{
    private static readonly Dictionary<string, string[]> Patterns = new(StringComparer.Ordinal)
    {
        ["Ground"] = new[]
        {
            ".........",
            ".X...X...",
            "..X...X..",
            "...X...X.",
            "....X...X",
            "...X...X.",
            "..X...X..",
            ".X...X...",
            "........."
        },
        ["Swim"] = new[]
        {
            ".........",
            ".XX...XX.",
            "X..X.X..X",
            "....X....",
            ".........",
            ".XX...XX.",
            "X..X.X..X",
            "....X....",
            "........."
        },
        ["Climb"] = new[]
        {
            "....X....",
            "...XXX...",
            "..X.X.X..",
            "....X....",
            ".........",
            "...X.....",
            "..XXX..X.",
            ".XXXXXXXX",
            "XXXXXXXXX"
        },
        ["Glide"] = new[]
        {
            ".........",
            "X........",
            "XX.......",
            "XXX......",
            ".XXXX....",
            "..XXXXXX.",
            "...XXXXXX",
            "....XX...",
            "........."
        }
    };

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    /// <summary>The glyph for a segment kind name ("Ground", "Swim", "Climb", "Glide").</summary>
    public static Texture2D For(string kind, Color ink)
    {
        var key = $"{kind}:{ink.ToHtml()}";
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var pattern = Patterns.TryGetValue(kind, out var found) ? found : Patterns["Ground"];
        var image = Image.CreateEmpty(pattern[0].Length, pattern.Length, false, Image.Format.Rgba8);
        for (var y = 0; y < pattern.Length; y++)
        {
            for (var x = 0; x < pattern[y].Length; x++)
                image.SetPixel(x, y, pattern[y][x] == 'X' ? ink : Colors.Transparent);
        }

        var texture = ImageTexture.CreateFromImage(image);
        Cache[key] = texture;
        return texture;
    }
}
