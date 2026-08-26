using System;
using System.Collections.Generic;
using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Simulation;

public abstract record GameSimulationEvent;
public sealed record CreatureBecameAdultEvent(
    string CreatureId,
    string Name,
    EvolutionSpecialization Specialization,
    string PromotedStatId,
    int PreviousRank,
    int NewRank) : GameSimulationEvent;
public sealed record CreatureHatchedEvent(string EggId, string CreatureId, string Name) : GameSimulationEvent;
public sealed record EggFailedEvent(string EggId) : GameSimulationEvent;

public sealed record SimulationStepResult(bool Changed, IReadOnlyList<GameSimulationEvent> Events);

/// <summary>
/// Advances persistent garden simulation from an explicit elapsed duration. This contains no
/// Godot frame, scene, UI, persistence, or wall-clock dependency, so lifecycle/hatching/economy can
/// be tested and reused from any runtime host without changing presentation code.
/// </summary>
public sealed class AdvanceSimulationUseCase
{
    private readonly GameBalanceRules _rules;

    public AdvanceSimulationUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public SimulationStepResult Advance(GameStateData state, float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0.0f)
            return new SimulationStepResult(false, Array.Empty<GameSimulationEvent>());

        var changed = AdvanceGardenIncome(state, elapsedSeconds);
        var events = new List<GameSimulationEvent>();

        foreach (var creature in state.Voidlings)
        {
            if (creature.BreedCooldownSeconds > 0.0f)
            {
                creature.BreedCooldownSeconds = Math.Max(0.0f, creature.BreedCooldownSeconds - elapsedSeconds);
                changed = true;
            }

            if (creature.Stage != LifeStage.Child)
                continue;

            creature.AgeSeconds += elapsedSeconds;
            changed = true;
            if (creature.AgeSeconds < _rules.Lifecycle.ChildToAdultSeconds)
                continue;

            var evolution = EvolutionService.ResolveFirstEvolution(creature, _rules);
            creature.Stage = LifeStage.Adult;
            events.Add(new CreatureBecameAdultEvent(
                creature.Id,
                creature.Name,
                evolution.Specialization,
                evolution.PromotedStatId,
                evolution.PreviousRank,
                evolution.NewRank));
        }

        var hatchQueue = new List<EggData>();
        foreach (var egg in state.OwnedEggs)
        {
            if (egg.State != EggState.Incubating)
                continue;

            egg.IncubationSeconds += elapsedSeconds;
            changed = true;
            if (egg.IncubationSeconds >= egg.RequiredIncubationSeconds)
                hatchQueue.Add(egg);
        }

        foreach (var egg in hatchQueue)
        {
            if (!egg.IsViable)
            {
                egg.State = EggState.Failed;
                egg.FailureResolved = true;
                events.Add(new EggFailedEvent(egg.Id));
                continue;
            }

            var creature = Hatch(state, egg);
            events.Add(new CreatureHatchedEvent(egg.Id, creature.Id, creature.Name));
        }

        return new SimulationStepResult(changed, events);
    }

    private bool AdvanceGardenIncome(GameStateData state, float elapsedSeconds)
    {
        var coinsPerMinute = Math.Max(0.0f, _rules.Economy.GardenCoinsPerMinute);
        if (coinsPerMinute <= 0.0f)
            return false;

        var totalCoins = state.GardenIncomeCoinRemainder +
                         elapsedSeconds * (double)coinsPerMinute / 60.0;
        if (!double.IsFinite(totalCoins) || totalCoins < 0.0)
            return false;

        var wholeCoins = Math.Floor(totalCoins);
        state.GardenIncomeCoinRemainder = totalCoins - wholeCoins;
        if (wholeCoins < 1.0)
            return false;

        var available = Math.Max(0L, (long)int.MaxValue - state.Coins);
        var awarded = Math.Min((long)wholeCoins, available);
        if (awarded <= 0)
            return false;

        state.Coins += (int)awarded;
        return true;
    }

    private VoidlingData Hatch(GameStateData state, EggData egg)
    {
        var suffix = state.Voidlings.Count + state.DepartedVoidlings.Count + 1;
        var creature = new VoidlingData
        {
            Id = egg.Id,
            Name = $"Voidling {suffix}",
            Genome = egg.Genome,
            Stage = LifeStage.Child,
            ParentAId = egg.ParentAId,
            ParentBId = egg.ParentBId,
            FamilyGeneration = egg.FamilyGeneration,
            InbreedingBurdenLevel = egg.InbreedingBurdenLevel,
            InbreedingHistoryFlag = egg.InbreedingHistoryFlag,
            TintHex = egg.TintHex,
            RareTraits = egg.RareTraits,
            WorldX = egg.WorldX,
            WorldY = egg.WorldY
        };

        foreach (var statId in _rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;

        state.Voidlings.Add(creature);
        state.OwnedEggs.Remove(egg);
        return creature;
    }
}
