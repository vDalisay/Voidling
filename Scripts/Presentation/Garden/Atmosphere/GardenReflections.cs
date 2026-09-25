using System;
using System.Collections.Generic;
using Godot;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// Everything the sea reflects, rendered upside down into its own transparent buffer that follows the
/// Garden camera pixel for pixel. The sea shader reads the buffer back through its ripples, so the
/// reflections pick up the water's motion and tint, and land drawn over the sea hides any part of a
/// reflection that does not fall on water.
///
/// A reflection is only kept visible while the water below its owner is close enough to show it,
/// which keeps a crowd in the middle of the island from costing anything.
/// </summary>
public partial class GardenReflections : Node
{
    /// <summary>How far below a standing Voidling's feet the sea surface lies: the cliff height.</summary>
    public const float WaterDepth = GardenCoast.CliffHeight;

    private readonly Dictionary<string, (VoidlingReflection2D Reflection, Func<Vector2> Ground, float Height)> _voidlings =
        new(StringComparer.Ordinal);
    private readonly List<(Sprite2D Sprite, float GroundY)> _statics = new();

    private SubViewport _viewport = null!;
    private Node2D _world = null!;
    private Node2D _staticRoot = null!;

    public Texture2D Texture => _viewport.GetTexture();

    /// <summary>Stops redrawing the buffer while nothing can see it.</summary>
    public void SetRendering(bool rendering)
        => _viewport.RenderTargetUpdateMode = rendering ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            Name = "ReflectionBuffer",
            TransparentBg = true,
            Disable3D = true,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = new Vector2I(64, 64)
        };
        AddChild(_viewport);
        _world = new Node2D { Name = "Mirrored" };
        _viewport.AddChild(_world);
        _staticRoot = new Node2D { Name = "Scenery" };
        _world.AddChild(_staticRoot);
    }

    public void Track(string id, AnimatedSprite2D body, VoidlingVisualAppearance appearance, Func<Vector2> ground, float bodyHeight)
    {
        Untrack(id);
        var reflection = new VoidlingReflection2D { Name = "Reflection_" + id };
        _world.AddChild(reflection);
        reflection.Setup(body, appearance);
        _voidlings[id] = (reflection, ground, bodyHeight);
    }

    public void Untrack(string id)
    {
        if (!_voidlings.Remove(id, out var entry))
            return;
        if (GodotObject.IsInstanceValid(entry.Reflection))
            entry.Reflection.QueueFree();
    }

    /// <summary>Replaces the mirrored scenery (trees and the like) with the given sprites.</summary>
    public void SetScenery(IEnumerable<(Texture2D Texture, Vector2 Center, Vector2 Scale, float GroundY)> scenery)
    {
        foreach (var (sprite, _) in _statics)
        {
            if (GodotObject.IsInstanceValid(sprite))
                sprite.QueueFree();
        }
        _statics.Clear();

        foreach (var (texture, center, scale, groundY) in scenery)
        {
            var mirrorY = groundY + WaterDepth;
            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = new Vector2(center.X, 2.0f * mirrorY - center.Y),
                Scale = new Vector2(scale.X, -scale.Y)
            };
            _staticRoot.AddChild(sprite);
            _statics.Add((sprite, groundY));
        }
    }

    /// <summary>
    /// Matches the buffer to the view it is composited into and moves every reflection into place.
    /// </summary>
    public void Sync(Viewport view, GardenIslandField field)
    {
        var size = view.GetWindow()?.Size ?? new Vector2I(640, 360);
        if (view is SubViewport sub)
            size = sub.Size;
        if (_viewport.Size != size)
            _viewport.Size = size;
        _viewport.CanvasTransform = view.GetFinalTransform() * view.CanvasTransform;

        var stale = new List<string>();
        foreach (var (id, (reflection, ground, height)) in _voidlings)
        {
            if (!reflection.HasSource)
            {
                stale.Add(id);
                continue;
            }

            var feet = ground();
            var mirrorY = feet.Y + WaterDepth;
            var visible = WaterBelow(field, feet.X, mirrorY + WaterDepth - 2.0f, height);
            reflection.Visible = visible;
            if (visible)
                reflection.Follow(mirrorY);
        }

        foreach (var id in stale)
            Untrack(id);

        foreach (var (sprite, groundY) in _statics)
            sprite.Visible = WaterBelow(field, sprite.Position.X, groundY + WaterDepth * 2.0f - 2.0f, 40.0f);
    }

    /// <summary>
    /// Whether any water lies in the column a reflection would occupy, from its feet downwards.
    /// </summary>
    private static bool WaterBelow(GardenIslandField field, float x, float fromY, float height)
    {
        for (var y = fromY; y <= fromY + height; y += 4.0f)
        {
            if (!field.IsLand(x, y) || !field.IsLand(x - 6.0f, y) || !field.IsLand(x + 6.0f, y))
                return true;
        }

        return false;
    }
}
