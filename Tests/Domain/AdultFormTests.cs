using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Persistence;
using Voidling.Domain.Evolution;
using Voidling.Domain.Lifecycle;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

/// <summary>
/// At adulthood the highest stat decides the form if it reached level 10; stamina on top or no
/// stat at 10 makes a Neutral adult, and ties are an even, reproducible pick.
/// </summary>
public sealed class AdultFormTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Theory]
    [InlineData("run", EvolutionSpecialization.Run, "run")]
    [InlineData("swim", EvolutionSpecialization.Swim, "water")]
    [InlineData("fly", EvolutionSpecialization.Fly, "fly")]
    [InlineData("power", EvolutionSpecialization.Power, "power")]
    public void HighestStatAtLevelTen_PicksThatForm(string statId, EvolutionSpecialization form, string visualTypeId)
    {
        var baby = Baby("grower", (statId, 10), ("stamina", 4));

        var result = EvolutionService.ResolveFirstEvolution(baby, Rules);

        Assert.Equal(form, result.Specialization);
        Assert.Equal(form, baby.EvolutionSpecialization);
        Assert.Equal(visualTypeId, baby.Appearance.VisualTypeId);
        Assert.Equal(statId, result.DecidingStatId);
        Assert.Equal(10, result.DecidingLevel);
    }

    [Fact]
    public void StaminaOnTop_MakesANeutralAdult()
    {
        var baby = Baby("stamina", ("stamina", 30), ("run", 20));

        var result = EvolutionService.ResolveFirstEvolution(baby, Rules);

        Assert.Equal(EvolutionSpecialization.Generalist, result.Specialization);
        Assert.Equal("neutral", baby.Appearance.VisualTypeId);
        Assert.Equal("stamina", result.PromotedStatId);
    }

    [Fact]
    public void NothingAtLevelTen_MakesANeutralAdult()
    {
        var baby = Baby("untrained", ("fly", 9), ("run", 3));

        var result = EvolutionService.ResolveFirstEvolution(baby, Rules);

        Assert.Equal(EvolutionSpecialization.Generalist, result.Specialization);
        Assert.Equal("neutral", baby.Appearance.VisualTypeId);
    }

    [Fact]
    public void TiedHighestStats_AreAnEvenReproduciblePick()
    {
        var picks = new List<EvolutionSpecialization>();
        for (var index = 0; index < 60; index++)
        {
            var first = Baby($"twin-{index}", ("run", 15), ("swim", 15));
            var again = Baby($"twin-{index}", ("run", 15), ("swim", 15));
            var pick = EvolutionService.ResolveFirstEvolution(first, Rules).Specialization;

            Assert.Equal(pick, EvolutionService.ResolveFirstEvolution(again, Rules).Specialization);
            Assert.Contains(pick, new[] { EvolutionSpecialization.Run, EvolutionSpecialization.Swim });
            picks.Add(pick);
        }

        Assert.Contains(EvolutionSpecialization.Run, picks);
        Assert.Contains(EvolutionSpecialization.Swim, picks);
    }

    [Fact]
    public void Adulthood_PromotesTheWinningStatOnceAndNeverAgain()
    {
        var baby = Baby("promoted", ("power", 12));
        baby.Genome.AbilityGenes["power"] = new GenePairData { AlleleA = 2, AlleleB = 1, ExpressedAlleleIndex = 0 };

        var first = EvolutionService.ResolveFirstEvolution(baby, Rules);
        var second = EvolutionService.ResolveFirstEvolution(baby, Rules);

        Assert.Equal(2, first.PreviousRank);
        Assert.Equal(3, first.NewRank);
        Assert.Equal(3, baby.Genome.AbilityGenes["power"].AlleleA);
        Assert.False(second.Promoted);
        Assert.Equal(3, baby.Genome.AbilityGenes["power"].AlleleA);
    }

    [Fact]
    public void Reincarnation_MakesABabyThatPicksAFormAgain()
    {
        var adult = Baby("reborn", ("swim", 20));
        EvolutionService.ResolveFirstEvolution(adult, Rules);
        adult.Stage = LifeStage.Adult;

        new ReincarnationService().ApplyReincarnation(adult, Rules.Reincarnation, Rules.Stats);

        Assert.Equal(EvolutionSpecialization.None, adult.EvolutionSpecialization);
        Assert.Equal("normal", adult.Appearance.VisualTypeId);
    }

    [Fact]
    public void Migration_TurnsAdultsFromOlderSavesNeutralAndLeavesBabies()
    {
        var adult = new VoidlingData { Id = "old-adult", Stage = LifeStage.Adult, EvolutionSpecialization = EvolutionSpecialization.Swim };
        var baby = new VoidlingData { Id = "old-baby", Stage = LifeStage.Child };
        var state = new GameStateData { SaveVersion = 22 };
        state.Voidlings.Add(adult);
        state.Voidlings.Add(baby);

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal("neutral", adult.Appearance.VisualTypeId);
        Assert.Equal(EvolutionSpecialization.Generalist, adult.EvolutionSpecialization);
        Assert.Equal("normal", baby.Appearance.VisualTypeId);
    }

    private static VoidlingData Baby(string id, params (string StatId, int Level)[] levels)
    {
        var baby = new VoidlingData { Id = id, Name = id, Stage = LifeStage.Child };
        foreach (var statId in Rules.Genetics.StatIds)
        {
            baby.Genome.AbilityGenes[statId] = new GenePairData { AlleleA = 2, AlleleB = 2 };
            baby.Stats[statId] = new StatProgressData();
        }

        foreach (var (statId, level) in levels)
            baby.Stats[statId] = new StatProgressData { Level = level, Points = level * 18 };

        return baby;
    }
}
