using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Domain.Evolution;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Creatures;

public enum VoidlingCareDemeanor
{
    Settled,
    NeedsCare
}

public sealed record VoidlingStatProfileProjection(
    string StatId,
    int DnaProfile1Rank,
    int DnaProfile2Rank,
    int ExpressedPotentialRank,
    int TrainingPoints,
    int TrainingPointCap,
    int TrainingLevel,
    double TrainingLevelProgress,
    float EffectiveValue);

public sealed record VoidlingRareTraitProfileProjection(
    string TraitId,
    string FounderCreatureId,
    string FounderDisplayName,
    int GenerationFromFounder,
    bool CanTransmit);

/// <summary>
/// Immutable semantic appearance read model. UI/presentation never needs to traverse the mutable
/// Genome to understand color/tone/pattern/shiny/coat genetics.
/// </summary>
public sealed record VoidlingAppearanceProfileProjection(
    int ColorDnaProfile1,
    int ColorDnaProfile2,
    int ExpressedColorProfileIndex,
    int ExpressedColorAllele,
    int ToneDnaProfile1,
    int ToneDnaProfile2,
    int ExpressedToneProfileIndex,
    AppearanceTone Tone,
    int PatternDnaProfile1,
    int PatternDnaProfile2,
    int ExpressedPatternProfileIndex,
    int PatternAllele,
    int ShinyDnaProfile1,
    int ShinyDnaProfile2,
    bool Shiny,
    int CoatDnaProfile1,
    int CoatDnaProfile2,
    int ExpressedCoatProfileIndex,
    int CoatAllele);

public sealed record VoidlingProfileProjection(
    string CreatureId,
    string DisplayName,
    bool IsAdult,
    EvolutionSpecialization EvolutionSpecialization,
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
    IReadOnlyList<VoidlingRareTraitProfileProjection> RareTraits,
    VoidlingAppearanceProfileProjection Appearance)
{
    public VoidlingCareDemeanor CareDemeanor { get; init; } = VoidlingCareDemeanor.NeedsCare;

    // Null until the player has actually discovered the preference. Presentation never receives the
    // hidden FavoriteFoodId through this read model before that point.
    public string? DiscoveredFavoriteFoodId { get; init; }
}

/// <summary>
/// Builds immutable player-information read models from the mutable save aggregate. UI receives
/// explicit DNA-profile values, trained progression/caps, semantic evolution/appearance and lineage
/// labels without traversing Genome, TrainingPoints, RareTraits or lineage collections itself. This
/// intentionally reports current facts only; it does not calculate exact offspring probabilities,
/// expose hidden evolution influence values, or reveal hidden happiness/stress numbers.
/// </summary>
public sealed class VoidlingProfileProjectionService
{
    private readonly GameBalanceRules _rules;
    private readonly IReadOnlyList<string> _statIds;
    private readonly StatCalculator _stats;
    private readonly AppearancePhenotypeResolver _appearance;
    private readonly LineageArchiveService _lineage = new();

    public VoidlingProfileProjectionService(GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
        _statIds = rules.Genetics.StatIds;
        _stats = new StatCalculator(rules.Stats);
        _appearance = new AppearancePhenotypeResolver(rules.Appearance);
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
                _stats.GetTrainingPointCap(creature, statId),
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

        var phenotype = _appearance.Resolve(creature.Genome);
        var appearance = new VoidlingAppearanceProfileProjection(
            creature.Genome.ColorAlleleA,
            creature.Genome.ColorAlleleB,
            creature.Genome.ExpressedColorIndex,
            phenotype.ColorAllele,
            creature.Genome.ToneAlleleA,
            creature.Genome.ToneAlleleB,
            creature.Genome.ExpressedToneIndex,
            phenotype.Tone,
            creature.Genome.PatternAlleleA,
            creature.Genome.PatternAlleleB,
            creature.Genome.ExpressedPatternIndex,
            phenotype.PatternAllele,
            creature.Genome.ShinyAlleleA,
            creature.Genome.ShinyAlleleB,
            phenotype.Shiny,
            creature.Genome.CoatAlleleA,
            creature.Genome.CoatAlleleB,
            creature.Genome.ExpressedCoatIndex,
            phenotype.CoatAllele);

        return new VoidlingProfileProjection(
            creature.Id,
            creature.Name,
            creature.Stage == LifeStage.Adult,
            creature.EvolutionSpecialization,
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
            rareTraits,
            appearance)
        {
            CareDemeanor = ResolveCareDemeanor(creature),
            DiscoveredFavoriteFoodId = creature.FavoriteFoodDiscovered &&
                                       _statIds.Contains(creature.FavoriteFoodId ?? string.Empty)
                ? creature.FavoriteFoodId
                : null
        };
    }

    private VoidlingCareDemeanor ResolveCareDemeanor(VoidlingData creature)
        => creature.Needs.Happiness >= _rules.Reincarnation.MinimumHappiness &&
           creature.Needs.Stress <= _rules.Reincarnation.MaximumStress
            ? VoidlingCareDemeanor.Settled
            : VoidlingCareDemeanor.NeedsCare;

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
