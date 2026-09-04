using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Mirrors the base AnimatedSprite2D's semantic animation/frame onto independently authored overlay
/// atlases. The overlay sheets use the same frame grid as their owning body definition. Optional
/// motion groups can trail vertical body motion slightly while preserving exact relative placement
/// between all layers in the group (for example the front and back halves of one wing set).
/// </summary>
public partial class VoidlingVisualLayerSync2D : Node2D
{
    private sealed class LayerRuntime
    {
        public LayerRuntime(AnimatedSprite2D sprite, VoidlingVisualLayerDefinition definition)
        {
            Sprite = sprite;
            Definition = definition;
        }

        public AnimatedSprite2D Sprite { get; }
        public VoidlingVisualLayerDefinition Definition { get; }
    }

    private sealed class MotionGroupRuntime
    {
        public MotionGroupRuntime(float lagSeconds, float maxOffsetAtScaleOne)
        {
            LagSeconds = lagSeconds;
            MaxOffsetAtScaleOne = maxOffsetAtScaleOne;
        }

        public float LagSeconds { get; }
        public float MaxOffsetAtScaleOne { get; }
        public float OffsetYAtScaleOne { get; set; }
    }

    private AnimatedSprite2D? _target;
    private readonly List<LayerRuntime> _layers = new();
    private readonly Dictionary<string, MotionGroupRuntime> _motionGroups =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _hasPreviousTargetGlobalY;
    private float _previousTargetGlobalY;

    public void Setup(
        AnimatedSprite2D target,
        VoidlingVisualDefinition definition,
        VoidlingVisualAppearance appearance,
        bool race)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _layers.Clear();
        _motionGroups.Clear();
        _hasPreviousTargetGlobalY = false;

        foreach (var layerDefinition in VoidlingVisualFactory.ResolveLayers(definition, appearance.LayerIds))
        {
            RegisterMotionGroup(layerDefinition);

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
            AddChild(layer);
            _layers.Add(new LayerRuntime(layer, layerDefinition));
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

        SyncFacingFromSemanticAnimation();
        UpdateMotionGroups(delta);
        SyncNow();
    }

    private void RegisterMotionGroup(VoidlingVisualLayerDefinition layerDefinition)
    {
        var groupId = layerDefinition.MotionGroupId?.Trim() ?? string.Empty;
        if (groupId.Length == 0 ||
            layerDefinition.VerticalFollowLagSeconds <= 0.0f ||
            layerDefinition.MaxVerticalLagAtScaleOne <= 0.0f)
        {
            return;
        }

        if (_motionGroups.TryGetValue(groupId, out var existing))
        {
            if (!Mathf.IsEqualApprox(existing.LagSeconds, layerDefinition.VerticalFollowLagSeconds) ||
                !Mathf.IsEqualApprox(existing.MaxOffsetAtScaleOne, layerDefinition.MaxVerticalLagAtScaleOne))
            {
                throw new InvalidOperationException(
                    $"Voidling motion group '{groupId}' must use one shared follow-lag configuration.");
            }
            return;
        }

        _motionGroups.Add(
            groupId,
            new MotionGroupRuntime(
                layerDefinition.VerticalFollowLagSeconds,
                layerDefinition.MaxVerticalLagAtScaleOne));
    }

    /// <summary>
    /// The supplied normal sheet is authored facing right. Semantic left/right animation names are
    /// therefore also the canonical facing contract: left mirrors the complete assembly, right uses
    /// the authored orientation. Up/down preserve the most recent horizontal facing.
    /// </summary>
    private void SyncFacingFromSemanticAnimation()
    {
        if (_target == null)
            return;

        var animation = _target.Animation.ToString();
        if (string.Equals(animation, "walk_left", StringComparison.Ordinal))
            _target.FlipH = true;
        else if (string.Equals(animation, "walk_right", StringComparison.Ordinal))
            _target.FlipH = false;
    }

    private void UpdateMotionGroups(double delta)
    {
        if (_target == null || _motionGroups.Count == 0)
            return;

        var currentGlobalY = _target.GlobalPosition.Y;
        if (!_hasPreviousTargetGlobalY)
        {
            _previousTargetGlobalY = currentGlobalY;
            _hasPreviousTargetGlobalY = true;
            ApplyMotionGroupOffsets();
            return;
        }

        var globalDeltaY = currentGlobalY - _previousTargetGlobalY;
        _previousTargetGlobalY = currentGlobalY;

        // Layer positions are expressed in source pixels beneath the target sprite. Convert global
        // movement back to that local scale before applying the counter-motion.
        var globalScaleY = MathF.Max(0.001f, MathF.Abs(_target.GlobalScale.Y));
        var localDeltaY = globalDeltaY / globalScaleY;

        foreach (var group in _motionGroups.Values)
        {
            // Counter the body's movement first, then let the wing group ease back onto it. When the
            // body jumps upward this leaves the wings slightly low; while falling it leaves them
            // slightly high, producing the requested soft "fall onto the body" motion.
            group.OffsetYAtScaleOne = Mathf.Clamp(
                group.OffsetYAtScaleOne - localDeltaY,
                -group.MaxOffsetAtScaleOne,
                group.MaxOffsetAtScaleOne);

            var relaxation = group.LagSeconds <= 0.0f
                ? 1.0f
                : 1.0f - MathF.Exp(-(float)Math.Max(0.0, delta) / group.LagSeconds);
            group.OffsetYAtScaleOne = Mathf.Lerp(group.OffsetYAtScaleOne, 0.0f, relaxation);
        }

        ApplyMotionGroupOffsets();
    }

    private void ApplyMotionGroupOffsets()
    {
        foreach (var runtime in _layers)
        {
            var position = runtime.Definition.OffsetAtScaleOne;
            var groupId = runtime.Definition.MotionGroupId?.Trim() ?? string.Empty;
            if (groupId.Length > 0 && _motionGroups.TryGetValue(groupId, out var group))
                position.Y += group.OffsetYAtScaleOne;
            runtime.Sprite.Position = position;
        }
    }

    private void SyncNow()
    {
        if (_target == null)
            return;

        foreach (var runtime in _layers)
        {
            var layer = runtime.Sprite;
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
