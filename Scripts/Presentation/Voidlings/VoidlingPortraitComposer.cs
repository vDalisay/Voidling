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
    private const string LayerPrefix = "__voidling_portrait_layer_";

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
        portrait.Texture = VoidlingVisualFactory.CreatePortraitTexture(definition.DefinitionId);
        ApplyBasePalette(portrait, appearance);

        foreach (var child in portrait.GetChildren())
        {
            if (child is Node node && node.Name.ToString().StartsWith(LayerPrefix, StringComparison.Ordinal))
            {
                portrait.RemoveChild(node);
                node.Free();
            }
        }

        var layerIndex = 0;
        foreach (var layerDefinition in VoidlingVisualFactory.ResolveLayers(definition, appearance.LayerIds))
        {
            var layer = new TextureRect
            {
                Name = $"{LayerPrefix}{layerIndex++}",
                Texture = CreateLayerPortraitTexture(definition, layerDefinition),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = layerDefinition.ZIndexOffset
            };
            layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            layer.Position += layerDefinition.OffsetAtScaleOne;
            layer.Scale = Vector2.One * layerDefinition.ScaleMultiplier;
            VoidlingVisualFactory.ApplyLayerPalette(layer, definition, layerDefinition, appearance);
            portrait.AddChild(layer);
        }
    }

    private static void ApplyBasePalette(TextureRect portrait, VoidlingVisualAppearance appearance)
    {
        // Reuse the canonical palette implementation rather than maintaining a portrait-specific
        // shader calculation. The temporary sprite is never added to the tree; only its immutable
        // material/modulate result is retained by the TextureRect.
        var probe = new AnimatedSprite2D();
        VoidlingVisualFactory.ApplyAppearance(probe, appearance, race: false);
        portrait.Material = probe.Material;
        portrait.Modulate = probe.Modulate;
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
