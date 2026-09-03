using System;
using System.Collections.Generic;
using Voidling.Domain.Care;
using Voidling.Domain.Evolution;
using Voidling.Domain.Hatching;
using Voidling.Domain.Lifecycle;
using Voidling.Domain.Rules;
using Voidling.Domain.Shop;
using Voidling.Domain.Training;
using VoidlingGame;

namespace Voidling.Application.Simulation;

public abstract record GameSimulationEvent;
public sealed record CreatureBecameAdultEvent(string CreatureId, string Name, EvolutionSpecialization Specialization, string PromotedStatId, int PreviousRank, int NewRank) : GameSimulationEvent;
public sealed record CreatureEnteredCocoonEvent(string CreatureId, string Name, bool WillReincarnate) : GameSimulationEvent;
public sealed record CreatureReincarnatedEvent(string CreatureId, string Name, int ReincarnationCount) : GameSimulationEvent;
public sealed record CreatureDiedEvent(string CreatureId, string Name) : GameSimulationEvent;
public sealed record CreatureCareRiskEvent(string CreatureId, string Name) : GameSimulationEvent;
public sealed record CreaturePassiveTrainingCappedEvent(string CreatureId, string Name, string StatId) : GameSimulationEvent;
public sealed record CreatureHatchedEvent(string EggId, string CreatureId, string Name) : GameSimulationEvent;
public sealed record EggFailedEvent(string EggId) : GameSimulationEvent;
public sealed record EggWaitingForGardenSpaceEvent(string EggId) : GameSimulationEvent;
public sealed record SimulationStepResult(bool Changed, IReadOnlyList<GameSimulationEvent> Events);

/// <summary>Deterministic explicit-elapsed garden simulation. No wall-clock or presentation ownership.</summary>
public sealed class AdvanceSimulationUseCase
{
    private const float TimeEpsilon = 0.000001f;
    private readonly GameBalanceRules _rules;
    private readonly CreatureNeedsService _needs = new();
    private readonly ReincarnationService _reincarnation = new();
    private readonly PassiveTrainingService _passiveTraining = new();
    private readonly StoreEggFactory _storeEggFactory;

