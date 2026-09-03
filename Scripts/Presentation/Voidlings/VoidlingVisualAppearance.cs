using System;
using System.Collections.Generic;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Presentation-safe semantic appearance snapshot. It contains no texture paths or atlas data and
/// can be constructed from persistent/local/network appearance state before resolving the catalog.
/// </summary>
public readonly record struct VoidlingVisualAppearance(
    string VisualTypeId,
    float PaletteHue,
    IReadOnlyList<string> LayerIds,
    string FallbackTintHex)
{
    public static VoidlingVisualAppearance From(VoidlingAppearanceData? appearance, string fallbackTintHex)
    {
        appearance ??= new VoidlingAppearanceData();
        var typeId = string.IsNullOrWhiteSpace(appearance.VisualTypeId)
            ? VoidlingAppearanceData.DefaultVisualTypeId
            : appearance.VisualTypeId;
        var layers = appearance.LayerIds == null
            ? Array.Empty<string>()
            : new List<string>(appearance.LayerIds).ToArray();
        return new VoidlingVisualAppearance(typeId, appearance.PaletteHue, layers, fallbackTintHex);
    }
}
