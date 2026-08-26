using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Headless CI probe for the canonical Voidling art contract. It intentionally exercises the same
/// factory used by Garden, remote Garden, races and UI portraits rather than parsing the .tres file
/// independently, so a broken art revision fails before it can drift between presentation surfaces.
/// </summary>
public partial class VoidlingVisualSmokeProbe : Node
{
    public override void _Ready()
    {
        try
        {
            ValidateWorldFrames();
            ValidateRaceFrames();
            ValidatePortrait();
            ValidateGeometry();

            GD.Print(
                $"[voidling-visual-smoke] VOIDLING_VISUAL_SMOKE_SUCCESS definition={VoidlingVisualFactory.DefinitionId}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[voidling-visual-smoke] VOIDLING_VISUAL_SMOKE_FAILED: {exception.Message}");
            GetTree().Quit(5);
        }
    }

    private static void ValidateWorldFrames()
    {
        var frames = VoidlingVisualFactory.GetWorldFrames();
        RequireAnimation(frames, "walk_down");
        RequireAnimation(frames, "walk_up");
        RequireAnimation(frames, "walk_left");
        RequireAnimation(frames, "walk_right");
    }

    private static void ValidateRaceFrames()
    {
        var frames = VoidlingVisualFactory.GetRaceFrames();
        RequireAnimation(frames, "run");
        RequireAnimation(frames, "swim");
    }

    private static void ValidatePortrait()
    {
        var portraitTexture = VoidlingVisualFactory.CreatePortraitTexture();
        if (portraitTexture.GetWidth() <= 0 || portraitTexture.GetHeight() <= 0)
            throw new InvalidOperationException("Canonical portrait texture resolved with invalid dimensions.");

        // Exercise the actual shared UI construction path used by breeding, details, family tree,
        // racing and multiplayer trading rather than validating only the lower-level atlas crop.
        var portrait = UiFactory.CreatePortrait(
            Colors.White,
            false,
            0,
            new Vector2(48.0f, 48.0f));
        if (portrait.Texture == null)
            throw new InvalidOperationException("UiFactory portrait did not resolve the canonical Voidling texture.");
    }

    private static void ValidateGeometry()
    {
        foreach (var adult in new[] { false, true })
        {
            var scale = VoidlingVisualFactory.WorldScale(adult);
            var hitbox = VoidlingVisualFactory.WorldHitboxSize(adult);
            var shadow = VoidlingVisualFactory.ShadowRadii(scale);

            if (scale <= 0.0f || hitbox.X <= 0.0f || hitbox.Y <= 0.0f || shadow.X <= 0.0f || shadow.Y <= 0.0f)
                throw new InvalidOperationException($"Invalid {(adult ? "adult" : "child")} presentation geometry.");
        }

        if (VoidlingVisualFactory.RaceScale <= 0.0f)
            throw new InvalidOperationException("Invalid race presentation scale.");

        var raceShadow = VoidlingVisualFactory.BuildShadowPolygon(VoidlingVisualFactory.RaceScale, 20);
        if (raceShadow.Length != 20)
            throw new InvalidOperationException("Race shadow geometry did not resolve from the canonical definition.");

        // This calls the shared mutation metric path, proving halo anchors remain compatible with
        // the current art profile instead of being an independent hard-coded silhouette assumption.
        _ = VoidlingMutationVisualMetrics.ForSpriteTarget(VoidlingVisualFactory.AdultWorldScale);
        _ = VoidlingMutationVisualMetrics.ForPortrait(48.0f, new Vector2(48.0f, 48.0f));
    }

    private static void RequireAnimation(SpriteFrames frames, StringName animation)
    {
        if (!frames.HasAnimation(animation))
            throw new InvalidOperationException($"Missing required Voidling animation '{animation}'.");
        if (frames.GetFrameCount(animation) <= 0)
            throw new InvalidOperationException($"Voidling animation '{animation}' has no frames.");
    }
}
