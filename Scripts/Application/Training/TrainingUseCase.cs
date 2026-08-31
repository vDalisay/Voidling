using System;
using System.Linq;
using Voidling.Application.Garden;
using Voidling.Domain.Care;
using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Training;

public enum TrainingFailure
{
    None,
    UnknownStat,
    CreatureNotFound,
    NotEnoughCurrency,
    NoItemOwned,
    StatAtCap
}

public enum PassiveTrainingFailure
{
    None,
    UnknownStat,
    CreatureNotFound,
    NoPlacedModule
}

public enum GardenModuleFailure
{
    None,
    UnknownStat,
    DuplicateModuleId,
    ModuleNotFound,
    InvalidSlot,
    NotEnoughCurrency,
    MaxLevel
}

public readonly record struct TrainingPurchaseResult(TrainingFailure Failure)
{
    public bool Succeeded => Failure == TrainingFailure.None;
}

public readonly record struct TrainingApplicationResult(TrainingFailure Failure, int Gain)
{
    public bool Succeeded => Failure == TrainingFailure.None;
}

public readonly record struct PassiveTrainingAssignmentResult(
    PassiveTrainingFailure Failure,
    string StatId,
    bool Changed)
{
    public bool Succeeded => Failure == PassiveTrainingFailure.None;
}

public readonly record struct GardenModuleMutationResult(
    GardenModuleFailure Failure,
    bool Changed,
    int CoinsSpent = 0)
{
    public bool Succeeded => Failure == GardenModuleFailure.None;
}

/// <summary>
/// Coordinates active training inventory and Garden-backed passive training without UI,
/// persistence or Godot APIs. Both training paths share the same DNA-rank hard ceiling.
/// </summary>
public sealed class TrainingUseCase
{
    private readonly GameBalanceRules _rules;
    private readonly StatCalculator _stats;
    private readonly CreatureNeedsService _needs = new();

