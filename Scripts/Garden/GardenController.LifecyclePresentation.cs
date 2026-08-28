using System;
using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    private bool _lifecyclePresentationInstalled;

    private void InstallLifecyclePresentation()
    {
        if (_lifecyclePresentationInstalled ||
            _session == null ||
            !GodotObject.IsInstanceValid(_session))
        {
            return;
        }

        _lifecyclePresentationInstalled = true;
        _session.LifecycleCocoonRequested += OnLifecycleCocoonRequested;
        TreeExiting += DetachLifecyclePresentation;
    }

    private void DetachLifecyclePresentation()
    {
        if (!_lifecyclePresentationInstalled)
            return;

        _lifecyclePresentationInstalled = false;
        if (_session != null && GodotObject.IsInstanceValid(_session))
            _session.LifecycleCocoonRequested -= OnLifecycleCocoonRequested;
        TreeExiting -= DetachLifecyclePresentation;
    }

    private void OnLifecycleCocoonRequested(string creatureId, bool willReincarnate)
    {
        var position = _actors.TryGetValue(creatureId, out var actor) && GodotObject.IsInstanceValid(actor)
            ? ToLocal(actor.GlobalPosition)
            : new Vector2(416, 240);
        SpawnLifecycleCocoon(position, willReincarnate);
    }

    private void SpawnLifecycleCocoon(Vector2 position, bool willReincarnate)
    {
        var holder = new Node2D
        {
            Position = position + new Vector2(0, -8),
            Scale = new Vector2(0.55f, 0.55f),
            ZIndex = 95
        };
        AddChild(holder);

        var outer = new Polygon2D
        {
            Polygon = CreateCocoonPolygon(13.0f, 20.0f),
            Color = Color.FromHtml(willReincarnate ? "#D9F0C8" : "#A99FB4")
        };
        holder.AddChild(outer);

        var inner = new Polygon2D
        {
            Polygon = CreateCocoonPolygon(8.0f, 16.0f),
            Color = Color.FromHtml(willReincarnate ? "#F5F4C7" : "#706A7D")
        };
        holder.AddChild(inner);

        var symbol = UiFactory.CreateLabel(willReincarnate ? "↻" : "·", 10);
        symbol.Position = new Vector2(-4, -7);
        symbol.AddThemeColorOverride(
            "font_color",
            Color.FromHtml(willReincarnate ? "#6F8E62" : "#E7E1EA"));
        holder.AddChild(symbol);

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(holder, "scale", Vector2.One, 0.34)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(holder, "position", holder.Position + new Vector2(0, -5), 1.05)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(holder, "modulate:a", 0.0f, 0.32).SetDelay(0.82);
        tween.Finished += holder.QueueFree;
    }

    private static Vector2[] CreateCocoonPolygon(float radiusX, float radiusY)
    {
        const int pointCount = 18;
        var points = new Vector2[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var angle = Mathf.Tau * i / pointCount;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        return points;
    }
}
