using System;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class AppearanceGeneticsTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void ChildColorDna_ComesOnlyFromSelectedParentsAndIsDeterministic()
    {
        var first = Parent("a", 0.00f, 0.05f);
        var second = Parent("b", 0.60f, 0.67f);
        var service = new GenomeInheritanceService(Rules.Genetics, Rules.Appearance);

        var one = service.CreateChild(first, second, 99881UL);
        var two = service.CreateChild(first, second, 99881UL);

        Assert.Equal(one.PaletteHueA, two.PaletteHueA);
        Assert.Equal(one.PaletteHueB, two.PaletteHueB);
        Assert.Equal(one.ExpressedColorIndex, two.ExpressedColorIndex);
        Assert.Contains(one.PaletteHueA, new[] { 0.00f, 0.05f });
        Assert.Contains(one.PaletteHueB, new[] { 0.60f, 0.67f });
    }

    [Fact]
    public void ChildColorDna_LegacyParentsAreReadWithoutMutation()
    {
        var first = Parent("legacy-a", -1.0f, -1.0f);
        first.Genome.ColorAlleleA = 1;
        first.Genome.ColorAlleleB = 2;
        var second = Parent("legacy-b", -1.0f, -1.0f);
        second.Genome.ColorAlleleA = 3;
        second.Genome.ColorAlleleB = 4;
        var service = new GenomeInheritanceService(Rules.Genetics, Rules.Appearance);

        var child = service.CreateChild(first, second, 99882UL);

        Assert.Equal(-1.0f, first.Genome.PaletteHueA);
        Assert.Equal(-1.0f, first.Genome.PaletteHueB);
        Assert.Equal(-1.0f, second.Genome.PaletteHueA);
        Assert.Equal(-1.0f, second.Genome.PaletteHueB);
        Assert.True(VoidlingAppearanceData.IsValidHue(child.PaletteHueA));
        Assert.True(VoidlingAppearanceData.IsValidHue(child.PaletteHueB));
    }

    [Fact]
    public void PhenotypeResolution_KeepsExpressedColorGroup()
    {
        var genome = new GenomeData
        {
            PaletteHueA = 0.0f,
            PaletteHueB = 2.0f / 3.0f,
            ExpressedColorIndex = 0
        };
        var resolver = new ColorPhenotypeResolver(Rules.Appearance);

        var hue = resolver.ResolvePaletteHue(genome);

        Assert.Equal(0.0f, hue);
        genome.ExpressedColorIndex = 1;
        Assert.Equal(2.0f / 3.0f, resolver.ResolvePaletteHue(genome));
    }

    [Fact]
    public void FounderShades_AreInheritedWithoutChangingGroup()
    {
        Assert.Equal(Rules.Genetics.ColorAlleleCount, Rules.Appearance.PaletteHex.Count);
        var resolver = new ColorPhenotypeResolver(Rules.Appearance);
        var parent = Parent("red", resolver.HueForLegacyAllele(32), resolver.HueForLegacyAllele(33));
        parent.Genome.ColorAlleleA = 32;
        parent.Genome.ColorAlleleB = 33;
        var child = new GenomeInheritanceService(Rules.Genetics, Rules.Appearance).CreateChild(parent, parent, 541UL);
        var expressed = child.ExpressedColorIndex == 0 ? child.ColorAlleleA : child.ColorAlleleB;

        Assert.Contains(expressed, new[] { 32, 33 });
        Assert.Equal(Rules.Appearance.PaletteHex[expressed], resolver.ResolveTint(child));
    }

    [Fact]
    public void PhenotypeResolution_LegacyGenomeFallbackIsPure()
    {
        var genome = new GenomeData
        {
            ColorAlleleA = 1,
            ColorAlleleB = 4,
            PaletteHueA = -1.0f,
            PaletteHueB = -1.0f,
            ExpressedColorIndex = 1
        };
        var resolver = new ColorPhenotypeResolver(Rules.Appearance);

        var hue = resolver.ResolvePaletteHue(genome);
        var tint = resolver.ResolveTint(genome);

        Assert.True(VoidlingAppearanceData.IsValidHue(hue));
        Assert.StartsWith("#", tint);
        Assert.Equal(-1.0f, genome.PaletteHueA);
        Assert.Equal(-1.0f, genome.PaletteHueB);
        Assert.Equal(1, genome.ExpressedColorIndex);
    }

    /// <summary>
    /// Colour is Mendelian, not paint mixing. A parent shows one of its two hues and can silently
    /// carry the other, so a red x blue pairing can legitimately produce a child in a third colour
    /// family when both parents pass on their hidden allele.
    /// </summary>
    [Fact]
    public void ChildColor_CanExpressACarriedHueNeitherParentShows()
    {
        const float red = 0.00f;
        const float blue = 0.62f;
        const float carriedAmber = 0.11f;
        const float carriedLime = 0.24f;

        // Each parent expresses index 0 (red / blue) while carrying an unrelated hue at index 1.
        var redParent = Parent("shown-red", red, carriedAmber);
        redParent.Genome.ExpressedColorIndex = 0;
        var blueParent = Parent("shown-blue", blue, carriedLime);
        blueParent.Genome.ExpressedColorIndex = 0;

        var service = new GenomeInheritanceService(Rules.Genetics, Rules.Appearance);
        var resolver = new ColorPhenotypeResolver(Rules.Appearance);

        var carriedOnlyChildren = 0;
        for (var seed = 1UL; seed <= 400UL; seed++)
        {
            var child = service.CreateChild(redParent, blueParent, seed);

            // Every allele a child holds came from one of the four parental alleles.
            Assert.Contains(child.PaletteHueA, new[] { red, carriedAmber });
            Assert.Contains(child.PaletteHueB, new[] { blue, carriedLime });

            if (child.PaletteHueA != carriedAmber || child.PaletteHueB != carriedLime)
                continue;

            carriedOnlyChildren++;
            var hue = resolver.ResolvePaletteHue(child);
            Assert.True(
                CircularDistance(hue, red) > 0.05f && CircularDistance(hue, blue) > 0.05f,
                $"Seed {seed} produced hue {hue}, which should sit away from both shown parent hues.");
        }

        Assert.True(
            carriedOnlyChildren > 0,
            "Two carried alleles should sometimes meet; otherwise the carrier case is unreachable.");
    }

    private static VoidlingData Parent(string id, float firstHue, float secondHue)
    {
        var parent = new VoidlingData { Id = id, Stage = LifeStage.Adult };
        foreach (var statId in Rules.Genetics.StatIds)
        {
            parent.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 2,
                AlleleB = 2,
                ExpressedAlleleIndex = 0
            };
        }
        parent.Genome.PaletteHueA = firstHue;
        parent.Genome.PaletteHueB = secondHue;
        return parent;
    }

    private static float CircularDistance(float a, float b)
    {
        var delta = Math.Abs(a - b);
        return Math.Min(delta, 1.0f - delta);
    }
}
