using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// One composited visual layer variant (for example a front wing, back wing, or crown). Layers reuse
/// the owning body definition's frame grid/state mapping so every authored pixel stays registered.
/// Optional motion-group metadata lets multiple render layers move as one secondary unit without
/// consumer screens knowing which layers form that unit.
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
    /// Layers with the same non-empty motion group share one runtime follow offset. This is useful for
    /// a front/back wing pair: they remain rigid relative to each other while the pair can trail the
    /// body very slightly during vertical movement.
    /// </summary>
    [Export]
    public string MotionGroupId { get; set; } = string.Empty;

    /// <summary>
    /// Time constant, in seconds, used when a motion group catches up to the body's vertical position.
    /// Zero disables secondary follow motion.
    /// </summary>
    [Export(PropertyHint.Range, "0,0.5,0.005")]
    public float VerticalFollowLagSeconds { get; set; } = 0.0f;

    /// <summary>
    /// Maximum vertical displacement of the motion group in source pixels at scale 1.
    /// </summary>
    [Export(PropertyHint.Range, "0,8,0.1")]
    public float MaxVerticalLagAtScaleOne { get; set; } = 0.0f;

    /// <summary>
    /// Optional palette slots specific to this layer. When empty and PaletteAffected=true, the body
    /// definition's source palette is reused. Pixels outside the listed colors are left untouched.
    /// </summary>
    [Export]
    public Godot.Collections.Array<Color> SourcePaletteColors { get; set; } = new();
}
