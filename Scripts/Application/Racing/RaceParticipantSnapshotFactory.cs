using System;
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
    private readonly StatCalculator _stats;

    public RaceParticipantSnapshotFactory(GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _stats = new StatCalculator(rules.Stats);
    }

    public RaceParticipantSnapshot Create(VoidlingData creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return new RaceParticipantSnapshot(
            CreatureId: creature.Id,
            DisplayName: creature.Name,
            TintHex: creature.TintHex,
            Run: _stats.GetEffectiveStat(creature, "run"),
            Swim: _stats.GetEffectiveStat(creature, "swim"),
            Fly: _stats.GetEffectiveStat(creature, "fly"),
            Power: _stats.GetEffectiveStat(creature, "power"),
            Stamina: _stats.GetEffectiveStat(creature, "stamina"));
    }
}
