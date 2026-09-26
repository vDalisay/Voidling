using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Applies the same semantic body family, palette and developer-authored layer recipe used by world
/// sprites to UI portraits. This keeps breeding, roster, trade, lineage and podium views from
/// becoming simplified/independent appearance implementations.
/// </summary>
public static class VoidlingPortraitComposer
{
    internal const string BodyNodeName = "__voidling_portrait_body";
    internal const string LayerNodePrefix = "__voidling_portrait_layer_";

    public static TextureRect Create(VoidlingVisualAppearance appearance, Vector2 minimumSize)
    {
        var portrait = new TextureRect
        {
            CustomMinimumSize = minimumSize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        Apply(portrait, appearance);
        return portrait;
    }

    public static void Apply(TextureRect portrait, VoidlingVisualAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(portrait);
        var definition = VoidlingVisualFactory.ResolveDefinition(appearance.VisualTypeId);
        var resolvedLayers = VoidlingVisualFactory.ResolveLayers(definition, appearance.LayerIds);

        // The portrait root is deliberately transparent. Body and overlays are sibling children so
        // negative-Z layers stay behind the body without accidentally falling behind the containing
        // panel/window. A bias preserves exactly the same relative Z ordering used by world sprites.
        portrait.Texture = null;
        portrait.Material = null;

        foreach (var child in portrait.GetChildren())
        {
            if (child is not Node node)
                continue;

            var name = node.Name.ToString();
            if (name == BodyNodeName || name.StartsWith(LayerNodePrefix, StringComparison.Ordinal))
            {
                portrait.RemoveChild(node);
                node.Free();
            }
        }

        var minimumRelativeZ = 0;
        foreach (var layerDefinition in resolvedLayers)
            minimumRelativeZ = Math.Min(minimumRelativeZ, layerDefinition.ZIndexOffset);
        var zBias = -minimumRelativeZ;

        var body = new TextureRect
        {
            Name = BodyNodeName,
            Texture = VoidlingVisualFactory.CreatePortraitTexture(definition.DefinitionId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = zBias
        };
        ApplyBasePalette(body, appearance);
        portrait.AddChild(body);
        var surfaces = new List<(TextureRect Node, Vector2 Offset, Vector2 Size)>
        {
            (body, Vector2.Zero, new Vector2(definition.FrameWidth, definition.FrameHeight))
        };

        var layerIndex = 0;
        foreach (var layerDefinition in resolvedLayers)
        {
            var layer = new TextureRect
            {
                Name = $"{LayerNodePrefix}{layerIndex++}",
                Texture = CreateLayerPortraitTexture(definition, layerDefinition),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = zBias + layerDefinition.ZIndexOffset
            };
            VoidlingVisualFactory.ApplyLayerPalette(layer, definition, layerDefinition, appearance);
            portrait.AddChild(layer);
            var layerSize = new Vector2(
                layerDefinition.FrameWidth > 0 ? layerDefinition.FrameWidth : definition.FrameWidth,
                layerDefinition.FrameHeight > 0 ? layerDefinition.FrameHeight : definition.FrameHeight);
            var portraitFrameOffset = layerDefinition.FrameYOffsets.Length > definition.PortraitColumn
                ? layerDefinition.FrameYOffsets[definition.PortraitColumn]
                : 0;
            surfaces.Add((layer,
                layerDefinition.OffsetAtScaleOne + new Vector2(0, portraitFrameOffset),
                layerSize * layerDefinition.ScaleMultiplier));
        }

        LayoutSurfaces(portrait, surfaces);
    }

    private static void LayoutSurfaces(
        TextureRect portrait,
        IReadOnlyList<(TextureRect Node, Vector2 Offset, Vector2 Size)> surfaces)
    {
        var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var surface in surfaces)
        {
            minimum = minimum.Min(surface.Offset - surface.Size * 0.5f);
            maximum = maximum.Max(surface.Offset + surface.Size * 0.5f);
        }

        var box = portrait.CustomMinimumSize;
        if (box.X <= 0 || box.Y <= 0)
            box = portrait.Size;
        if (box.X <= 0 || box.Y <= 0)
            box = new Vector2(48, 48);
        var scale = Mathf.Min(box.X / (maximum.X - minimum.X), box.Y / (maximum.Y - minimum.Y));
        var center = (minimum + maximum) * 0.5f;
        foreach (var surface in surfaces)
        {
            var topLeft = (surface.Offset - surface.Size * 0.5f - center) * scale;
            var bottomRight = topLeft + surface.Size * scale;
            surface.Node.AnchorLeft = 0.5f;
            surface.Node.AnchorRight = 0.5f;
            surface.Node.AnchorTop = 0.5f;
            surface.Node.AnchorBottom = 0.5f;
            surface.Node.OffsetLeft = topLeft.X;
            surface.Node.OffsetTop = topLeft.Y;
            surface.Node.OffsetRight = bottomRight.X;
            surface.Node.OffsetBottom = bottomRight.Y;
        }
    }

    private static void ApplyBasePalette(TextureRect body, VoidlingVisualAppearance appearance)
    {
        // Reuse the canonical palette implementation rather than maintaining a portrait-specific
        // shader calculation. The temporary sprite is never added to the tree; only its immutable
        // body material/modulate result is retained by the TextureRect.
        var probe = new AnimatedSprite2D();
        VoidlingVisualFactory.ApplyAppearance(probe, appearance, race: false);
        body.Material = probe.Material;
        body.Modulate = probe.Modulate;
        probe.Free();
    }

    private static Texture2D CreateLayerPortraitTexture(
        VoidlingVisualDefinition definition,
        VoidlingVisualLayerDefinition layer)
    {
        if (layer.BaseAtlas == null)
            throw new InvalidOperationException($"Voidling layer '{layer.LayerId}' has no portrait atlas.");

        return new AtlasTexture
        {
            Atlas = layer.BaseAtlas,
            Region = new Rect2(
                (layer.SelfAnimatedFrameCount == 1 ? 0 : definition.PortraitColumn) *
                    (layer.FrameWidth > 0 ? layer.FrameWidth : definition.FrameWidth),
                definition.PortraitRow * (layer.FrameHeight > 0 ? layer.FrameHeight : definition.FrameHeight),
                layer.FrameWidth > 0 ? layer.FrameWidth : definition.FrameWidth,
                layer.FrameHeight > 0 ? layer.FrameHeight : definition.FrameHeight)
        };
    }
}
