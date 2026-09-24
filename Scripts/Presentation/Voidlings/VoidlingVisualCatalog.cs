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
}
