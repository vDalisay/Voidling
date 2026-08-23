using System;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

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

        var colorA = StableRandom.Create(seed, "inherit:color:a");
        var colorB = StableRandom.Create(seed, "inherit:color:b");
        genome.ColorAlleleA = colorA.NextDouble() < 0.5 ? parentA.Genome.ColorAlleleA : parentA.Genome.ColorAlleleB;
        genome.ColorAlleleB = colorB.NextDouble() < 0.5 ? parentB.Genome.ColorAlleleA : parentB.Genome.ColorAlleleB;
        genome.ExpressedColorIndex = StableRandom.Create(seed, "express:color").NextDouble() < 0.5 ? 0 : 1;
        return genome;
    }

    private static GenePairData GetGene(VoidlingData data, string statId)
        => data.Genome.AbilityGenes.TryGetValue(statId, out var gene) ? gene : new GenePairData();
}
