using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// One composited visual layer variant (for example wings/basic or crystal/tall). Layers reuse the
/// owning body definition's frame grid/state mapping so every frame stays pixel-perfect synchronized.
/// </summary>
[GlobalClass]
public partial class VoidlingVisualLayerDefinition : Resource
{
    [Export]
    public string LayerId { get; set; } = string.Empty;

    [Export]
    public string SlotId { get; set; } = string.Empty;

    [Export]
    public Texture2D BaseAtlas { get; set; } = null!;

    [Export]
    public Texture2D SwimAtlas { get; set; } = null!;

    [Export]
    public int ZIndexOffset { get; set; } = 1;

    [Export]
    public Vector2 OffsetAtScaleOne { get; set; } = Vector2.Zero;

    [Export(PropertyHint.Range, "0.01,4,0.01")]
    public float ScaleMultiplier { get; set; } = 1.0f;

    [Export]
    public bool PaletteAffected { get; set; } = true;

    /// <summary>
    /// Optional palette slots specific to this layer. When empty and PaletteAffected=true, the body
    /// definition's source palette is reused. Pixels outside the listed colors are left untouched.
    /// </summary>
    [Export]
    public Godot.Collections.Array<Color> SourcePaletteColors { get; set; } = new();
}
