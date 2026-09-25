using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Racing;

/// <summary>
/// The Sprout Lands tiles the race is built from, sliced once into standalone textures.
///
/// Standalone textures are what make tiled <c>DrawTextureRect</c> and UV-mapped polygons work: an
/// AtlasTexture region repeats its whole atlas. Where the packs only ship a short piece (the cliff is
/// one 16px ledge), the taller surface is composed here from the pack's own pixels rather than
/// painted from scratch, so it keeps the pack's palette and shading.
/// </summary>
internal static class RaceTrackTiles
{
    private static readonly Dictionary<string, Image> SheetCache = new(StringComparer.Ordinal);

    internal const string Basic = "res://Assets/Sprout Lands - Sprites - Basic pack/";
    internal const string Premium = "res://Assets/Sprout Lands - Sprites - premium pack/";
    internal const string EarlyAccess = "res://Assets/Sprout Sorry pack/Early Access/";

    private const string GrassSheet = Premium + "Tilesets/ground tiles/New tiles/Grass_tiles_v2.png";
    private const string GrassHillSheet = Premium + "Tilesets/ground tiles/New tiles/Grass_Hill_Tiles_v2.png";
    private const string SoilSheet = Premium + "Tilesets/ground tiles/New tiles/Soil_Ground_Tiles.png";
    private const string CliffSheet = Premium + "Tilesets/ground tiles/Old tiles/Hills.png";
    private const string BridgeSheet = Premium + "Tilesets/Building parts/Wooden_Bridge_v2.png";
    private const string FenceSheet = Premium + "Tilesets/Building parts/Fences.png";
    private const string TreeSheet = Premium + "Objects/Trees, stumps and bushes.png";
    private const string NatureSheet = Premium + "Objects/Mushrooms, Flowers, Stones.png";
    private const string WaterObjectSheet = Premium + "Objects/Water Objects.png";
    private const string WaterPlantSheet = EarlyAccess + "Plant update 2/Birch wood Biom water plants.png";
    private const string FishSheet = EarlyAccess + "Ocean Pack/Fish Sprites.png";
    private const string SplashSheet = EarlyAccess + "Ocean Pack/fishing water splash frames and rod.png";
    private const string SignSheet = Premium + "Objects/signs.png";

    // Ground.
    internal static readonly Texture2D Grass = Slice(GrassSheet, 0, 80, 16, 16);
    internal static readonly Texture2D GrassTufts = Slice(GrassSheet, 0, 96, 16, 16);
    internal static readonly Texture2D GrassFlowers = Slice(GrassSheet, 80, 80, 16, 16);
    internal static readonly Texture2D GrassMoss = Slice(GrassSheet, 48, 80, 16, 16);

    // Dirt racing surface: (1,0) top fringe, (1,1) body, (1,2) bottom fringe of the rounded blob.
    internal static readonly Texture2D DirtTop = Slice(SoilSheet, 16, 0, 16, 16);
    internal static readonly Texture2D DirtBottom = Slice(SoilSheet, 16, 32, 16, 16);
    internal static readonly Texture2D DirtFill = Slice(SoilSheet, 0, 80, 16, 16);
    internal static readonly Texture2D DirtWorn = Slice(SoilSheet, 32, 80, 16, 16);

    // Bank edges: the land-side lip that meets the water. "Right" ends on its right side.
    internal static readonly Texture2D GrassBankRight = Slice(GrassHillSheet, 32, 16, 16, 16);
    internal static readonly Texture2D GrassBankLeft = Slice(GrassHillSheet, 0, 16, 16, 16);
    internal static readonly Texture2D DirtBankRight = Slice(SoilSheet, 32, 16, 16, 16);
    internal static readonly Texture2D DirtBankLeft = Slice(SoilSheet, 0, 16, 16, 16);

    /// <summary>
    /// Earth cliff of any height, composed from the premium ledge's block courses (rows 39-45),
    /// alternate courses offset half a block like laid stone. The ledge alone is only 16px tall.
    /// </summary>
    internal static readonly Texture2D CliffFace = ComposeCliffFace();
    internal static readonly Texture2D CliffFoot = Slice(CliffSheet, 16, 44, 16, 4);
    internal static readonly Texture2D CliffOverhang = Slice(CliffSheet, 16, 32, 16, 8);

    // Wooden take-off ramp deck: rows 33-46 of the bridge span, which tile without seams.
    internal static readonly Texture2D Planks = Slice(BridgeSheet, 0, 33, 16, 14);

    // Premium fences: column 0 is a vertical run (top, middle, bottom); row 0 columns 1-3 are the
    // left, middle and right pieces of a horizontal run.
    internal static readonly Texture2D FenceVerticalTop = Slice(FenceSheet, 0, 0, 16, 16);
    internal static readonly Texture2D FenceVerticalMid = Slice(FenceSheet, 0, 16, 16, 16);
    internal static readonly Texture2D FenceVerticalBottom = Slice(FenceSheet, 0, 32, 16, 16);
    internal static readonly Texture2D FencePost = Slice(FenceSheet, 0, 48, 16, 16);
    internal static readonly Texture2D FenceRail = Slice(FenceSheet, 32, 0, 16, 16);
    internal static readonly Texture2D FenceRailLeft = Slice(FenceSheet, 16, 0, 16, 16);
    internal static readonly Texture2D FenceRailRight = Slice(FenceSheet, 48, 0, 16, 16);

