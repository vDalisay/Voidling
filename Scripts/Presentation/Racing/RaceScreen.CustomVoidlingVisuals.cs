using System;
using Godot;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private static readonly Texture2D PipRaceTexture = GD.Load<Texture2D>(
        "res://Assets/Voidlings/Pip/dark_voidling.png");
    private static readonly Texture2D MallowRaceTexture = GD.Load<Texture2D>(
        "res://Assets/Voidlings/Mallow/dark_voidling.png");

    public override void _EnterTree()
    {
        // _Ready builds the race visuals. Apply the creature-specific presentation immediately
        // afterwards so every race mode (run/swim/glide) keeps the same Voidling identity used
        // in the garden instead of falling back to the legacy placeholder spritesheet.
        CallDeferred(MethodName.ApplyCustomVoidlingRaceVisuals);
    }

    private void ApplyCustomVoidlingRaceVisuals()
    {
        foreach (var visual in _visuals.Values)
        {
            var texture = ResolveCustomRaceTexture(visual.Entrant.Participant.DisplayName);
            if (texture == null)
                continue;

            visual.Sprite.SpriteFrames = BuildStaticRaceFrames(texture);
            visual.Sprite.Scale = Vector2.One * 0.31f;
            visual.Sprite.Modulate = Colors.White;
            visual.Sprite.SpeedScale = 1.0f;
            visual.Sprite.Play(visual.VisualMode == "swim" ? "swim" : "run");

            // Match the smaller garden footprint rather than retaining the oversized race
            // placeholder shadow that was authored for 48x48 character sprites.
            visual.Shadow.Polygon = BuildEllipsePoints(5.2f, 1.8f, 18);
        }
    }

    private static Texture2D? ResolveCustomRaceTexture(string displayName)
    {
        if (string.Equals(displayName, "Pip", StringComparison.OrdinalIgnoreCase))
            return PipRaceTexture;
        if (string.Equals(displayName, "Mallow", StringComparison.OrdinalIgnoreCase))
            return MallowRaceTexture;
        return null;
    }

    private static SpriteFrames BuildStaticRaceFrames(Texture2D texture)
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        AddStaticRaceAnimation(frames, "run", texture);
        AddStaticRaceAnimation(frames, "swim", texture);

        return frames;
    }

    private static void AddStaticRaceAnimation(SpriteFrames frames, string name, Texture2D texture)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, true);
        frames.SetAnimationSpeed(name, 1.0);
        frames.AddFrame(name, texture);
    }
}
