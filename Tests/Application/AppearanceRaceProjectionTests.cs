using System;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Application.Racing;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class AppearanceRaceProjectionTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void RaceEntrant_FreezesSemanticAppearanceIndependentlyFromLiveGenome()
    {
        var creature = CreateCreature("racer");
        creature.Genome.ColorAlleleA = 11;
        creature.Genome.ColorAlleleB = 11;
        creature.Genome.ToneAlleleA = AppearanceAlleles.MonoTone;
        creature.Genome.ToneAlleleB = AppearanceAlleles.MonoTone;
        creature.Genome.PatternAlleleA = 3;
        creature.Genome.PatternAlleleB = 3;
        creature.Genome.ShinyAlleleA = AppearanceAlleles.Shiny;
        creature.Genome.ShinyAlleleB = AppearanceAlleles.Shiny;
        creature.Genome.CoatAlleleA = AppearanceAlleles.GlowCoat;
        creature.Genome.CoatAlleleB = AppearanceAlleles.GlowCoat;

        var entrant = new RaceEntryFactory(Rules).CreateOwnedEntrant(creature);
        var appearance = Assert.IsType<AppearancePhenotype>(entrant.Appearance);

        Assert.Equal(11, appearance.ColorAllele);
        Assert.Equal(AppearanceTone.MonoTone, appearance.Tone);
        Assert.Equal(3, appearance.PatternAllele);
        Assert.True(appearance.Shiny);
        Assert.Equal(AppearanceAlleles.GlowCoat, appearance.CoatAllele);

        creature.Genome.ColorAlleleA = AppearanceAlleles.NormalColor;
        creature.Genome.PatternAlleleA = AppearanceAlleles.DefaultPattern;
        creature.Genome.ShinyAlleleA = AppearanceAlleles.NonShiny;
        creature.Genome.CoatAlleleA = AppearanceAlleles.NoSpecialCoat;

        Assert.Equal(11, appearance.ColorAllele);
        Assert.Equal(3, appearance.PatternAllele);
        Assert.True(appearance.Shiny);
        Assert.Equal(AppearanceAlleles.GlowCoat, appearance.CoatAllele);
    }

    [Fact]
    public void MultiplayerRaceCodec_RoundTripsCosmeticPhenotypeWithoutPuttingItInSimulationStats()
    {
        var state = new GameStateData();
        var creature = CreateCreature("shared-racer");
        creature.Genome.PatternAlleleA = 2;
        creature.Genome.PatternAlleleB = 2;
        creature.Genome.ShinyAlleleA = AppearanceAlleles.Shiny;
        creature.Genome.ShinyAlleleB = AppearanceAlleles.Shiny;
        creature.Genome.CoatAlleleA = AppearanceAlleles.GlistenCoat;
        creature.Genome.CoatAlleleB = AppearanceAlleles.GlistenCoat;
        state.Voidlings.Add(creature);

        var selection = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(selection.TryCreate(
            state,
            new PlatformUserId(1001),
            creature.Id,
            out var first,
            out var firstError), firstError);
        Assert.True(selection.TryCreate(
            state,
            new PlatformUserId(1002),
            creature.Id,
            out var second,
            out var secondError), secondError);

        var raceFactory = new MultiplayerRaceEntryFactory(Rules);
        var payload = raceFactory.CreateStartPayload(
            Guid.NewGuid().ToString("D"),
            new[] { first, second });
        var encoded = MultiplayerRaceStartCodec.Encode(payload);

        Assert.True(MultiplayerRaceStartCodec.TryDecode(encoded, out var decoded, out var decodeError), decodeError);
        Assert.Equal(2, decoded.Entrants[0].Appearance!.PatternAllele);
        Assert.True(decoded.Entrants[0].Appearance!.Shiny);
        Assert.Equal(AppearanceAlleles.GlistenCoat, decoded.Entrants[0].Appearance!.CoatAllele);

        Assert.True(raceFactory.TryResolve(decoded, out var resolved, out var resolveError), resolveError);
        var resolvedAppearance = Assert.IsType<AppearancePhenotype>(resolved.Entry.Entrants[0].Appearance);
        Assert.Equal(decoded.Entrants[0].Appearance, resolvedAppearance);
        Assert.Equal(first.Participant.Run, resolved.Entry.Entrants[0].Participant.Run);
        Assert.Equal(first.Participant.Swim, resolved.Entry.Entrants[0].Participant.Swim);
    }

    private static VoidlingData CreateCreature(string id)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            TintHex = Rules.Appearance.PaletteHex[11]
        };

        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 2,
                AlleleB = 3,
                ExpressedAlleleIndex = 1
            };
            creature.TrainingPoints[statId] = 0;
        }

        creature.Genome.ColorAlleleA = 11;
        creature.Genome.ColorAlleleB = 11;
        return creature;
    }
}
