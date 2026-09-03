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
    public void PaletteBlend_BlueWinnerMovesTowardPurpleWhileRedWinnerMovesTowardPink()
    {
        const float red = 0.0f;
        const float blue = 2.0f / 3.0f;
        const float influence = 0.18f;

        var blueWinner = ColorPhenotypeResolver.MoveHueToward(blue, red, influence);
        var redWinner = ColorPhenotypeResolver.MoveHueToward(red, blue, influence);

        Assert.InRange(blueWinner, 0.72f, 0.73f);
        Assert.InRange(redWinner, 0.93f, 0.95f);
        Assert.NotEqual(blueWinner, redWinner);
    }

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
    public void PhenotypeResolution_NudgesWinnerWithoutCollapsingToMidpoint()
    {
        var genome = new GenomeData
        {
            PaletteHueA = 0.0f,
            PaletteHueB = 2.0f / 3.0f,
            ExpressedColorIndex = 0
        };
        var resolver = new ColorPhenotypeResolver(Rules.Appearance);

        var hue = resolver.ResolvePaletteHue(genome);

        Assert.InRange(hue, 0.93f, 0.95f);
        Assert.True(CircularDistance(hue, 0.0f) < CircularDistance(hue, 2.0f / 3.0f));
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
