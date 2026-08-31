using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Application.Daily;
using Voidling.Application.Garden;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Domain.Breeding;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Persistence;

/// <summary>
/// Owns backward-compatible normalization of the serialized game-state aggregate.
/// </summary>
public sealed class GameStateMigrationService
{
    public const int CurrentSaveVersion = 19;

    private readonly GameBalanceRules _rules;
    private readonly LineageArchiveService _lineage = new();
    private readonly CreatureNeedsService _needs = new();
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
        state.EggShells ??= new List<EggShellData>();
        state.TrainingItems ??= new Dictionary<string, int>(StringComparer.Ordinal);
        state.GardenModules ??= new List<GardenModuleData>();
        state.PendingTradeJournal ??= new List<PendingTradeJournalEntry>();
        state.AppliedTradeIds ??= new List<string>();
        state.AppliedMultiplayerRaceIds ??= new List<string>();
        state.DailyRaceAttempts ??= new List<DailyRaceAttemptData>();
        state.DailyLogin ??= new DailyLoginStateData();
        state.DailyMissions ??= new DailyMissionStateData();
        state.DailyMissions.Missions ??= new List<DailyMissionProgressData>();

        if (previousVersion < 4)
        {
            state.MasterVolume = 1.0f;
            state.AutoFinishRaces = true;
        }

        if (previousVersion < 14)
        {
            state.SoundEffectVolume = 1.0f;
            state.UiSoundVolume = 1.0f;
        }

        if (previousVersion < 15)
        {
            // The tutorial is a first-launch experience. Existing saves predate that launch moment,
            // so migrate them as completed instead of interrupting established players after update.
            state.TutorialCompleted = true;
        }

        state.MasterVolume = NormalizeVolume(state.MasterVolume);
        state.SoundEffectVolume = NormalizeVolume(state.SoundEffectVolume);
        state.UiSoundVolume = NormalizeVolume(state.UiSoundVolume);

        foreach (var statId in _rules.Genetics.StatIds)
        {
            if (!state.TrainingItems.ContainsKey(statId))
                state.TrainingItems[statId] = 0;
        }

        NormalizeGardenModules(state);

        foreach (var creature in state.Voidlings.Concat(state.DepartedVoidlings))
        {
            creature.TrainingPoints ??= new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var statId in _rules.Genetics.StatIds)
            {
                if (!creature.TrainingPoints.ContainsKey(statId))
                    creature.TrainingPoints[statId] = 0;

                creature.TrainingPoints[statId] = Math.Clamp(
                    creature.TrainingPoints[statId],
                    0,
                    _stats.GetTrainingPointCap(creature, statId));
            }

            creature.RareTraits ??= new List<RareTraitData>();
            creature.Needs ??= new CreatureNeedsState();
            _needs.Normalize(creature.Needs);

            if (!_rules.Genetics.StatIds.Contains(creature.PassiveTrainingStatId ?? string.Empty))
            {
                creature.PassiveTrainingStatId = string.Empty;
                creature.PassiveTrainingModuleId = string.Empty;
                creature.PassiveTrainingPointsPerMinute = 0.0f;
                creature.PassiveTrainingPointRemainder = 0.0;
            }
            else if (!double.IsFinite(creature.PassiveTrainingPointRemainder) ||
                     creature.PassiveTrainingPointRemainder < 0.0 ||
                     creature.PassiveTrainingPointRemainder >= 1.0)
            {
                creature.PassiveTrainingPointRemainder = 0.0;
            }

