using System;
using System.Collections.Generic;
using Voidling.Application.Racing;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class PersonalityGeneticsTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void FounderVector_IsCompleteStableAndDeterministic()
    {
        var factory = new GenomeFactory(Rules.Genetics);
        var first = factory.CreateRandom(123456UL);
        var second = factory.CreateRandom(123456UL);

        Assert.Equal(PersonalityTraitIds.All.Count, first.PersonalityGenes.Count);
        foreach (var traitId in PersonalityTraitIds.All)
        {
            var firstPair = first.PersonalityGenes[traitId];
            var secondPair = second.PersonalityGenes[traitId];
            Assert.InRange(firstPair.AlleleA, PersonalityGenetics.MinAllele, PersonalityGenetics.MaxAllele);
            Assert.InRange(firstPair.AlleleB, PersonalityGenetics.MinAllele, PersonalityGenetics.MaxAllele);
            Assert.Equal(firstPair.AlleleA, secondPair.AlleleA);
            Assert.Equal(firstPair.AlleleB, secondPair.AlleleB);
            Assert.Equal(firstPair.ExpressedAlleleIndex, secondPair.ExpressedAlleleIndex);
        }
    }

    [Fact]
    public void ChildVector_InheritsOneAlleleFromEachSelectedParent()
    {
        var factory = new GenomeFactory(Rules.Genetics);
        var parentA = CreateCreature("a", factory.CreateRandom(111UL));
        var parentB = CreateCreature("b", factory.CreateRandom(222UL));

        var child = new GenomeInheritanceService(Rules.Genetics).CreateChild(parentA, parentB, 333UL);

        foreach (var traitId in PersonalityTraitIds.All)
        {
            var a = parentA.Genome.PersonalityGenes[traitId];
            var b = parentB.Genome.PersonalityGenes[traitId];
            var inherited = child.PersonalityGenes[traitId];
            Assert.Contains(inherited.AlleleA, new[] { a.AlleleA, a.AlleleB });
            Assert.Contains(inherited.AlleleB, new[] { b.AlleleA, b.AlleleB });
        }
    }

    [Fact]
    public void LegacyGenomeWithoutPersonality_RemainsNeutralInsteadOfBeingRerolled()
    {
        var genome = new GenomeData { PersonalityGenes = null! };

        var tendency = PersonalityGenetics.ResolveDominant(genome);

        Assert.Equal(PersonalityPolarity.Neutral, tendency.Polarity);
        Assert.Equal(string.Empty, tendency.TraitId);
        foreach (var traitId in PersonalityTraitIds.All)
            Assert.Equal(0.0f, PersonalityGenetics.GetNormalizedExpressedValue(genome, traitId));
    }

    [Fact]
    public void PersonalityVector_DoesNotAffectRaceSnapshot()
    {
        var factory = new GenomeFactory(Rules.Genetics);
        var calmGenome = factory.CreateRandom(987UL);
        var intenseGenome = factory.CreateRandom(987UL);
        foreach (var traitId in PersonalityTraitIds.All)
        {
            intenseGenome.PersonalityGenes[traitId] = new GenePairData
            {
                AlleleA = PersonalityGenetics.MaxAllele,
                AlleleB = PersonalityGenetics.MaxAllele,
                ExpressedAlleleIndex = 0
            };
        }

        var calm = CreateCreature("calm", calmGenome);
        var intense = CreateCreature("intense", intenseGenome);
        var snapshots = new RaceParticipantSnapshotFactory(Rules);
        var first = snapshots.Create(calm);
        var second = snapshots.Create(intense);

        Assert.Equal(first.Run, second.Run);
        Assert.Equal(first.Swim, second.Swim);
        Assert.Equal(first.Fly, second.Fly);
        Assert.Equal(first.Power, second.Power);
        Assert.Equal(first.Stamina, second.Stamina);
    }

    private static VoidlingData CreateCreature(string id, GenomeData genome)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = genome,
            TrainingPoints = new Dictionary<string, int>(StringComparer.Ordinal)
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }
}
