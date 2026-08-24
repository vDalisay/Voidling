using System;
using System.Linq;
using Godot;

namespace VoidlingGame;

/// <summary>
/// World-space mutation presentation that can follow any challenge sprite.
/// Keeping this independent from a specific minigame makes mutation visuals consistent
/// whenever an owned Voidling is rendered outside the garden.
/// </summary>
public partial class MutationAdornment2D : Node2D
{
    private AnimatedSprite2D? _target;
    private bool _showAngel;
    private int _sparkleCount;

    public void Setup(VoidlingData data, AnimatedSprite2D target)
    {
        var showAngel = GameRules.HasMutation(data, GameRules.AngelMutationId);
        var sparkleCount = data.RareTraits.Count(t =>
            !string.Equals(t.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase));
        Setup(showAngel, sparkleCount, target);
    }

    public void Setup(bool showAngel, int sparkleCount, AnimatedSprite2D target)
    {
        _target = target;
        _showAngel = showAngel;
        _sparkleCount = Math.Max(0, sparkleCount);
        ZIndex = target.ZIndex + 8;
        Position = target.Position;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target))
        {
            QueueFree();
            return;
        }

        Position = _target.Position;
        if (_sparkleCount > 0)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (_showAngel)
            DrawPerspectiveHalo();

        if (_sparkleCount > 0)
            DrawTraitSparkles();
    }

    private void DrawPerspectiveHalo()
    {
        var center = new Vector2(0, -25.0f);
        const float rx = 10.0f;
        const float ry = 3.0f;
        const int points = 32;
        var ellipse = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            ellipse[i] = center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
        }

        for (var i = points / 2; i < points; i++)
            DrawLine(ellipse[i], ellipse[(i + 1) % points], Color.FromHtml("#B98C32"), 1.7f, true);
        for (var i = 0; i < points / 2; i++)
            DrawLine(ellipse[i], ellipse[i + 1], Color.FromHtml("#F1CE55"), 2.2f, true);
        for (var i = 2; i < 7; i++)
            DrawLine(ellipse[i], ellipse[i + 1], Color.FromHtml("#FFF2A8"), 1.0f, true);
    }

    private void DrawTraitSparkles()
    {
        var t = (float)Time.GetTicksMsec() / 340.0f;
        var count = Mathf.Clamp(_sparkleCount + 1, 2, 4);
        for (var i = 0; i < count; i++)
        {
            var angle = t + i * Mathf.Tau / count;
            var p = new Vector2(Mathf.Cos(angle) * 15.0f, -8.0f + Mathf.Sin(angle) * 11.0f);
            var pulse = 1.0f + Mathf.Sin(t * 2.0f + i) * 0.25f;
            DrawCircle(p, 1.25f * pulse, Color.FromHtml("#FFF7B7"));
        }
    }
}
