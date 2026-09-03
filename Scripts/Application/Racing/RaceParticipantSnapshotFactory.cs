using System;
using System.Linq;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Racing;

/// <summary>
/// Maps persisted creature state into immutable race-entry data. This is the boundary where
/// live garden state is intentionally frozen for deterministic simulation/replay.
/// </summary>
public sealed class RaceParticipantSnapshotFactory
{
    private const int MaxVisualTypeIdLength = 64;
    private const int MaxAppearanceLayerIdLength = 128;

    private readonly StatCalculator _stats;

    public RaceParticipantSnapshotFactory(GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _stats = new StatCalculator(rules.Stats);
    }

    public RaceParticipantSnapshot Create(VoidlingData creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var appearance = creature.Appearance;
        var visualTypeId = appearance != null &&
                           VoidlingAppearanceData.IsValidSemanticId(appearance.VisualTypeId, MaxVisualTypeIdLength)
            ? appearance.VisualTypeId.Trim().ToLowerInvariant()
            : VoidlingAppearanceData.DefaultVisualTypeId;
        var paletteHue = appearance != null && VoidlingAppearanceData.IsValidHue(appearance.PaletteHue)
            ? VoidlingAppearanceData.NormalizeHue(appearance.PaletteHue)
            : VoidlingAppearanceData.LegacyUninitializedPaletteHue;
        var layerIds = appearance?.LayerIds?
            .Where(id => VoidlingAppearanceData.IsValidSemanticId(id, MaxAppearanceLayerIdLength))
            .ToArray() ?? Array.Empty<string>();

        return new RaceParticipantSnapshot(
            CreatureId: creature.Id,
            DisplayName: creature.Name,
            TintHex: creature.TintHex,
            Run: _stats.GetEffectiveStat(creature, "run"),
            Swim: _stats.GetEffectiveStat(creature, "swim"),
            Fly: _stats.GetEffectiveStat(creature, "fly"),
            Power: _stats.GetEffectiveStat(creature, "power"),
            Stamina: _stats.GetEffectiveStat(creature, "stamina"),
            VisualTypeId: visualTypeId,
            PaletteHue: paletteHue,
            LayerIdsKey: RaceParticipantSnapshot.BuildLayerIdsKey(layerIds));
    }
}
