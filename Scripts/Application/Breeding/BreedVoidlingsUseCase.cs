using System;
using System.Linq;
using Voidling.Application.Ports;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Breeding;

public enum BreedingFailure
{
    None,
    ParentNotFound,
    SameParent,
    ParentNotAdult,
    ParentOnCooldown,
    InvalidEggId,
    DuplicateAssetId,
    PersistenceFailed
}

public readonly record struct BreedingPreview(
    BreedingFailure Failure,
    bool Related,
    int ChildBurden,
    int HatchFailurePercent,
    bool IsCleanOutcross)
{
    public bool CanBreed => Failure == BreedingFailure.None;
}

public sealed record BreedingResult(
    BreedingFailure Failure,
    EggData? Egg,
    bool Related,
    int ChildBurden,
    int HatchFailurePercent)
{
    public bool Succeeded => Failure == BreedingFailure.None && Egg != null;
}

/// <summary>
/// Player-initiated breeding orchestration. IDs/seeds/world coordinates are explicit inputs and
/// all inherited egg state is frozen exactly once at creation time. Execute mutates only the supplied
/// aggregate; ExecuteAndPersist adds the Application persistence boundary and rolls that mutation back
/// when the repository rejects the transaction. Presentation/animation never determines the outcome.
/// </summary>
public sealed class BreedVoidlingsUseCase
{
    private readonly GameBalanceRules _rules;
    private readonly RelationshipService _relationships;
    private readonly LineageArchiveService _lineage;
    private readonly InbreedingBurdenService _burden;
    private readonly GenomeInheritanceService _genomeInheritance;
    private readonly RareTraitInheritanceService _rareTraits;
    private readonly HatchViabilityService _viability;
    private readonly ColorPhenotypeResolver _colors;

