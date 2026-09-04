using System;
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
        body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ApplyBasePalette(body, appearance);
        portrait.AddChild(body);

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
            layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            layer.Position += layerDefinition.OffsetAtScaleOne;
            layer.Scale = Vector2.One * layerDefinition.ScaleMultiplier;
            VoidlingVisualFactory.ApplyLayerPalette(layer, definition, layerDefinition, appearance);
            portrait.AddChild(layer);
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
                definition.PortraitColumn * definition.FrameWidth,
                definition.PortraitRow * definition.FrameHeight,
                definition.FrameWidth,
                definition.FrameHeight)
        };
    }
}
