using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// One authoritative presentation catalog for all base Voidling body families. Definitions are keyed
/// by stable semantic IDs (normal/water/power/etc.), never by display name or asset path.
/// </summary>
[GlobalClass]
public partial class VoidlingVisualCatalog : Resource
{
    [Export]
    public string DefaultVisualTypeId { get; set; } = "normal";

    [Export]
    public Godot.Collections.Array<VoidlingVisualDefinition> Definitions { get; set; } = new();

    /// <summary>
    /// Semantic visual types drawn in the artist's own colors: color DNA is not applied to them.
    /// Neutral adults and special variants look the way they were drawn; babies and typed adults
    /// show their color DNA.
    /// </summary>
    [Export]
    public string[] AuthoredColorVisualTypeIds { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// Stand-in hues for authored-color types whose own art is not registered yet: the fallback body
    /// is tinted to this hue (turns, 0..1) so, for example, the Swamp guy reads green until his sheet
    /// arrives. Parallel to <see cref="PlaceholderHues"/>.
    /// </summary>
    [Export]
    public string[] PlaceholderHueVisualTypeIds { get; set; } = System.Array.Empty<string>();

    [Export]
    public float[] PlaceholderHues { get; set; } = System.Array.Empty<float>();
}
