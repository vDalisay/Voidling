using System;
using System.Collections.Generic;
using Voidling.Domain.Care;
using Voidling.Domain.Evolution;
using Voidling.Domain.Lifecycle;
using Voidling.Domain.Rules;
using Voidling.Domain.Training;
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
public sealed record CreatureEnteredCocoonEvent(string CreatureId, string Name, bool WillReincarnate) : GameSimulationEvent;
public sealed record CreatureReincarnatedEvent(string CreatureId, string Name, int ReincarnationCount) : GameSimulationEvent;
public sealed record CreatureDiedEvent(string CreatureId, string Name) : GameSimulationEvent;
public sealed record CreatureCareRiskEvent(string CreatureId, string Name) : GameSimulationEvent;
public sealed record CreaturePassiveTrainingCappedEvent(string CreatureId, string Name, string StatId) : GameSimulationEvent;
public sealed record CreatureHatchedEvent(string EggId, string CreatureId, string Name) : GameSimulationEvent;
public sealed record EggFailedEvent(string EggId) : GameSimulationEvent;

public sealed record SimulationStepResult(bool Changed, IReadOnlyList<GameSimulationEvent> Events);

/// <summary>
/// Advances persistent garden simulation from an explicit elapsed duration. This contains no
/// Godot frame, scene, UI, persistence, or wall-clock dependency. Creature time is segmented at
/// child/adult/lifecycle boundaries so the same elapsed duration produces the same authoritative
/// state whether it arrives in one large update or many smaller updates.
/// </summary>
public sealed class AdvanceSimulationUseCase
{
    private const float TimeEpsilon = 0.000001f;

    private readonly GameBalanceRules _rules;
    private readonly CreatureNeedsService _needs = new();
    private readonly ReincarnationService _reincarnation = new();
    private readonly PassiveTrainingService _passiveTraining = new();

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
        var deathQueue = new List<VoidlingData>();

        foreach (var creature in state.Voidlings)
        {
            changed |= AdvanceCreature(creature, elapsedSeconds, events, out var died);
            if (died)
                deathQueue.Add(creature);
        }

        foreach (var creature in deathQueue)
        {
            if (!state.Voidlings.Remove(creature))
                continue;
            if (!state.DepartedVoidlings.Contains(creature))
                state.DepartedVoidlings.Add(creature);
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

    private bool AdvanceCreature(
        VoidlingData creature,
        float elapsedSeconds,
        List<GameSimulationEvent> events,
        out bool died)
    {
        died = false;
        var changed = false;
        var remaining = elapsedSeconds;

        while (remaining > TimeEpsilon)
        {
            if (creature.Stage == LifeStage.Child)
            {
                var childToAdultSeconds = Math.Max(0.1f, _rules.Lifecycle.ChildToAdultSeconds);
                var age = Math.Max(0.0f, creature.AgeSeconds);
                var timeToAdult = Math.Max(0.0f, childToAdultSeconds - age);

                if (timeToAdult > TimeEpsilon)
                {
                    var segment = Math.Min(remaining, timeToAdult);
                    changed |= AdvanceCreatureContinuousState(creature, segment, events);
                    creature.AgeSeconds = Math.Min(childToAdultSeconds, age + segment);
                    changed = true;
                    remaining = Math.Max(0.0f, remaining - segment);

                    if (creature.AgeSeconds < childToAdultSeconds - TimeEpsilon)
                        break;
                }
                else if (!creature.AgeSeconds.Equals(childToAdultSeconds))
                {
                    creature.AgeSeconds = childToAdultSeconds;
                    changed = true;
                }

                var evolution = EvolutionService.ResolveFirstEvolution(creature, _rules);
                creature.Stage = LifeStage.Adult;
                creature.AgeSeconds = childToAdultSeconds;
                events.Add(new CreatureBecameAdultEvent(
                    creature.Id,
                    creature.Name,
                    evolution.Specialization,
                    evolution.PromotedStatId,
                    evolution.PreviousRank,
                    evolution.NewRank));
                changed = true;
                continue;
            }

            var adultLifespanSeconds = Math.Max(0.1f, _rules.Reincarnation.AdultLifespanSeconds);
            var adultAge = Math.Max(0.0f, creature.AdultAgeSeconds);
            var timeToLifecycleEnd = Math.Max(0.0f, adultLifespanSeconds - adultAge);

            if (timeToLifecycleEnd > TimeEpsilon)
            {
                var segment = Math.Min(remaining, timeToLifecycleEnd);
                changed |= AdvanceCreatureContinuousState(creature, segment, events);
                creature.AdultAgeSeconds = Math.Min(adultLifespanSeconds, adultAge + segment);
                changed = true;
                remaining = Math.Max(0.0f, remaining - segment);

                if (creature.AdultAgeSeconds < adultLifespanSeconds - TimeEpsilon)
                    break;
            }
            else if (!creature.AdultAgeSeconds.Equals(adultLifespanSeconds))
            {
                creature.AdultAgeSeconds = adultLifespanSeconds;
                changed = true;
            }

            var decision = _reincarnation.Decide(creature, _rules.Reincarnation);
            var willReincarnate = decision.Outcome == LifecycleEndOutcome.Reincarnate;
            events.Add(new CreatureEnteredCocoonEvent(creature.Id, creature.Name, willReincarnate));

            if (willReincarnate)
            {
                _reincarnation.ApplyReincarnation(creature, _rules.Reincarnation);
                events.Add(new CreatureReincarnatedEvent(creature.Id, creature.Name, creature.ReincarnationCount));
                changed = true;
                continue;
            }

            creature.DepartureReason = CreatureDepartureReason.Death;
            events.Add(new CreatureDiedEvent(creature.Id, creature.Name));
            died = true;
            changed = true;
            break;
        }

        return changed;
    }

    private bool AdvanceCreatureContinuousState(
        VoidlingData creature,
        float elapsedSeconds,
        List<GameSimulationEvent> events)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0.0f)
            return false;

