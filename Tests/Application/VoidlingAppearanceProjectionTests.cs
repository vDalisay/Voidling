using Voidling.Application.Creatures;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class VoidlingAppearanceProjectionTests
{
    [Fact]
    public void Projection_ExposesGenotypeAndResolvedChaoStylePhenotypeWithoutGenomeReference()
    {
        var rules = GameBalanceRules.DemoDefaults;
        var creature = new VoidlingData
        {
            Id = "appearance",
            Name = "Appearance",
            Stage = LifeStage.Adult,
            TintHex = rules.Appearance.PaletteHex[4]
        };
        foreach (var statId in rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 1,
                AlleleB = 1,
                ExpressedAlleleIndex = 0
            };
        }

        creature.Genome.ColorAlleleA = 0;
        creature.Genome.ColorAlleleB = 4;
        creature.Genome.ExpressedColorIndex = 0;
        creature.Genome.ToneAlleleA = AppearanceAlleles.TwoTone;
        creature.Genome.ToneAlleleB = AppearanceAlleles.MonoTone;
        creature.Genome.ExpressedToneIndex = 1;
        creature.Genome.PatternAlleleA = 0;
        creature.Genome.PatternAlleleB = 3;
        creature.Genome.ShinyAlleleA = 0;
        creature.Genome.ShinyAlleleB = 1;
        creature.Genome.CoatAlleleA = 0;
        creature.Genome.CoatAlleleB = AppearanceAlleles.GlowCoat;

        var state = new GameStateData();
        state.Voidlings.Add(creature);
        var projection = new VoidlingProfileProjectionService(rules).Create(state, creature.Id)!;

        Assert.Equal(0, projection.Appearance.ColorDnaProfile1);
        Assert.Equal(4, projection.Appearance.ColorDnaProfile2);
        Assert.Equal(4, projection.Appearance.ExpressedColorAllele);
        Assert.Equal(AppearanceTone.MonoTone, projection.Appearance.Tone);
        Assert.Equal(3, projection.Appearance.PatternAllele);
        Assert.True(projection.Appearance.Shiny);
        Assert.Equal(AppearanceAlleles.GlowCoat, projection.Appearance.CoatAllele);
        Assert.DoesNotContain(
            typeof(VoidlingAppearanceProfileProjection).GetProperties(),
            property => property.PropertyType == typeof(GenomeData));
    }
}
