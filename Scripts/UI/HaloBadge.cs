using Godot;

namespace VoidlingGame;

public partial class HaloBadge : Control
{
    public override void _Draw()
    {
        var center = new Vector2(Size.X * 0.5f, Mathf.Max(4.0f, Size.Y * 0.13f));
        var radius = Mathf.Clamp(Size.X * 0.16f, 4.0f, 13.0f);
        DrawArc(center, radius, 0.0f, Mathf.Tau, 28, Color.FromHtml("#F4D35E"), 2.0f, true);
        DrawArc(center + new Vector2(0, 1), radius * 0.75f, 0.0f, Mathf.Tau, 24, Color.FromHtml("#FFF4AE"), 1.0f, true);
    }
}
