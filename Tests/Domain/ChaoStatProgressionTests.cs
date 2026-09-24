using System.Collections.Generic;
using Voidling.Domain.Lifecycle;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

/// <summary>
/// Stats follow Chao Garden: every rank levels 0-99, a level-up is worth
/// 3 × rank + 11 + random(1..5) points, points are capped, and reincarnation returns every stat to
/// level 1 keeping 10% of its points.
/// </summary>
public sealed class ChaoStatProgressionTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;
    private static readonly StatGrowthRules Stats = Rules.Stats;

    [Theory]
    [InlineData(0, 12, 16)]
    [InlineData(1, 15, 19)]
    [InlineData(2, 18, 22)]
    [InlineData(3, 21, 25)]
    [InlineData(4, 24, 28)]
    [InlineData(5, 27, 31)]
    public void LevelUp_AddsThreePerRankPlusElevenPlusOneToFive(int rank, int minimum, int maximum)
    {
        var service = new StatProgressionService();
        for (var level = 1; level <= 99; level++)
        {
            var points = service.RollLevelUpPoints(Creature("roller", rank), "run", level, Stats);
            Assert.InRange(points, minimum, maximum);
        }
    }

    [Fact]
    public void EveryRank_CanReachTheLevelCap()
    {
        var service = new StatProgressionService();
        for (var rank = 0; rank <= 5; rank++)
        {
            var creature = Creature($"rank-{rank}", rank);
            service.TrainToLevel(creature, "swim", 99, Stats);

            Assert.Equal(99, creature.Stats["swim"].Level);
            Assert.Equal(0, creature.Stats["swim"].Progress);
        }
    }

    [Fact]
    public void HigherRank_IsWorthMorePointsAtTheSameLevel()
    {
        var service = new StatProgressionService();
        var weak = Creature("weak", rank: 2);
        var strong = Creature("strong", rank: 5);
        service.TrainToLevel(weak, "fly", 99, Stats);
        service.TrainToLevel(strong, "fly", 99, Stats);
        var calculator = new StatCalculator(Stats);

        Assert.True(strong.Stats["fly"].Points > weak.Stats["fly"].Points);
        Assert.True(calculator.GetEffectiveStat(strong, "fly") > calculator.GetEffectiveStat(weak, "fly"));
        Assert.InRange(strong.Stats["fly"].Points, 99 * 27, 99 * 31);
    }

    [Fact]
    public void Progress_FillsTenStepsPerLevel()
    {
        var creature = Creature("trainee", rank: 0);

        var result = new StatProgressionService().AddProgress(creature, "run", 25, Stats);

        Assert.Equal(2, result.LevelsGained);
        Assert.Equal(2, creature.Stats["run"].Level);
        Assert.Equal(5, creature.Stats["run"].Progress);
        Assert.Equal(0.5f, new StatCalculator(Stats).GetLevelProgress(creature, "run"));
    }

    [Fact]
    public void SameCreature_AlwaysGainsTheSamePoints()
    {
        var first = Creature("same", rank: 3);
        var second = Creature("same", rank: 3);
        var service = new StatProgressionService();

        service.AddProgress(first, "power", 400, Stats);
        service.AddProgress(second, "power", 400, Stats);

        Assert.Equal(first.Stats["power"].Points, second.Stats["power"].Points);
    }

    [Fact]
    public void Points_StopAtTheCap()
    {
        var creature = Creature("capped", rank: 5);
        creature.Stats["run"] = new StatProgressData { Level = 50, Points = Stats.PointCap - 5 };

        new StatProgressionService().AddProgress(creature, "run", Stats.ProgressPerLevel * 3, Stats);

        Assert.Equal(53, creature.Stats["run"].Level);
        Assert.Equal(Stats.PointCap, creature.Stats["run"].Points);
    }

    [Fact]
    public void MaxLevel_TakesNoMoreProgress()
    {
        var creature = Creature("maxed", rank: 1);
        creature.Stats["run"] = new StatProgressData { Level = 99, Points = 1500 };

        var result = new StatProgressionService().AddProgress(creature, "run", 50, Stats);

        Assert.False(result.Changed);
        Assert.Equal(99, creature.Stats["run"].Level);
        Assert.Equal(1500, creature.Stats["run"].Points);
    }

    [Fact]
    public void Reincarnation_ReturnsEveryStatToLevelOneKeepingTenPercentOfPoints()
    {
        var creature = Creature("elder", rank: 4);
        creature.Stage = LifeStage.Adult;
        creature.Stats["run"] = new StatProgressData { Level = 60, Progress = 7, Points = 1509 };
        creature.Stats["swim"] = new StatProgressData { Level = 3, Progress = 2, Points = 9 };

        new ReincarnationService().ApplyReincarnation(creature, Rules.Reincarnation, Stats);

        Assert.Equal(1, creature.Stats["run"].Level);
        Assert.Equal(0, creature.Stats["run"].Progress);
        Assert.Equal(150, creature.Stats["run"].Points);
        Assert.Equal(1, creature.Stats["swim"].Level);
        Assert.Equal(0, creature.Stats["swim"].Points);
        Assert.Equal(LifeStage.Child, creature.Stage);
    }

    private static VoidlingData Creature(string id, int rank)
    {
        var creature = new VoidlingData { Id = id, Name = id };
        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData { AlleleA = rank, AlleleB = rank };
            creature.Stats[statId] = new StatProgressData();
        }

        return creature;
    }
}
