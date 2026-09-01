using System;
using System.Linq;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

/// <summary>
/// Explicit pure founder/store appearance. This is how rare cosmetic bloodlines such as shiny,
/// glow, glisten or authored patterns enter the gene pool without inventing spontaneous mutation
/// rates. Once introduced, ordinary breeding uses the same Chao-style inheritance rules.
/// </summary>
public sealed record FounderAppearanceTemplate(
    int ColorAllele,
    AppearanceTone Tone = AppearanceTone.TwoTone,
    int PatternAllele = AppearanceAlleles.DefaultPattern,
    bool Shiny = false,
    int CoatAllele = AppearanceAlleles.NoSpecialCoat);

public sealed class GenomeFactory
{
    private readonly GeneticsRules _rules;

    public GenomeFactory(GeneticsRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public GenomeData CreateRandom(ulong seed)
    {
        var colorRandom = StableRandom.Create(seed, "random:color");
        var color = colorRandom.Next(Math.Max(1, _rules.ColorAlleleCount));
        return CreateRandom(seed, new FounderAppearanceTemplate(color));
    }

    public GenomeData CreateRandom(ulong seed, FounderAppearanceTemplate appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ValidateAppearance(appearance);

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

        // Chao-style founders/store stock are pure at appearance loci. Heterozygous appearance
        // genes arise by crossing bloodlines rather than hidden founder DNA.
        var tone = (int)appearance.Tone;
        var shiny = appearance.Shiny ? AppearanceAlleles.Shiny : AppearanceAlleles.NonShiny;
        genome.ColorAlleleA = appearance.ColorAllele;
        genome.ColorAlleleB = appearance.ColorAllele;
        genome.ExpressedColorIndex = 0;
        genome.ToneAlleleA = tone;
        genome.ToneAlleleB = tone;
        genome.ExpressedToneIndex = 0;
        genome.PatternAlleleA = appearance.PatternAllele;
        genome.PatternAlleleB = appearance.PatternAllele;
        genome.ExpressedPatternIndex = 0;
        genome.ShinyAlleleA = shiny;
        genome.ShinyAlleleB = shiny;
        genome.CoatAlleleA = appearance.CoatAllele;
        genome.CoatAlleleB = appearance.CoatAllele;
        genome.ExpressedCoatIndex = 0;

        // Personality uses independent stable RNG substreams so adding the vector cannot perturb
        // existing ability/appearance rolls for the same seed.
        PersonalityGenetics.PopulateFounder(genome, seed);
        return genome;
    }

    private void ValidateAppearance(FounderAppearanceTemplate appearance)
    {
        if (appearance.ColorAllele < 0 || appearance.ColorAllele >= Math.Max(1, _rules.ColorAlleleCount))
            throw new ArgumentOutOfRangeException(nameof(appearance), "Founder colour allele is outside the configured palette.");
        if (appearance.Tone is not (AppearanceTone.TwoTone or AppearanceTone.MonoTone))
            throw new ArgumentOutOfRangeException(nameof(appearance), "Founder tone is invalid.");
        if (appearance.PatternAllele < 0)
            throw new ArgumentOutOfRangeException(nameof(appearance), "Founder pattern allele cannot be negative.");
        if (appearance.CoatAllele < 0)
            throw new ArgumentOutOfRangeException(nameof(appearance), "Founder coat allele cannot be negative.");
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
