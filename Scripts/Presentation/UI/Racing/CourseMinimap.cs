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

        // Sky over ground rather than a bare strip, so the miniature reads as the side-on course
        // the player is about to run instead of a progress bar.
        var trackTop = height * 0.46f;
        var trackHeight = Mathf.Max(7.0f, height * 0.30f);
        DrawRect(new Rect2(0, 0, width, trackTop), Color.FromHtml("#BFE0EC"));
        DrawRect(new Rect2(0, trackTop + trackHeight, width, height - trackTop - trackHeight), Color.FromHtml("#D8C8A4"));

        foreach (var segment in _segments)
        {
            var left = Fraction(segment.StartX) * width;
            var span = Mathf.Max(1.0f, Fraction(segment.EndX) * width - left);
            var color = KindColors.TryGetValue(segment.Kind, out var kindColor) ? kindColor : KindColors["Ground"];

            // The ground runs unbroken beneath the whole course. A climb rises out of it and a glide
            // floats over it, so the strip shows elevation instead of a flat colour sequence.
            var groundColor = segment.Kind == "Glide" ? KindColors["Ground"] : color;
            var top = segment.Kind == "Climb" ? trackTop - trackHeight * 0.55f : trackTop;
            var tall = segment.Kind == "Climb" ? trackHeight * 1.55f : trackHeight;
            DrawRect(new Rect2(left, top, span, tall), groundColor);
            DrawRect(new Rect2(left, top, span, 2.0f), groundColor.Darkened(0.28f));

            if (segment.Kind != "Glide") continue;
            var glideTop = trackTop - trackHeight * 1.25f;
            DrawRect(new Rect2(left, glideTop, span, trackHeight * 0.85f), color);
            DrawRect(new Rect2(left, glideTop, span, 2.0f), color.Darkened(0.28f));
        }

        foreach (var obstacle in _obstacles)
        {
            var x = Fraction(obstacle) * width;
            DrawRect(new Rect2(x - 1.0f, trackTop - 5.0f, 2.0f, 5.0f), Color.FromHtml("#6B573F"));
            DrawRect(new Rect2(x - 2.5f, trackTop - 7.0f, 5.0f, 2.0f), Color.FromHtml("#8A6A4B"));
        }

        DrawPost(2.0f, trackTop, trackHeight, Color.FromHtml("#5B8F5A"));
        DrawFinish(width - 6.0f, trackTop, trackHeight);
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
