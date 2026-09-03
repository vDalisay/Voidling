using System;
using System.Collections.Generic;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

/// <summary>
/// Creates a child's genome from the two selected parents only. Ability and continuous-hue color DNA
/// retain the validated inheritance model; personality uses independent atmospheric-only substreams.
/// </summary>
public sealed class GenomeInheritanceService
{
    private readonly GeneticsRules _rules;
    private readonly ColorPhenotypeResolver _colors;

    public GenomeInheritanceService(GeneticsRules rules) : this(rules, GameBalanceRules.DemoDefaults.Appearance) { }

    public GenomeInheritanceService(GeneticsRules rules, AppearanceRules appearance)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _colors = new ColorPhenotypeResolver(appearance ?? throw new ArgumentNullException(nameof(appearance)));
    }

    public GenomeData CreateChild(VoidlingData parentA, VoidlingData parentB, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);
        var genome = new GenomeData();
        foreach (var statId in _rules.StatIds)
        {
            var alleleA = AbilityGeneExpression.PickAllele(GetGene(parentA, statId), StableRandom.Create(seed, $"inherit:{statId}:a"));
            var alleleB = AbilityGeneExpression.PickAllele(GetGene(parentB, statId), StableRandom.Create(seed, $"inherit:{statId}:b"));
            genome.AbilityGenes[statId] = AbilityGeneExpression.CreatePair(alleleA, alleleB, StableRandom.Create(seed, $"express:{statId}"), _rules);
        }

        ApplyNormalRankBreakthrough(parentA, parentB, genome, seed);
        var parentAProfile = StableRandom.Create(seed, "inherit:color:a").Next(2);
        var parentBProfile = StableRandom.Create(seed, "inherit:color:b").Next(2);
        genome.ColorAlleleA = parentAProfile == 0 ? parentA.Genome.ColorAlleleA : parentA.Genome.ColorAlleleB;
        genome.ColorAlleleB = parentBProfile == 0 ? parentB.Genome.ColorAlleleA : parentB.Genome.ColorAlleleB;
        genome.PaletteHueA = _colors.AlleleHue(parentA.Genome, parentAProfile);
        genome.PaletteHueB = _colors.AlleleHue(parentB.Genome, parentBProfile);
        genome.ExpressedColorIndex = StableRandom.Create(seed, "express:color").NextDouble() < 0.5 ? 0 : 1;
        PersonalityGenetics.Inherit(parentA.Genome, parentB.Genome, genome, seed);
        return genome;
    }

    private void ApplyNormalRankBreakthrough(VoidlingData parentA, VoidlingData parentB, GenomeData childGenome, ulong seed)
    {
        if (_rules.AbilityRankBreakthroughChance <= 0.0 || _rules.GradeWeights.Count == 0) return;
        if (StableRandom.Create(seed, "inherit:ability:breakthrough:roll").NextDouble() >= _rules.AbilityRankBreakthroughChance) return;
        var maxGrade = _rules.GradeWeights.Count - 1;
        var eligibleStats = new List<string>();
        foreach (var statId in _rules.StatIds) if (BestParentalAllele(parentA, parentB, statId) < maxGrade) eligibleStats.Add(statId);
        if (eligibleStats.Count == 0) return;
        var selectedStatId = eligibleStats[StableRandom.Create(seed, "inherit:ability:breakthrough:stat").Next(eligibleStats.Count)];
        var targetGrade = Math.Min(BestParentalAllele(parentA, parentB, selectedStatId) + 1, maxGrade);
        var inherited = childGenome.AbilityGenes[selectedStatId];
        var alleleA = inherited.AlleleA;
        var alleleB = inherited.AlleleB;
        if (StableRandom.Create(seed, $"inherit:ability:breakthrough:{selectedStatId}:profile").Next(2) == 0) alleleA = targetGrade; else alleleB = targetGrade;
        childGenome.AbilityGenes[selectedStatId] = AbilityGeneExpression.CreatePair(alleleA, alleleB, StableRandom.Create(seed, $"express:{selectedStatId}"), _rules);
    }

    private static int BestParentalAllele(VoidlingData parentA, VoidlingData parentB, string statId)
    {
        var first = GetGene(parentA, statId); var second = GetGene(parentB, statId);
        return Math.Max(Math.Max(first.AlleleA, first.AlleleB), Math.Max(second.AlleleA, second.AlleleB));
    }
    private static GenePairData GetGene(VoidlingData data, string statId) => data.Genome.AbilityGenes.TryGetValue(statId, out var gene) ? gene : new GenePairData();
}
