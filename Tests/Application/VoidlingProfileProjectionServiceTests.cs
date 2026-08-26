using System.Linq;
using Voidling.Application.Creatures;
using Voidling.Domain.Breeding;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class VoidlingProfileProjectionServiceTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Projection_SeparatesInheritedPotentialFromTrainingProgress()
    {
        var state = new GameStateData();
        var creature = CreateCreature("child");
        creature.Genome.AbilityGenes["run"] = new GenePairData
        {
            AlleleA = 1,
            AlleleB = 4,
            ExpressedAlleleIndex = 1
        };
        creature.TrainingPoints["run"] = 24;
        state.Voidlings.Add(creature);

        var projection = new VoidlingProfileProjectionService(Rules).Create(state, creature.Id)!;
        var run = Assert.Single(projection.Stats.Where(stat => stat.StatId == "run"));

        Assert.Equal(1, run.DnaProfile1Rank);
        Assert.Equal(4, run.DnaProfile2Rank);
        Assert.Equal(4, run.ExpressedPotentialRank);
        Assert.Equal(24, run.TrainingPoints);
        Assert.Equal(3, run.TrainingLevel);
        Assert.Equal(0.0, run.TrainingLevelProgress, 5);
        Assert.True(run.EffectiveValue > 12.0f + 4 * 13.0f);
    }

    [Fact]
    public void Projection_ResolvesRareTraitFounderThroughArchivedLineage()
    {
        var state = new GameStateData();
        var creature = CreateCreature("child");
        creature.RareTraits.Add(new RareTraitData
        {
            TraitId = "Angel",
            FounderCreatureId = "departed-founder",
            GenerationFromFounder = 2,
            CanTransmit = false
        });
        state.Voidlings.Add(creature);
        state.LineageArchive.Add(new LineageArchiveEntry(
            "departed-founder",
            "Old Star",
            "",
            "",
            0,
            "#FFFFFF",
            false));

        var projection = new VoidlingProfileProjectionService(Rules).Create(state, creature.Id)!;
        var mutation = Assert.Single(projection.RareTraits);

        Assert.Equal("Angel", mutation.TraitId);
        Assert.Equal("departed-founder", mutation.FounderCreatureId);
        Assert.Equal("Old Star", mutation.FounderDisplayName);
        Assert.Equal(2, mutation.GenerationFromFounder);
        Assert.False(mutation.CanTransmit);
    }

    [Fact]
    public void Projection_PreservesActiveBurdenAndHistoricalMarkAsDifferentFacts()
    {
        var state = new GameStateData();
        var creature = CreateCreature("cleansed");
        creature.InbreedingBurdenLevel = 0;
        creature.InbreedingHistoryFlag = true;
        state.Voidlings.Add(creature);

        var projection = new VoidlingProfileProjectionService(Rules).Create(state, creature.Id)!;

        Assert.Equal(0, projection.ActiveInbreedingBurden);
        Assert.True(projection.InbreedingHistoryFlag);
    }

    [Fact]
    public void Projection_IsImmutableSnapshotOfMutableCreatureCollections()
    {
        var state = new GameStateData();
        var creature = CreateCreature("snapshot");
        creature.RareTraits.Add(new RareTraitData
        {
            TraitId = "Lustrous",
            FounderCreatureId = creature.Id,
            CanTransmit = true
        });
        state.Voidlings.Add(creature);

        var projection = new VoidlingProfileProjectionService(Rules).Create(state, creature.Id)!;
        var projectedRun = projection.Stats.Single(stat => stat.StatId == "run");
        var projectedTrait = Assert.Single(projection.RareTraits);

        creature.Genome.AbilityGenes["run"].AlleleA = 5;
        creature.TrainingPoints["run"] = 120;
        creature.RareTraits[0].TraitId = "Changed";

        Assert.Equal(1, projectedRun.DnaProfile1Rank);
        Assert.Equal(0, projectedRun.TrainingPoints);
        Assert.Equal("Lustrous", projectedTrait.TraitId);
    }

    [Fact]
    public void Projection_DoesNotExposeOffspringProbabilityData()
    {
        var publicProperties = typeof(VoidlingProfileProjection).GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(VoidlingStatProfileProjection).GetProperties().Select(property => property.Name))
            .ToArray();

        Assert.DoesNotContain(publicProperties, name =>
            name.Contains("Chance", System.StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Probability", System.StringComparison.OrdinalIgnoreCase));
    }

    private static VoidlingData CreateCreature(string id)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            FamilyGeneration = 3,
            TintHex = "#F6F0C9"
        };

        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 1,
                AlleleB = 2,
                ExpressedAlleleIndex = 0
            };
            creature.TrainingPoints[statId] = 0;
        }

        creature.Genome.ColorAlleleA = 2;
        creature.Genome.ColorAlleleB = 7;
        creature.Genome.ExpressedColorIndex = 1;
        return creature;
    }
}
