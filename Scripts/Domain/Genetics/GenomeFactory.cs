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

        // Chao-style founders/store stock are pure at appearance loci. This keeps breeding legible:
        // heterozygous appearance genes arise by crossing bloodlines rather than hidden founder DNA.
        // Rare coats/patterns can later be introduced by authored store/founder variants without
        // changing the inheritance algorithm or any sprite consumer.
        var colorRandom = StableRandom.Create(seed, "random:color");
        var color = colorRandom.Next(Math.Max(1, _rules.ColorAlleleCount));
        genome.ColorAlleleA = color;
        genome.ColorAlleleB = color;
        genome.ExpressedColorIndex = 0;

        genome.ToneAlleleA = AppearanceAlleles.TwoTone;
        genome.ToneAlleleB = AppearanceAlleles.TwoTone;
        genome.ExpressedToneIndex = 0;

        genome.PatternAlleleA = AppearanceAlleles.DefaultPattern;
        genome.PatternAlleleB = AppearanceAlleles.DefaultPattern;
        genome.ExpressedPatternIndex = 0;

        genome.ShinyAlleleA = AppearanceAlleles.NonShiny;
        genome.ShinyAlleleB = AppearanceAlleles.NonShiny;

        genome.CoatAlleleA = AppearanceAlleles.NoSpecialCoat;
        genome.CoatAlleleB = AppearanceAlleles.NoSpecialCoat;
        genome.ExpressedCoatIndex = 0;
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
