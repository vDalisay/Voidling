using System.Linq;
using System.Text.Json;
using Voidling.Application.Breeding;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class LineageTreeProjectionServiceTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Projection_IncludesArchiveOnlyAncestorsWithoutAddingThemToRoster()
    {
        var state = new GameStateData();
        var child = CreateCreature("child", "archived-parent", "local-parent", generation: 1);
        var localParent = CreateCreature("local-parent", "", "", generation: 0);
        state.Voidlings.Add(child);
        state.Voidlings.Add(localParent);
        state.LineageArchive.Add(new LineageArchiveEntry(
            "archived-parent",
            "Archived Parent",
            "",
            "",
            0,
            "#AABBCC",
            false));

        var projection = new LineageTreeProjectionService(Rules).Create(state, child.Id);

        var archived = Assert.Single(projection.Members.Where(member => member.CreatureId == "archived-parent"));
        Assert.Equal(LineageMemberPresence.Archived, archived.Presence);
        Assert.Null(archived.ActiveInbreedingBurden);
        Assert.False(archived.HasAngelMutation);
        Assert.Equal(0, archived.OtherMutationCount);
        Assert.Empty(archived.Stats);
        Assert.DoesNotContain(state.Voidlings, creature => creature.Id == archived.CreatureId);
        Assert.DoesNotContain(state.DepartedVoidlings, creature => creature.Id == archived.CreatureId);
    }

    [Fact]
    public void Projection_SeparatesActiveBurdenFromPermanentHistoricalMark()
    {
        var state = new GameStateData();
        var creature = CreateCreature("cleansed", "", "", generation: 4);
        creature.InbreedingBurdenLevel = 0;
        creature.InbreedingHistoryFlag = true;
        state.Voidlings.Add(creature);

        var projection = new LineageTreeProjectionService(Rules).Create(state, creature.Id);
        var member = Assert.Single(projection.Members);

        Assert.Equal(0, member.ActiveInbreedingBurden);
        Assert.True(member.InbreedingHistoryFlag);
    }

    [Fact]
    public void Projection_ProjectsPortraitMutationMetadataWithoutUiInterpretation()
    {
        var state = new GameStateData();
        var creature = CreateCreature("mutated", "", "", generation: 0);
        creature.RareTraits.Add(new RareTraitData { TraitId = MutationIds.Angel });
        creature.RareTraits.Add(new RareTraitData { TraitId = "Lustrous" });
        creature.RareTraits.Add(new RareTraitData { TraitId = "Prismatic" });
        state.Voidlings.Add(creature);

        var projection = new LineageTreeProjectionService(Rules).Create(state, creature.Id);
        var member = Assert.Single(projection.Members);

        Assert.True(member.HasAngelMutation);
        Assert.Equal(2, member.OtherMutationCount);
    }

    [Fact]
    public void Projection_SnapshotsStatsInsteadOfExposingMutableCreatureState()
    {
        var state = new GameStateData();
        var creature = CreateCreature("runner", "", "", generation: 0);
        creature.TrainingPoints["run"] = 24;
        creature.Genome.AbilityGenes["run"] = new GenePairData
        {
            AlleleA = 2,
            AlleleB = 4,
            ExpressedAlleleIndex = 1
        };
        state.Voidlings.Add(creature);

        var projection = new LineageTreeProjectionService(Rules).Create(state, creature.Id);
        var run = Assert.Single(Assert.Single(projection.Members).Stats.Where(stat => stat.StatId == "run"));

        creature.TrainingPoints["run"] = 0;
        creature.Genome.AbilityGenes["run"].AlleleB = 0;

        Assert.Equal(2, run.AlleleA);
        Assert.Equal(4, run.AlleleB);
        Assert.Equal(4, run.ExpressedAllele);
        Assert.Equal(3, run.Level);
    }

    [Fact]
    public void SaveJsonRoundTrip_PreservesPedigreeAndBurdenHistorySeparately()
    {
        var state = new GameStateData();
        var creature = CreateCreature("child", "parent-a", "parent-b", generation: 2);
        creature.InbreedingBurdenLevel = 1;
        creature.InbreedingHistoryFlag = true;
        state.Voidlings.Add(creature);
        state.LineageArchive.Add(new LineageArchiveEntry(
            "parent-a", "Parent A", "grandparent", "", 1, "#FFFFFF", true));
        state.LineageArchive.Add(new LineageArchiveEntry(
            "parent-b", "Parent B", "", "", 1, "#FFFFFF", false));
        state.LineageArchive.Add(new LineageArchiveEntry(
            "grandparent", "Grandparent", "", "", 0, "#FFFFFF", false));

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<GameStateData>(json)!;
        var projection = new LineageTreeProjectionService(Rules).Create(loaded, creature.Id);

        var loadedChild = Assert.Single(projection.Members.Where(member => member.CreatureId == creature.Id));
        var loadedParent = Assert.Single(projection.Members.Where(member => member.CreatureId == "parent-a"));
        Assert.Equal("parent-a", loadedChild.ParentAId);
        Assert.Equal("parent-b", loadedChild.ParentBId);
        Assert.Equal(1, loadedChild.ActiveInbreedingBurden);
        Assert.True(loadedChild.InbreedingHistoryFlag);
        Assert.True(loadedParent.InbreedingHistoryFlag);
        Assert.Null(loadedParent.ActiveInbreedingBurden);
    }

    private static VoidlingData CreateCreature(string id, string parentAId, string parentBId, int generation)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            ParentAId = parentAId,
            ParentBId = parentBId,
            FamilyGeneration = generation,
            TintHex = "#F6F0C9"
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
