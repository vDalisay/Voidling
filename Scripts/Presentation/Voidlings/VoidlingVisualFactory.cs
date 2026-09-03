using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Canonical construction path for Voidling sprites, animation frames, portraits, palette swaps,
/// composited layers and art-dependent geometry. Consumers use semantic type/layer/color state and
/// never know texture paths, source palette slots, frame rows or atlas dimensions.
/// </summary>
public static class VoidlingVisualFactory
{
    public const string CatalogPath =
        "res://Resources/Presentation/Voidlings/DefaultVoidlingVisualCatalog.tres";
    public const string PaletteShaderPath =
        "res://Resources/Presentation/Voidlings/VoidlingPaletteSwap.gdshader";

    private const int MaxPaletteSlots = 8;

    private static readonly VoidlingVisualCatalog Catalog = LoadCatalog();
    private static readonly IReadOnlyDictionary<string, VoidlingVisualDefinition> Definitions =
        BuildDefinitionMap(Catalog);
    private static readonly Dictionary<string, SpriteFrames> WorldFrames = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SpriteFrames> RaceFrames = new(StringComparer.Ordinal);
    private static readonly Shader PaletteShader = GD.Load<Shader>(PaletteShaderPath)
        ?? throw new InvalidOperationException($"Voidling palette shader could not be loaded from {PaletteShaderPath}.");

    private static VoidlingVisualDefinition DefaultDefinition => ResolveDefinition(Catalog.DefaultVisualTypeId);

    public static string DefinitionId => DefaultDefinition.DefinitionId;
    public static IReadOnlyCollection<string> VisualTypeIds => Definitions.Keys.ToArray();
    public static int FrameWidth => DefaultDefinition.FrameWidth;
    public static int FrameHeight => DefaultDefinition.FrameHeight;
    public static float AdultWorldScale => DefaultDefinition.AdultWorldScale;
    public static float RaceScale => DefaultDefinition.RaceScale;
    public static float HeldScaleMultiplier => DefaultDefinition.HeldScaleMultiplier;
    public static float HeldSpriteYOffset => DefaultDefinition.HeldSpriteYOffset;
    public static float MutationAdultCenterYOffset => DefaultDefinition.MutationAdultCenterYOffset;
    public static float MutationCompactCenterYOffset => DefaultDefinition.MutationCompactCenterYOffset;
    public static float MutationCompactScaleThreshold => DefaultDefinition.MutationCompactScaleThreshold;
    public static float PortraitMutationCompactPixelThreshold => DefaultDefinition.PortraitMutationCompactPixelThreshold;

    public static VoidlingVisualDefinition ResolveDefinition(string? visualTypeId)
    {
        if (!string.IsNullOrWhiteSpace(visualTypeId) &&
            Definitions.TryGetValue(visualTypeId.Trim().ToLowerInvariant(), out var definition))
        {
            return definition;
        }

        if (Definitions.TryGetValue(Catalog.DefaultVisualTypeId, out var fallback))
            return fallback;

        throw new InvalidOperationException(
            $"Voidling visual catalog has no default definition '{Catalog.DefaultVisualTypeId}'.");
    }

    public static SpriteFrames GetWorldFrames(string? visualTypeId = null)
    {
        var definition = ResolveDefinition(visualTypeId);
        if (!WorldFrames.TryGetValue(definition.DefinitionId, out var frames))
        {
            frames = BuildWorldFrames(definition, definition.BaseAtlas);
            WorldFrames.Add(definition.DefinitionId, frames);
        }
        return frames;
    }

    public static SpriteFrames GetRaceFrames(string? visualTypeId = null)
    {
        var definition = ResolveDefinition(visualTypeId);
        if (!RaceFrames.TryGetValue(definition.DefinitionId, out var frames))
        {
            frames = BuildRaceFrames(definition, definition.BaseAtlas, definition.SwimAtlas ?? definition.BaseAtlas);
            RaceFrames.Add(definition.DefinitionId, frames);
        }
        return frames;
    }

