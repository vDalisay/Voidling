using System;
using System.Linq;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Headless CI probe for the canonical Voidling art contract. Every cataloged body family must
/// resolve world/race/portrait output through the same production factory before an art PR can pass.
/// </summary>
public partial class VoidlingVisualSmokeProbe : Node
{
    public override void _Ready()
    {
        try
        {
            foreach (var visualTypeId in VoidlingVisualFactory.VisualTypeIds)
            {
                ValidateWorldFrames(visualTypeId);
                ValidateRaceFrames(visualTypeId);
                ValidatePortrait(visualTypeId);
                ValidateGeometry(visualTypeId);
                ValidateComposedSprite(visualTypeId, race: false);
                ValidateComposedSprite(visualTypeId, race: true);
            }

            GD.Print(
                $"[voidling-visual-smoke] VOIDLING_VISUAL_SMOKE_SUCCESS types={string.Join(',', VoidlingVisualFactory.VisualTypeIds)}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[voidling-visual-smoke] VOIDLING_VISUAL_SMOKE_FAILED: {exception.Message}");
            GetTree().Quit(5);
        }
    }

    private void ValidatePortrait(string visualTypeId)
    {
        var definition = VoidlingVisualFactory.ResolveDefinition(visualTypeId);
        var hue = definition.SourcePaletteColors.Count > 0 ? 0.63f : -1.0f;
        var appearance = new VoidlingVisualAppearance(
            visualTypeId,
            hue,
            definition.DefaultLayerIds,
            "#F6F0C9");
        var portrait = UiFactory.CreatePortrait(
            appearance,
            hasAngelMutation: true,
            otherMutationCount: 1,
            new Vector2(48.0f, 48.0f));
        AddChild(portrait);

        var body = portrait.GetNodeOrNull<TextureRect>(VoidlingPortraitComposer.BodyNodeName);
        if (body == null || body.Texture == null || body.Texture.GetWidth() <= 0 || body.Texture.GetHeight() <= 0)
            throw new InvalidOperationException($"Portrait for '{visualTypeId}' resolved with invalid body dimensions.");
        if (definition.SourcePaletteColors.Count > 0 && body.Material == null)
            throw new InvalidOperationException($"Palette-enabled portrait '{visualTypeId}' did not receive a palette material.");
        if (portrait.GetNodeOrNull<Control>("__mutation_badge") == null)
            throw new InvalidOperationException($"Portrait mutation overlay for '{visualTypeId}' was not composed.");

        var expectedLayers = VoidlingVisualFactory.ResolveLayers(definition, appearance.LayerIds);
        var portraitLayers = portrait.GetChildren()
            .OfType<TextureRect>()
            .Where(child => child.Name.ToString().StartsWith(
                VoidlingPortraitComposer.LayerNodePrefix,
                StringComparison.Ordinal))
            .ToArray();
        if (portraitLayers.Length != expectedLayers.Count)
        {
            throw new InvalidOperationException(
                $"Portrait '{visualTypeId}' resolved {portraitLayers.Length} visual layers; expected {expectedLayers.Count}.");
        }

        for (var i = 0; i < expectedLayers.Count; i++)
        {
            var expected = expectedLayers[i];
            var actual = portraitLayers[i];
            if (actual.Texture == null)
                throw new InvalidOperationException($"Portrait layer '{expected.LayerId}' for '{visualTypeId}' has no texture.");
            if (actual.ZIndex - body.ZIndex != expected.ZIndexOffset)
            {
                throw new InvalidOperationException(
                    $"Portrait layer '{expected.LayerId}' for '{visualTypeId}' changed relative Z order.");
            }
        }

        portrait.QueueFree();
    }

    private static void ValidateWorldFrames(string visualTypeId)
    {
        var frames = VoidlingVisualFactory.GetWorldFrames(visualTypeId);
        RequireAnimation(frames, "walk_down");
        RequireAnimation(frames, "walk_up");
        RequireAnimation(frames, "walk_left");
        RequireAnimation(frames, "walk_right");
    }

    private static void ValidateRaceFrames(string visualTypeId)
    {
        var frames = VoidlingVisualFactory.GetRaceFrames(visualTypeId);
        RequireAnimation(frames, "run");
        RequireAnimation(frames, "swim");
    }

