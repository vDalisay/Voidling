using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Application.Persistence;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class LineageArchiveServiceTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Migration_VersionFourBuildsLineageArchiveWithoutRerollingGenome()
    {
        var genome = new GenomeFactory(Rules.Genetics).CreateRandom(404UL);
        var creature = CreateAdult("child", genome, "parent-a", "parent-b");
        var state = new GameStateData
        {
            SaveVersion = 4,
            Voidlings = new List<VoidlingData> { creature },
            LineageArchive = null!
        };

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Same(genome, creature.Genome);
        var entry = Assert.Single(state.LineageArchive);
        Assert.Equal(creature.Id, entry.CreatureId);
        Assert.Equal("parent-a", entry.ParentAId);
        Assert.Equal("parent-b", entry.ParentBId);
        Assert.Empty(state.PendingTradeJournal);
        Assert.Empty(state.AppliedTradeIds);
    }

    [Fact]
    public void BreedingPreview_DetectsSharedAncestorThatExistsOnlyInArchive()
    {
        var first = CreateAdult("first", new GenomeFactory(Rules.Genetics).CreateRandom(1UL), "founder", "unrelated-a");
        var second = CreateAdult("second", new GenomeFactory(Rules.Genetics).CreateRandom(2UL), "founder", "unrelated-b");
        var state = new GameStateData();
        state.Voidlings.Add(first);
        state.Voidlings.Add(second);
        state.LineageArchive.Add(new LineageArchiveEntry("founder", "Founder", "", "", 0, "#FFFFFF", false));
        state.LineageArchive.Add(new LineageArchiveEntry("unrelated-a", "A", "", "", 0, "#FFFFFF", false));
        state.LineageArchive.Add(new LineageArchiveEntry("unrelated-b", "B", "", "", 0, "#FFFFFF", false));

        var preview = new BreedVoidlingsUseCase(Rules).Preview(state, first.Id, second.Id);

        Assert.True(preview.CanBreed);
        Assert.True(preview.Related);
        Assert.Equal(1, preview.ChildBurden);
    }

    [Fact]
    public void TryMerge_RejectsConflictingAncestryIdentityWithoutMutatingArchive()
    {
        var state = new GameStateData();
        state.LineageArchive.Add(new LineageArchiveEntry(
            "child",
            "Child",
            "parent-a",
            "parent-b",
            1,
            "#AAAAAA",
            false));
        var service = new LineageArchiveService();

        var merged = service.TryMerge(
            state,
            new[]
            {
                new LineageArchiveEntry(
                    "child",
                    "Forged Child",
                    "different-parent",
                    "parent-b",
                    1,
                    "#BBBBBB",
                    true)
            },
            out var error);

        Assert.False(merged);
        Assert.Contains("conflict", error!, System.StringComparison.OrdinalIgnoreCase);
        var original = Assert.Single(state.LineageArchive);
        Assert.Equal("parent-a", original.ParentAId);
        Assert.False(original.InbreedingHistoryFlag);
    }

    [Fact]
    public void AncestryClosure_IncludesRootAndAncestorsOnlyToConfiguredDepth()
    {
        var state = new GameStateData();
        state.LineageArchive.AddRange(new[]
        {
            new LineageArchiveEntry("root", "Root", "parent", "", 2, "#AAAAAA", false),
            new LineageArchiveEntry("parent", "Parent", "grandparent", "", 1, "#BBBBBB", false),
            new LineageArchiveEntry("grandparent", "Grandparent", "older", "", 0, "#CCCCCC", false),
            new LineageArchiveEntry("older", "Older", "", "", 0, "#DDDDDD", false),
            new LineageArchiveEntry("unrelated", "Unrelated", "", "", 0, "#EEEEEE", false)
        });
        var service = new LineageArchiveService();

        var closure = service.GetAncestryClosure(state, new[] { "root" }, maxAncestorDepth: 2);
        var ids = closure.Select(entry => entry.CreatureId).ToHashSet();

        Assert.Contains("root", ids);
        Assert.Contains("parent", ids);
        Assert.Contains("grandparent", ids);
        Assert.DoesNotContain("older", ids);
        Assert.DoesNotContain("unrelated", ids);
    }

    private static VoidlingData CreateAdult(
        string id,
        GenomeData genome,
        string parentAId,
        string parentBId)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = genome,
            ParentAId = parentAId,
            ParentBId = parentBId,
            FamilyGeneration = 1
        };

        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;

        return creature;
    }
}
