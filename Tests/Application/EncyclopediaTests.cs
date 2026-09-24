using System.Linq;
using Voidling.Application.Collection;
using Voidling.Application.Persistence;
using Voidling.Application.Simulation;
using Voidling.Domain.Collection;
using Voidling.Domain.Creatures;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// The journal has one entry per form and special variant. Hatching, growing up and a special
/// variant hatching unlock them the first time; the first discoverer is remembered.
/// </summary>
public sealed class EncyclopediaTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void TheJournal_ListsEveryFormAndTheSwampGuy()
    {
        var ids = EncyclopediaCatalog.All.Select(entry => entry.Id).ToArray();

        Assert.Equal(new[] { "baby", "neutral", "run", "swim", "fly", "power", "swamp-guy" }, ids);
        var projection = EncyclopediaRecorder.Project(new GameStateData());
        Assert.Equal(0, projection.DiscoveredCount);
        Assert.Equal(ids.Length, projection.Total);
    }

    [Fact]
    public void HatchingAndGrowingUp_UnlockEntriesOnce()
    {
        var state = new GameStateData();
        state.OwnedEggs.Add(new EggData
        {
            Id = "egg", Genome = new GenomeFactory(Rules.Genetics).CreateRandom(5UL),
            IsViable = true, FailureResolved = true, RequiredIncubationSeconds = 1.0f
        });
        var simulation = new AdvanceSimulationUseCase(Rules);

        var hatch = simulation.Advance(state, 2.0f);

        var discovered = Assert.Single(hatch.Events.OfType<EncyclopediaEntryDiscoveredEvent>());
        Assert.Equal("baby", discovered.EntryId);
        var baby = Assert.Single(state.Voidlings);

        // Growing up untrained makes a Neutral adult: a second entry, with its discoverer's name.
        var grown = simulation.Advance(state, Rules.Lifecycle.ChildToAdultSeconds);
        Assert.Contains(grown.Events.OfType<EncyclopediaEntryDiscoveredEvent>(), e => e.EntryId == "neutral");
        var record = state.Encyclopedia.Single(entry => entry.EntryId == "neutral");
        Assert.Equal((2, baby.Name), (record.Order, record.CreatureName));

        // A second baby adds nothing new.
        state.OwnedEggs.Add(new EggData
        {
            Id = "egg-2", Genome = new GenomeFactory(Rules.Genetics).CreateRandom(6UL),
            IsViable = true, FailureResolved = true, RequiredIncubationSeconds = 1.0f
        });
        Assert.Empty(simulation.Advance(state, 2.0f).Events.OfType<EncyclopediaEntryDiscoveredEvent>());
        Assert.Equal(2, EncyclopediaRecorder.Project(state).DiscoveredCount);
    }

    [Fact]
    public void TheSwampGuyHatching_UnlocksHisEntry()
    {
        var state = new GameStateData();
        var swampGuy = new VoidlingData
        {
            Id = "swamp-guy", Name = "Swampy", SpecialVariantId = SpecialVariantCatalog.SwampGuyId,
            Appearance = new VoidlingAppearanceData { VisualTypeId = SpecialVariantCatalog.SwampGuy.VisualTypeId }
        };

        Assert.Equal("swamp-guy", EncyclopediaRecorder.Discover(state, swampGuy));
        Assert.Null(EncyclopediaRecorder.Discover(state, swampGuy));
        Assert.True(EncyclopediaRecorder.Project(state).Entries.Single(entry => entry.EntryId == "swamp-guy").Discovered);
    }

    [Fact]
    public void Migration_DropsUnknownAndDuplicateEntries()
    {
        var state = new GameStateData { SaveVersion = GameStateMigrationService.CurrentSaveVersion };
        state.Encyclopedia.Add(new EncyclopediaDiscoveryData { EntryId = "swim", Order = 3, CreatureName = "Later" });
        state.Encyclopedia.Add(new EncyclopediaDiscoveryData { EntryId = "swim", Order = 1, CreatureName = "First" });
        state.Encyclopedia.Add(new EncyclopediaDiscoveryData { EntryId = "dragon", Order = 2 });

        new GameStateMigrationService(Rules).Normalize(state);

        var entry = Assert.Single(state.Encyclopedia);
        Assert.Equal(("swim", "First"), (entry.EntryId, entry.CreatureName));
    }
}
