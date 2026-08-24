using System;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

/// <summary>
/// Canonical world-space mutation renderer used by the garden and challenge screens.
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
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_target == null)
            return;

        var spriteScale = Mathf.Abs(_target.Scale.X);
        if (_showAngel)
            DrawPixelHalo(VoidlingMutationVisualMetrics.ForSpriteTarget(spriteScale));

        if (_sparkleCount > 0)
            DrawTraitSparkles(spriteScale);
    }

    private void DrawPixelHalo(AngelHaloVisual halo)
    {
        foreach (var pixel in VoidlingMutationVisualMetrics.BuildPixels(halo))
            DrawRect(pixel.Rect, VoidlingMutationVisualMetrics.ColorFor(pixel.Tone));
    }

    private void DrawTraitSparkles(float spriteScale)
    {
        var ratio = Mathf.Max(0.25f, spriteScale / 0.62f);
        var t = (float)Time.GetTicksMsec() / 340.0f;
        var count = Mathf.Clamp(_sparkleCount + 1, 2, 4);
        for (var i = 0; i < count; i++)
        {
            var angle = t + i * Mathf.Tau / count;
            var p = new Vector2(
                Mathf.Cos(angle) * 15.0f * ratio,
                -8.0f * ratio + Mathf.Sin(angle) * 11.0f * ratio);
            var pulse = 1.0f + Mathf.Sin(t * 2.0f + i) * 0.25f;
            DrawCircle(p, 1.25f * ratio * pulse, Color.FromHtml("#FFF7B7"));
        }
    }
}
