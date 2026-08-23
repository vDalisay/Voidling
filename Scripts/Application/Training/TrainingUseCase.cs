using System;
using System.Linq;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Application.Training;

public enum TrainingFailure
{
    None,
    UnknownStat,
    CreatureNotFound,
    NotEnoughCurrency,
    NoItemOwned
}

public readonly record struct TrainingPurchaseResult(TrainingFailure Failure)
{
    public bool Succeeded => Failure == TrainingFailure.None;
}

public readonly record struct TrainingApplicationResult(TrainingFailure Failure, int Gain)
{
    public bool Succeeded => Failure == TrainingFailure.None;
}

/// <summary>
/// Coordinates training inventory and stat progression without UI, persistence or Godot APIs.
/// The caller owns seed allocation and persistence so both remain explicit side effects.
/// </summary>
public sealed class TrainingUseCase
{
    private readonly GameBalanceRules _rules;

    public TrainingUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
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

    public TrainingApplicationResult ApplyTrainingItem(GameStateData state, string creatureId, string statId, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!_rules.Genetics.StatIds.Contains(statId))
            return new TrainingApplicationResult(TrainingFailure.UnknownStat, 0);

        var creature = state.Voidlings.FirstOrDefault(v => v.Id == creatureId);
        if (creature == null)
            return new TrainingApplicationResult(TrainingFailure.CreatureNotFound, 0);

        state.TrainingItems.TryGetValue(statId, out var count);
        if (count <= 0)
            return new TrainingApplicationResult(TrainingFailure.NoItemOwned, 0);

        state.TrainingItems[statId] = count - 1;
        var gain = StableRandom.Create(seed, $"training:{creatureId}:{statId}").Next(5, 10);
        creature.TrainingPoints.TryGetValue(statId, out var current);
        creature.TrainingPoints[statId] = Math.Min(_rules.Stats.MaxTrainingPoints, current + gain);
        return new TrainingApplicationResult(TrainingFailure.None, gain);
    }
}
