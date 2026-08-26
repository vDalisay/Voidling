using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Creatures;

public sealed record VoidlingStatProfileProjection(
    string StatId,
    int DnaProfile1Rank,
    int DnaProfile2Rank,
    int ExpressedPotentialRank,
    int TrainingPoints,
    int TrainingLevel,
    double TrainingLevelProgress,
    float EffectiveValue);

public sealed record VoidlingRareTraitProfileProjection(
    string TraitId,
    string FounderCreatureId,
    string FounderDisplayName,
    int GenerationFromFounder,
    bool CanTransmit);

public sealed record VoidlingProfileProjection(
    string CreatureId,
    string DisplayName,
    bool IsAdult,
    int FamilyGeneration,
    int ActiveInbreedingBurden,
    bool InbreedingHistoryFlag,
    string TintHex,
    bool HasAngelMutation,
    int OtherMutationCount,
    int ColorDnaProfile1,
    int ColorDnaProfile2,
    int ExpressedColorProfileIndex,
    IReadOnlyList<VoidlingStatProfileProjection> Stats,
    IReadOnlyList<VoidlingRareTraitProfileProjection> RareTraits);

/// <summary>
/// Builds immutable player-information read models from the mutable save aggregate. UI receives
/// explicit DNA-profile values, trained progression and lineage labels without traversing Genome,
/// TrainingPoints, RareTraits or lineage collections itself. This intentionally reports current
/// facts only; it does not calculate exact offspring probabilities.
/// </summary>
public sealed class VoidlingProfileProjectionService
{
    private readonly IReadOnlyList<string> _statIds;
    private readonly StatCalculator _stats;
    private readonly LineageArchiveService _lineage = new();

    public VoidlingProfileProjectionService(GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _statIds = rules.Genetics.StatIds;
        _stats = new StatCalculator(rules.Stats);
    }

    public VoidlingProfileProjection? Create(GameStateData state, string creatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(creatureId))
            return null;

        var creature = state.Voidlings.FirstOrDefault(candidate =>
                           string.Equals(candidate.Id, creatureId, StringComparison.Ordinal))
                       ?? state.DepartedVoidlings.FirstOrDefault(candidate =>
                           string.Equals(candidate.Id, creatureId, StringComparison.Ordinal));
        if (creature == null)
            return null;

        return Project(creature, BuildLineageNameIndex(state));
    }

    public IReadOnlyList<VoidlingProfileProjection> CreateActive(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var lineageNames = BuildLineageNameIndex(state);
        return state.Voidlings.Select(creature => Project(creature, lineageNames)).ToArray();
    }

    private VoidlingProfileProjection Project(
        VoidlingData creature,
        IReadOnlyDictionary<string, string> lineageNames)
    {
        var stats = _statIds.Select(statId =>
        {
            var gene = StatCalculator.GetGene(creature, statId);
            return new VoidlingStatProfileProjection(
                statId,
                gene.AlleleA,
                gene.AlleleB,
                gene.ExpressedValue,
                _stats.GetTrainingPoints(creature, statId),
                _stats.GetLevel(creature, statId),
                _stats.GetLevelProgress(creature, statId),
                _stats.GetEffectiveStat(creature, statId));
        }).ToArray();

        var rareTraits = creature.RareTraits?
            .Where(trait => !string.IsNullOrWhiteSpace(trait.TraitId))
            .Select(trait => new VoidlingRareTraitProfileProjection(
                trait.TraitId,
                trait.FounderCreatureId,
                ResolveFounderName(trait.FounderCreatureId, lineageNames),
                Math.Max(0, trait.GenerationFromFounder),
                trait.CanTransmit))
            .ToArray() ?? Array.Empty<VoidlingRareTraitProfileProjection>();
        var hasAngelMutation = rareTraits.Any(trait =>
            string.Equals(trait.TraitId, MutationIds.Angel, StringComparison.OrdinalIgnoreCase));
        var otherMutationCount = rareTraits.Count(trait =>
            !string.Equals(trait.TraitId, MutationIds.Angel, StringComparison.OrdinalIgnoreCase));

        return new VoidlingProfileProjection(
            creature.Id,
            creature.Name,
            creature.Stage == LifeStage.Adult,
            Math.Max(0, creature.FamilyGeneration),
            Math.Max(0, creature.InbreedingBurdenLevel),
            creature.InbreedingHistoryFlag,
            creature.TintHex,
            hasAngelMutation,
            otherMutationCount,
            creature.Genome.ColorAlleleA,
            creature.Genome.ColorAlleleB,
            creature.Genome.ExpressedColorIndex,
            stats,
            rareTraits);
    }

    private IReadOnlyDictionary<string, string> BuildLineageNameIndex(GameStateData state)
        => _lineage.GetEffectiveLineage(state)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.CreatureId))
            .GroupBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => string.IsNullOrWhiteSpace(group.First().DisplayName)
                    ? "Unknown"
                    : group.First().DisplayName,
                StringComparer.Ordinal);

    private static string ResolveFounderName(
        string founderCreatureId,
        IReadOnlyDictionary<string, string> lineageNames)
    {
        if (string.IsNullOrWhiteSpace(founderCreatureId))
            return "Unknown";
        return lineageNames.TryGetValue(founderCreatureId, out var name) ? name : "Unknown";
    }
}