        var changed = false;
        var careWasLifecycleSafe = IsCareLifecycleSafe(creature);
        changed |= _needs.Advance(creature.Needs, elapsedSeconds, _rules.Needs);
        if (careWasLifecycleSafe && !IsCareLifecycleSafe(creature))
            events.Add(new CreatureCareRiskEvent(creature.Id, creature.Name));

        var passiveResult = _passiveTraining.Advance(creature, elapsedSeconds, _rules);
        changed |= passiveResult.Changed;
        if (passiveResult.ReachedCap)
            events.Add(new CreaturePassiveTrainingCappedEvent(creature.Id, creature.Name, passiveResult.StatId));

        if (creature.BreedCooldownSeconds > 0.0f)
        {
            var nextCooldown = Math.Max(0.0f, creature.BreedCooldownSeconds - elapsedSeconds);
            if (!creature.BreedCooldownSeconds.Equals(nextCooldown))
            {
                creature.BreedCooldownSeconds = nextCooldown;
                changed = true;
            }
        }

        return changed;
    }

    private bool IsCareLifecycleSafe(VoidlingData creature)
        => creature.Needs.Happiness >= _rules.Reincarnation.MinimumHappiness &&
           creature.Needs.Stress <= _rules.Reincarnation.MaximumStress;

    private bool AdvanceGardenIncome(GameStateData state, float elapsedSeconds)
    {
        var coinsPerMinute = Math.Max(0.0f, _rules.Economy.GardenCoinsPerMinute);
        if (coinsPerMinute <= 0.0f)
            return false;

        var totalCoins = state.GardenIncomeCoinRemainder + elapsedSeconds * (double)coinsPerMinute / 60.0;
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
            Needs = new CreatureNeedsState(),
            WorldX = egg.WorldX,
            WorldY = egg.WorldY
        };

        foreach (var statId in _rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;

        state.Voidlings.Add(creature);
        state.EggShells.Add(new EggShellData
        {
            Id = egg.Id,
            Source = egg.Source,
            TintHex = egg.TintHex
        });
        state.OwnedEggs.Remove(egg);
        return creature;
    }
}
