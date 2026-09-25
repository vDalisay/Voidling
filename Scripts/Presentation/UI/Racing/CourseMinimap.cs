using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Racing;

public readonly record struct CourseMinimapSegment(string Kind, float StartX, float EndX);

/// <summary>
/// Miniature of an authored course drawn from its own segments and obstacles, so a course that
/// gains or loses a stretch cannot advertise a stale shape. Presentation only: the strip reads the
/// same semantic segment kinds the live track and the section labels use.
/// </summary>
public partial class CourseMinimap : Control
{
    private static readonly Dictionary<string, Color> KindColors = new(StringComparer.Ordinal)
    {
        ["Ground"] = Color.FromHtml("#8FC57E"),
        ["Swim"] = Color.FromHtml("#6BA8D8"),
        ["Climb"] = Color.FromHtml("#BE8F5F"),
        ["Glide"] = Color.FromHtml("#C9B4E6")
    };

    /// <summary>One colour per segment kind, shared with the live in-race course strip.</summary>
    public static Color ColorForKind(string kind)
        => KindColors.TryGetValue(kind, out var color) ? color : KindColors["Ground"];

    private IReadOnlyList<CourseMinimapSegment> _segments = Array.Empty<CourseMinimapSegment>();
    private IReadOnlyList<float> _obstacles = Array.Empty<float>();
    private float _startX;
    private float _endX = 1.0f;

    public void SetCourse(
        float startX,
        float endX,
        IReadOnlyList<CourseMinimapSegment> segments,
        IReadOnlyList<float> obstacles)
    {
        _startX = startX;
        _endX = Mathf.Max(endX, startX + 1.0f);
        _segments = segments ?? Array.Empty<CourseMinimapSegment>();
        _obstacles = obstacles ?? Array.Empty<float>();
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        var width = Size.X;
        var height = Size.Y;
        if (width <= 1.0f || height <= 1.0f) return;

        // A side-on elevation profile: sky, then the course's own silhouette. The ground runs along
        // a baseline; a climb lifts it onto a clifftop that holds until the next water, rivers dip
        // below it, and a glide leaves a dotted flight line over its lake. Section glyphs float
        // over each stretch, the same glyphs the signposts on the real track carry.
        DrawRect(new Rect2(0, 0, width, height), Color.FromHtml("#CFE8EE"));
        DrawRect(new Rect2(0, height * 0.55f, width, height * 0.45f), Color.FromHtml("#E4EFD6"));

        var baseline = height * 0.72f;
        var cliff = Mathf.Max(5.0f, height * 0.24f);
        var raised = false;
        foreach (var segment in _segments)
        {
            var left = Fraction(segment.StartX) * width;
            var right = Mathf.Max(left + 1.0f, Fraction(segment.EndX) * width);
            var color = ColorForKind(segment.Kind);

            if (segment.Kind == "Climb")
                raised = true;
            else if (segment.Kind is "Swim" or "Glide")
                raised = false;

            if (segment.Kind is "Swim" or "Glide")
            {
                var water = segment.Kind == "Glide" ? KindColors["Swim"].Lightened(0.15f) : KindColors["Swim"];
                DrawRect(new Rect2(left, baseline + 2.0f, right - left, height - baseline - 2.0f), water);
                for (var x = left + 2.0f; x < right - 3.0f; x += 6.0f)
                    DrawRect(new Rect2(x, baseline + 4.0f, 3.0f, 1.0f), new Color(1, 1, 1, 0.7f));
            }
            else
            {
                var top = segment.Kind == "Climb" ? baseline - cliff : raised ? baseline - cliff : baseline;
                if (segment.Kind == "Climb")
                {
                    // The wall itself, then the clifftop.
                    var wall = Mathf.Min(4.0f, (right - left) * 0.4f);
                    DrawColoredPolygon(new[]
                    {
                        new Vector2(left, height), new Vector2(left, baseline),
                        new Vector2(left + wall, top), new Vector2(right, top), new Vector2(right, height)
                    }, color.Darkened(0.1f));
                    DrawRect(new Rect2(left + wall, top, right - left - wall, 2.0f), KindColors["Ground"].Darkened(0.2f));
                }
                else
                {
                    DrawRect(new Rect2(left, top, right - left, height - top), raised ? KindColors["Climb"] : color);
                    DrawRect(new Rect2(left, top, right - left, 2.0f), KindColors["Ground"].Darkened(0.25f));
                }
            }

            if (segment.Kind == "Glide")
            {
                // The flight: a dotted arc from the lip down to the far shore.
                var from = new Vector2(left, baseline - cliff - 4.0f);
                var to = new Vector2(right, baseline - 1.0f);
                for (var t = 0.0f; t <= 1.0f; t += 0.08f)
                {
                    var point = from.Lerp(to, t) + new Vector2(0.0f, -Mathf.Sin(t * Mathf.Pi) * cliff * 0.6f + t * t * 2.0f);
                    DrawRect(new Rect2(point.X - 1.0f, point.Y - 1.0f, 2.0f, 2.0f), color.Darkened(0.35f));
                }
            }

            if (segment.Kind != "Ground" && right - left > 12.0f)
            {
                var glyph = RaceSectionGlyphs.For(segment.Kind, Color.FromHtml("#4A3A2C"));
                var size = Mathf.Clamp(height * 0.34f, 9.0f, 18.0f);
                var center = (left + right) * 0.5f;
                var chipTop = Mathf.Max(1.0f, baseline - cliff - size - 6.0f);
                DrawRect(new Rect2(center - size * 0.5f - 2.0f, chipTop - 2.0f, size + 4.0f, size + 4.0f), color);
                DrawRect(new Rect2(center - size * 0.5f - 2.0f, chipTop + size + 1.0f, size + 4.0f, 1.0f), color.Darkened(0.3f));
                DrawTextureRect(glyph, new Rect2(center - size * 0.5f, chipTop, size, size), false);
            }
        }

        foreach (var obstacle in _obstacles)
        {
            var x = Fraction(obstacle) * width;
            var top = SurfaceAt(obstacle, baseline, cliff);
            DrawRect(new Rect2(x - 1.0f, top - 5.0f, 2.0f, 5.0f), Color.FromHtml("#6B573F"));
            DrawRect(new Rect2(x - 2.5f, top - 6.0f, 5.0f, 2.0f), Color.FromHtml("#8A6A4B"));
        }

        DrawPost(2.0f, baseline, cliff * 0.9f, Color.FromHtml("#5B8F5A"));
        DrawFinish(width - 6.0f, baseline, cliff * 0.9f);
    }

