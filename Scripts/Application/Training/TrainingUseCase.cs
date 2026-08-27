using System;
using System.Linq;
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
    CreatureNotFound
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

/// <summary>
/// Coordinates active training inventory and persistent passive-training assignments without UI,
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

    public PassiveTrainingAssignmentResult SetPassiveTraining(GameStateData state, string creatureId, string? statId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var requested = statId ?? string.Empty;
        if (requested.Length > 0 && !_rules.Genetics.StatIds.Contains(requested))
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.UnknownStat, string.Empty, false);

        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        if (creature == null)
            return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.CreatureNotFound, string.Empty, false);

        var changed = !string.Equals(creature.PassiveTrainingStatId, requested, StringComparison.Ordinal) ||
                      creature.PassiveTrainingPointRemainder != 0.0;
        if (changed)
        {
            creature.PassiveTrainingStatId = requested;
            creature.PassiveTrainingPointRemainder = 0.0;
        }

        return new PassiveTrainingAssignmentResult(PassiveTrainingFailure.None, requested, changed);
    }
}