using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class HaloBadge : Control
{
    public bool ShowAngel { get; set; }
    public int SparkleCount { get; set; }
    public float NominalSpritePixels { get; set; } = 48.0f;

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
        var halo = VoidlingMutationVisualMetrics.ForPortrait(NominalSpritePixels, Size);
        var ellipse = VoidlingMutationVisualMetrics.BuildEllipse(halo);
        var half = ellipse.Length / 2;

        for (var i = half; i < ellipse.Length; i++)
            DrawLine(ellipse[i], ellipse[(i + 1) % ellipse.Length], Color.FromHtml("#B98C32"), halo.BackWidth, true);
        for (var i = 0; i < half; i++)
            DrawLine(ellipse[i], ellipse[i + 1], Color.FromHtml("#F1CE55"), halo.FrontWidth, true);
        for (var i = 2; i < 7; i++)
            DrawLine(ellipse[i], ellipse[i + 1], Color.FromHtml("#FFF2A8"), halo.ShineWidth, true);
    }

    private void DrawSparkles()
    {
        var spriteScale = Mathf.Max(0.20f, NominalSpritePixels / 48.0f);
        var ratio = Mathf.Max(0.25f, spriteScale / 0.62f);
        var time = (float)Time.GetTicksMsec() / 420.0f;
        var count = Mathf.Clamp(SparkleCount + 1, 2, 4);
        for (var i = 0; i < count; i++)
        {
            var angle = time + i * Mathf.Tau / count;
            var p = new Vector2(Size.X * 0.5f, Size.Y * 0.5f - 5.0f * ratio) +
                    new Vector2(Mathf.Cos(angle) * 15.0f * ratio, Mathf.Sin(angle) * 11.0f * ratio);
            var pulse = 1.0f + Mathf.Sin(time * 2.2f + i) * 0.25f;
            DrawCircle(p, 1.2f * ratio * pulse, Color.FromHtml("#FFF7B7"));
        }
    }
}
