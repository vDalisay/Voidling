using System;
using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Canonical construction path for base Voidling sprites, animation frames, portraits and
/// art-dependent presentation geometry. Consumers request semantic presentation output and never
/// know texture paths, frame rows or atlas dimensions.
/// </summary>
public static class VoidlingVisualFactory
{
    public const string DefaultDefinitionPath =
        "res://Resources/Presentation/Voidlings/DefaultVoidlingVisual.tres";

    private static readonly VoidlingVisualDefinition Definition = LoadDefinition();
    private static readonly SpriteFrames WorldFrames = BuildWorldFrames(Definition);
    private static readonly SpriteFrames RaceFrames = BuildRaceFrames(Definition);

    public static string DefinitionId => Definition.DefinitionId;
    public static float AdultWorldScale => Definition.AdultWorldScale;
    public static float RaceScale => Definition.RaceScale;
    public static float ShadowCenterYOffset => Definition.ShadowCenterYOffset;
    public static float HeldScaleMultiplier => Definition.HeldScaleMultiplier;
    public static float HeldSpriteYOffset => Definition.HeldSpriteYOffset;

    public static SpriteFrames GetWorldFrames() => WorldFrames;

    public static SpriteFrames GetRaceFrames() => RaceFrames;

    public static float WorldScale(bool adult)
        => adult ? Definition.AdultWorldScale : Definition.ChildWorldScale;

    public static Vector2 WorldHitboxSize(bool adult)
        => adult ? Definition.AdultHitboxSize : Definition.ChildHitboxSize;

    public static float WorldSpriteCenterYOffset(float spriteScale)
        => Definition.WorldSpriteCenterYOffsetAtScaleOne * spriteScale;

    public static float RaceSpriteCenterYOffset()
        => Definition.RaceSpriteCenterYOffset;

    public static Vector2 ShadowRadii(float spriteScale)
    {
        var referenceScale = Math.Max(0.001f, Definition.AdultWorldScale);
        return Definition.AdultShadowRadii * (spriteScale / referenceScale);
    }

