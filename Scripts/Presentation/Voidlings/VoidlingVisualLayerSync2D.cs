using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Mirrors the base AnimatedSprite2D's semantic animation/frame onto independently authored overlay
/// atlases. The overlay sheets use the same frame grid as their owning body definition, so wings,
/// crystals and future adornment layers remain perfectly synchronized without consumer code.
/// </summary>
public partial class VoidlingVisualLayerSync2D : Node
{
    private AnimatedSprite2D? _target;
    private readonly List<AnimatedSprite2D> _layers = new();

    public void Setup(
        AnimatedSprite2D target,
        VoidlingVisualDefinition definition,
        VoidlingVisualAppearance appearance,
        bool race)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        foreach (var layerDefinition in VoidlingVisualFactory.ResolveLayers(definition, appearance.LayerIds))
        {
            var layer = new AnimatedSprite2D
            {
                SpriteFrames = VoidlingVisualFactory.CreateLayerFrames(definition, layerDefinition, race),
                Position = layerDefinition.OffsetAtScaleOne,
                Scale = Vector2.One * layerDefinition.ScaleMultiplier,
                ZIndex = layerDefinition.ZIndexOffset,
                ZAsRelative = true,
                Centered = target.Centered
            };
            VoidlingVisualFactory.ApplyLayerPalette(layer, definition, layerDefinition, appearance);
            target.AddChild(layer);
            _layers.Add(layer);
        }

        SyncNow();
    }

    public override void _Process(double delta)
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target))
        {
            QueueFree();
            return;
        }
        SyncNow();
    }

    private void SyncNow()
    {
        if (_target == null)
            return;

        foreach (var layer in _layers)
        {
            if (!GodotObject.IsInstanceValid(layer))
                continue;
            if (layer.SpriteFrames == null || !layer.SpriteFrames.HasAnimation(_target.Animation))
            {
                layer.Visible = false;
                continue;
            }

            layer.Visible = _target.Visible;
            layer.Animation = _target.Animation;
            var count = layer.SpriteFrames.GetFrameCount(layer.Animation);
            layer.Frame = count <= 0 ? 0 : Math.Clamp(_target.Frame, 0, count - 1);
            layer.FlipH = _target.FlipH;
            layer.FlipV = _target.FlipV;
        }
    }
}
