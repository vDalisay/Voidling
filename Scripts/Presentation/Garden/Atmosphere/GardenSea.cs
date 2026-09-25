using Godot;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// The sea: one rectangle as large as the camera can ever see, drawn with the sea shader. It owns
/// the shader's inputs (the island field texture, the reflection buffer and the day, weather and
/// wind values) and nothing else.
/// </summary>
public partial class GardenSea : Node2D
{
    private static readonly Shader SeaShader = GD.Load<Shader>(GardenAtmosphereAssets.ShaderRoot + "GardenSea.gdshader");
    private static readonly Texture2D WaterTile = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - premium pack/Tilesets/ground tiles/water frames/Water_1.png");

    /// <summary>Everything the Garden camera is allowed to show, with room to spare.</summary>
    private static readonly Rect2 Extent = new(-2600.0f, -2200.0f, 6000.0f, 4800.0f);

    private ShaderMaterial _material = null!;

    public override void _Ready()
    {
        TextureRepeat = TextureRepeatEnum.Enabled;
        _material = new ShaderMaterial { Shader = SeaShader };
        _material.SetShaderParameter("water_tile", WaterTile);
        Material = _material;
    }

    public override void _Draw() => DrawRect(Extent, Colors.White);

    public void SetField(GardenIslandField field)
    {
        var image = Image.CreateFromData(field.Columns, field.Rows, false, Image.Format.L8, field.EncodeBytes());
        _material.SetShaderParameter("island_field", ImageTexture.CreateFromImage(image));
        _material.SetShaderParameter("field_rect", new Vector4(field.OriginX, field.OriginY, field.Width, field.Height));
        _material.SetShaderParameter("field_range", field.MaxDistance);
    }

    public void SetReflections(Texture2D texture) => _material.SetShaderParameter("reflections", texture);

    public void SetConditions(float night, float rain, float mist, float wind)
    {
        _material.SetShaderParameter("night", night);
        _material.SetShaderParameter("rain", rain);
        _material.SetShaderParameter("mist", mist);
        _material.SetShaderParameter("wind", wind);
    }
}
