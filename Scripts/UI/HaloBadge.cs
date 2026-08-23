using Godot;

namespace VoidlingGame;

public partial class HaloBadge : Control
{
    public override void _Draw()
    {
        var center = new Vector2(Size.X * 0.5f, Mathf.Max(5.0f, Size.Y * 0.14f));
        var radiusX = Mathf.Clamp(Size.X * 0.17f, 4.5f, 13.5f);
        var radiusY = Mathf.Clamp(radiusX * 0.30f, 1.4f, 4.0f);
        const int points = 32;

        var ellipse = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            ellipse[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        var back = Color.FromHtml("#C99B37");
        var gold = Color.FromHtml("#F1CE55");
        var shine = Color.FromHtml("#FFF2A8");

        for (var i = 0; i < points; i++)
            DrawLine(ellipse[i], ellipse[(i + 1) % points], back, 2.0f, true);

        // Bottom half is the front rim of the tilted halo.
        for (var i = 0; i < points / 2; i++)
            DrawLine(ellipse[i], ellipse[i + 1], gold, 2.5f, true);

        for (var i = 2; i < 8; i++)
            DrawLine(ellipse[i], ellipse[i + 1], shine, 1.15f, true);
    }
}
