using System;
using System.Collections.Generic;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Swaps exact colours in RGBA8 pixel data, the way a pixel artist recolours a sprite: every pixel
/// of a listed colour becomes its replacement all at once (so a swap chain like A→B, B→C never
/// turns A into C), alpha is kept, and any colour not listed is left alone. Colours are 0xRRGGBB.
/// Plain C#, so it is tested without the engine.
/// </summary>
public static class PaletteSwap
{
    public static byte[] Apply(byte[] rgba, IReadOnlyDictionary<int, int> swaps)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(swaps);
        if (rgba.Length % 4 != 0)
            throw new ArgumentException("RGBA data must be whole pixels.", nameof(rgba));

        var result = (byte[])rgba.Clone();
        for (var i = 0; i < result.Length; i += 4)
        {
            if (result[i + 3] == 0)
                continue;

            var colour = (result[i] << 16) | (result[i + 1] << 8) | result[i + 2];
            if (!swaps.TryGetValue(colour, out var replacement))
                continue;

            result[i] = (byte)(replacement >> 16);
            result[i + 1] = (byte)(replacement >> 8);
            result[i + 2] = (byte)replacement;
        }

        return result;
    }
}