            NormalizePassiveModuleAssignment(state, creature);
        }

        foreach (var egg in state.OwnedEggs.Concat(state.StoreEggs))
            egg.RareTraits ??= new List<RareTraitData>();

        _lineage.EnsureCurrentEntries(state);

        state.PendingTradeJournal.RemoveAll(entry =>
            entry == null || string.IsNullOrWhiteSpace(entry.TradeId));
        state.AppliedTradeIds = state.AppliedTradeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        state.MultiplayerWins = Math.Max(0, state.MultiplayerWins);
        state.AppliedMultiplayerRaceIds = state.AppliedMultiplayerRaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .TakeLast(256)
            .ToList();

        state.DailyRaceAttempts = state.DailyRaceAttempts
            .Where(attempt => DailyFriendRaceService.IsStructurallyValid(attempt, requireCurrentRules: false, out _))
            .GroupBy(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .TakeLast(DailyFriendRaceService.MaxAttemptHistory)
            .ToList();

        state.DailyLogin.LastClaimDayNumber = Math.Max(0, state.DailyLogin.LastClaimDayNumber);
        state.DailyLogin.Streak = Math.Max(0, state.DailyLogin.Streak);
        if (state.DailyLogin.LastClaimDayNumber == 0)
            state.DailyLogin.Streak = 0;

        state.DailyMissions.DayNumber = Math.Max(0, state.DailyMissions.DayNumber);
        state.DailyMissions.Missions.RemoveAll(mission =>
            mission == null || string.IsNullOrWhiteSpace(mission.MissionId));
        foreach (var mission in state.DailyMissions.Missions)
            mission.Progress = Math.Max(0, mission.Progress);

        if (!double.IsFinite(state.GardenIncomeCoinRemainder) ||
            state.GardenIncomeCoinRemainder < 0.0 ||
            state.GardenIncomeCoinRemainder >= 1.0)
        {
            state.GardenIncomeCoinRemainder = 0.0;
        }

        var rotationInterval = Math.Max(1.0, _rules.Shop.EggRotationIntervalSeconds);
        if (!double.IsFinite(state.ShopEggRotationElapsedSeconds) || state.ShopEggRotationElapsedSeconds < 0.0)
        {
            state.ShopEggRotationElapsedSeconds = 0.0;
        }
        else if (state.ShopEggRotationElapsedSeconds >= rotationInterval)
        {
            state.ShopEggRotationElapsedSeconds %= rotationInterval;
        }

        state.SaveVersion = CurrentSaveVersion;
    }

    private void NormalizeGardenModules(GameStateData state)
    {
        var normalized = new List<GardenModuleData>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var occupiedSlots = new HashSet<int>();
        var slotCount = Math.Max(1, _rules.GardenModules.SlotCount);
        var maxLevel = Math.Max(1, _rules.GardenModules.MaxLevel);

        foreach (var module in state.GardenModules)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.Id) ||
                !_rules.Genetics.StatIds.Contains(module.StatId ?? string.Empty))
            {
                continue;
            }

            module.Id = module.Id.Trim();
            if (!ids.Add(module.Id))
                continue;

            module.Level = Math.Clamp(module.Level, 1, maxLevel);
            if (module.SlotIndex < -1 || module.SlotIndex >= slotCount ||
                (module.SlotIndex >= 0 && !occupiedSlots.Add(module.SlotIndex)))
            {
                module.SlotIndex = -1;
            }

            normalized.Add(module);
        }

        state.GardenModules = normalized;
    }

    private void NormalizePassiveModuleAssignment(GameStateData state, VoidlingData creature)
    {
        creature.PassiveTrainingModuleId ??= string.Empty;
        if (string.IsNullOrEmpty(creature.PassiveTrainingModuleId))
        {
            // Save versions before modules used direct stat assignments. Preserve them at the
            // authorable legacy base rate until the player explicitly chooses a module-backed stat.
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            return;
        }

        var module = state.GardenModules.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, creature.PassiveTrainingModuleId, StringComparison.Ordinal));
        if (module == null)
        {
            creature.PassiveTrainingStatId = string.Empty;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
            return;
        }

        creature.PassiveTrainingStatId = module.StatId;
        creature.PassiveTrainingPointsPerMinute = module.SlotIndex >= 0
            ? _rules.GardenModules.PointsPerMinuteForLevel(module.Level)
            : 0.0f;
        if (creature.PassiveTrainingPointsPerMinute <= 0.0f)
            creature.PassiveTrainingPointRemainder = 0.0;
    }

    private static float NormalizeVolume(float volume)
        => float.IsFinite(volume) ? Math.Clamp(volume, 0.0f, 1.0f) : 1.0f;
}
