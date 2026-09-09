using Godot;
using Voidling.Domain.Evolution;

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

    // These entries intentionally default to blank. The product specification has not yet locked
    // which evolution specializations should change body family, so blank means "keep the current
    // semantic appearance" rather than silently choosing a morphology rule in Presentation.
    [ExportGroup("Morphology Mapping (inactive while blank)")]
    [Export]
    public string GeneralistMorphologyVisualTypeId { get; set; } = string.Empty;

    [Export]
    public string RunMorphologyVisualTypeId { get; set; } = string.Empty;

    [Export]
    public string SwimMorphologyVisualTypeId { get; set; } = string.Empty;

    [Export]
    public string FlyMorphologyVisualTypeId { get; set; } = string.Empty;

    [Export]
    public string PowerMorphologyVisualTypeId { get; set; } = string.Empty;

    public string ResolveMorphologyVisualTypeId(
        EvolutionSpecialization specialization,
        string? fallbackVisualTypeId)
        => VoidlingMorphologyVisualResolver.Resolve(
            specialization,
            fallbackVisualTypeId,
            DefaultVisualTypeId,
            new VoidlingMorphologyVisualMapping(
                GeneralistMorphologyVisualTypeId,
                RunMorphologyVisualTypeId,
                SwimMorphologyVisualTypeId,
                FlyMorphologyVisualTypeId,
                PowerMorphologyVisualTypeId));
}
