using System;
using Godot;
using Voidling.Domain.Genetics;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Bridges immutable semantic phenotype projections/network snapshots to the canonical visual
/// factory. This avoids leaking a full owner's Genome into presentation/network contracts while
/// keeping all mask/shader selection in VoidlingVisualFactory.
/// </summary>
public static class VoidlingAppearanceVisualAdapter
{
    public static void Apply(
        CanvasItem item,
        string tintHex,
        int tone,
        int patternAllele,
        bool shiny,
        int coatAllele,
        VoidlingAppearanceContext context)
    {
        ArgumentNullException.ThrowIfNull(item);

        var normalizedTone = tone == AppearanceAlleles.MonoTone
            ? AppearanceAlleles.MonoTone
            : AppearanceAlleles.TwoTone;
        var normalizedShiny = shiny ? AppearanceAlleles.Shiny : AppearanceAlleles.NonShiny;
        var pattern = Math.Max(0, patternAllele);
        var coat = Math.Max(0, coatAllele);

        // This object is presentation-only and never persisted. Homozygous values encode an already
        // resolved phenotype so the Domain resolver cannot choose a different equal-dominance side.
        var renderGenome = new GenomeData
        {
            ToneAlleleA = normalizedTone,
            ToneAlleleB = normalizedTone,
            PatternAlleleA = pattern,
            PatternAlleleB = pattern,
            ShinyAlleleA = normalizedShiny,
            ShinyAlleleB = normalizedShiny,
            CoatAlleleA = coat,
            CoatAlleleB = coat
        };

        VoidlingVisualFactory.ApplyAppearance(item, renderGenome, tintHex, context);
    }
}
