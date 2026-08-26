using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

public enum AppearanceTone
{
    TwoTone = AppearanceAlleles.TwoTone,
    MonoTone = AppearanceAlleles.MonoTone
}

/// <summary>
/// Pure semantic appearance output. Presentation decides how these values map to authored masks,
/// palette fills and materials; Domain never knows texture paths or shader details.
/// </summary>
public sealed record AppearancePhenotype(
    int ColorAllele,
    AppearanceTone Tone,
    int PatternAllele,
    bool Shiny,
    int CoatAllele);

/// <summary>
/// Chao-style phenotype dominance for appearance loci:
/// - normal color is recessive; non-normal colors are equally dominant;
/// - mono/two-tone are equally dominant;
/// - shiny is dominant over non-shiny;
/// - no special coat is recessive; special coats are equally dominant;
/// - Voidling pattern uses the same recessive-default/equal-dominance rule as color/coat.
/// Equal-dominance ties use the persisted deterministic birth-time expressed index.
/// </summary>
public sealed class AppearancePhenotypeResolver
{
    private readonly AppearanceRules _rules;

    public AppearancePhenotypeResolver(AppearanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>
    /// Resolves semantic phenotype without palette knowledge. This is safe for Presentation to use
    /// when choosing mask layers/material effects because it contains no Godot or asset concerns.
    /// </summary>
    public static AppearancePhenotype ResolveSemantic(GenomeData genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        var color = ResolveRecessiveZero(
            Math.Max(0, genome.ColorAlleleA),
            Math.Max(0, genome.ColorAlleleB),
            genome.ExpressedColorIndex);
        var tone = ResolveEqual(
            NormalizeBinary(genome.ToneAlleleA),
            NormalizeBinary(genome.ToneAlleleB),
            genome.ExpressedToneIndex);
        var pattern = ResolveRecessiveZero(
            Math.Max(0, genome.PatternAlleleA),
            Math.Max(0, genome.PatternAlleleB),
            genome.ExpressedPatternIndex);
        var coat = ResolveRecessiveZero(
            Math.Max(0, genome.CoatAlleleA),
            Math.Max(0, genome.CoatAlleleB),
            genome.ExpressedCoatIndex);

        return new AppearancePhenotype(
            color,
            tone == AppearanceAlleles.MonoTone ? AppearanceTone.MonoTone : AppearanceTone.TwoTone,
            pattern,
            genome.ShinyAlleleA == AppearanceAlleles.Shiny || genome.ShinyAlleleB == AppearanceAlleles.Shiny,
            coat);
    }

    public AppearancePhenotype Resolve(GenomeData genome)
    {
        var semantic = ResolveSemantic(genome);
        return semantic with { ColorAllele = ClampColor(semantic.ColorAllele) };
    }

    public string ResolveTint(GenomeData genome)
    {
        var phenotype = Resolve(genome);
        if (_rules.PaletteHex.Count == 0)
            return "#F6F0C9";

        return _rules.PaletteHex[Math.Clamp(phenotype.ColorAllele, 0, _rules.PaletteHex.Count - 1)];
    }

    private int ClampColor(int allele)
    {
        if (_rules.PaletteHex.Count == 0)
            return 0;
        return Math.Clamp(allele, 0, _rules.PaletteHex.Count - 1);
    }

    private static int NormalizeBinary(int allele)
        => allele == AppearanceAlleles.MonoTone ? AppearanceAlleles.MonoTone : AppearanceAlleles.TwoTone;

    private static int ResolveEqual(int alleleA, int alleleB, int expressedIndex)
    {
        if (alleleA == alleleB)
            return alleleA;
        return expressedIndex == 1 ? alleleB : alleleA;
    }

    private static int ResolveRecessiveZero(int alleleA, int alleleB, int expressedIndex)
    {
        if (alleleA == alleleB)
            return alleleA;
        if (alleleA == 0)
            return alleleB;
        if (alleleB == 0)
            return alleleA;
        return expressedIndex == 1 ? alleleB : alleleA;
    }
}
