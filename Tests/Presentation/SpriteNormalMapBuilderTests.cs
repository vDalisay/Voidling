using System;
using Voidling.Presentation.Lighting;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class SpriteNormalMapBuilderTests
{
    private const int Size = 32;

    // A 20x20 opaque square centred in a 32x32 transparent image.
    private static byte[] Square(int size = Size, int from = 6, int to = 26, byte shade = 160)
    {
        var rgba = new byte[size * size * 4];
        for (var y = from; y < to; y++)
        {
            for (var x = from; x < to; x++)
            {
                var i = (y * size + x) * 4;
                rgba[i] = shade;
                rgba[i + 1] = shade;
                rgba[i + 2] = shade;
                rgba[i + 3] = 255;
            }
        }
        return rgba;
    }

    private static (int R, int G, int B) NormalAt(byte[] normals, int x, int y, int width = Size)
    {
        var i = (y * width + x) * 4;
        return (normals[i], normals[i + 1], normals[i + 2]);
    }

    [Fact]
    public void Build_TransparentPixelsFaceTheViewer()
    {
        var normals = SpriteNormalMapBuilder.Build(Square(), Size, Size, Size, Size);

        Assert.Equal((128, 128, 255), NormalAt(normals, 1, 1));
    }

    [Fact]
    public void Build_EdgesLeanOutwards()
    {
        var normals = SpriteNormalMapBuilder.Build(Square(), Size, Size, Size, Size);

        Assert.True(NormalAt(normals, 6, 16).R < 100, "Left edge should face left.");
        Assert.True(NormalAt(normals, 25, 16).R > 156, "Right edge should face right.");
        // OpenGL convention: green up.
        Assert.True(NormalAt(normals, 16, 6).G > 156, "Top edge should face up.");
        Assert.True(NormalAt(normals, 16, 25).G < 100, "Bottom edge should face down.");
    }

    [Fact]
    public void Build_TheMiddleOfALargeShapeFacesTheViewer()
    {
        var normals = SpriteNormalMapBuilder.Build(Square(), Size, Size, Size, Size);
        var (r, g, b) = NormalAt(normals, 16, 16);

        Assert.InRange(r, 120, 136);
        Assert.InRange(g, 120, 136);
        Assert.True(b > 240);
    }

    [Fact]
    public void Build_RoundsGraduallyFromTheEdgeInwards()
    {
        var normals = SpriteNormalMapBuilder.Build(Square(), Size, Size, Size, Size);

        // Tilt eases off moving in from the left edge towards the middle.
        var edge = 128 - NormalAt(normals, 6, 16).R;
        var inside = 128 - NormalAt(normals, 8, 16).R;
        var deeper = 128 - NormalAt(normals, 11, 16).R;
        Assert.True(edge > inside && inside > deeper && deeper >= 0);
    }

    [Fact]
    public void Build_KeepsEachFrameToItself()
    {
        // Two frames side by side, each filled edge to edge: without per-frame handling the seam
        // between them would read as the flat middle of one wide shape.
        const int frame = 16;
        var rgba = new byte[frame * 2 * frame * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = rgba[i + 1] = rgba[i + 2] = 160;
            rgba[i + 3] = 255;
        }

        var normals = SpriteNormalMapBuilder.Build(rgba, frame * 2, frame, frame, frame);

        Assert.True(NormalAt(normals, frame - 1, 8, frame * 2).R > 156, "Left frame's right edge should face right.");
        Assert.True(NormalAt(normals, frame, 8, frame * 2).R < 100, "Right frame's left edge should face left.");
    }

    [Fact]
    public void Build_IgnoresTheArtworksOwnShading()
    {
        // A dark stroke drawn across a light shape is shading, not a dent: the map matches a plain square.
        var rgba = Square(shade: 230);
        for (var y = 6; y < 26; y++)
        {
            var i = (y * Size + 16) * 4;
            rgba[i] = rgba[i + 1] = rgba[i + 2] = 20;
        }

        var normals = SpriteNormalMapBuilder.Build(rgba, Size, Size, Size, Size);

        Assert.Equal(SpriteNormalMapBuilder.Build(Square(shade: 230), Size, Size, Size, Size), normals);
    }

    [Fact]
    public void Build_OnePixelWideDetailStaysFlat()
    {
        // A single-pixel stalk has nothing to round across, so it is lit like the ground behind it.
        var rgba = new byte[Size * Size * 4];
        for (var y = 4; y < 28; y++)
        {
            var i = (y * Size + 16) * 4;
            rgba[i] = rgba[i + 1] = rgba[i + 2] = 160;
            rgba[i + 3] = 255;
        }

        var normals = SpriteNormalMapBuilder.Build(rgba, Size, Size, Size, Size);

        Assert.Equal((128, 128, 255), NormalAt(normals, 16, 16));
    }

    [Fact]
    public void Build_RejectsMismatchedData()
        => Assert.Throws<ArgumentException>(() => SpriteNormalMapBuilder.Build(new byte[8], 4, 4, 4, 4));
}
