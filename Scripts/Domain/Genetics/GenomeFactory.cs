using System;
using System.Linq;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

public sealed class GenomeFactory
{
    private readonly GeneticsRules _rules;
    private readonly ColorPhenotypeResolver _colors;

    public GenomeFactory(GeneticsRules rules)
        : this(rules, GameBalanceRules.DemoDefaults.Appearance)
    {
    }

    public GenomeFactory(GeneticsRules rules, AppearanceRules appearance)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _colors = new ColorPhenotypeResolver(appearance ?? throw new ArgumentNullException(nameof(appearance)));
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
        genome.PaletteHueA = _colors.HueForLegacyAllele(genome.ColorAlleleA);
        genome.PaletteHueB = _colors.HueForLegacyAllele(genome.ColorAlleleB);
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
