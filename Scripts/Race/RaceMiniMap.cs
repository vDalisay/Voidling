using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Racing;

namespace VoidlingGame;

public sealed class RaceMiniMapPoint
{
    public string Id { get; init; } = "";
    public Color Color { get; init; } = Colors.White;
    public float Progress { get; init; }
    public bool IsPlayer { get; init; }
}

/// <summary>
/// The in-race course strip: the authored segments in their own colours with every racer's dot on
/// them. Segment colours come from <see cref="CourseMinimap"/> so the live strip and the race-entry
/// miniature cannot drift apart.
/// </summary>
public partial class RaceMiniMap : Control
{
    private IReadOnlyList<RaceMiniMapPoint> _points = new List<RaceMiniMapPoint>();
    private IReadOnlyList<CourseMinimapSegment> _segments = Array.Empty<CourseMinimapSegment>();
    private float _startX;
    private float _endX = 1.0f;

    public void SetPoints(IReadOnlyList<RaceMiniMapPoint> points)
    {
        _points = points;
        QueueRedraw();
    }

    public void SetCourse(float startX, float endX, IReadOnlyList<CourseMinimapSegment> segments)
    {
        _startX = startX;
        _endX = Mathf.Max(endX, startX + 1.0f);
        _segments = segments ?? Array.Empty<CourseMinimapSegment>();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var left = 6.0f;
        var right = Mathf.Max(left + 1.0f, Size.X - 6.0f);
        var centerY = Size.Y * 0.5f;
        var trackTop = centerY - 5.0f;
        const float trackHeight = 10.0f;

        // The lane the segments sit in, so a course with a single stretch still reads as a track.
        DrawRect(
            new Rect2(left - 2.0f, trackTop - 2.0f, right - left + 4.0f, trackHeight + 4.0f),
            Color.FromHtml("#E4D6B4"));
        if (_segments.Count == 0)
        {
            DrawRect(new Rect2(left, trackTop, right - left, trackHeight), CourseMinimap.ColorForKind("Ground"));
        }
        else
        {
            foreach (var segment in _segments)
            {
                var segmentLeft = Mathf.Lerp(left, right, Fraction(segment.StartX));
                var segmentRight = Mathf.Lerp(left, right, Fraction(segment.EndX));
                DrawRect(
                    new Rect2(segmentLeft, trackTop, Mathf.Max(1.0f, segmentRight - segmentLeft), trackHeight),
                    CourseMinimap.ColorForKind(segment.Kind));
            }
        }

        DrawRect(new Rect2(left - 3.0f, trackTop - 5.0f, 3.0f, trackHeight + 10.0f), Color.FromHtml("#5B8F5A"));
        DrawRect(new Rect2(right, trackTop - 5.0f, 3.0f, trackHeight + 10.0f), Color.FromHtml("#9C514B"));

        foreach (var point in _points.Where(p => !p.IsPlayer))
            DrawPoint(point, left, right, centerY);

        // Player is always rendered last so its marker wins the z-order.
        var player = _points.FirstOrDefault(p => p.IsPlayer);
        if (player != null)
        {
            var x = Mathf.Lerp(left, right, Mathf.Clamp(player.Progress, 0.0f, 1.0f));
            DrawCircle(new Vector2(x, centerY), 5.2f, Colors.Black);
            DrawCircle(new Vector2(x, centerY), 3.5f, player.Color);
        }
    }

    private void DrawPoint(RaceMiniMapPoint point, float left, float right, float centerY)
    {
        var x = Mathf.Lerp(left, right, Mathf.Clamp(point.Progress, 0.0f, 1.0f));
        DrawCircle(new Vector2(x, centerY), 3.9f, new Color(0.12f, 0.14f, 0.12f, 0.85f));
        DrawCircle(new Vector2(x, centerY), 2.8f, point.Color);
    }

    private float Fraction(float x) => Mathf.Clamp((x - _startX) / (_endX - _startX), 0.0f, 1.0f);
}
