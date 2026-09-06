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

        var trackTop = height * 0.34f;
        var trackHeight = Mathf.Max(6.0f, height * 0.32f);
        DrawRect(new Rect2(0, trackTop - 2.0f, width, trackHeight + 4.0f), Color.FromHtml("#E4D6B4"));

        foreach (var segment in _segments)
        {
            var left = Fraction(segment.StartX) * width;
            var right = Fraction(segment.EndX) * width;
            var color = KindColors.TryGetValue(segment.Kind, out var kindColor) ? kindColor : KindColors["Ground"];
            DrawRect(new Rect2(left, trackTop, Mathf.Max(1.0f, right - left), trackHeight), color);
        }

        foreach (var obstacle in _obstacles)
        {
            var x = Fraction(obstacle) * width;
            DrawRect(new Rect2(x - 1.0f, trackTop - 3.0f, 2.0f, trackHeight + 6.0f), Color.FromHtml("#6B573F"));
        }

        // Start and finish posts bracket the strip so the reading direction is unambiguous.
        DrawFlag(0.0f, trackTop, trackHeight, Color.FromHtml("#5B8F5A"));
        DrawFlag(width - 3.0f, trackTop, trackHeight, Color.FromHtml("#9C514B"));
    }

    private void DrawFlag(float x, float trackTop, float trackHeight, Color color)
    {
        DrawRect(new Rect2(x, trackTop - 8.0f, 3.0f, trackHeight + 16.0f), color);
    }

    private float Fraction(float x) => Mathf.Clamp((x - _startX) / (_endX - _startX), 0.0f, 1.0f);
}
