using System;
using System.Collections.Generic;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

/// <summary>
/// Creates a child's normal genome from the two selected parents only. Each ability locus keeps
/// exactly two visible DNA-profile values: profile 1 comes from parent A and profile 2 from parent B.
/// Deeper ancestry is intentionally outside this service and is reserved for pedigree/inbreeding.
/// </summary>
public sealed class GenomeInheritanceService
{
    private readonly GeneticsRules _rules;

    public GenomeInheritanceService(GeneticsRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public GenomeData CreateChild(VoidlingData parentA, VoidlingData parentB, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);

        var genome = new GenomeData();
        foreach (var statId in _rules.StatIds)
        {
            var alleleA = AbilityGeneExpression.PickAllele(
                GetGene(parentA, statId),
                StableRandom.Create(seed, $"inherit:{statId}:a"));
            var alleleB = AbilityGeneExpression.PickAllele(
                GetGene(parentB, statId),
                StableRandom.Create(seed, $"inherit:{statId}:b"));

            genome.AbilityGenes[statId] = AbilityGeneExpression.CreatePair(
                alleleA,
                alleleB,
                StableRandom.Create(seed, $"express:{statId}"),
                _rules);
        }

        ApplyNormalRankBreakthrough(parentA, parentB, genome, seed);

        var colorA = StableRandom.Create(seed, "inherit:color:a");
        var colorB = StableRandom.Create(seed, "inherit:color:b");
        genome.ColorAlleleA = colorA.NextDouble() < 0.5 ? parentA.Genome.ColorAlleleA : parentA.Genome.ColorAlleleB;
        genome.ColorAlleleB = colorB.NextDouble() < 0.5 ? parentB.Genome.ColorAlleleA : parentB.Genome.ColorAlleleB;
        genome.ExpressedColorIndex = StableRandom.Create(seed, "express:color").NextDouble() < 0.5 ? 0 : 1;
        return genome;
    }

    private void ApplyNormalRankBreakthrough(
        VoidlingData parentA,
        VoidlingData parentB,
        GenomeData childGenome,
        ulong seed)
    {
        if (_rules.AbilityRankBreakthroughChance <= 0.0 || _rules.GradeWeights.Count == 0)
            return;

        var roll = StableRandom.Create(seed, "inherit:ability:breakthrough:roll");
        if (roll.NextDouble() >= _rules.AbilityRankBreakthroughChance)
            return;

        var maxGrade = _rules.GradeWeights.Count - 1;
        var eligibleStats = new List<string>();
        foreach (var candidateStatId in _rules.StatIds)
        {
            if (BestParentalAllele(parentA, parentB, candidateStatId) < maxGrade)
                eligibleStats.Add(candidateStatId);
        }

        if (eligibleStats.Count == 0)
            return;

        var statRandom = StableRandom.Create(seed, "inherit:ability:breakthrough:stat");
        var selectedStatId = eligibleStats[statRandom.Next(eligibleStats.Count)];
        var targetGrade = Math.Min(BestParentalAllele(parentA, parentB, selectedStatId) + 1, maxGrade);
        var inherited = childGenome.AbilityGenes[selectedStatId];
        var profileRandom = StableRandom.Create(seed, $"inherit:ability:breakthrough:{selectedStatId}:profile");

        var alleleA = inherited.AlleleA;
        var alleleB = inherited.AlleleB;
        if (profileRandom.Next(2) == 0)
            alleleA = targetGrade;
        else
            alleleB = targetGrade;

        // Re-resolve expression from the same stable expression substream so the new higher allele
        // follows the normal heterozygous expression rule instead of being automatically expressed.
        childGenome.AbilityGenes[selectedStatId] = AbilityGeneExpression.CreatePair(
            alleleA,
            alleleB,
            StableRandom.Create(seed, $"express:{selectedStatId}"),
            _rules);
    }

    private static int BestParentalAllele(VoidlingData parentA, VoidlingData parentB, string statId)
    {
        var first = GetGene(parentA, statId);
        var second = GetGene(parentB, statId);
        return Math.Max(
            Math.Max(first.AlleleA, first.AlleleB),
            Math.Max(second.AlleleA, second.AlleleB));
    }

    private static GenePairData GetGene(VoidlingData data, string statId)
        => data.Genome.AbilityGenes.TryGetValue(statId, out var gene) ? gene : new GenePairData();
}