    public TrainingUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _stats = new StatCalculator(_rules.Stats);
    }

    public TrainingPurchaseResult BuyTrainingItem(GameStateData state, string statId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!_rules.Genetics.StatIds.Contains(statId))
            return new TrainingPurchaseResult(TrainingFailure.UnknownStat);
        if (state.Coins < _rules.Shop.TrainingItemPrice)
            return new TrainingPurchaseResult(TrainingFailure.NotEnoughCurrency);

        state.Coins -= _rules.Shop.TrainingItemPrice;
        state.TrainingItems.TryGetValue(statId, out var count);
        state.TrainingItems[statId] = count + 1;
        return new TrainingPurchaseResult(TrainingFailure.None);
    }

    public TrainingFailure ValidateTrainingItem(GameStateData state, string creatureId, string statId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_rules.Genetics.StatIds.Contains(statId))
            return TrainingFailure.UnknownStat;

        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        if (creature == null)
            return TrainingFailure.CreatureNotFound;
        if (_stats.GetTrainingPoints(creature, statId) >= _stats.GetTrainingPointCap(creature, statId))
            return TrainingFailure.StatAtCap;

        state.TrainingItems.TryGetValue(statId, out var count);
        return count > 0 ? TrainingFailure.None : TrainingFailure.NoItemOwned;
    }

    public TrainingApplicationResult ApplyTrainingItem(GameStateData state, string creatureId, string statId, ulong seed)
    {
        var failure = ValidateTrainingItem(state, creatureId, statId);
        if (failure != TrainingFailure.None)
            return new TrainingApplicationResult(failure, 0);

        var creature = state.Voidlings.First(v => v.Id == creatureId);
        state.TrainingItems[statId]--;

        var rolledGain = StableRandom.Create(seed, $"training:{creatureId}:{statId}").Next(5, 10);
        var current = _stats.GetTrainingPoints(creature, statId);
        var cap = _stats.GetTrainingPointCap(creature, statId);
        var updated = Math.Min(cap, current + rolledGain);
        var appliedGain = Math.Max(0, updated - current);
        creature.TrainingPoints[statId] = updated;

        EvolutionService.ApplyTrainingInfluence(creature, statId, appliedGain, _rules.Stats);
        _needs.ApplyTrainingTreat(creature.Needs, _rules.Needs);
        return new TrainingApplicationResult(TrainingFailure.None, appliedGain);
    }

    public GardenModuleMutationResult BuyGardenModule(GameStateData state, string moduleId, string statId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_rules.Genetics.StatIds.Contains(statId))
            return new GardenModuleMutationResult(GardenModuleFailure.UnknownStat, false);
        if (string.IsNullOrWhiteSpace(moduleId) || state.GardenModules.Any(module => module.Id == moduleId))
            return new GardenModuleMutationResult(GardenModuleFailure.DuplicateModuleId, false);

        var cost = Math.Max(0, _rules.GardenModules.PurchaseCost);
        if (state.Coins < cost)
            return new GardenModuleMutationResult(GardenModuleFailure.NotEnoughCurrency, false);

        state.Coins -= cost;
        state.GardenModules.Add(new GardenModuleData
        {
            Id = moduleId,
            StatId = statId,
            Level = 1,
            SlotIndex = -1
        });
        return new GardenModuleMutationResult(GardenModuleFailure.None, true, cost);
    }

    public GardenModuleMutationResult PlaceGardenModule(GameStateData state, string moduleId, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (slotIndex < -1 || slotIndex >= Math.Max(1, _rules.GardenModules.SlotCount))
            return new GardenModuleMutationResult(GardenModuleFailure.InvalidSlot, false);

        var module = state.GardenModules.FirstOrDefault(candidate => candidate.Id == moduleId);
        if (module == null)
            return new GardenModuleMutationResult(GardenModuleFailure.ModuleNotFound, false);
        if (module.SlotIndex == slotIndex)
            return new GardenModuleMutationResult(GardenModuleFailure.None, false);

        if (slotIndex >= 0)
        {
            var occupying = state.GardenModules.FirstOrDefault(candidate =>
                candidate.Id != module.Id && candidate.SlotIndex == slotIndex);
            if (occupying != null)
            {
                occupying.SlotIndex = module.SlotIndex;
                RefreshAssignedCreatureRates(state, occupying);
            }
        }

        module.SlotIndex = slotIndex;
        RefreshAssignedCreatureRates(state, module);
        return new GardenModuleMutationResult(GardenModuleFailure.None, true);
    }

    public GardenModuleMutationResult UpgradeGardenModule(GameStateData state, string moduleId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var module = state.GardenModules.FirstOrDefault(candidate => candidate.Id == moduleId);
        if (module == null)
            return new GardenModuleMutationResult(GardenModuleFailure.ModuleNotFound, false);

        var cost = _rules.GardenModules.UpgradeCostForLevel(module.Level);
        if (cost < 0)
            return new GardenModuleMutationResult(GardenModuleFailure.MaxLevel, false);
        if (state.Coins < cost)
            return new GardenModuleMutationResult(GardenModuleFailure.NotEnoughCurrency, false);

        state.Coins -= cost;
        module.Level = Math.Min(_rules.GardenModules.MaxLevel, module.Level + 1);
        RefreshAssignedCreatureRates(state, module);
        return new GardenModuleMutationResult(GardenModuleFailure.None, true, cost);
    }

    /// <summary>
    /// Compatibility/direct assignment retained for pre-module saves and pure Application callers.
    /// Player-facing UI should call SetPassiveTrainingFromPlacedModule instead.
    /// </summary>
    public PassiveTrainingAssignmentResult SetPassiveTraining(GameStateData state, string creatureId, string? statId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var requested = statId ?? string.Empty;
        if (requested.Length > 0 && !_rules.Genetics.StatIds.Contains(requested))
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.UnknownStat, string.Empty, false);

        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        if (creature == null)
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.CreatureNotFound, string.Empty, false);
        if (requested.Length == 0)
            return ClearPassiveTraining(creature);

        var changed = !string.Equals(creature.PassiveTrainingStatId, requested, StringComparison.Ordinal) ||
                      creature.PassiveTrainingModuleId.Length > 0 ||
                      creature.PassiveTrainingPointsPerMinute != 0.0f ||
                      creature.PassiveTrainingPointRemainder != 0.0;
        if (changed)
        {
            creature.PassiveTrainingStatId = requested;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
        }

        return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.None, requested, changed);
    }

    public PassiveTrainingAssignmentResult SetPassiveTrainingFromPlacedModule(
        GameStateData state,
        string creatureId,
        string? statId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var requested = statId ?? string.Empty;
        if (requested.Length > 0 && !_rules.Genetics.StatIds.Contains(requested))
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.UnknownStat, string.Empty, false);

        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        if (creature == null)
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.CreatureNotFound, string.Empty, false);
        if (requested.Length == 0)
            return ClearPassiveTraining(creature);

        var module = state.GardenModules
            .Where(candidate => candidate.SlotIndex >= 0 && string.Equals(candidate.StatId, requested, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Level)
            .ThenBy(candidate => candidate.SlotIndex)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (module == null)
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.NoPlacedModule, requested, false);

        var changed = !string.Equals(creature.PassiveTrainingStatId, requested, StringComparison.Ordinal) ||
                      !string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal) ||
                      creature.PassiveTrainingPointRemainder != 0.0 ||
                      !creature.PassiveTrainingPointsPerMinute.Equals(RateFor(module));
        if (changed)
        {
            creature.PassiveTrainingStatId = requested;
            creature.PassiveTrainingModuleId = module.Id;
            creature.PassiveTrainingPointsPerMinute = RateFor(module);
            creature.PassiveTrainingPointRemainder = 0.0;
        }

        return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.None, requested, changed);
    }

    private PassiveTrainingAssignmentResult ClearPassiveTraining(VoidlingData creature)
    {
        var changed = creature.PassiveTrainingStatId.Length > 0 ||
                      creature.PassiveTrainingModuleId.Length > 0 ||
                      creature.PassiveTrainingPointsPerMinute != 0.0f ||
                      creature.PassiveTrainingPointRemainder != 0.0;
        if (changed)
        {
            creature.PassiveTrainingStatId = string.Empty;
            creature.PassiveTrainingModuleId = string.Empty;
            creature.PassiveTrainingPointsPerMinute = 0.0f;
            creature.PassiveTrainingPointRemainder = 0.0;
        }

        return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.None, string.Empty, changed);
    }

    private void RefreshAssignedCreatureRates(GameStateData state, GardenModuleData module)
    {
        var rate = RateFor(module);
        foreach (var creature in state.Voidlings)
        {
            if (!string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal))
                continue;

            creature.PassiveTrainingStatId = module.StatId;
            creature.PassiveTrainingPointsPerMinute = rate;
            if (rate <= 0.0f)
                creature.PassiveTrainingPointRemainder = 0.0;
        }
    }

    private float RateFor(GardenModuleData module)
        => module.SlotIndex >= 0
            ? _rules.GardenModules.PointsPerMinuteForLevel(module.Level)
            : 0.0f;
}
