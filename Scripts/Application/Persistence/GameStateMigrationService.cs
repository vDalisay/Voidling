using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Application.Daily;
using Voidling.Application.Garden;
using Voidling.Application.Training;
using Voidling.Domain.Garden;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Domain.Breeding;
using Voidling.Domain.Care;
using Voidling.Domain.Genetics;
using Voidling.Domain.Preferences;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Persistence;

/// <summary>
/// Backward-compatible, deterministic save normalization. Existing genetics/appearance are never
/// rerolled; semantic v9 appearance migration remains authoritative while later progression fields
/// receive deterministic defaults.
/// </summary>
public sealed class GameStateMigrationService
{
    public const int CurrentSaveVersion = 22;

    private readonly GameBalanceRules _rules;
    private readonly LineageArchiveService _lineage = new();
    private readonly CreatureNeedsService _needs = new();
    private readonly FavoriteFoodPreferenceService _favoriteFood = new();
    private readonly StatCalculator _stats;
    private readonly ColorPhenotypeResolver _colors;

    public GameStateMigrationService(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _stats = new StatCalculator(_rules.Stats);
        _colors = new ColorPhenotypeResolver(_rules.Appearance);
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

        state.Voidlings.RemoveAll(static value => value is null);
        state.DepartedVoidlings.RemoveAll(static value => value is null);
        state.OwnedEggs.RemoveAll(static value => value is null);
        state.StoreEggs.RemoveAll(static value => value is null);
        state.EggShells.RemoveAll(static value => value is null);

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
            // Tutorial was introduced later; do not interrupt established saves.
            state.TutorialCompleted = true;
        }

        state.MasterVolume = NormalizeVolume(state.MasterVolume);
        state.SoundEffectVolume = NormalizeVolume(state.SoundEffectVolume);
        state.UiSoundVolume = NormalizeVolume(state.UiSoundVolume);

        foreach (var statId in _rules.Genetics.StatIds)
        {
            if (!state.TrainingItems.ContainsKey(statId)) state.TrainingItems[statId] = 0;
            state.TrainingItems[statId] = Math.Max(0, state.TrainingItems[statId]);
        }

        NormalizeGardenModules(state, previousVersion);
        foreach (var creature in state.Voidlings.Concat(state.DepartedVoidlings)) NormalizeCreature(state, creature);

        foreach (var egg in state.OwnedEggs.Concat(state.StoreEggs))
        {
            egg.Genome ??= new GenomeData();
            NormalizeGenome(egg.Genome);
            egg.RareTraits ??= new List<RareTraitData>();
            egg.Appearance = NormalizeAppearance(egg.Genome, egg.Appearance);
            egg.IncubationSeconds = NonNegativeFinite(egg.IncubationSeconds);
            egg.RequiredIncubationSeconds = NonNegativeFinite(egg.RequiredIncubationSeconds);
            egg.FamilyGeneration = Math.Max(0, egg.FamilyGeneration);
            egg.InbreedingBurdenLevel = Math.Max(0, egg.InbreedingBurdenLevel);
        }

        _lineage.EnsureCurrentEntries(state);

