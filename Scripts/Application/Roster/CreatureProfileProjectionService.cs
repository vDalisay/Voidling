using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Roster;

public enum CreatureCareDemeanor
{
    Settled,
    NeedsCare
}

public sealed record CreatureProfileStatProjection(
    string StatId,
    string InheritedRank,
    int TrainingLevel,
    int EffectiveValue,
    double TrainingProgress,
    string Dna1Rank,
    string Dna2Rank)
{
    public int TrainingPoints { get; init; }
    public int TrainingPointCap { get; init; }
}

public sealed record CreatureProfileRareTraitProjection(
    string TraitId,
    string FounderName,
    int GenerationFromFounder,
    bool CanTransmit);

public sealed record CreaturePersonalityProjection(string TraitId, PersonalityPolarity Polarity);

public sealed record CreatureProfileProjection(
    string CreatureId,
    string Name,
    bool IsAdult,
    int FamilyGeneration,
    int InbreedingBurden,
    LineageRiskBand LineageRisk,
    string TintHex,
    string VisualTypeId,
    float PaletteHue,
    IReadOnlyList<string> LayerIds,
    bool HasAngelMutation,
    int OtherMutationCount,
    int ColorAlleleA,
    int ColorAlleleB,
    int ExpressedColorIndex,
    IReadOnlyList<CreatureProfileStatProjection> Stats,
    IReadOnlyList<CreatureProfileRareTraitProjection> RareTraits)
{
    public CreatureCareDemeanor CareDemeanor { get; init; } = CreatureCareDemeanor.NeedsCare;
    public string? DiscoveredFavoriteFoodId { get; init; }
    public CreaturePersonalityProjection Personality { get; init; } =
        new(string.Empty, PersonalityPolarity.Neutral);
}

/// <summary>
/// Builds immutable player-information snapshots. Presentation receives already interpreted ranks,
/// training progress, effective stats, mutation metadata, semantic appearance and lineage risk
/// instead of traversing mutable save/domain objects or reimplementing gameplay rules.
/// </summary>
public sealed class CreatureProfileProjectionService
{
    private const string AngelMutationId = "Angel";

    private readonly GameBalanceRules _rules;
    private readonly IReadOnlyList<string> _statIds;
    private readonly StatCalculator _stats;

    public CreatureProfileProjectionService(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _statIds = rules.Genetics.StatIds;
        _stats = new StatCalculator(rules.Stats);
    }

    public CreatureProfileProjection? Create(GameStateData state, string creatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(creatureId))
            return null;

        var creature = state.Voidlings.FirstOrDefault(value =>
            string.Equals(value.Id, creatureId, StringComparison.Ordinal));
        if (creature == null)
            return null;

        var names = BuildLineageNameIndex(state);
        var stats = _statIds.Select(statId =>
        {
            var gene = StatCalculator.GetGene(creature, statId);
            return new CreatureProfileStatProjection(
                statId,
                GradeName(gene.ExpressedValue),
                _stats.GetLevel(creature, statId),
                Math.Clamp((int)MathF.Round(_stats.GetEffectiveStat(creature, statId)), 0, 100),
                _stats.GetLevelProgress(creature, statId),
                GradeName(gene.AlleleA),
                GradeName(gene.AlleleB))
            {
                TrainingPoints = _stats.GetTrainingPoints(creature, statId),
                TrainingPointCap = _stats.GetTrainingPointCap(creature, statId)
            };
        }).ToArray();

        var rareTraits = creature.RareTraits?
            .Where(trait => !string.IsNullOrWhiteSpace(trait.TraitId))
            .Select(trait => new CreatureProfileRareTraitProjection(
                trait.TraitId,
                names.TryGetValue(trait.FounderCreatureId ?? string.Empty, out var founderName)
                    ? founderName
                    : "Unknown",
                Math.Max(0, trait.GenerationFromFounder),
                trait.CanTransmit))
            .ToArray() ?? Array.Empty<CreatureProfileRareTraitProjection>();
        var hasAngel = rareTraits.Any(trait =>
            string.Equals(trait.TraitId, AngelMutationId, StringComparison.OrdinalIgnoreCase));
        var appearance = (creature.Appearance ?? new VoidlingAppearanceData()).CreateCanonicalCopy();
        var layerIds = appearance.LayerIds.ToArray();
        var personality = PersonalityGenetics.ResolveDominant(creature.Genome);

        return new CreatureProfileProjection(
            creature.Id,
            creature.Name,
            creature.Stage == LifeStage.Adult,
            Math.Max(0, creature.FamilyGeneration),
            Math.Max(0, creature.InbreedingBurdenLevel),
            LineageRiskProjection.FromBurden(creature.InbreedingBurdenLevel),
            creature.TintHex,
            appearance.VisualTypeId,
            appearance.PaletteHue,
            Array.AsReadOnly(layerIds),
            hasAngel,
            rareTraits.Count(trait =>
                !string.Equals(trait.TraitId, AngelMutationId, StringComparison.OrdinalIgnoreCase)),
            creature.Genome.ColorAlleleA,
            creature.Genome.ColorAlleleB,
            creature.Genome.ExpressedColorIndex,
            Array.AsReadOnly(stats),
            Array.AsReadOnly(rareTraits))
        {
            CareDemeanor = creature.Needs.Happiness >= _rules.Reincarnation.MinimumHappiness &&
                           creature.Needs.Stress <= _rules.Reincarnation.MaximumStress
                ? CreatureCareDemeanor.Settled
                : CreatureCareDemeanor.NeedsCare,
            DiscoveredFavoriteFoodId = creature.FavoriteFoodDiscovered &&
                                       _statIds.Contains(creature.FavoriteFoodId ?? string.Empty)
                ? creature.FavoriteFoodId
                : null,
            Personality = new CreaturePersonalityProjection(personality.TraitId, personality.Polarity)
        };
    }

    private static Dictionary<string, string> BuildLineageNameIndex(GameStateData state)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var creature in state.Voidlings.Concat(state.DepartedVoidlings))
        {
            if (!string.IsNullOrWhiteSpace(creature.Id) && !names.ContainsKey(creature.Id))
                names.Add(creature.Id, string.IsNullOrWhiteSpace(creature.Name) ? "Unknown" : creature.Name);
        }

        foreach (var archive in state.LineageArchive)
        {
            if (!string.IsNullOrWhiteSpace(archive.CreatureId) && !names.ContainsKey(archive.CreatureId))
                names.Add(archive.CreatureId, string.IsNullOrWhiteSpace(archive.DisplayName) ? "Unknown" : archive.DisplayName);
        }

        return names;
    }

    private static string GradeName(int grade)
        => Math.Clamp(grade, 0, 5) switch
        {
            0 => "E",
            1 => "D",
            2 => "C",
            3 => "B",
            4 => "A",
            _ => "S"
        };
}