    public BreedVoidlingsUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _relationships = new RelationshipService(rules.Genetics.RelatedAncestorDepth);
        _lineage = new LineageArchiveService();
        _burden = new InbreedingBurdenService();
        _genomeInheritance = new GenomeInheritanceService(rules.Genetics);
        _rareTraits = new RareTraitInheritanceService(rules.Genetics);
        _viability = new HatchViabilityService(rules.Breeding);
        _colors = new ColorPhenotypeResolver(rules.Appearance);
    }

    public BreedingPreview Preview(GameStateData state, string parentAId, string parentBId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var (failure, first, second) = Validate(state, parentAId, parentBId);
        if (failure != BreedingFailure.None || first == null || second == null)
            return new BreedingPreview(failure, false, 0, 0, false);

        var related = _relationships.AreRelated(first, second, _lineage.GetEffectiveLineage(state));
        var childBurden = _burden.ComputeChildBurden(first, second, related);
        var maxParentBurden = Math.Max(first.InbreedingBurdenLevel, second.InbreedingBurdenLevel);
        return new BreedingPreview(
            BreedingFailure.None,
            related,
            childBurden,
            _viability.FailurePercent(childBurden),
            !related && childBurden < maxParentBurden);
    }

    public BreedingResult Execute(
        GameStateData state,
        string parentAId,
        string parentBId,
        ulong eggSeed,
        string eggId,
        float worldX,
        float worldY)
    {
        ArgumentNullException.ThrowIfNull(state);
        var (failure, first, second) = Validate(state, parentAId, parentBId);
        if (failure != BreedingFailure.None || first == null || second == null)
            return new BreedingResult(failure, null, false, 0, 0);

        var idFailure = ValidateEggId(state, eggId);
        if (idFailure != BreedingFailure.None)
            return new BreedingResult(idFailure, null, false, 0, 0);

        var related = _relationships.AreRelated(first, second, _lineage.GetEffectiveLineage(state));
        var childBurden = _burden.ComputeChildBurden(first, second, related);
        var genome = _genomeInheritance.CreateChild(first, second, eggSeed);
        var egg = new EggData
        {
            Id = eggId,
            Source = EggSource.Bred,
            Seed = eggSeed,
            Genome = genome,
            ParentAId = first.Id,
            ParentBId = second.Id,
            FamilyGeneration = Math.Max(first.FamilyGeneration, second.FamilyGeneration) + 1,
            InbreedingBurdenLevel = childBurden,
            InbreedingHistoryFlag = related || first.InbreedingHistoryFlag || second.InbreedingHistoryFlag,
            IsViable = _viability.RollViability(eggSeed, childBurden),
            FailureResolved = true,
            RequiredIncubationSeconds = _rules.Hatching.IncubationSeconds,
            TintHex = _colors.ResolveTint(genome),
            RareTraits = _rareTraits.Inherit(first, second, eggSeed),
            WorldX = worldX,
            WorldY = worldY
        };

        state.OwnedEggs.Add(egg);
        first.BreedCooldownSeconds = _rules.Breeding.CooldownSeconds;
        second.BreedCooldownSeconds = _rules.Breeding.CooldownSeconds;

        return new BreedingResult(
            BreedingFailure.None,
            egg,
            related,
            childBurden,
            _viability.FailurePercent(childBurden));
    }

    public BreedingResult ExecuteAndPersist(
        GameStateData state,
        string parentAId,
        string parentBId,
        ulong eggSeed,
        string eggId,
        float worldX,
        float worldY,
        IGameStateRepository repository)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(repository);

        var first = state.Voidlings.FirstOrDefault(v => v.Id == parentAId);
        var second = state.Voidlings.FirstOrDefault(v => v.Id == parentBId);
        var firstCooldown = first?.BreedCooldownSeconds ?? 0.0f;
        var secondCooldown = second?.BreedCooldownSeconds ?? 0.0f;
        var lineageBefore = state.LineageArchive?.ToList() ?? new();

        var result = Execute(state, parentAId, parentBId, eggSeed, eggId, worldX, worldY);
        if (!result.Succeeded || result.Egg == null)
            return result;

        try
        {
            repository.Save(state);
            return result;
        }
        catch
        {
            state.OwnedEggs.Remove(result.Egg);
            if (first != null)
                first.BreedCooldownSeconds = firstCooldown;
            if (second != null)
                second.BreedCooldownSeconds = secondCooldown;
            state.LineageArchive = lineageBefore;

            return new BreedingResult(
                BreedingFailure.PersistenceFailed,
                null,
                result.Related,
                result.ChildBurden,
                result.HatchFailurePercent);
        }
    }

    private static (BreedingFailure Failure, VoidlingData? First, VoidlingData? Second) Validate(
        GameStateData state,
        string firstId,
        string secondId)
    {
        var first = state.Voidlings.FirstOrDefault(v => v.Id == firstId);
        var second = state.Voidlings.FirstOrDefault(v => v.Id == secondId);
        if (first == null || second == null)
            return (BreedingFailure.ParentNotFound, first, second);
        if (first.Id == second.Id)
            return (BreedingFailure.SameParent, first, second);
        if (first.Stage != LifeStage.Adult || second.Stage != LifeStage.Adult)
            return (BreedingFailure.ParentNotAdult, first, second);
        if (first.BreedCooldownSeconds > 0.0f || second.BreedCooldownSeconds > 0.0f)
            return (BreedingFailure.ParentOnCooldown, first, second);
        return (BreedingFailure.None, first, second);
    }

    private static BreedingFailure ValidateEggId(GameStateData state, string eggId)
    {
        if (string.IsNullOrWhiteSpace(eggId) || eggId.Length > 128)
            return BreedingFailure.InvalidEggId;

        var duplicate = state.OwnedEggs.Any(egg => egg.Id == eggId) ||
                        state.StoreEggs.Any(egg => egg.Id == eggId) ||
                        state.Voidlings.Any(creature => creature.Id == eggId) ||
                        state.DepartedVoidlings.Any(creature => creature.Id == eggId) ||
                        state.LineageArchive.Any(entry => entry.CreatureId == eggId);
        return duplicate ? BreedingFailure.DuplicateAssetId : BreedingFailure.None;
    }
}
