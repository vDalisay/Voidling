using System;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class GeneticsArchitectureTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void StableRandom_SameSeedAndSalt_ProducesSameStream()
    {
        var first = StableRandom.Create(123456789UL, "gene:run");
        var second = StableRandom.Create(123456789UL, "gene:run");

        for (var i = 0; i < 20; i++)
            Assert.Equal(first.Next(), second.Next());
    }

    [Fact]
    public void StableRandom_DifferentSalt_IsIndependent()
    {
        var first = StableRandom.Create(42UL, "gene:run");
        var second = StableRandom.Create(42UL, "gene:swim");

        Assert.NotEqual(first.Next(), second.Next());
    }

    [Fact]
    public void GenomeFactory_SameSeed_ProducesSameGenome()
    {
        var factory = new GenomeFactory(Rules.Genetics);

        var first = factory.CreateRandom(998877UL);
        var second = factory.CreateRandom(998877UL);

        AssertGenomeEqual(first, second);
    }

    [Fact]
    public void ChildGenome_GetsOneAbilityAlleleFromEachSelectedParent()
    {
        var parentA = CreateParent("a", 0, 1);
        var parentB = CreateParent("b", 4, 5);
        var inheritance = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 0.0 });

        for (ulong seed = 1; seed <= 100; seed++)
        {
            var child = inheritance.CreateChild(parentA, parentB, seed);
            foreach (var statId in Rules.Genetics.StatIds)
            {
                var gene = child.AbilityGenes[statId];
                Assert.Contains(gene.AlleleA, new[] { 0, 1 });
                Assert.Contains(gene.AlleleB, new[] { 4, 5 });
            }
        }
    }

    [Fact]
    public void ChildGenome_DoesNotUseTrainingPointsAsInheritedPotential()
    {
        var parentA = CreateParent("a", 0, 0);
        var parentB = CreateParent("b", 0, 0);
        foreach (var statId in Rules.Genetics.StatIds)
        {
            parentA.TrainingPoints[statId] = 10_000;
            parentB.TrainingPoints[statId] = 10_000;
        }

        var inheritance = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 0.0 });
        var child = inheritance.CreateChild(parentA, parentB, 123UL);

        foreach (var statId in Rules.Genetics.StatIds)
        {
            Assert.Equal(0, child.AbilityGenes[statId].AlleleA);
            Assert.Equal(0, child.AbilityGenes[statId].AlleleB);
        }
    }

    [Fact]
    public void ChildGenome_DoesNotResurrectNormalStatsFromDeeperAncestry()
    {
        var parentA = CreateParent("a", 1, 1, "ancestor-a", "ancestor-b");
        var parentB = CreateParent("b", 1, 1, "ancestor-c", "ancestor-d");
        var inheritance = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 0.0 });

        var child = inheritance.CreateChild(parentA, parentB, 456UL);

        foreach (var statId in Rules.Genetics.StatIds)
        {
            Assert.Equal(1, child.AbilityGenes[statId].AlleleA);
            Assert.Equal(1, child.AbilityGenes[statId].AlleleB);
        }
    }

    [Fact]
    public void AbilityRankBreakthrough_ChangesAtMostOneStatByExactlyOneRank()
    {
        const int parentalBest = 3; // B
        var parentA = CreateParent("a", parentalBest, parentalBest);
        var parentB = CreateParent("b", parentalBest, parentalBest);
        var inheritance = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 1.0 });

        var child = inheritance.CreateChild(parentA, parentB, 987654UL);
        var breakthroughStats = 0;

        foreach (var statId in Rules.Genetics.StatIds)
        {
            var gene = child.AbilityGenes[statId];
            var childBest = Math.Max(gene.AlleleA, gene.AlleleB);
            Assert.InRange(childBest, parentalBest, parentalBest + 1);
            if (childBest == parentalBest + 1)
                breakthroughStats++;
        }

        Assert.Equal(1, breakthroughStats);
    }

    [Fact]
    public void AbilityRankBreakthrough_NeverExceedsS()
    {
        var maxGrade = Rules.Genetics.GradeWeights.Count - 1;
        var parentA = CreateParent("a", maxGrade, maxGrade);
        var parentB = CreateParent("b", maxGrade, maxGrade);
        var inheritance = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 1.0 });

        var child = inheritance.CreateChild(parentA, parentB, 11UL);

        foreach (var statId in Rules.Genetics.StatIds)
        {
            Assert.Equal(maxGrade, child.AbilityGenes[statId].AlleleA);
            Assert.Equal(maxGrade, child.AbilityGenes[statId].AlleleB);
        }
    }

    [Fact]
    public void ChildGenome_WithBreakthroughRule_RemainsDeterministicForSeed()
    {
        var parentA = CreateParent("a", 2, 3);
        var parentB = CreateParent("b", 2, 3);
        var inheritance = new GenomeInheritanceService(
            Rules.Genetics with { AbilityRankBreakthroughChance = 1.0 });

        var first = inheritance.CreateChild(parentA, parentB, 123456UL);
        var second = inheritance.CreateChild(parentA, parentB, 123456UL);

        AssertGenomeEqual(first, second);
    }

    [Fact]
    public void RareTraitTransmission_IsConfiguredAtTenPercentAndPreservesDepthRule()
    {
        Assert.Equal(0.10, Rules.Genetics.RareTraitTransmissionChance, 8);

        var parent = CreateParent("founder", 2, 2);
        parent.RareTraits.Add(new RareTraitData
        {
            TraitId = "Angel",
            FounderCreatureId = parent.Id,
            GenerationFromFounder = 0,
            CanTransmit = true
        });
        var other = CreateParent("other", 2, 2);
        var service = new RareTraitInheritanceService(Rules.Genetics);

        var inherited = 0;
        RareTraitData? sample = null;
        for (ulong seed = 1; seed <= 10_000; seed++)
        {
            var result = service.Inherit(parent, other, seed);
            if (result.Count == 0)
                continue;

            inherited++;
            sample ??= result[0];
        }

        Assert.InRange(inherited, 800, 1_200);
        Assert.NotNull(sample);
        Assert.Equal(1, sample!.GenerationFromFounder);
        Assert.True(sample.CanTransmit);

        parent.RareTraits[0].GenerationFromFounder = 1;
        for (ulong seed = 1; seed <= 10_000; seed++)
        {
            var result = service.Inherit(parent, other, seed);
            if (result.Count == 0)
                continue;

            Assert.Equal(2, result[0].GenerationFromFounder);
            Assert.False(result[0].CanTransmit);
            return;
        }

        throw new Xunit.Sdk.XunitException("Expected at least one deterministic transmission sample.");
    }

    [Fact]
    public void RelationshipService_DetectsSiblingAndAncestorRelationships()
    {
        var founderA = CreateParent("founder-a", 2, 2);
        var founderB = CreateParent("founder-b", 2, 2);
        var childA = CreateParent("child-a", 2, 2, founderA.Id, founderB.Id);
        var childB = CreateParent("child-b", 2, 2, founderA.Id, founderB.Id);
        var unrelated = CreateParent("unrelated", 2, 2);
        var population = new[] { founderA, founderB, childA, childB, unrelated };
        var service = new RelationshipService(Rules.Genetics.RelatedAncestorDepth);

        Assert.True(service.AreRelated(childA, childB, population));
        Assert.True(service.AreRelated(founderA, childA, population));
        Assert.False(service.AreRelated(childA, unrelated, population));
    }

    [Fact]
    public void InbreedingBurden_EscalatesAndCleanOutcrossReducesOneLevel()
    {
        var service = new InbreedingBurdenService();
        var burdenTwo = CreateParent("burdened", 2, 2);
        burdenTwo.InbreedingBurdenLevel = 2;
        var clean = CreateParent("clean", 2, 2);

        Assert.Equal(3, service.ComputeChildBurden(burdenTwo, clean, related: true));
        Assert.Equal(1, service.ComputeChildBurden(burdenTwo, clean, related: false));
    }

    [Fact]
    public void HatchViability_UsesConfiguredFailureLadder()
    {
        var service = new HatchViabilityService(Rules.Breeding);

        Assert.Equal(0, service.FailurePercent(0));
        Assert.Equal(20, service.FailurePercent(1));
        Assert.Equal(50, service.FailurePercent(2));
        Assert.Equal(80, service.FailurePercent(3));
        Assert.Equal(100, service.FailurePercent(4));
        Assert.True(service.RollViability(1UL, 0));
        Assert.False(service.RollViability(1UL, 4));
    }

    private static VoidlingData CreateParent(
        string id,
        int alleleA,
        int alleleB,
        string parentAId = "",
        string parentBId = "")
    {
        var data = new VoidlingData
        {
            Id = id,
            ParentAId = parentAId,
            ParentBId = parentBId
        };

        foreach (var statId in Rules.Genetics.StatIds)
        {
            data.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = alleleA,
                AlleleB = alleleB,
                ExpressedAlleleIndex = 0
            };
        }

        data.Genome.ColorAlleleA = 0;
        data.Genome.ColorAlleleB = 1;
        return data;
    }

    private static void AssertGenomeEqual(GenomeData expected, GenomeData actual)
    {
        Assert.Equal(expected.ColorAlleleA, actual.ColorAlleleA);
        Assert.Equal(expected.ColorAlleleB, actual.ColorAlleleB);
        Assert.Equal(expected.ExpressedColorIndex, actual.ExpressedColorIndex);

        foreach (var statId in Rules.Genetics.StatIds)
        {
            var left = expected.AbilityGenes[statId];
            var right = actual.AbilityGenes[statId];
            Assert.Equal(left.AlleleA, right.AlleleA);
            Assert.Equal(left.AlleleB, right.AlleleB);
            Assert.Equal(left.ExpressedAlleleIndex, right.ExpressedAlleleIndex);
        }
    }
}
