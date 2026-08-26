using System;
using Godot;
using Voidling.Domain.Genetics;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Presentation-only adapter from immutable semantic appearance into the centralized Voidling
/// material pipeline. This deliberately reconstructs a homozygous render genome so no UI surface
/// needs the owner's mutable GenomeData or needs to duplicate dominance rules.
/// </summary>
public static class VoidlingAppearancePresenter
{
    public static AppearancePhenotype DefaultPhenotype { get; } = new(
        AppearanceAlleles.NormalColor,
        AppearanceTone.TwoTone,
        AppearanceAlleles.DefaultPattern,
        false,
        AppearanceAlleles.NoSpecialCoat);

    public static void Apply(
        CanvasItem item,
        AppearancePhenotype? phenotype,
        string tintHex,
        VoidlingAppearanceContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        VoidlingVisualFactory.ApplyAppearance(
            item,
            CreateRenderGenome(phenotype ?? DefaultPhenotype),
            tintHex,
            context);
    }

    public static TextureRect CreatePortrait(
        string tintHex,
        AppearancePhenotype? phenotype,
        bool hasAngelMutation,
        int otherMutationCount,
        Vector2 minimumSize)
    {
        var portrait = UiFactory.CreatePortrait(
            ParseTint(tintHex),
            hasAngelMutation,
            otherMutationCount,
            minimumSize);
        ApplyPortrait(
            portrait,
            tintHex,
            phenotype,
            hasAngelMutation,
            otherMutationCount);
        return portrait;
    }

    public static void ApplyPortrait(
        TextureRect portrait,
        string tintHex,
        AppearancePhenotype? phenotype,
        bool hasAngelMutation,
        int otherMutationCount)
    {
        ArgumentNullException.ThrowIfNull(portrait);

        // Preserve UiFactory's mutation badge lifecycle, then move tinting onto the portrait itself
        // so child badges/halos are not recolored with the body.
        UiFactory.SetPortraitData(
            portrait,
            ParseTint(tintHex),
            hasAngelMutation,
            otherMutationCount);
        portrait.SelfModulate = Colors.White;
        portrait.Modulate = Colors.White;
        Apply(
            portrait,
            phenotype,
            tintHex,
            VoidlingAppearanceContext.Portrait);

        if (portrait.Material == null)
        {
            portrait.SelfModulate = portrait.Modulate;
            portrait.Modulate = Colors.White;
        }
    }

    public static GenomeData CreateRenderGenome(AppearancePhenotype phenotype)
    {
        ArgumentNullException.ThrowIfNull(phenotype);

        var tone = phenotype.Tone == AppearanceTone.MonoTone
            ? AppearanceAlleles.MonoTone
            : AppearanceAlleles.TwoTone;
        var shiny = phenotype.Shiny
            ? AppearanceAlleles.Shiny
            : AppearanceAlleles.NonShiny;
        var color = Math.Max(0, phenotype.ColorAllele);
        var pattern = Math.Max(0, phenotype.PatternAllele);
        var coat = Math.Max(0, phenotype.CoatAllele);

        return new GenomeData
        {
            ColorAlleleA = color,
            ColorAlleleB = color,
            ToneAlleleA = tone,
            ToneAlleleB = tone,
            PatternAlleleA = pattern,
            PatternAlleleB = pattern,
            ShinyAlleleA = shiny,
            ShinyAlleleB = shiny,
            CoatAlleleA = coat,
            CoatAlleleB = coat
        };
    }

    private static Color ParseTint(string tintHex)
    {
        if (string.IsNullOrWhiteSpace(tintHex))
            return Color.FromHtml("#F6F0C9");
        try
        {
            return Color.FromHtml(tintHex);
        }
        catch
        {
            return Color.FromHtml("#F6F0C9");
        }
    }
}
