using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Application.Roster;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class PlayerInformationProjectionTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void CreatureProfile_SeparatesInheritedRankFromTrainingProgressAndSnapshotsValues()
    {
        var state = new GameStateData();
        var creature = CreateCreature("profile");
        creature.Stage = LifeStage.Adult;
        creature.InbreedingBurdenLevel = 2;
        creature.Genome.AbilityGenes["run"] = new GenePairData
        {
            AlleleA = 2,
            AlleleB = 4,
            ExpressedAlleleIndex = 1
        };
        creature.TrainingPoints["run"] = 29;
        state.Voidlings.Add(creature);

        var projection = new CreatureProfileProjectionService(Rules).Create(state, creature.Id);
        var run = Assert.Single(projection!.Stats.Where(stat => stat.StatId == "run"));

        Assert.Equal("A", run.InheritedRank);
        Assert.Equal("C", run.Dna1Rank);
        Assert.Equal("A", run.Dna2Rank);
        Assert.Equal(3, run.TrainingLevel);
        Assert.Equal(80, run.EffectiveValue);
        Assert.InRange(run.TrainingProgress, 0.416, 0.417);
        Assert.Equal(LineageRiskBand.Moderate, projection.LineageRisk);

        creature.Genome.AbilityGenes["run"].AlleleB = 0;
        creature.TrainingPoints["run"] = 0;
        creature.InbreedingBurdenLevel = 0;

        Assert.Equal("A", run.InheritedRank);
        Assert.Equal(3, run.TrainingLevel);
        Assert.Equal(80, run.EffectiveValue);
        Assert.Equal(LineageRiskBand.Moderate, projection.LineageRisk);
    }

    [Theory]
    [InlineData(0, LineageRiskBand.None)]
    [InlineData(1, LineageRiskBand.Low)]
    [InlineData(2, LineageRiskBand.Moderate)]
    [InlineData(3, LineageRiskBand.High)]
    [InlineData(4, LineageRiskBand.Critical)]
    [InlineData(99, LineageRiskBand.Critical)]
    public void BreedingPairInfo_ProjectsQualitativeBandAndConfirmedHatchFailureRisk(
        int burden,
        LineageRiskBand expected)
    {
        var internalPreview = new BreedingPreview(
            BreedingFailure.None,
            Related: burden > 0,
            ChildBurden: burden,
            HatchFailurePercent: 73,
            IsCleanOutcross: false);

        var projection = new BreedingPairInfoProjectionService().Create(internalPreview);

        Assert.True(projection.CanBreed);
        Assert.Equal(expected, projection.LineageRisk);
        Assert.Equal(burden, projection.ChildBurden);
        Assert.Equal(73, projection.HatchFailurePercent);
        Assert.DoesNotContain(
            projection.GetType().GetProperties(),
            property => property.Name.Contains("StatChance", System.StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("ColorChance", System.StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("OffspringProbability", System.StringComparison.OrdinalIgnoreCase));
    }

    private static VoidlingData CreateCreature(string id)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            TintHex = "#ABCDEF"
        };
        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 1,
                AlleleB = 1,
                ExpressedAlleleIndex = 0
            };
            creature.TrainingPoints[statId] = 0;
        }
        return creature;
    }
}
