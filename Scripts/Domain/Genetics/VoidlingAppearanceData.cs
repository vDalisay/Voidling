using System;
using System.Collections.Generic;
using System.Linq;

namespace VoidlingGame;

/// <summary>
/// Stable semantic appearance state. Saves/network payloads may carry these IDs and color values,
/// but never Godot resource paths, atlas coordinates, source palette colors or layer textures.
/// Presentation resolves the recipe through the Voidling visual catalog.
/// </summary>
public sealed class VoidlingAppearanceData
{
    public const string DefaultVisualTypeId = "normal";
    public const float LegacyUninitializedPaletteHue = -1.0f;
    public const char LayerSeparator = '|';
    public const int MaxVisualTypeIdLength = 64;
    public const int MaxLayerCount = 16;
    public const int MaxLayerIdLength = 128;

    /// <summary>
    /// Semantic body/morphology family such as normal, water or power. The production visual
    /// catalog decides which sprite definition represents this ID.
    /// </summary>
    public string VisualTypeId { get; set; } = DefaultVisualTypeId;

    /// <summary>
    /// Resolved palette anchor in turns [0,1). Exactly -1 is the legacy/uninitialized sentinel and
    /// is deterministically reconstructed from color DNA during save migration.
    /// </summary>
    public float PaletteHue { get; set; } = LegacyUninitializedPaletteHue;

    /// <summary>
    /// Optional semantic layer selections (for example a wing or crystal variation). Empty means
    /// use the visual type's developer-authored default layer set.
    /// </summary>
    public List<string> LayerIds { get; set; } = new();

    public static bool IsValidHue(float value)
        => float.IsFinite(value) && value >= 0.0f && value < 1.0f;

    public static bool IsValidStoredHue(float value)
        => value == LegacyUninitializedPaletteHue || IsValidHue(value);

    public static bool IsValidSemanticId(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Contains('/') || value.Contains('\\'))
        {
            return false;
        }

        return value.All(character => !char.IsControl(character));
    }

    public static float NormalizeHue(float value)
    {
        if (!float.IsFinite(value))
            return 0.0f;
        value %= 1.0f;
        return value < 0.0f ? value + 1.0f : value;
    }

    public static string BuildLayerIdsKey(IEnumerable<string>? layerIds)
    {
        if (layerIds == null)
            return string.Empty;
        return string.Join(
            LayerSeparator,
            layerIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
    }

    public static string[] ParseLayerIdsKey(string? key)
        => string.IsNullOrWhiteSpace(key)
            ? Array.Empty<string>()
            : key.Split(
                LayerSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Returns a safe canonical copy for read/projection paths. Invalid semantic IDs are discarded
    /// rather than leaking resource-like strings into snapshots, and the source object is untouched.
    /// </summary>
    public VoidlingAppearanceData CreateCanonicalCopy()
    {
        var visualTypeId = IsValidSemanticId(VisualTypeId, MaxVisualTypeIdLength)
            ? VisualTypeId.Trim().ToLowerInvariant()
            : DefaultVisualTypeId;
        var paletteHue = IsValidHue(PaletteHue)
            ? NormalizeHue(PaletteHue)
            : LegacyUninitializedPaletteHue;
        var layers = (LayerIds ?? new List<string>())
            .Where(id => IsValidSemanticId(id, MaxLayerIdLength))
            .Take(MaxLayerCount)
            .ToArray();

        return new VoidlingAppearanceData
        {
            VisualTypeId = visualTypeId,
            PaletteHue = paletteHue,
            LayerIds = ParseLayerIdsKey(BuildLayerIdsKey(layers)).ToList()
        };
    }

    public void Normalize()
    {
        VisualTypeId = string.IsNullOrWhiteSpace(VisualTypeId)
            ? DefaultVisualTypeId
            : VisualTypeId.Trim().ToLowerInvariant();
        PaletteHue = IsValidHue(PaletteHue)
            ? NormalizeHue(PaletteHue)
            : LegacyUninitializedPaletteHue;
        LayerIds ??= new List<string>();
        LayerIds = ParseLayerIdsKey(BuildLayerIdsKey(LayerIds)).ToList();
    }
}
