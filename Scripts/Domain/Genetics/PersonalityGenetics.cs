using System;
using System.Collections.Generic;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

/// <summary>
/// Stable semantic personality loci reserved by the genetics plan. These IDs are save data and must
/// not be renamed when presentation copy changes.
/// </summary>
public static class PersonalityTraitIds
{
    public const string Curiosity = "personality.curiosity";
    public const string Energy = "personality.energy";
    public const string Naivety = "personality.naivety";
    public const string Appetite = "personality.appetite";
    public const string Carefree = "personality.carefree";
    public const string Kindness = "personality.kindness";
    public const string Solitude = "personality.solitude";
    public const string Vitality = "personality.vitality";
    public const string Recovery = "personality.recovery";
    public const string Skillfulness = "personality.skillfulness";
    public const string Sociability = "personality.sociability";
    public const string Chattiness = "personality.chattiness";
    public const string Fickleness = "personality.fickleness";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Curiosity, Energy, Naivety, Appetite, Carefree, Kindness, Solitude,
        Vitality, Recovery, Skillfulness, Sociability, Chattiness, Fickleness
    });
}

public enum PersonalityPolarity
{
    Neutral,
    Low,
    High
}

public readonly record struct PersonalityTendency(string TraitId, PersonalityPolarity Polarity);

/// <summary>
/// Diploid atmospheric personality genetics. Alleles use the documented normalized runtime range
/// [-1,+1], persisted as integer hundredths [-100,+100] so JSON remains stable and exact.
/// Personality is intentionally excluded from v1 race/performance calculations.
/// </summary>
public static class PersonalityGenetics
{
    public const int MinAllele = -100;
    public const int MaxAllele = 100;
    private const int FlavorThreshold = 20;

    public static void PopulateFounder(GenomeData genome, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(genome);
        genome.PersonalityGenes ??= new Dictionary<string, GenePairData>(StringComparer.Ordinal);

        foreach (var traitId in PersonalityTraitIds.All)
        {
            var random = StableRandom.Create(seed, $"random:{traitId}");
            genome.PersonalityGenes[traitId] = new GenePairData
            {
                AlleleA = random.Next(MinAllele, MaxAllele + 1),
                AlleleB = random.Next(MinAllele, MaxAllele + 1),
                ExpressedAlleleIndex = StableRandom.Create(seed, $"express:{traitId}").Next(2)
            };
        }
    }

    public static void Inherit(GenomeData parentA, GenomeData parentB, GenomeData child, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);
        ArgumentNullException.ThrowIfNull(child);
        child.PersonalityGenes ??= new Dictionary<string, GenePairData>(StringComparer.Ordinal);

        foreach (var traitId in PersonalityTraitIds.All)
        {
            child.PersonalityGenes[traitId] = new GenePairData
            {
                AlleleA = PickParentAllele(parentA, traitId, StableRandom.Create(seed, $"inherit:{traitId}:a")),
                AlleleB = PickParentAllele(parentB, traitId, StableRandom.Create(seed, $"inherit:{traitId}:b")),
                ExpressedAlleleIndex = StableRandom.Create(seed, $"express:{traitId}").Next(2)
            };
        }
    }

    public static GenePairData GetPair(GenomeData genome, string traitId)
    {
        ArgumentNullException.ThrowIfNull(genome);
        if (genome.PersonalityGenes != null &&
            genome.PersonalityGenes.TryGetValue(traitId, out var pair) && pair != null)
        {
            return new GenePairData
            {
                AlleleA = Math.Clamp(pair.AlleleA, MinAllele, MaxAllele),
                AlleleB = Math.Clamp(pair.AlleleB, MinAllele, MaxAllele),
                ExpressedAlleleIndex = pair.ExpressedAlleleIndex == 1 ? 1 : 0
            };
        }

        // Missing loci are legacy-safe neutral DNA; old saves are never rerolled during migration.
        return new GenePairData();
    }

    public static float GetNormalizedExpressedValue(GenomeData genome, string traitId)
        => GetPair(genome, traitId).ExpressedValue / 100.0f;

    public static PersonalityTendency ResolveDominant(GenomeData genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        var strongestId = string.Empty;
        var strongestValue = 0;

        foreach (var traitId in PersonalityTraitIds.All)
        {
            var value = GetPair(genome, traitId).ExpressedValue;
            if (Math.Abs(value) <= Math.Abs(strongestValue))
                continue;

            strongestId = traitId;
            strongestValue = value;
        }

        if (string.IsNullOrEmpty(strongestId) || Math.Abs(strongestValue) < FlavorThreshold)
            return new PersonalityTendency(string.Empty, PersonalityPolarity.Neutral);

        return new PersonalityTendency(
            strongestId,
            strongestValue > 0 ? PersonalityPolarity.High : PersonalityPolarity.Low);
    }

    private static int PickParentAllele(GenomeData genome, string traitId, Random random)
    {
        var pair = GetPair(genome, traitId);
        return random.Next(2) == 0 ? pair.AlleleA : pair.AlleleB;
    }
}