    // Roadside dressing.
    internal static readonly Texture2D[] Trees =
    {
        Slice(TreeSheet, 0, 0, 16, 32),
        Slice(TreeSheet, 16, 0, 32, 32),
        Slice(TreeSheet, 48, 0, 32, 32),
        Slice(TreeSheet, 80, 0, 32, 32),
        Slice(TreeSheet, 144, 48, 48, 48)
    };
    internal static readonly Texture2D[] Bushes =
    {
        Slice(TreeSheet, 0, 48, 16, 16),
        Slice(TreeSheet, 16, 48, 16, 16),
        Slice(TreeSheet, 32, 48, 16, 16),
        Slice(TreeSheet, 48, 48, 16, 16)
    };
    internal static readonly Texture2D[] Stumps =
    {
        Slice(TreeSheet, 0, 96, 16, 16),
        Slice(TreeSheet, 32, 96, 32, 16)
    };
    internal static readonly Texture2D[] Flowers =
    {
        Slice(NatureSheet, 0, 48, 16, 16),
        Slice(NatureSheet, 64, 48, 16, 16),
        Slice(NatureSheet, 80, 48, 16, 16),
        Slice(NatureSheet, 128, 48, 16, 16),
        Slice(NatureSheet, 144, 48, 16, 16),
        Slice(NatureSheet, 0, 64, 16, 16),
        Slice(NatureSheet, 64, 64, 16, 16),
        Slice(NatureSheet, 128, 64, 16, 16)
    };
    internal static readonly Texture2D[] Stones =
    {
        Slice(NatureSheet, 0, 16, 16, 16),
        Slice(NatureSheet, 16, 16, 16, 16),
        Slice(NatureSheet, 32, 16, 16, 16),
        Slice(NatureSheet, 48, 16, 16, 16)
    };
    internal static readonly Texture2D[] GrassClumps =
    {
        Slice(NatureSheet, 32, 32, 16, 16),
        Slice(NatureSheet, 48, 32, 16, 16)
    };

    // Water dressing.
    internal static readonly Texture2D[] Water =
    {
        Load(Premium + "Tilesets/ground tiles/water frames/Water_1.png"),
        Load(Premium + "Tilesets/ground tiles/water frames/Water_2.png"),
        Load(Premium + "Tilesets/ground tiles/water frames/Water_3.png"),
        Load(Premium + "Tilesets/ground tiles/water frames/Water_4.png")
    };
    internal static readonly Texture2D[] WaterRocks =
    {
        Slice(WaterObjectSheet, 0, 0, 16, 16),
        Slice(WaterObjectSheet, 16, 0, 16, 16),
        Slice(WaterObjectSheet, 32, 0, 16, 16),
        Slice(WaterObjectSheet, 48, 0, 16, 16)
    };
    internal static readonly Texture2D[] LilyPads =
    {
        Slice(WaterObjectSheet, 128, 0, 16, 16),
        Slice(WaterObjectSheet, 144, 0, 16, 16),
        Slice(WaterObjectSheet, 160, 0, 16, 16),
        Slice(WaterPlantSheet, 112, 16, 16, 16),
        Slice(WaterPlantSheet, 128, 16, 16, 16)
    };
    internal static readonly Texture2D[] Reeds =
    {
        Slice(WaterPlantSheet, 0, 0, 16, 16),
        Slice(WaterPlantSheet, 16, 0, 16, 16),
        Slice(WaterPlantSheet, 32, 0, 16, 16),
        Slice(WaterObjectSheet, 96, 0, 16, 16),
        Slice(WaterObjectSheet, 112, 0, 16, 16)
    };
    internal static readonly Texture2D[] Fish =
    {
        Slice(FishSheet, 96, 0, 16, 16),
        Slice(FishSheet, 32, 32, 16, 16),
        Slice(FishSheet, 96, 48, 16, 16),
        Slice(FishSheet, 80, 64, 16, 16)
    };

    /// <summary>Premium splash frames: the droplet burst a fishing float throws up, five frames.</summary>
    internal static readonly Texture2D[] Splash =
    {
        Slice(SplashSheet, 0, 128, 32, 20),
        Slice(SplashSheet, 32, 128, 32, 20),
        Slice(SplashSheet, 64, 128, 32, 20),
        Slice(SplashSheet, 96, 128, 32, 20),
        Slice(SplashSheet, 128, 128, 32, 20)
    };

    internal static readonly Texture2D BlankSign = Slice(SignSheet, 0, 0, 16, 16);

    internal static Texture2D Load(string path)
        => GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Race track art '{path}' is missing.");

    /// <summary>
    /// Cuts one region out of a tilesheet as a standalone texture.
    /// </summary>
    internal static Texture2D Slice(string path, int x, int y, int width, int height)
        => ImageTexture.CreateFromImage(Sheet(path).GetRegion(new Rect2I(x, y, width, height)));

    private static Image Sheet(string path)
    {
        if (SheetCache.TryGetValue(path, out var image))
            return image;

        image = Load(path).GetImage()
            ?? throw new InvalidOperationException($"Race track art '{path}' has no readable image.");
        if (image.IsCompressed())
            image.Decompress();
        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);
        SheetCache[path] = image;
        return image;
    }

    private static Texture2D ComposeCliffFace()
    {
        const int courseTop = 39;
        const int courseHeight = 7;
        var sheet = Sheet(CliffSheet);
        var face = Image.CreateEmpty(32, courseHeight * 2, false, Image.Format.Rgba8);
        for (var course = 0; course < 2; course++)
        {
            var shift = course * 8;
            for (var x = 0; x < 32; x++)
            {
                var sourceX = 16 + (x + shift) % 16;
                for (var y = 0; y < courseHeight; y++)
                    face.SetPixel(x, course * courseHeight + y, sheet.GetPixel(sourceX, courseTop + y));
            }
        }

        return ImageTexture.CreateFromImage(face);
    }
}
