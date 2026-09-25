using System.Collections.Generic;
using Godot;
using Voidling.Presentation.UI.Common;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// The "yes!" of a confirmation: a one-frame flash ring and a pop of square pixels and pack stars
/// that arc out under gravity and fade. Every pixel is drawn on whole UI pixels, so the burst stays
/// as crisp as the art around it. It frees itself when done and never takes input.
/// </summary>
public partial class PixelBurst : Control
{
    private static readonly Color[] Leafy =
    {
        Color.FromHtml("#FFF6C8"), Color.FromHtml("#B9E07A"), Color.FromHtml("#6FB85A"), Color.FromHtml("#F6D36B")
    };
    private static readonly Color[] Rosy =
    {
        Color.FromHtml("#FFF1F0"), Color.FromHtml("#F4A3B4"), Color.FromHtml("#E4677F"), Color.FromHtml("#F6D36B")
    };
    private static readonly Rect2 TinyStar = new(3, 4, 10, 8);
    private static uint _seed = 17;

    private IReadOnlyList<BurstParticle> _particles = new List<BurstParticle>();
    private Color[] _palette = Leafy;
    private float _elapsed;
    private const float Duration = 0.8f;

    public enum Palette { Leafy, Rosy }

    public static void Spawn(Node context, Vector2 globalCenter, Palette palette = Palette.Leafy, int count = 18)
    {
        if (UiMotion.Reduced || !GodotObject.IsInstanceValid(context) || !context.IsInsideTree())
            return;
        _seed = _seed * 1664525u + 1013904223u;
        var burst = new PixelBurst
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Position = globalCenter.Round(),
            _palette = palette == Palette.Rosy ? Rosy : Leafy
        };
        burst._particles = UiMotionMath.Burst(count, _seed, 55.0f, 120.0f, burst._palette.Length);
        UiFxLayer.For(context).AddChild(burst);
    }

    public override void _Ready()
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(Advance), 0.0f, Duration, Duration);
        tween.Finished += QueueFree;
    }

    private void Advance(float seconds)
    {
        _elapsed = seconds;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // The flash: a hollow square that opens out over the first few frames.
        if (_elapsed < 0.12f)
        {
            var radius = Mathf.Round(4 + _elapsed * 110.0f);
            var ring = new Color(1.0f, 0.98f, 0.86f, 1.0f - _elapsed / 0.12f);
            DrawRect(new Rect2(-radius, -radius, radius * 2, radius * 2), ring, false, 2.0f);
        }

        foreach (var particle in _particles)
        {
            if (_elapsed > particle.Lifetime) continue;
            var (x, y) = UiMotionMath.BurstOffset(particle, _elapsed);
            var fade = Mathf.Clamp(1.0f - (_elapsed - particle.Lifetime * 0.6f) / (particle.Lifetime * 0.4f), 0.0f, 1.0f);
            if (particle.Sparkle)
            {
                DrawTextureRectRegion(UiSkin.Stars, new Rect2(x - 5, y - 4, 10, 8), TinyStar, new Color(1, 1, 1, fade));
                continue;
            }
            var color = _palette[particle.ColorIndex];
            color.A = fade;
            DrawRect(new Rect2(x, y, particle.Size, particle.Size), color);
        }
    }
}