    private static void ValidateGeometry(string visualTypeId)
    {
        foreach (var adult in new[] { false, true })
        {
            var scale = VoidlingVisualFactory.WorldScale(adult, visualTypeId);
            var hitbox = VoidlingVisualFactory.WorldHitboxSize(adult, visualTypeId);
            var shadow = VoidlingVisualFactory.ShadowRadii(scale, visualTypeId);
            if (scale <= 0.0f || hitbox.X <= 0.0f || hitbox.Y <= 0.0f || shadow.X <= 0.0f || shadow.Y <= 0.0f)
                throw new InvalidOperationException($"Invalid {visualTypeId}/{(adult ? "adult" : "child")} geometry.");
        }

        var raceScale = VoidlingVisualFactory.RaceScaleFor(visualTypeId);
        if (raceScale <= 0.0f || VoidlingVisualFactory.BuildShadowPolygon(raceScale, 20, visualTypeId).Length != 20)
            throw new InvalidOperationException($"Invalid race geometry for '{visualTypeId}'.");

        _ = VoidlingMutationVisualMetrics.ForSpriteTarget(
            VoidlingVisualFactory.WorldScale(adult: true, visualTypeId),
            visualTypeId);
        _ = VoidlingMutationVisualMetrics.ForPortrait(48.0f, new Vector2(48.0f, 48.0f), visualTypeId);
    }

    private void ValidateComposedSprite(string visualTypeId, bool race)
    {
        var definition = VoidlingVisualFactory.ResolveDefinition(visualTypeId);
        var hue = definition.SourcePaletteColors.Count > 0 ? 0.63f : -1.0f;
        var appearance = new VoidlingVisualAppearance(
            visualTypeId,
            hue,
            definition.DefaultLayerIds,
            "#F6F0C9");
        var sprite = new AnimatedSprite2D();
        VoidlingVisualFactory.ApplyAppearance(sprite, appearance, race);
        AddChild(sprite);

        StringName animation = race ? "run" : "walk_down";
        sprite.Play(animation);

        if (sprite.SpriteFrames == null || !sprite.SpriteFrames.HasAnimation(animation))
        {
            throw new InvalidOperationException(
                $"Composed {(race ? "race" : "world")} visual for '{visualTypeId}' has no '{animation}' frames.");
        }
        if (definition.SourcePaletteColors.Count > 0 && sprite.Material == null)
            throw new InvalidOperationException($"Palette-enabled visual '{visualTypeId}' did not receive a palette material.");

        var expectedLayers = VoidlingVisualFactory.ResolveLayers(definition, appearance.LayerIds);
        var layerRoot = sprite.GetNodeOrNull<VoidlingVisualLayerSync2D>("__voidling_layers");
        if (layerRoot == null)
        {
            throw new InvalidOperationException(
                $"Composed {(race ? "race" : "world")} visual for '{visualTypeId}' has no canonical layer root.");
        }

        var actualLayers = layerRoot.GetChildren().OfType<AnimatedSprite2D>().ToArray();
        if (actualLayers.Length != expectedLayers.Count)
        {
            throw new InvalidOperationException(
                $"{(race ? "Race" : "World")} visual '{visualTypeId}' resolved {actualLayers.Length} layers; expected {expectedLayers.Count}.");
        }

        for (var i = 0; i < expectedLayers.Count; i++)
        {
            var expected = expectedLayers[i];
            var actual = actualLayers[i];
            if (actual.ZIndex != expected.ZIndexOffset)
            {
                throw new InvalidOperationException(
                    $"{(race ? "Race" : "World")} layer '{expected.LayerId}' for '{visualTypeId}' changed relative Z order.");
            }
            if (actual.SpriteFrames == null || !actual.SpriteFrames.HasAnimation(animation))
            {
                throw new InvalidOperationException(
                    $"{(race ? "Race" : "World")} layer '{expected.LayerId}' for '{visualTypeId}' has no '{animation}' frames.");
            }
        }

        sprite.QueueFree();
    }

    private static void RequireAnimation(SpriteFrames frames, StringName animation)
    {
        if (!frames.HasAnimation(animation))
            throw new InvalidOperationException($"Missing required Voidling animation '{animation}'.");
    }
}