    public static float WorldScale(bool adult, string? visualTypeId = null)
    {
        var definition = ResolveDefinition(visualTypeId);
        return adult ? definition.AdultWorldScale : definition.ChildWorldScale;
    }

    public static Vector2 WorldHitboxSize(bool adult, string? visualTypeId = null)
    {
        var definition = ResolveDefinition(visualTypeId);
        return adult ? definition.AdultHitboxSize : definition.ChildHitboxSize;
    }

    public static float WorldSpriteCenterYOffset(float spriteScale, string? visualTypeId = null)
        => ResolveDefinition(visualTypeId).WorldSpriteCenterYOffsetAtScaleOne * spriteScale;

    public static float RaceScaleFor(string? visualTypeId)
        => ResolveDefinition(visualTypeId).RaceScale;

    // The race sprite sits on its ground pivot exactly like the Garden does, so the shared shadow
    // offset lands under the same feet at either scale instead of drifting into a floating gap.
    public static float RaceSpriteCenterYOffset(string? visualTypeId = null)
    {
        var definition = ResolveDefinition(visualTypeId);
        return definition.WorldSpriteCenterYOffsetAtScaleOne * definition.RaceScale;
    }

    public static float ShadowCenterYOffset(float spriteScale, string? visualTypeId = null)
        => ResolveDefinition(visualTypeId).ShadowCenterYOffsetAtScaleOne * spriteScale;

    public static Vector2 ShadowRadii(float spriteScale, string? visualTypeId = null)
    {
        var definition = ResolveDefinition(visualTypeId);
        var referenceScale = Math.Max(0.001f, definition.AdultWorldScale);
        return definition.AdultShadowRadii * (spriteScale / referenceScale);
    }

