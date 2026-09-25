using System;
using System.Collections.Generic;
using Voidling.Presentation.UI.Common;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class PaletteSwapTests
{
    private static byte[] Pixels(params (int Colour, byte Alpha)[] pixels)
    {
        var rgba = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            rgba[i * 4] = (byte)(pixels[i].Colour >> 16);
            rgba[i * 4 + 1] = (byte)(pixels[i].Colour >> 8);
            rgba[i * 4 + 2] = (byte)pixels[i].Colour;
            rgba[i * 4 + 3] = pixels[i].Alpha;
        }
        return rgba;
    }

    [Fact]
    public void Apply_ReplacesListedColoursAndKeepsAlpha()
    {
        var result = PaletteSwap.Apply(Pixels((0xDCB98A, 255), (0xDCB98A, 128)),
            new Dictionary<int, int> { [0xDCB98A] = 0xE8CFA6 });

        Assert.Equal(Pixels((0xE8CFA6, 255), (0xE8CFA6, 128)), result);
    }

    [Fact]
    public void Apply_SwapsAllAtOnceRatherThanChaining()
    {
        // One step lighter along a ramp: the darker shade must not run on to the lightest.
        var swaps = new Dictionary<int, int> { [0xDCB98A] = 0xE8CFA6, [0xE8CFA6] = 0xF3E5C2 };

        var result = PaletteSwap.Apply(Pixels((0xDCB98A, 255), (0xE8CFA6, 255)), swaps);

        Assert.Equal(Pixels((0xE8CFA6, 255), (0xF3E5C2, 255)), result);
    }

    [Fact]
    public void Apply_LeavesUnlistedAndTransparentPixelsAlone()
    {
        var source = Pixels((0x90625D, 255), (0xDCB98A, 0));

        var result = PaletteSwap.Apply(source, new Dictionary<int, int> { [0xDCB98A] = 0xE8CFA6 });

        Assert.Equal(source, result);
        Assert.NotSame(source, result);
    }

    [Fact]
    public void Apply_RejectsPartialPixels()
        => Assert.Throws<ArgumentException>(() => PaletteSwap.Apply(new byte[6], new Dictionary<int, int>()));
}
