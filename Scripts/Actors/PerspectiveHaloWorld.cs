using Godot;

namespace VoidlingGame;

public partial class PerspectiveHaloWorld : Node2D
{
    public float RadiusX { get; set; } = 9.0f;
    public float RadiusY { get; set; } = 2.6f;

    public override void _Draw()
    {
        const int points = 32;
        var ellipse = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            ellipse[i] = new Vector2(Mathf.Cos(angle) * RadiusX, Mathf.Sin(angle) * RadiusY);
        }

        var back = Color.FromHtml("#C99B37");
        var gold = Color.FromHtml("#F1CE55");
        var shine = Color.FromHtml("#FFF2A8");

        // Darker complete rim gives the ellipse depth and keeps it readable over any tint.
        for (var i = 0; i < points; i++)
            DrawLine(ellipse[i], ellipse[(i + 1) % points], back, 2.0f, true);

        // The lower half is the front edge from the viewer's perspective, so it gets
        // the brighter, thicker treatment rather than looking like a flat circle.
        for (var i = 0; i < points / 2; i++)
            DrawLine(ellipse[i], ellipse[i + 1], gold, 2.6f, true);

        for (var i = 2; i < 8; i++)
            DrawLine(ellipse[i], ellipse[i + 1], shine, 1.25f, true);
    }
}