        state.PendingTradeJournal.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.TradeId));
        state.AppliedTradeIds = state.AppliedTradeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        state.MultiplayerWins = Math.Max(0, state.MultiplayerWins);
        state.AppliedMultiplayerRaceIds = state.AppliedMultiplayerRaceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).TakeLast(256).ToList();
        state.DailyRaceAttempts = state.DailyRaceAttempts
            .Where(attempt => DailyFriendRaceService.IsStructurallyValid(attempt, requireCurrentRules: false, out _))
            .GroupBy(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .TakeLast(DailyFriendRaceService.MaxAttemptHistory)
            .ToList();

        state.DailyLogin.LastClaimDayNumber = Math.Max(0, state.DailyLogin.LastClaimDayNumber);
        state.DailyLogin.Streak = Math.Max(0, state.DailyLogin.Streak);
        if (state.DailyLogin.LastClaimDayNumber == 0) state.DailyLogin.Streak = 0;

        state.DailyMissions.DayNumber = Math.Max(0, state.DailyMissions.DayNumber);
        state.DailyMissions.Missions.RemoveAll(mission => mission == null || string.IsNullOrWhiteSpace(mission.MissionId));
        foreach (var mission in state.DailyMissions.Missions) mission.Progress = Math.Max(0, mission.Progress);

        if (!double.IsFinite(state.GardenIncomeCoinRemainder) || state.GardenIncomeCoinRemainder < 0.0 || state.GardenIncomeCoinRemainder >= 1.0)
            state.GardenIncomeCoinRemainder = 0.0;

        var rotationInterval = Math.Max(1.0, _rules.Shop.EggRotationIntervalSeconds);
        if (!double.IsFinite(state.ShopEggRotationElapsedSeconds) || state.ShopEggRotationElapsedSeconds < 0.0)
            state.ShopEggRotationElapsedSeconds = 0.0;
        else if (state.ShopEggRotationElapsedSeconds >= rotationInterval)
            state.ShopEggRotationElapsedSeconds %= rotationInterval;

        state.SaveVersion = CurrentSaveVersion;
    }

    private void NormalizeCreature(GameStateData state, VoidlingData creature)
    {
        creature.Id ??= string.Empty;
        creature.Name = string.IsNullOrWhiteSpace(creature.Name) ? "Voidling" : creature.Name;
        creature.Genome ??= new GenomeData();
        NormalizeGenome(creature.Genome);
        creature.TrainingPoints ??= new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var statId in _rules.Genetics.StatIds)
        {
            if (!creature.TrainingPoints.ContainsKey(statId)) creature.TrainingPoints[statId] = 0;
            creature.TrainingPoints[statId] = Math.Clamp(creature.TrainingPoints[statId], 0, _stats.GetTrainingPointCap(creature, statId));
        }

        creature.RareTraits ??= new List<RareTraitData>();
        creature.Appearance = NormalizeAppearance(creature.Genome, creature.Appearance);
        creature.Needs ??= new CreatureNeedsState();
        _needs.Normalize(creature.Needs);
        _favoriteFood.Normalize(creature, _rules.Genetics.StatIds);
        creature.FamilyGeneration = Math.Max(0, creature.FamilyGeneration);
        creature.InbreedingBurdenLevel = Math.Max(0, creature.InbreedingBurdenLevel);
        creature.ReincarnationCount = Math.Max(0, creature.ReincarnationCount);
        creature.AgeSeconds = NonNegativeFinite(creature.AgeSeconds);
        creature.AdultAgeSeconds = NonNegativeFinite(creature.AdultAgeSeconds);
        creature.BreedCooldownSeconds = NonNegativeFinite(creature.BreedCooldownSeconds);
        creature.SwimFlyInfluence = FiniteOrZero(creature.SwimFlyInfluence);
        creature.RunPowerInfluence = FiniteOrZero(creature.RunPowerInfluence);
        creature.EvolutionMagnitude = NonNegativeFinite(creature.EvolutionMagnitude);

        if (!_rules.Genetics.StatIds.Contains(creature.PassiveTrainingStatId ?? string.Empty))
        {
            creature.PassiveTrainingStatId = string.Empty;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
        }
        else if (!double.IsFinite(creature.PassiveTrainingPointRemainder) || creature.PassiveTrainingPointRemainder < 0.0 || creature.PassiveTrainingPointRemainder >= 1.0)
        {
            creature.PassiveTrainingPointRemainder = 0.0;
        }
        NormalizePassiveModuleAssignment(state, creature);
    }

    private void NormalizeGenome(GenomeData genome)
    {
        genome.AbilityGenes ??= new Dictionary<string, GenePairData>(StringComparer.Ordinal);
        genome.PersonalityGenes ??= new Dictionary<string, GenePairData>(StringComparer.Ordinal);
        foreach (var statId in _rules.Genetics.StatIds)
        {
            if (!genome.AbilityGenes.TryGetValue(statId, out var gene) || gene == null)
            {
                gene = new GenePairData(); genome.AbilityGenes[statId] = gene;
            }
            gene.AlleleA = Math.Clamp(gene.AlleleA, 0, 5);
            gene.AlleleB = Math.Clamp(gene.AlleleB, 0, 5);
            gene.ExpressedAlleleIndex = gene.ExpressedAlleleIndex == 1 ? 1 : 0;
        }
    }

    private VoidlingAppearanceData NormalizeAppearance(GenomeData genome, VoidlingAppearanceData? appearance)
    {
        // Preserve the validated v9 migration exactly: legacy palette genes are initialized
        // deterministically and semantic appearance never stores resource paths.
        _colors.EnsurePaletteGenes(genome);
        appearance ??= new VoidlingAppearanceData();
        appearance.Normalize();
        if (!VoidlingAppearanceData.IsValidHue(appearance.PaletteHue))
            appearance.PaletteHue = _colors.ResolvePaletteHue(genome);
        return appearance;
    }

    private void NormalizeGardenModules(GameStateData state, int previousVersion)
    {
        var normalized = new List<GardenModuleData>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var occupied = new HashSet<(int Q, int R)>();
        var maxLevel = Math.Max(1, _rules.GardenModules.MaxLevel);
        foreach (var module in state.GardenModules)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.Id)) continue;
            module.Id = module.Id.Trim();
            if (!ids.Add(module.Id)) continue;
            module.StatId ??= string.Empty;
            // Blank is plain ground now; anything that is neither blank nor a real stat is junk.
            if (module.StatId.Length > 0 && !_rules.Genetics.StatIds.Contains(module.StatId)) continue;
            module.Level = Math.Clamp(module.Level, 1, maxLevel);
            module.SlotIndex = -1;
            if (GardenTileShape.Find(module.ShapeId ?? string.Empty) == null)
                module.ShapeId = GardenTileShape.Single.Id;

            // The hex grid grew to a size a Voidling can live on, so tiles from an older save no
            // longer sit where they used to. They stay owned, at their stat and level, and go back
            // to the inventory for the player to re-place on the new island.
            if (previousVersion < 22)
            {
                module.Placed = false;
                module.ShapeId = GardenTileShape.Single.Id;
            }

            // ponytail: overlap only, no connectivity re-check on load. Pieces can only be placed
            // connected, so a save can only lose connectivity if it was hand-edited.
            if (module.Placed && !occupied.Add((module.HexQ, module.HexR))) module.Placed = false;
            normalized.Add(module);
        }
        state.GardenModules = normalized;
        TrainingUseCase.EnsureStarterHex(state);
    }

    private void NormalizePassiveModuleAssignment(GameStateData state, VoidlingData creature)
    {
        creature.PassiveTrainingModuleId ??= string.Empty;
        if (string.IsNullOrEmpty(creature.PassiveTrainingModuleId))
        {
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            return;
        }
        var module = state.GardenModules.FirstOrDefault(candidate => string.Equals(candidate.Id, creature.PassiveTrainingModuleId, StringComparison.Ordinal));
        if (module == null)
        {
            creature.PassiveTrainingStatId = string.Empty;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
            return;
        }
        if (!module.Placed || module.StatId.Length == 0)
        {
            // The ground it was training on is gone, back in the inventory, or plain grass now.
            creature.PassiveTrainingStatId = string.Empty;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
            return;
        }
        creature.PassiveTrainingStatId = module.StatId;
        creature.PassiveTrainingPointsPerMinute = _rules.GardenModules.PointsPerMinuteForLevel(module.Level);
        if (creature.PassiveTrainingPointsPerMinute <= 0.0f) creature.PassiveTrainingPointRemainder = 0.0;
    }

    private static float NormalizeVolume(float value) => float.IsFinite(value) ? Math.Clamp(value, 0.0f, 1.0f) : 1.0f;
    private static float NonNegativeFinite(float value) => float.IsFinite(value) ? Math.Max(0.0f, value) : 0.0f;
    private static float FiniteOrZero(float value) => float.IsFinite(value) ? value : 0.0f;
}
