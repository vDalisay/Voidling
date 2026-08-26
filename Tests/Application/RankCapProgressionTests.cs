using Voidling.Application.Persistence;
using Voidling.Application.Training;
using Voidling.Domain.Evolution;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class RankCapProgressionTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 40)]
    [InlineData(2, 60)]
    [InlineData(3, 80)]
    [InlineData(4, 100)]
    [InlineData(5, 120)]
    public void DnaRank_DefinesHardTrainingCeiling(int rank, int expectedCap)
    {
        var creature = CreateCreature(rank);
        var stats = new StatCalculator(Rules.Stats);

        Assert.Equal(expectedCap, stats.GetTrainingPointCap(creature, "run"));
    }

    [Fact]
    public void TrainingAtCurrentRankCap_DoesNotConsumeItem()
    {
        var state = new GameStateData();
        var creature = CreateCreature(rank: 0);
        creature.TrainingPoints["run"] = Rules.Stats.RankCaps.E;
        state.Voidlings.Add(creature);
        state.TrainingItems["run"] = 1;
        var training = new TrainingUseCase(Rules);

        var validation = training.ValidateTrainingItem(state, creature.Id, "run");
        var result = training.ApplyTrainingItem(state, creature.Id, "run", 999UL);

        Assert.Equal(TrainingFailure.StatAtCap, validation);
        Assert.Equal(TrainingFailure.StatAtCap, result.Failure);
        Assert.Equal(1, state.TrainingItems["run"]);
        Assert.Equal(Rules.Stats.RankCaps.E, creature.TrainingPoints["run"]);
    }

    [Fact]
    public void FirstEvolutionPromotion_UnlocksHigherTrainingCeiling()
    {
        var creature = CreateCreature(rank: 4);
        creature.RunPowerInfluence = -1.0f;
        creature.TrainingPoints["run"] = Rules.Stats.RankCaps.A;
        var stats = new StatCalculator(Rules.Stats);

        var before = stats.GetTrainingPointCap(creature, "run");
        var evolution = EvolutionService.ResolveFirstEvolution(creature, Rules);
        var after = stats.GetTrainingPointCap(creature, "run");

        Assert.Equal(100, before);
        Assert.True(evolution.Promoted);
        Assert.Equal(5, creature.Genome.AbilityGenes["run"].ExpressedValue);
        Assert.Equal(120, after);
    }

    [Fact]
    public void Migration_ClampsLegacyBankedTrainingToCurrentDnaRank()
    {
        var state = new GameStateData { SaveVersion = 8 };
        var creature = CreateCreature(rank: 1);
        creature.TrainingPoints["run"] = 120;
        state.Voidlings.Add(creature);

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal(40, creature.TrainingPoints["run"]);
    }

    private static VoidlingData CreateCreature(int rank)
    {
        var creature = new VoidlingData
        {
            Id = "rank-cap-test",
            Stage = LifeStage.Child
        };

        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = rank,
                AlleleB = rank,
                ExpressedAlleleIndex = 0
            };
            creature.TrainingPoints[statId] = 0;
        }

        return creature;
    }
}
