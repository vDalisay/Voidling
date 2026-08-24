using System;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer;

/// <summary>
/// Builds an explicit network snapshot from a locally owned Voidling. Ownership validation happens
/// before anything is sent, and only the fields required by connected-Garden presentation leave the
/// local save aggregate.
/// </summary>
public sealed class SharedVoidlingSnapshotFactory
{
    public bool TryCreateOwned(
        GameStateData state,
        PlatformUser owner,
        string creatureId,
        float zoneX,
        float zoneY,
        out SharedVoidlingSnapshot snapshot,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(owner);

        snapshot = default!;
        error = null;

        if (owner.Id.Value == 0)
        {
            error = "Local platform identity is unavailable.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(creatureId))
        {
            error = "Creature ID is required.";
            return false;
        }

        if (!float.IsFinite(zoneX) || !float.IsFinite(zoneY))
        {
            error = "Connected-zone position must be finite.";
            return false;
        }

        var creature = state.Voidlings.FirstOrDefault(v =>
            string.Equals(v.Id, creatureId, StringComparison.Ordinal));
        if (creature == null)
        {
            error = "The selected Voidling is not owned by this save.";
            return false;
        }

        var displayName = string.IsNullOrWhiteSpace(creature.Name)
            ? "Voidling"
            : creature.Name.Trim();
        if (displayName.Length > ConnectedZoneValidation.MaxDisplayNameLength)
            displayName = displayName[..ConnectedZoneValidation.MaxDisplayNameLength];

        var rareTraitIds = creature.RareTraits
            .Select(trait => trait.TraitId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(ConnectedZoneValidation.MaxRareTraits)
            .Cast<string>()
            .ToArray();

        snapshot = new SharedVoidlingSnapshot(
            creature.Id,
            owner.Id,
            displayName,
            creature.TintHex,
            creature.Stage,
            Math.Max(0, creature.FamilyGeneration),
            rareTraitIds,
            zoneX,
            zoneY);

        if (!ConnectedZoneValidation.IsValidSharedVoidling(snapshot))
        {
            snapshot = default!;
            error = "The selected Voidling cannot be represented safely in the connected zone.";
            return false;
        }

        return true;
    }
}
