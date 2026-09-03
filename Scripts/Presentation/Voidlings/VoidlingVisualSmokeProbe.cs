using System;
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
                ValidateComposedSprite(visualTypeId);
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

    private static void ValidatePortrait(string visualTypeId)
    {
        var portraitTexture = VoidlingVisualFactory.CreatePortraitTexture(visualTypeId);
        if (portraitTexture.GetWidth() <= 0 || portraitTexture.GetHeight() <= 0)
            throw new InvalidOperationException($"Portrait for '{visualTypeId}' resolved with invalid dimensions.");
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
    }

    private void ValidateComposedSprite(string visualTypeId)
    {
        var definition = VoidlingVisualFactory.ResolveDefinition(visualTypeId);
        var hue = definition.SourcePaletteColors.Count > 0 ? 0.63f : -1.0f;
        var sprite = new AnimatedSprite2D();
        VoidlingVisualFactory.ApplyAppearance(
            sprite,
            new VoidlingVisualAppearance(
                visualTypeId,
                hue,
                definition.DefaultLayerIds,
                "#F6F0C9"),
            race: false);
        AddChild(sprite);
        sprite.Play("walk_down");

        if (sprite.SpriteFrames == null || !sprite.SpriteFrames.HasAnimation("walk_down"))
            throw new InvalidOperationException($"Composed world visual for '{visualTypeId}' has no movement frames.");
        if (definition.SourcePaletteColors.Count > 0 && sprite.Material == null)
            throw new InvalidOperationException($"Palette-enabled visual '{visualTypeId}' did not receive a palette material.");

        sprite.QueueFree();
    }

    private static void RequireAnimation(SpriteFrames frames, StringName animation)
    {
        if (!frames.HasAnimation(animation))
            throw new InvalidOperationException($"Missing required Voidling animation '{animation}'.");
    }
}
