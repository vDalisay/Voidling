using System;
using VoidlingGame;

namespace Voidling.Domain.Breeding;

/// <summary>
/// Minimal persistent ancestry identity. It deliberately excludes genome/training/runtime state so
/// family relationships can survive departure or multiplayer ownership transfer without retaining
/// complete historical creature objects.
/// </summary>
public sealed record LineageArchiveEntry(
    string CreatureId,
    string DisplayName,
    string ParentAId,
    string ParentBId,
    int FamilyGeneration,
    string TintHex,
    bool InbreedingHistoryFlag)
{
    public static LineageArchiveEntry FromVoidling(VoidlingData creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return new LineageArchiveEntry(
            creature.Id,
            creature.Name,
            creature.ParentAId,
            creature.ParentBId,
            Math.Max(0, creature.FamilyGeneration),
            creature.TintHex,
            creature.InbreedingHistoryFlag);
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
