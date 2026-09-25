using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Lighting;

/// <summary>
/// Pairs a sprite sheet with a normal map generated from its own pixels, as a
/// <see cref="CanvasTexture"/> that Godot's 2D lights shade. Frames cut from the returned texture
/// with <see cref="AtlasTexture"/> carry the normal map with them.
///
/// The map is built once per sheet and frame size and cached. A sheet whose pixels cannot be read
/// (a renderer that keeps no image data) comes back unchanged, so nothing fails without lighting.
/// </summary>
public static class SpriteNormalMaps
{
    private static ShaderMaterial? _litMaterial;

    /// <summary>
    /// The material for a lit sprite that needs no shader of its own: it lights the sprite the same
    /// way the Voidling palette shader does, so an unpaletted accessory is shaded like the body it
    /// sits on and the Garden's mushrooms and placed trees like its own trees. Shared, since it has
    /// no parameters.
    /// </summary>
    public static ShaderMaterial LitMaterial => _litMaterial ??= new ShaderMaterial
    {
        Shader = GD.Load<Shader>("res://Resources/Presentation/Lighting/LitSprite.gdshader")
    };

    private static readonly Dictionary<(ulong Texture, int Width, int Height), Texture2D> Cache = new();

    /// <summary>A lit version of a single sprite.</summary>
    public static Texture2D Lit(Texture2D texture)
        => Lit(texture, texture.GetWidth(), texture.GetHeight());

    /// <summary>A lit version of a sheet of equal frames.</summary>
    public static Texture2D Lit(Texture2D sheet, int frameWidth, int frameHeight)
    {
        if (sheet is CanvasTexture)
            return sheet;

        var key = (sheet.GetInstanceId(), frameWidth, frameHeight);
        if (Cache.TryGetValue(key, out var lit))
            return lit;

        lit = Build(sheet, frameWidth, frameHeight) ?? sheet;
        Cache[key] = lit;
        return lit;
    }

    private static Texture2D? Build(Texture2D sheet, int frameWidth, int frameHeight)
    {
        var image = sheet.GetImage();
        if (image == null || image.IsEmpty())
            return null;

        image = (Image)image.Duplicate();
        if (image.IsCompressed() && image.Decompress() != Error.Ok)
            return null;
        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);

        var width = image.GetWidth();
        var height = image.GetHeight();
        var normals = SpriteNormalMapBuilder.Build(image.GetData(), width, height, frameWidth, frameHeight);
        var normalImage = Image.CreateFromData(width, height, false, Image.Format.Rgba8, normals);
        return new CanvasTexture
        {
            ResourceName = sheet.ResourceName + " (lit)",
            DiffuseTexture = sheet,
            NormalTexture = ImageTexture.CreateFromImage(normalImage)
        };
    }
}
