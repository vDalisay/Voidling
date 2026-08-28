using System;
using Godot;

namespace Voidling.Presentation.Garden;

/// <summary>
/// Presentation-only Garden ambience driven by the computer's local clock. The overlay is attached
/// to the Garden world so the normal UI CanvasLayer remains untinted, and it never affects gameplay.
/// </summary>
public partial class GardenAmbienceController : Node
{
    private const double RefreshIntervalSeconds = 30.0;

    private ColorRect? _timeOverlay;
    private double _refreshAccumulator;

    public override void _Ready()
    {
        var garden = GetNode<Node2D>("../Garden");
        _timeOverlay = new ColorRect
        {
            Position = new Vector2(-600.0f, -500.0f),
            Size = new Vector2(2200.0f, 1400.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 95
        };
        garden.AddChild(_timeOverlay);
        ApplyLocalTime(DateTime.Now);
    }

    public override void _Process(double delta)
    {
        _refreshAccumulator += Math.Max(0.0, delta);
        if (_refreshAccumulator < RefreshIntervalSeconds)
            return;

        _refreshAccumulator = 0.0;
        ApplyLocalTime(DateTime.Now);
    }

    private void ApplyLocalTime(DateTime localNow)
    {
        if (_timeOverlay == null || !GodotObject.IsInstanceValid(_timeOverlay))
            return;

        var alpha = GardenLocalTimeAmbience.NightOverlayAlpha(localNow.TimeOfDay);
        _timeOverlay.Color = new Color(0.08f, 0.12f, 0.24f, alpha);
    }
}
