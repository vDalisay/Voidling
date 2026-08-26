using Voidling.Domain.Genetics;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class AppearanceFounderTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void AuthoredFounder_IsPureAcrossEveryAppearanceLocus()
    {
        var template = new FounderAppearanceTemplate(
            ColorAllele: 12,
            Tone: AppearanceTone.MonoTone,
            PatternAllele: 4,
            Shiny: true,
            CoatAllele: AppearanceAlleles.GlistenCoat);

        var genome = new GenomeFactory(Rules.Genetics).CreateRandom(123UL, template);

        Assert.Equal(12, genome.ColorAlleleA);
        Assert.Equal(genome.ColorAlleleA, genome.ColorAlleleB);
        Assert.Equal((int)AppearanceTone.MonoTone, genome.ToneAlleleA);
        Assert.Equal(genome.ToneAlleleA, genome.ToneAlleleB);
        Assert.Equal(4, genome.PatternAlleleA);
        Assert.Equal(genome.PatternAlleleA, genome.PatternAlleleB);
        Assert.Equal(AppearanceAlleles.Shiny, genome.ShinyAlleleA);
        Assert.Equal(genome.ShinyAlleleA, genome.ShinyAlleleB);
        Assert.Equal(AppearanceAlleles.GlistenCoat, genome.CoatAlleleA);
        Assert.Equal(genome.CoatAlleleA, genome.CoatAlleleB);
    }

    [Fact]
    public void StoreEgg_WithAuthoredAppearanceFreezesVariantAtInventoryEntry()
    {
        var template = new FounderAppearanceTemplate(
            ColorAllele: 11,
            PatternAllele: 2,
            Shiny: true,
            CoatAllele: AppearanceAlleles.GlowCoat);
        var factory = new StoreEggFactory(Rules);

        var first = factory.Create("rare-egg", 991UL, template);
        var replay = factory.Create("rare-egg", 991UL, template);

        Assert.Equal(11, first.Genome.ColorAlleleA);
        Assert.Equal(2, first.Genome.PatternAlleleA);
        Assert.Equal(AppearanceAlleles.Shiny, first.Genome.ShinyAlleleA);
        Assert.Equal(AppearanceAlleles.GlowCoat, first.Genome.CoatAlleleA);
        Assert.Equal(first.TintHex, replay.TintHex);
        Assert.Equal(first.Genome.ColorAlleleA, replay.Genome.ColorAlleleA);
        Assert.Equal(first.Genome.PatternAlleleA, replay.Genome.PatternAlleleA);
        Assert.Equal(first.Genome.ShinyAlleleA, replay.Genome.ShinyAlleleA);
        Assert.Equal(first.Genome.CoatAlleleA, replay.Genome.CoatAlleleA);
    }

    [Fact]
    public void DefaultPalette_PreservesExistingTenSlotsAndAppendsFour()
    {
        Assert.Equal(14, Rules.Genetics.ColorAlleleCount);
        Assert.Equal(14, Rules.Appearance.PaletteHex.Count);
        Assert.Equal("#F6F0C9", Rules.Appearance.PaletteHex[0]);
        Assert.Equal("#D9D1C6", Rules.Appearance.PaletteHex[9]);
    }
}
