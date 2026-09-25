using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// A pack icon drawn at its own pixel size and allowed to overhang its layout slot, like a sticker
/// on a label: the slot keeps the row compact while the art stays at 1:1 and crisp.
/// </summary>
public partial class Sticker : Control
{
    private Texture2D? _texture;

    public Texture2D? Texture
    {
        get => _texture;
        set { _texture = value; QueueRedraw(); }
    }

    /// <summary>How far the art reaches left and up past the slot's corner.</summary>
    public Vector2 Overhang { get; set; }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        if (_texture == null) return;
        DrawTexture(_texture, (-Overhang).Round());
    }
}
