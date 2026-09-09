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
}
