using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public sealed class RaceMiniMapPoint
{
    public string Id { get; init; } = "";
    public Color Color { get; init; } = Colors.White;
    public float Progress { get; init; }
    public bool IsPlayer { get; init; }
}

public partial class RaceMiniMap : Control
{
    private IReadOnlyList<RaceMiniMapPoint> _points = new List<RaceMiniMapPoint>();

    public void SetPoints(IReadOnlyList<RaceMiniMapPoint> points)
    {
        _points = points;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(0.18f, 0.22f, 0.20f, 0.78f));

        var left = 12.0f;
        var right = Size.X - 12.0f;
        var centerY = Size.Y * 0.52f;

        DrawLine(new Vector2(left, centerY), new Vector2(right, centerY), Color.FromHtml("#E9D59B"), 9.0f);
        DrawLine(new Vector2(left, centerY), new Vector2(right, centerY), Color.FromHtml("#B76545"), 5.0f);

        DrawLine(new Vector2(left, centerY - 8), new Vector2(left, centerY + 8), Colors.White, 2.0f);
        DrawLine(new Vector2(right, centerY - 8), new Vector2(right, centerY + 8), Colors.White, 2.0f);

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

    private static void DrawPoint(RaceMiniMapPoint point, float left, float right, float centerY)
    {
        var x = Mathf.Lerp(left, right, Mathf.Clamp(point.Progress, 0.0f, 1.0f));
        DrawCircle(new Vector2(x, centerY), 3.5f, point.Color);
    }
}
