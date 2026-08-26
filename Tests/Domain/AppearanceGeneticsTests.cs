using System;
using System.Text.Json;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class AppearanceGeneticsTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Child_InheritsExactlyOneAppearanceAlleleFromEachParentPerLocus()
    {
        var first = CreateParent("a");
        first.Genome.ColorAlleleA = 0;
        first.Genome.ColorAlleleB = 1;
        first.Genome.ToneAlleleA = 0;
        first.Genome.ToneAlleleB = 1;
        first.Genome.PatternAlleleA = 0;
        first.Genome.PatternAlleleB = 2;
        first.Genome.ShinyAlleleA = 0;
        first.Genome.ShinyAlleleB = 1;
        first.Genome.CoatAlleleA = 0;
        first.Genome.CoatAlleleB = 2;

        var second = CreateParent("b");
        second.Genome.ColorAlleleA = 4;
        second.Genome.ColorAlleleB = 5;
        second.Genome.ToneAlleleA = 1;
        second.Genome.ToneAlleleB = 0;
        second.Genome.PatternAlleleA = 3;
        second.Genome.PatternAlleleB = 4;
        second.Genome.ShinyAlleleA = 1;
        second.Genome.ShinyAlleleB = 0;
        second.Genome.CoatAlleleA = 1;
        second.Genome.CoatAlleleB = 3;

        var inheritance = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 0.0 });

        for (ulong seed = 1; seed <= 256; seed++)
        {
            var child = inheritance.CreateChild(first, second, seed);
            Assert.Contains(child.ColorAlleleA, new[] { 0, 1 });
            Assert.Contains(child.ColorAlleleB, new[] { 4, 5 });
            Assert.Contains(child.ToneAlleleA, new[] { 0, 1 });
            Assert.Contains(child.ToneAlleleB, new[] { 0, 1 });
            Assert.Contains(child.PatternAlleleA, new[] { 0, 2 });
            Assert.Contains(child.PatternAlleleB, new[] { 3, 4 });
            Assert.Contains(child.ShinyAlleleA, new[] { 0, 1 });
            Assert.Contains(child.ShinyAlleleB, new[] { 0, 1 });
            Assert.Contains(child.CoatAlleleA, new[] { 0, 2 });
            Assert.Contains(child.CoatAlleleB, new[] { 1, 3 });
        }
    }

    [Fact]
    public void Phenotype_UsesChaoStyleDominanceRules()
    {
        var genome = new GenomeData
        {
            ColorAlleleA = AppearanceAlleles.NormalColor,
            ColorAlleleB = 4,
            ExpressedColorIndex = 0,
            ToneAlleleA = AppearanceAlleles.TwoTone,
            ToneAlleleB = AppearanceAlleles.MonoTone,
            ExpressedToneIndex = 1,
            PatternAlleleA = AppearanceAlleles.DefaultPattern,
            PatternAlleleB = 3,
            ExpressedPatternIndex = 0,
            ShinyAlleleA = AppearanceAlleles.NonShiny,
            ShinyAlleleB = AppearanceAlleles.Shiny,
            CoatAlleleA = AppearanceAlleles.NoSpecialCoat,
            CoatAlleleB = AppearanceAlleles.GlistenCoat,
            ExpressedCoatIndex = 0
        };

        var phenotype = new AppearancePhenotypeResolver(Rules.Appearance).Resolve(genome);

        Assert.Equal(4, phenotype.ColorAllele); // normal color is recessive
        Assert.Equal(AppearanceTone.MonoTone, phenotype.Tone); // tone is equal dominance + birth tie-break
        Assert.Equal(3, phenotype.PatternAllele); // default pattern is recessive
        Assert.True(phenotype.Shiny); // shiny is dominant
        Assert.Equal(AppearanceAlleles.GlistenCoat, phenotype.CoatAllele); // normal coat is recessive
    }

    [Fact]
    public void Phenotype_DifferentNonNormalAllelesUsePersistedBirthTieBreaker()
    {
        var resolver = new AppearancePhenotypeResolver(Rules.Appearance);
        var genome = new GenomeData
        {
            ColorAlleleA = 2,
            ColorAlleleB = 5,
            PatternAlleleA = 2,
            PatternAlleleB = 4,
            CoatAlleleA = AppearanceAlleles.GlowCoat,
            CoatAlleleB = AppearanceAlleles.GlistenCoat
        };

        genome.ExpressedColorIndex = 0;
        genome.ExpressedPatternIndex = 0;
        genome.ExpressedCoatIndex = 0;
        var first = resolver.Resolve(genome);
        Assert.Equal(2, first.ColorAllele);
        Assert.Equal(2, first.PatternAllele);
        Assert.Equal(AppearanceAlleles.GlowCoat, first.CoatAllele);

        genome.ExpressedColorIndex = 1;
        genome.ExpressedPatternIndex = 1;
        genome.ExpressedCoatIndex = 1;
        var second = resolver.Resolve(genome);
        Assert.Equal(5, second.ColorAllele);
        Assert.Equal(4, second.PatternAllele);
        Assert.Equal(AppearanceAlleles.GlistenCoat, second.CoatAllele);
    }

    [Fact]
    public void FounderGenome_IsPureAtAppearanceLoci()
    {
        var genome = new GenomeFactory(Rules.Genetics).CreateRandom(123456UL);

        Assert.Equal(genome.ColorAlleleA, genome.ColorAlleleB);
        Assert.Equal(AppearanceAlleles.TwoTone, genome.ToneAlleleA);
        Assert.Equal(genome.ToneAlleleA, genome.ToneAlleleB);
        Assert.Equal(AppearanceAlleles.DefaultPattern, genome.PatternAlleleA);
        Assert.Equal(genome.PatternAlleleA, genome.PatternAlleleB);
        Assert.Equal(AppearanceAlleles.NonShiny, genome.ShinyAlleleA);
        Assert.Equal(genome.ShinyAlleleA, genome.ShinyAlleleB);
        Assert.Equal(AppearanceAlleles.NoSpecialCoat, genome.CoatAlleleA);
        Assert.Equal(genome.CoatAlleleA, genome.CoatAlleleB);
    }

    [Fact]
    public void OldSaveShape_DefaultsNewAppearanceLociToVanillaRecessiveValues()
    {
        const string oldJson = "{\"AbilityGenes\":{},\"ColorAlleleA\":0,\"ColorAlleleB\":1,\"ExpressedColorIndex\":0}";
        var genome = JsonSerializer.Deserialize<GenomeData>(oldJson)!;
        var phenotype = new AppearancePhenotypeResolver(Rules.Appearance).Resolve(genome);

        Assert.Equal(1, phenotype.ColorAllele);
        Assert.Equal(AppearanceTone.TwoTone, phenotype.Tone);
        Assert.Equal(AppearanceAlleles.DefaultPattern, phenotype.PatternAllele);
        Assert.False(phenotype.Shiny);
        Assert.Equal(AppearanceAlleles.NoSpecialCoat, phenotype.CoatAllele);
    }

    [Fact]
    public void AppearanceInheritance_IsDeterministicForSeed()
    {
        var first = CreateParent("a");
        var second = CreateParent("b");
        first.Genome.ColorAlleleA = 0;
        first.Genome.ColorAlleleB = 3;
        first.Genome.ShinyAlleleA = 0;
        first.Genome.ShinyAlleleB = 1;
        second.Genome.ColorAlleleA = 4;
        second.Genome.ColorAlleleB = 7;
        second.Genome.CoatAlleleA = 0;
        second.Genome.CoatAlleleB = 2;

        var service = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 0.0 });
        var left = service.CreateChild(first, second, 998877UL);
        var right = service.CreateChild(first, second, 998877UL);

        Assert.Equal(left.ColorAlleleA, right.ColorAlleleA);
        Assert.Equal(left.ColorAlleleB, right.ColorAlleleB);
        Assert.Equal(left.ExpressedColorIndex, right.ExpressedColorIndex);
        Assert.Equal(left.ToneAlleleA, right.ToneAlleleA);
        Assert.Equal(left.ToneAlleleB, right.ToneAlleleB);
        Assert.Equal(left.ExpressedToneIndex, right.ExpressedToneIndex);
        Assert.Equal(left.PatternAlleleA, right.PatternAlleleA);
        Assert.Equal(left.PatternAlleleB, right.PatternAlleleB);
        Assert.Equal(left.ExpressedPatternIndex, right.ExpressedPatternIndex);
        Assert.Equal(left.ShinyAlleleA, right.ShinyAlleleA);
        Assert.Equal(left.ShinyAlleleB, right.ShinyAlleleB);
        Assert.Equal(left.CoatAlleleA, right.CoatAlleleA);
        Assert.Equal(left.CoatAlleleB, right.CoatAlleleB);
        Assert.Equal(left.ExpressedCoatIndex, right.ExpressedCoatIndex);
    }

    private static VoidlingData CreateParent(string id)
    {
        var data = new VoidlingData { Id = id };
        foreach (var statId in Rules.Genetics.StatIds)
        {
            data.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 2,
                AlleleB = 2,
                ExpressedAlleleIndex = 0
            };
        }
        return data;
    }
}
