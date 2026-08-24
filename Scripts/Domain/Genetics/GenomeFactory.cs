using System;
using System.Linq;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

public sealed class GenomeFactory
{
    private readonly GeneticsRules _rules;

    public GenomeFactory(GeneticsRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public GenomeData CreateRandom(ulong seed)
    {
        var genome = new GenomeData();

        foreach (var statId in _rules.StatIds)
        {
            var random = StableRandom.Create(seed, $"random:{statId}");
            var alleleA = RollGrade(random);
            var alleleB = RollGrade(random);
            genome.AbilityGenes[statId] = AbilityGeneExpression.CreatePair(
                alleleA,
                alleleB,
                StableRandom.Create(seed, $"express:{statId}"),
                _rules);
        }

        var colorRandom = StableRandom.Create(seed, "random:color");
        genome.ColorAlleleA = colorRandom.Next(_rules.ColorAlleleCount);
        genome.ColorAlleleB = colorRandom.Next(_rules.ColorAlleleCount);
        genome.ExpressedColorIndex = colorRandom.NextDouble() < 0.5 ? 0 : 1;
        return genome;
    }

    private int RollGrade(Random random)
    {
        var total = _rules.GradeWeights.Sum();
        var roll = random.Next(total);
        var cumulative = 0;

        for (var i = 0; i < _rules.GradeWeights.Count; i++)
        {
            cumulative += _rules.GradeWeights[i];
            if (roll < cumulative)
                return i;
        }

        return _rules.GradeWeights.Count - 1;
    }
}
