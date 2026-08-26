using System;
using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Shared source of truth for creature-specific presentation assets. Any Voidling visual override
/// must flow through this catalog so garden, race and portrait renderers cannot drift apart.
/// </summary>
public static class VoidlingVisualCatalog
{
    public const float LegacyAdultScale = 0.62f;
    public const float LegacyChildScale = 0.31f;
    public const float CustomScaleMultiplier = 0.5f;
    public const float RaceCustomScale = 0.31f;

    private const int PipFrameWidth = 32;
    private const int PipFrameHeight = 32;
    private const int PipFrameCount = 5;

    private static readonly Texture2D LegacyTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png");
    private static readonly Texture2D PipWalkTexture = GD.Load<Texture2D>(
        "res://Assets/Voidlings/Pip/voidling_walk_sheet.png");
    private static readonly Texture2D MallowTexture = GD.Load<Texture2D>(
        "res://Assets/Voidlings/Mallow/dark_voidling.png");

    public static bool IsPip(string displayName)
        => string.Equals(displayName, "Pip", StringComparison.OrdinalIgnoreCase);

    public static bool IsMallow(string displayName)
        => string.Equals(displayName, "Mallow", StringComparison.OrdinalIgnoreCase);

    public static bool UsesCustomVisual(string displayName)
        => IsPip(displayName) || IsMallow(displayName);

    public static float WorldScale(string displayName, bool adult)
    {
        var baseScale = adult ? LegacyAdultScale : LegacyChildScale;
        return UsesCustomVisual(displayName) ? baseScale * CustomScaleMultiplier : baseScale;
    }

    public static Color Modulate(string displayName, Color fallbackTint)
        => UsesCustomVisual(displayName) ? Colors.White : fallbackTint;

    public static Texture2D PortraitTexture(string displayName)
    {
        if (IsPip(displayName))
            return PipFrame(0);
        if (IsMallow(displayName))
            return MallowTexture;

        return new AtlasTexture
        {
            Atlas = LegacyTexture,
            Region = new Rect2(0, 0, 48, 48)
        };
    }

    public static SpriteFrames BuildWorldFrames(string displayName)
    {
        if (IsPip(displayName))
            return BuildPipDirectionalFrames();
        if (IsMallow(displayName))
            return BuildStaticDirectionalFrames(MallowTexture);

        return BuildLegacyDirectionalFrames();
    }

    public static SpriteFrames BuildRaceFrames(string displayName)
    {
        if (IsPip(displayName))
        {
            var frames = new SpriteFrames();
            frames.RemoveAnimation("default");
            AddPipAnimation(frames, "run", 9.0);
            AddPipAnimation(frames, "swim", 7.0);
            return frames;
        }

        if (IsMallow(displayName))
        {
            var frames = new SpriteFrames();
            frames.RemoveAnimation("default");
            AddStaticAnimation(frames, "run", MallowTexture);
            AddStaticAnimation(frames, "swim", MallowTexture);
            return frames;
        }

        throw new InvalidOperationException($"{displayName} does not have a custom race visual.");
    }

    private static SpriteFrames BuildPipDirectionalFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        AddPipAnimation(frames, "walk_down", 8.0);
        AddPipAnimation(frames, "walk_up", 8.0);
        AddPipAnimation(frames, "walk_left", 8.0);
        AddPipAnimation(frames, "walk_right", 8.0);
        return frames;
    }

    private static SpriteFrames BuildStaticDirectionalFrames(Texture2D texture)
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        AddStaticAnimation(frames, "walk_down", texture);
        AddStaticAnimation(frames, "walk_up", texture);
        AddStaticAnimation(frames, "walk_left", texture);
        AddStaticAnimation(frames, "walk_right", texture);
        return frames;
    }

    private static SpriteFrames BuildLegacyDirectionalFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        AddLegacyDirection(frames, "walk_down", 0);
        AddLegacyDirection(frames, "walk_up", 1);
        AddLegacyDirection(frames, "walk_left", 2);
        AddLegacyDirection(frames, "walk_right", 3);
        return frames;
    }

    private static void AddPipAnimation(SpriteFrames frames, string name, double speed)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, true);
        frames.SetAnimationSpeed(name, speed);
        for (var column = 0; column < PipFrameCount; column++)
            frames.AddFrame(name, PipFrame(column));
    }

    private static AtlasTexture PipFrame(int column)
        => new()
        {
            Atlas = PipWalkTexture,
            Region = new Rect2(column * PipFrameWidth, 0, PipFrameWidth, PipFrameHeight)
        };

    private static void AddLegacyDirection(SpriteFrames frames, string name, int row)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, true);
        frames.SetAnimationSpeed(name, 6.0);
        for (var column = 0; column < 4; column++)
        {
            frames.AddFrame(name, new AtlasTexture
            {
                Atlas = LegacyTexture,
                Region = new Rect2(column * 48, row * 48, 48, 48)
            });
        }
    }

    private static void AddStaticAnimation(SpriteFrames frames, string name, Texture2D texture)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, true);
        frames.SetAnimationSpeed(name, 1.0);
        frames.AddFrame(name, texture);
    }
}