    public static Vector2[] BuildShadowPolygon(float spriteScale, int points = 20)
    {
        if (points < 3)
            throw new ArgumentOutOfRangeException(nameof(points), "A shadow polygon needs at least three points.");

        var radii = ShadowRadii(spriteScale);
        var polygon = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            polygon[i] = new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
        }
        return polygon;
    }

    public static Texture2D CreatePortraitTexture()
        => CreateAtlasFrame(
            Definition.BaseAtlas,
            Definition.PortraitColumn,
            Definition.PortraitRow,
            Definition.FrameWidth,
            Definition.FrameHeight);

    private static VoidlingVisualDefinition LoadDefinition()
    {
        var definition = GD.Load<VoidlingVisualDefinition>(DefaultDefinitionPath)
            ?? throw new InvalidOperationException(
                $"Voidling visual definition could not be loaded from {DefaultDefinitionPath}.");

        Validate(definition);
        return definition;
    }

    private static void Validate(VoidlingVisualDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.DefinitionId))
            throw new InvalidOperationException("Voidling visual DefinitionId cannot be empty.");
        if (definition.BaseAtlas == null)
            throw new InvalidOperationException("Voidling visual definition requires BaseAtlas.");
        if (definition.FrameWidth <= 0 || definition.FrameHeight <= 0)
            throw new InvalidOperationException("Voidling frame dimensions must be positive.");
        if (definition.WorldFrameCount <= 0 || definition.RaceRunFrameCount <= 0 || definition.RaceSwimFrameCount <= 0)
            throw new InvalidOperationException("Voidling animation frame counts must be positive.");
        if (definition.AdultWorldScale <= 0.0f || definition.ChildWorldScale <= 0.0f || definition.RaceScale <= 0.0f)
            throw new InvalidOperationException("Voidling presentation scales must be positive.");
        if (definition.AdultShadowRadii.X <= 0.0f || definition.AdultShadowRadii.Y <= 0.0f)
            throw new InvalidOperationException("Voidling shadow radii must be positive.");

        ValidateAtlasCoverage(
            definition.BaseAtlas,
            definition.FrameWidth,
            definition.FrameHeight,
            definition.WorldFrameCount,
            definition.WalkDownRow,
            definition.WalkUpRow,
            definition.WalkLeftRow,
            definition.WalkRightRow,
            definition.RaceRunRow,
            definition.PortraitRow);

        var swimAtlas = definition.SwimAtlas ?? definition.BaseAtlas;
        ValidateAtlasCoverage(
            swimAtlas,
            definition.FrameWidth,
            definition.FrameHeight,
            definition.RaceSwimFrameCount,
            definition.RaceSwimRow);
    }

    private static void ValidateAtlasCoverage(
        Texture2D atlas,
        int frameWidth,
        int frameHeight,
        int requiredColumns,
        params int[] rows)
    {
        var maxRow = 0;
        foreach (var row in rows)
        {
            if (row < 0)
                throw new InvalidOperationException("Voidling animation rows cannot be negative.");
            maxRow = Math.Max(maxRow, row);
        }

        var requiredWidth = requiredColumns * frameWidth;
        var requiredHeight = (maxRow + 1) * frameHeight;
        if (atlas.GetWidth() < requiredWidth || atlas.GetHeight() < requiredHeight)
        {
            throw new InvalidOperationException(
                $"Voidling atlas '{atlas.ResourcePath}' is {atlas.GetWidth()}x{atlas.GetHeight()}, " +
                $"but the visual definition requires at least {requiredWidth}x{requiredHeight}.");
        }
    }

    private static SpriteFrames BuildWorldFrames(VoidlingVisualDefinition definition)
    {
        var frames = CreateEmptyFrames();
        AddAnimation(
            frames,
            "walk_down",
            definition.BaseAtlas,
            definition.WalkDownRow,
            definition.WorldFrameCount,
            definition.WorldAnimationFps,
            definition.FrameWidth,
            definition.FrameHeight);
        AddAnimation(
            frames,
            "walk_up",
            definition.BaseAtlas,
            definition.WalkUpRow,
            definition.WorldFrameCount,
            definition.WorldAnimationFps,
            definition.FrameWidth,
            definition.FrameHeight);
        AddAnimation(
            frames,
            "walk_left",
            definition.BaseAtlas,
            definition.WalkLeftRow,
            definition.WorldFrameCount,
            definition.WorldAnimationFps,
            definition.FrameWidth,
            definition.FrameHeight);
        AddAnimation(
            frames,
            "walk_right",
            definition.BaseAtlas,
            definition.WalkRightRow,
            definition.WorldFrameCount,
            definition.WorldAnimationFps,
            definition.FrameWidth,
            definition.FrameHeight);
        return frames;
    }

    private static SpriteFrames BuildRaceFrames(VoidlingVisualDefinition definition)
    {
        var frames = CreateEmptyFrames();
        AddAnimation(
            frames,
            "run",
            definition.BaseAtlas,
            definition.RaceRunRow,
            definition.RaceRunFrameCount,
            definition.RaceRunFps,
            definition.FrameWidth,
            definition.FrameHeight);
        AddAnimation(
            frames,
            "swim",
            definition.SwimAtlas ?? definition.BaseAtlas,
            definition.RaceSwimRow,
            definition.RaceSwimFrameCount,
            definition.RaceSwimFps,
            definition.FrameWidth,
            definition.FrameHeight);
        return frames;
    }

    private static SpriteFrames CreateEmptyFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        return frames;
    }

    private static void AddAnimation(
        SpriteFrames frames,
        StringName name,
        Texture2D atlas,
        int row,
        int frameCount,
        double fps,
        int frameWidth,
        int frameHeight)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, true);
        frames.SetAnimationSpeed(name, fps);
        for (var column = 0; column < frameCount; column++)
        {
            frames.AddFrame(
                name,
                CreateAtlasFrame(atlas, column, row, frameWidth, frameHeight));
        }
    }

    private static AtlasTexture CreateAtlasFrame(
        Texture2D atlas,
        int column,
        int row,
        int frameWidth,
        int frameHeight)
        => new()
        {
            Atlas = atlas,
            Region = new Rect2(
                column * frameWidth,
                row * frameHeight,
                frameWidth,
                frameHeight)
        };
}
