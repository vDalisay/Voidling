using Godot;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// The island's shore as the sea meets it: how tall the earth cliff under a south-facing edge is,
/// and the cliff face itself, composed from the premium Hills ledge's block courses (rows 39-45) with
/// alternate courses offset half a block like laid stone. The pack only ships a 16px ledge, so a
/// taller face is built from its own pixels rather than drawn from scratch.
/// </summary>
public static class GardenCoast
{
    /// <summary>World pixels from the grass edge down to the water: the sea's level below the land.</summary>
    public const float CliffHeight = 12.0f;

    private const string CliffSheet = GardenAtmosphereAssets.Premium + "Tilesets/ground tiles/Old tiles/Hills.png";

    private static Texture2D? _cliffFace;

    public static Texture2D CliffFace => _cliffFace ??= ComposeCliffFace();

    private static Texture2D ComposeCliffFace()
    {
        const int courseTop = 39;
        const int courseHeight = 7;
        var sheet = GardenAtmosphereAssets.Sheet(CliffSheet);
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