    public AdvanceSimulationUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _storeEggFactory = new StoreEggFactory(rules);
    }

    public SimulationStepResult Advance(GameStateData state, float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0.0f)
            return new SimulationStepResult(false, Array.Empty<GameSimulationEvent>());

        var changed = AdvanceGardenIncome(state, elapsedSeconds);
        changed |= AdvanceShopEggRotation(state, elapsedSeconds);
        var events = new List<GameSimulationEvent>();
        var deathQueue = new List<VoidlingData>();

        foreach (var creature in state.Voidlings)
        {
            changed |= AdvanceCreature(creature, elapsedSeconds, events, out var died);
            if (died) deathQueue.Add(creature);
        }
        foreach (var creature in deathQueue)
        {
            if (!state.Voidlings.Remove(creature)) continue;
            if (!state.DepartedVoidlings.Contains(creature)) state.DepartedVoidlings.Add(creature);
        }

        var hatchQueue = new List<EggData>();
        foreach (var egg in state.OwnedEggs)
        {
            if (egg.State == EggState.WaitingForSpace) { hatchQueue.Add(egg); continue; }
            if (egg.State != EggState.Incubating) continue;
            egg.IncubationSeconds += elapsedSeconds;
            changed = true;
            if (egg.IncubationSeconds >= egg.RequiredIncubationSeconds) hatchQueue.Add(egg);
        }
        foreach (var egg in hatchQueue)
        {
            if (!egg.IsViable)
            {
                egg.State = EggState.Failed; egg.FailureResolved = true; changed = true;
                events.Add(new EggFailedEvent(egg.Id)); continue;
            }
            if (state.Voidlings.Count >= Math.Max(1, _rules.Garden.MaxPopulation))
            {
                if (egg.State != EggState.WaitingForSpace)
                { egg.State = EggState.WaitingForSpace; changed = true; events.Add(new EggWaitingForGardenSpaceEvent(egg.Id)); }
                continue;
            }
            var creature = Hatch(state, egg); changed = true;
            events.Add(new CreatureHatchedEvent(egg.Id, creature.Id, creature.Name));
        }
        return new SimulationStepResult(changed, events);
    }

    private bool AdvanceCreature(VoidlingData creature, float elapsedSeconds, List<GameSimulationEvent> events, out bool died)
    {
        died = false; var changed = false; var remaining = elapsedSeconds;
        while (remaining > TimeEpsilon)
        {
            if (creature.Stage == LifeStage.Child)
            {
                var boundary = Math.Max(0.1f, _rules.Lifecycle.ChildToAdultSeconds);
                var age = Math.Max(0f, creature.AgeSeconds);
                var toAdult = Math.Max(0f, boundary - age);
                if (toAdult > TimeEpsilon)
                {
                    var segment = Math.Min(remaining, toAdult);
                    changed |= AdvanceCreatureContinuousState(creature, segment, events);
                    creature.AgeSeconds = Math.Min(boundary, age + segment); changed = true;
                    remaining = Math.Max(0f, remaining - segment);
                    if (creature.AgeSeconds < boundary - TimeEpsilon) break;
                }
                else if (!creature.AgeSeconds.Equals(boundary)) { creature.AgeSeconds = boundary; changed = true; }

                var evolution = EvolutionService.ResolveFirstEvolution(creature, _rules);
                creature.Stage = LifeStage.Adult; creature.AgeSeconds = boundary;
                events.Add(new CreatureBecameAdultEvent(creature.Id, creature.Name, evolution.Specialization, evolution.PromotedStatId, evolution.PreviousRank, evolution.NewRank));
                changed = true; continue;
            }

            var lifespan = Math.Max(0.1f, _rules.Reincarnation.AdultLifespanSeconds);
            var adultAge = Math.Max(0f, creature.AdultAgeSeconds);
            var toEnd = Math.Max(0f, lifespan - adultAge);
            if (toEnd > TimeEpsilon)
            {
                var segment = Math.Min(remaining, toEnd);
                changed |= AdvanceCreatureContinuousState(creature, segment, events);
                creature.AdultAgeSeconds = Math.Min(lifespan, adultAge + segment); changed = true;
                remaining = Math.Max(0f, remaining - segment);
                if (creature.AdultAgeSeconds < lifespan - TimeEpsilon) break;
            }
            else if (!creature.AdultAgeSeconds.Equals(lifespan)) { creature.AdultAgeSeconds = lifespan; changed = true; }

            var decision = _reincarnation.Decide(creature, _rules.Reincarnation);
            var willReincarnate = decision.Outcome == LifecycleEndOutcome.Reincarnate;
            events.Add(new CreatureEnteredCocoonEvent(creature.Id, creature.Name, willReincarnate));
            if (willReincarnate)
            {
                _reincarnation.ApplyReincarnation(creature, _rules.Reincarnation);
                events.Add(new CreatureReincarnatedEvent(creature.Id, creature.Name, creature.ReincarnationCount));
                changed = true; continue;
            }
            creature.DepartureReason = CreatureDepartureReason.Death;
            events.Add(new CreatureDiedEvent(creature.Id, creature.Name)); died = true; changed = true; break;
        }
        return changed;
    }

    private bool AdvanceCreatureContinuousState(VoidlingData creature, float elapsedSeconds, List<GameSimulationEvent> events)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f) return false;
        var changed = false;
        var safe = IsCareLifecycleSafe(creature);
        changed |= _needs.Advance(creature.Needs, elapsedSeconds, _rules.Needs);
        if (safe && !IsCareLifecycleSafe(creature)) events.Add(new CreatureCareRiskEvent(creature.Id, creature.Name));
        var passive = _passiveTraining.Advance(creature, elapsedSeconds, _rules);
        changed |= passive.Changed;
        if (passive.ReachedCap) events.Add(new CreaturePassiveTrainingCappedEvent(creature.Id, creature.Name, passive.StatId));
        if (creature.BreedCooldownSeconds > 0f)
        {
            var next = Math.Max(0f, creature.BreedCooldownSeconds - elapsedSeconds);
            if (!creature.BreedCooldownSeconds.Equals(next)) { creature.BreedCooldownSeconds = next; changed = true; }
        }
        return changed;
    }

    private bool IsCareLifecycleSafe(VoidlingData creature)
        => creature.Needs.Happiness >= _rules.Reincarnation.MinimumHappiness && creature.Needs.Stress <= _rules.Reincarnation.MaximumStress;

    private bool AdvanceGardenIncome(GameStateData state, float elapsedSeconds)
    {
        var rate = Math.Max(0f, _rules.Economy.GardenCoinsPerMinute);
        if (rate <= 0f) return false;
        var total = state.GardenIncomeCoinRemainder + elapsedSeconds * (double)rate / 60.0;
        if (!double.IsFinite(total) || total < 0) return false;
        var whole = Math.Floor(total); state.GardenIncomeCoinRemainder = total - whole;
        if (whole < 1) return false;
        var available = Math.Max(0L, (long)int.MaxValue - state.Coins);
        var awarded = Math.Min((long)whole, available);
        if (awarded <= 0) return false;
        state.Coins += (int)awarded; return true;
    }

    private bool AdvanceShopEggRotation(GameStateData state, float elapsedSeconds)
    {
        var interval = Math.Max(1.0, _rules.Shop.EggRotationIntervalSeconds);
        var total = state.ShopEggRotationElapsedSeconds + elapsedSeconds;
        if (!double.IsFinite(total) || total < 0) { state.ShopEggRotationElapsedSeconds = 0; return false; }
        var rotations = (long)Math.Floor(total / interval);
        state.ShopEggRotationElapsedSeconds = total - rotations * interval;
        if (rotations <= 0) return false;
        RefreshStoreInventory(state, rotations); return true;
    }

    private void RefreshStoreInventory(GameStateData state, long rotations)
    {
        var slots = Math.Max(1, _rules.Shop.StoreEggSlotCount);
        var baseCounter = state.SeedCounter;
        var allocations = unchecked(rotations * (long)slots);
        var firstFinalOffset = unchecked((rotations - 1L) * slots);
        var replacements = new List<EggData>(slots);
        for (var slot = 0; slot < slots; slot++)
        {
            var counter = unchecked(baseCounter + firstFinalOffset + slot + 1L);
            var seed = unchecked((ulong)counter);
            replacements.Add(_storeEggFactory.Create($"shop-{seed:x16}", seed));
        }
        state.SeedCounter = unchecked(baseCounter + allocations);
        state.StoreEggs.Clear(); state.StoreEggs.AddRange(replacements);
        state.ShopRareOfferItemId = RareShopOfferResolver.Resolve(unchecked((ulong)state.SeedCounter), _rules.Shop.RareOfferAppearanceChance);
    }

    private VoidlingData Hatch(GameStateData state, EggData egg)
    {
        var suffix = state.Voidlings.Count + state.DepartedVoidlings.Count + 1;
        var appearance = (egg.Appearance ?? new VoidlingAppearanceData()).CreateCanonicalCopy();
        var creature = new VoidlingData
        {
            Id = egg.Id, Name = $"Voidling {suffix}", Genome = egg.Genome, Stage = LifeStage.Child,
            ParentAId = egg.ParentAId, ParentBId = egg.ParentBId, FamilyGeneration = egg.FamilyGeneration,
            InbreedingBurdenLevel = egg.InbreedingBurdenLevel, InbreedingHistoryFlag = egg.InbreedingHistoryFlag,
            TintHex = egg.TintHex, Appearance = appearance, RareTraits = egg.RareTraits,
            Needs = new CreatureNeedsState(), WorldX = egg.WorldX, WorldY = egg.WorldY
        };
        foreach (var statId in _rules.Genetics.StatIds) creature.TrainingPoints[statId] = 0;
        state.Voidlings.Add(creature);
        state.EggShells.Add(new EggShellData { Id = egg.Id, Source = egg.Source, TintHex = egg.TintHex });
        state.OwnedEggs.Remove(egg);
        return creature;
    }
}
