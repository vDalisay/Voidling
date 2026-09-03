using System;
using System.Text.Json.Serialization;
using VoidlingGame;

namespace Voidling.Domain.Breeding;

/// <summary>
/// Minimal persistent ancestry identity plus the semantic appearance needed to keep historical
/// family-tree portraits visually faithful after a full creature record is gone. It deliberately
/// excludes genome/training/runtime state and never stores presentation resource paths.
/// </summary>
public sealed record LineageArchiveEntry(
    string CreatureId,
    string DisplayName,
    string ParentAId,
    string ParentBId,
    int FamilyGeneration,
    string TintHex,
    bool InbreedingHistoryFlag,
    string VisualTypeId = VoidlingAppearanceData.DefaultVisualTypeId,
    float PaletteHue = -1.0f,
    string LayerIdsKey = "")
{
    [JsonIgnore]
    public string[] LayerIds => VoidlingAppearanceData.ParseLayerIdsKey(LayerIdsKey);

    public static LineageArchiveEntry FromVoidling(VoidlingData creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var appearance = creature.Appearance ?? new VoidlingAppearanceData();
        appearance.Normalize();
        return new LineageArchiveEntry(
            creature.Id,
            creature.Name,
            creature.ParentAId,
            creature.ParentBId,
            Math.Max(0, creature.FamilyGeneration),
            creature.TintHex,
            creature.InbreedingHistoryFlag,
            appearance.VisualTypeId,
            appearance.PaletteHue,
            VoidlingAppearanceData.BuildLayerIdsKey(appearance.LayerIds));
    }

    public bool HasSameLineageIdentity(LineageArchiveEntry other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(CreatureId, other.CreatureId, StringComparison.Ordinal) &&
               string.Equals(ParentAId, other.ParentAId, StringComparison.Ordinal) &&
               string.Equals(ParentBId, other.ParentBId, StringComparison.Ordinal) &&
               FamilyGeneration == other.FamilyGeneration;
    }
}
