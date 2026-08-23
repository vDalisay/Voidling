using Godot;

namespace VoidlingGame;

public partial class HaloBadge : Control
{
    public bool ShowAngel { get; set; }
    public int SparkleCount { get; set; }

    public override void _Process(double delta)
    {
        if (SparkleCount > 0)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (ShowAngel)
            DrawHalo();

        if (SparkleCount > 0)
            DrawSparkles();
    }

    private void DrawHalo()
    {
        var center = new Vector2(Size.X * 0.5f, Mathf.Max(5.0f, Size.Y * 0.14f));
        var radiusX = Mathf.Clamp(Size.X * 0.18f, 4.5f, 14.0f);
        var radiusY = Mathf.Clamp(radiusX * 0.28f, 1.4f, 4.2f);
        const int points = 32;

        var ellipse = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            ellipse[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        var back = Color.FromHtml("#B98C32");
        var gold = Color.FromHtml("#F1CE55");
        var shine = Color.FromHtml("#FFF2A8");

        for (var i = points / 2; i < points; i++)
            DrawLine(ellipse[i], ellipse[(i + 1) % points], back, 1.8f, true);

        // The lower/front half is brighter, making the halo read as a tilted ellipse
        // facing the viewer rather than a flat ring above the character.
        for (var i = 0; i < points / 2; i++)
            DrawLine(ellipse[i], ellipse[i + 1], gold, 2.5f, true);

        for (var i = 2; i < 7; i++)
            DrawLine(ellipse[i], ellipse[i + 1], shine, 1.05f, true);
    }

    private void DrawSparkles()
    {
        var time = (float)Time.GetTicksMsec() / 420.0f;
        var count = Mathf.Clamp(SparkleCount + 1, 2, 4);
        for (var i = 0; i < count; i++)
        {
            var angle = time + i * Mathf.Tau / count;
            var radiusX = Mathf.Max(10.0f, Size.X * 0.25f);
            var radiusY = Mathf.Max(8.0f, Size.Y * 0.19f);
            var p = new Vector2(Size.X * 0.5f, Size.Y * 0.48f) +
                    new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            var pulse = 1.0f + Mathf.Sin(time * 2.2f + i) * 0.25f;
            DrawCircle(p, 1.2f * pulse, Color.FromHtml("#FFF7B7"));
        }
    }
}