    /// <summary>Profile height at a course X, so hurdles on a clifftop stand on it.</summary>
    private float SurfaceAt(float x, float baseline, float cliff)
    {
        var raised = false;
        foreach (var segment in _segments)
        {
            if (segment.Kind == "Climb") raised = true;
            else if (segment.Kind is "Swim" or "Glide") raised = false;
            if (x >= segment.StartX && x < segment.EndX)
                return raised ? baseline - cliff : baseline;
        }
        return baseline;
    }

    // A checkered flag closes the strip, the way the finish reads on the real track.
    private void DrawFinish(float x, float trackTop, float trackHeight)
    {
        var top = trackTop - trackHeight * 1.1f;
        DrawRect(new Rect2(x, top, 2.0f, trackHeight * 2.1f), Color.FromHtml("#5B4C40"));
        const float cell = 3.0f;
        for (var rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < 2; columnIndex++)
            {
                var dark = (rowIndex + columnIndex) % 2 == 0;
                DrawRect(
                    new Rect2(x + 2.0f + columnIndex * cell, top + rowIndex * cell, cell, cell),
                    dark ? Color.FromHtml("#3B322A") : Color.FromHtml("#F4EEDC"));
            }
        }
    }

    private void DrawPost(float x, float trackTop, float trackHeight, Color color)
    {
        DrawRect(new Rect2(x, trackTop - trackHeight * 1.1f, 2.0f, trackHeight * 2.1f), color.Darkened(0.35f));
        DrawRect(new Rect2(x + 2.0f, trackTop - trackHeight * 1.1f, 7.0f, 5.0f), color);
    }

    private float Fraction(float x) => Mathf.Clamp((x - _startX) / (_endX - _startX), 0.0f, 1.0f);
}
