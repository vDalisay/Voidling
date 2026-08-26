using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Domain.Breeding;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Persistence;

/// <summary>
/// Owns backward-compatible normalization of the serialized game-state aggregate.
/// Keep migrations explicit and monotonic: loading an old save may fill deterministic
/// defaults, but must never reroll existing genetics, eggs, lineage, or race data.
/// </summary>
public sealed class GameStateMigrationService
{
    public const int CurrentSaveVersion = 9;

    private readonly GameBalanceRules _rules;
    private readonly LineageArchiveService _lineage = new();
    private readonly StatCalculator _stats;

    public GameStateMigrationService(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _stats = new StatCalculator(_rules.Stats);
    }

    public void Normalize(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var previousVersion = state.SaveVersion;

        state.Voidlings ??= new List<VoidlingData>();
        state.DepartedVoidlings ??= new List<VoidlingData>();
        state.LineageArchive ??= new List<LineageArchiveEntry>();
        state.OwnedEggs ??= new List<EggData>();
        state.StoreEggs ??= new List<EggData>();
        state.TrainingItems ??= new Dictionary<string, int>(StringComparer.Ordinal);
        state.PendingTradeJournal ??= new List<PendingTradeJournalEntry>();
        state.AppliedTradeIds ??= new List<string>();
        state.AppliedMultiplayerRaceIds ??= new List<string>();
        state.DailyRaceAttempts ??= new List<DailyRaceAttemptData>();

        // Version 4 introduced persisted audio and race auto-finish settings.
        if (previousVersion < 4)
        {
            state.MasterVolume = 1.0f;
            state.AutoFinishRaces = true;
        }

        foreach (var statId in _rules.Genetics.StatIds)
        {
            if (!state.TrainingItems.ContainsKey(statId))
                state.TrainingItems[statId] = 0;
        }

        foreach (var creature in state.Voidlings.Concat(state.DepartedVoidlings))
        {
            creature.TrainingPoints ??= new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var statId in _rules.Genetics.StatIds)
            {
                if (!creature.TrainingPoints.ContainsKey(statId))
                    creature.TrainingPoints[statId] = 0;

                // Version 9 makes the DNA rank the actual hard training ceiling. Clamp legacy
                // over-cap data once rather than preserving banked points that could become active
                // after a later evolution rank promotion.
                creature.TrainingPoints[statId] = Math.Clamp(
                    creature.TrainingPoints[statId],
                    0,
                    _stats.GetTrainingPointCap(creature, statId));
            }

            creature.RareTraits ??= new List<RareTraitData>();
        }

        foreach (var egg in state.OwnedEggs.Concat(state.StoreEggs))
            egg.RareTraits ??= new List<RareTraitData>();

        // Version 5 introduced a minimal persistent ancestry graph. Populate it from every full
        // creature record already known locally, while preserving archive-only ancestors imported
        // through multiplayer trades. This is deterministic and never changes genes or IDs.
        _lineage.EnsureCurrentEntries(state);

        // Version 6 adds only empty trade durability collections to old saves. Pending journals and
        // applied transaction IDs are local data; loading a save never contacts Steam or a peer.
        state.PendingTradeJournal.RemoveAll(entry =>
            entry == null || string.IsNullOrWhiteSpace(entry.TradeId));
        state.AppliedTradeIds = state.AppliedTradeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Version 7 keeps the multiplayer-win total and a bounded local dedupe history. Both are
        // purely local persistence; normalization never queries Steam or attempts leaderboard IO.
        state.MultiplayerWins = Math.Max(0, state.MultiplayerWins);
        state.AppliedMultiplayerRaceIds = state.AppliedMultiplayerRaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .TakeLast(256)
            .ToList();

        // Version 8 persists local daily-race attempts. Keep at most one structurally valid attempt
        // per UTC day and retain old rules-version attempts instead of erasing them: an incompatible
        // update may prevent resume, but must not accidentally grant a second attempt that day.
        state.DailyRaceAttempts = state.DailyRaceAttempts
            .Where(attempt => DailyFriendRaceService.IsStructurallyValid(
                attempt,
                requireCurrentRules: false,
                out _))
            .GroupBy(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .TakeLast(DailyFriendRaceService.MaxAttemptHistory)
            .ToList();

        state.SaveVersion = CurrentSaveVersion;
    }
}