    public static Vector2[] BuildShadowPolygon(float spriteScale, int points = 20, string? visualTypeId = null)
    {
        if (points < 3)
            throw new ArgumentOutOfRangeException(nameof(points), "A shadow polygon needs at least three points.");

        var radii = ShadowRadii(spriteScale, visualTypeId);
        var polygon = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            polygon[i] = new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
        }
        return polygon;
    }

    public static Texture2D CreatePortraitTexture(string? visualTypeId = null)
    {
        var definition = ResolveDefinition(visualTypeId);
        return CreateAtlasFrame(
            definition.BaseAtlas,
            definition.PortraitColumn,
            definition.PortraitRow,
            definition.FrameWidth,
            definition.FrameHeight);
    }

    /// <summary>
    /// Applies the complete semantic appearance recipe to an existing base AnimatedSprite2D.
    /// Layer sprites are children of the base sprite so transforms stay aligned; a sync helper mirrors
    /// animation/frame selection without requiring Garden/Race code to know any layer details.
    /// </summary>
    public static void ApplyAppearance(
        AnimatedSprite2D sprite,
        VoidlingVisualAppearance appearance,
        bool race)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        var definition = ResolveDefinition(appearance.VisualTypeId);
        sprite.SpriteFrames = race ? GetRaceFrames(definition.DefinitionId) : GetWorldFrames(definition.DefinitionId);
        ApplyPaletteOrFallback(
            sprite,
            definition.SourcePaletteColors,
            appearance.PaletteHue,
            definition.PaletteMatchTolerance,
            appearance.FallbackTintHex);

        var existing = sprite.GetNodeOrNull<VoidlingVisualLayerSync2D>("__voidling_layers");
        if (existing != null && GodotObject.IsInstanceValid(existing))
            existing.Free();

        var layers = new VoidlingVisualLayerSync2D { Name = "__voidling_layers" };
        layers.Setup(sprite, definition, appearance, race);
        sprite.AddChild(layers);
    }

    internal static IReadOnlyList<VoidlingVisualLayerDefinition> ResolveLayers(
        VoidlingVisualDefinition definition,
        IReadOnlyList<string> requestedLayerIds)
    {
        var available = definition.Layers
            .Where(layer => layer != null && !string.IsNullOrWhiteSpace(layer.LayerId))
            .ToDictionary(layer => layer.LayerId, StringComparer.OrdinalIgnoreCase);
        var selectedBySlot = new Dictionary<string, VoidlingVisualLayerDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var defaultId in definition.DefaultLayerIds)
        {
            if (available.TryGetValue(defaultId, out var layer))
                selectedBySlot[layer.SlotId] = layer;
        }

        foreach (var requestedId in requestedLayerIds ?? Array.Empty<string>())
        {
            if (available.TryGetValue(requestedId, out var layer))
                selectedBySlot[layer.SlotId] = layer;
        }

        return selectedBySlot.Values
            .OrderBy(layer => layer.ZIndexOffset)
            .ThenBy(layer => layer.LayerId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static SpriteFrames CreateLayerFrames(
        VoidlingVisualDefinition definition,
        VoidlingVisualLayerDefinition layer,
        bool race)
    {
        var baseAtlas = layer.BaseAtlas;
        if (baseAtlas == null)
            throw new InvalidOperationException($"Voidling layer '{layer.LayerId}' has no BaseAtlas.");
        return race
            ? BuildRaceFrames(definition, baseAtlas, layer.SwimAtlas ?? baseAtlas)
            : BuildWorldFrames(definition, baseAtlas);
    }

    internal static void ApplyLayerPalette(
        CanvasItem item,
        VoidlingVisualDefinition definition,
        VoidlingVisualLayerDefinition layer,
        VoidlingVisualAppearance appearance)
    {
        if (!layer.PaletteAffected)
        {
            item.Material = null;
            item.Modulate = Colors.White;
            return;
        }

        var colors = layer.SourcePaletteColors.Count > 0
            ? layer.SourcePaletteColors
            : definition.SourcePaletteColors;
        ApplyPaletteOrFallback(
            item,
            colors,
            appearance.PaletteHue,
            definition.PaletteMatchTolerance,
            appearance.FallbackTintHex);
    }

    private static VoidlingVisualCatalog LoadCatalog()
    {
        var catalog = GD.Load<VoidlingVisualCatalog>(CatalogPath)
            ?? throw new InvalidOperationException($"Voidling visual catalog could not be loaded from {CatalogPath}.");
        Validate(catalog);
        return catalog;
    }

    private static IReadOnlyDictionary<string, VoidlingVisualDefinition> BuildDefinitionMap(VoidlingVisualCatalog catalog)
        => catalog.Definitions.ToDictionary(
            definition => definition.DefinitionId.Trim().ToLowerInvariant(),
            definition => definition,
            StringComparer.Ordinal);

    private static void Validate(VoidlingVisualCatalog catalog)
    {
        if (catalog.Definitions == null || catalog.Definitions.Count == 0)
            throw new InvalidOperationException("Voidling visual catalog requires at least one definition.");
        if (string.IsNullOrWhiteSpace(catalog.DefaultVisualTypeId))
            throw new InvalidOperationException("Voidling visual catalog requires a default semantic type ID.");

        catalog.DefaultVisualTypeId = catalog.DefaultVisualTypeId.Trim().ToLowerInvariant();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in catalog.Definitions)
        {
            ValidateDefinition(definition);
            var id = definition.DefinitionId.Trim().ToLowerInvariant();
            definition.DefinitionId = id;
            if (!ids.Add(id))
                throw new InvalidOperationException($"Duplicate Voidling visual definition ID '{id}'.");
        }

        if (!ids.Contains(catalog.DefaultVisualTypeId))
            throw new InvalidOperationException(
                $"Voidling default type '{catalog.DefaultVisualTypeId}' has no catalog definition.");
    }

    private static void ValidateDefinition(VoidlingVisualDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId))
            throw new InvalidOperationException("Voidling visual DefinitionId cannot be empty.");
        if (definition.BaseAtlas == null)
            throw new InvalidOperationException($"Voidling visual '{definition.DefinitionId}' requires BaseAtlas.");
        if (definition.FrameWidth <= 0 || definition.FrameHeight <= 0)
            throw new InvalidOperationException("Voidling frame dimensions must be positive.");
        if (definition.WorldFrameCount <= 0 || definition.RaceRunFrameCount <= 0 || definition.RaceSwimFrameCount <= 0)
            throw new InvalidOperationException("Voidling animation frame counts must be positive.");
        if (definition.PortraitColumn < 0 || definition.PortraitRow < 0)
            throw new InvalidOperationException("Voidling portrait coordinates cannot be negative.");
        if (definition.AdultWorldScale <= 0.0f || definition.ChildWorldScale <= 0.0f || definition.RaceScale <= 0.0f)
            throw new InvalidOperationException("Voidling presentation scales must be positive.");
        if (definition.AdultShadowRadii.X <= 0.0f || definition.AdultShadowRadii.Y <= 0.0f)
            throw new InvalidOperationException("Voidling shadow radii must be positive.");
        if (definition.AdultHitboxSize.X <= 0.0f || definition.AdultHitboxSize.Y <= 0.0f ||
            definition.ChildHitboxSize.X <= 0.0f || definition.ChildHitboxSize.Y <= 0.0f)
            throw new InvalidOperationException("Voidling hitbox sizes must be positive.");
        if (definition.HeldScaleMultiplier <= 0.0f)
            throw new InvalidOperationException("Voidling held scale multiplier must be positive.");
        if (definition.SourcePaletteColors.Count > MaxPaletteSlots)
            throw new InvalidOperationException($"Voidling '{definition.DefinitionId}' palette exceeds {MaxPaletteSlots} slots.");

        ValidateAtlasCoverage(
            definition.BaseAtlas,
            definition.FrameWidth,
            definition.FrameHeight,
            definition.WorldFrameCount,
            definition.WalkDownRow,
            definition.WalkUpRow,
            definition.WalkLeftRow,
            definition.WalkRightRow);
        ValidateAtlasCoverage(
            definition.BaseAtlas,
            definition.FrameWidth,
            definition.FrameHeight,
            definition.RaceRunFrameCount,
            definition.RaceRunRow);
        ValidateAtlasCoverage(
            definition.BaseAtlas,
            definition.FrameWidth,
            definition.FrameHeight,
            definition.PortraitColumn + 1,
            definition.PortraitRow);
        ValidateAtlasCoverage(
            definition.SwimAtlas ?? definition.BaseAtlas,
            definition.FrameWidth,
            definition.FrameHeight,
            definition.RaceSwimFrameCount,
            definition.RaceSwimRow);

        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in definition.Layers)
        {
            if (layer == null || string.IsNullOrWhiteSpace(layer.LayerId) || string.IsNullOrWhiteSpace(layer.SlotId))
                throw new InvalidOperationException($"Voidling '{definition.DefinitionId}' has a layer without LayerId/SlotId.");
            if (!layerIds.Add(layer.LayerId))
                throw new InvalidOperationException($"Voidling '{definition.DefinitionId}' has duplicate layer '{layer.LayerId}'.");
            if (layer.BaseAtlas == null)
                throw new InvalidOperationException($"Voidling layer '{layer.LayerId}' requires BaseAtlas.");
            if (layer.ScaleMultiplier <= 0.0f)
                throw new InvalidOperationException($"Voidling layer '{layer.LayerId}' scale must be positive.");
            if (layer.SourcePaletteColors.Count > MaxPaletteSlots)
                throw new InvalidOperationException($"Voidling layer '{layer.LayerId}' palette exceeds {MaxPaletteSlots} slots.");

            ValidateAtlasCoverage(
                layer.BaseAtlas,
                definition.FrameWidth,
                definition.FrameHeight,
                definition.WorldFrameCount,
                definition.WalkDownRow,
                definition.WalkUpRow,
                definition.WalkLeftRow,
                definition.WalkRightRow,
                definition.RaceRunRow);
            ValidateAtlasCoverage(
                layer.SwimAtlas ?? layer.BaseAtlas,
                definition.FrameWidth,
                definition.FrameHeight,
                definition.RaceSwimFrameCount,
                definition.RaceSwimRow);
        }

        foreach (var defaultLayerId in definition.DefaultLayerIds)
        {
            if (!layerIds.Contains(defaultLayerId))
                throw new InvalidOperationException(
                    $"Voidling '{definition.DefinitionId}' default layer '{defaultLayerId}' is not defined.");
        }
    }

    private static void ApplyPaletteOrFallback(
        CanvasItem item,
        Godot.Collections.Array<Color> sourceColors,
        float targetHue,
        float tolerance,
        string fallbackTintHex)
    {
        if (VoidlingAppearanceData.IsValidHue(targetHue) && sourceColors.Count > 0)
        {
            item.Modulate = Colors.White;
            item.Material = CreatePaletteMaterial(sourceColors, targetHue, tolerance);
            return;
        }

        item.Material = null;
        item.Modulate = ParseTint(fallbackTintHex);
    }

    private static ShaderMaterial CreatePaletteMaterial(
        Godot.Collections.Array<Color> sourceColors,
        float targetHue,
        float tolerance)
    {
        var material = new ShaderMaterial { Shader = PaletteShader };
        var count = Math.Min(MaxPaletteSlots, sourceColors.Count);
        material.SetShaderParameter("palette_size", count);
        material.SetShaderParameter("match_tolerance", Math.Clamp(tolerance, 0.0001f, 0.2f));

        var anchorHue = sourceColors.FirstOrDefault(color => color.S > 0.03f).H;
        var delta = ShortestHueDelta(anchorHue, targetHue);
        for (var i = 0; i < MaxPaletteSlots; i++)
        {
            var source = i < count ? sourceColors[i] : Colors.Transparent;
            var target = source;
            if (i < count && source.S > 0.03f)
                target = Color.FromHsv(NormalizeHue(source.H + delta), source.S, source.V, source.A);
            material.SetShaderParameter($"source_{i}", source);
            material.SetShaderParameter($"target_{i}", target);
        }
        return material;
    }

    private static float ShortestHueDelta(float source, float target)
    {
        var delta = NormalizeHue(target) - NormalizeHue(source);
        if (delta > 0.5f)
            delta -= 1.0f;
        else if (delta < -0.5f)
            delta += 1.0f;
        return delta;
    }

    private static float NormalizeHue(float hue)
    {
        hue %= 1.0f;
        return hue < 0.0f ? hue + 1.0f : hue;
    }

    private static Color ParseTint(string tintHex)
    {
        try { return string.IsNullOrWhiteSpace(tintHex) ? Colors.White : Color.FromHtml(tintHex); }
        catch { return Colors.White; }
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

    private static SpriteFrames BuildWorldFrames(VoidlingVisualDefinition definition, Texture2D atlas)
    {
        var frames = CreateEmptyFrames();
        AddAnimation(frames, "walk_down", atlas, definition.WalkDownRow, definition.WorldFrameCount,
            definition.WorldAnimationFps, definition.FrameWidth, definition.FrameHeight);
        AddAnimation(frames, "walk_up", atlas, definition.WalkUpRow, definition.WorldFrameCount,
            definition.WorldAnimationFps, definition.FrameWidth, definition.FrameHeight);
        AddAnimation(frames, "walk_left", atlas, definition.WalkLeftRow, definition.WorldFrameCount,
            definition.WorldAnimationFps, definition.FrameWidth, definition.FrameHeight);
        AddAnimation(frames, "walk_right", atlas, definition.WalkRightRow, definition.WorldFrameCount,
            definition.WorldAnimationFps, definition.FrameWidth, definition.FrameHeight);
        return frames;
    }

    private static SpriteFrames BuildRaceFrames(
        VoidlingVisualDefinition definition,
        Texture2D baseAtlas,
        Texture2D swimAtlas)
    {
        var frames = CreateEmptyFrames();
        AddAnimation(frames, "run", baseAtlas, definition.RaceRunRow, definition.RaceRunFrameCount,
            definition.RaceRunFps, definition.FrameWidth, definition.FrameHeight);
        AddAnimation(frames, "swim", swimAtlas, definition.RaceSwimRow, definition.RaceSwimFrameCount,
            definition.RaceSwimFps, definition.FrameWidth, definition.FrameHeight);
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
            frames.AddFrame(name, CreateAtlasFrame(atlas, column, row, frameWidth, frameHeight));
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
