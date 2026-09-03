using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class TradeTransferServiceTests
{
    private const ulong LobbyId = 77;
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void BuildTransferBundle_IncludesVoidlingAncestryClosure()
    {
        var state = new GameStateData();
        var child = CreateAdult("child", 10UL, "parent", "");
        state.Voidlings.Add(child);
        state.LineageArchive.Add(new LineageArchiveEntry("parent", "Parent", "grand", "", 1, "#BBBBBB", false));
        state.LineageArchive.Add(new LineageArchiveEntry("grand", "Grand", "older", "", 0, "#CCCCCC", false));
        state.LineageArchive.Add(new LineageArchiveEntry("older", "Older", "", "", 0, "#DDDDDD", false));
        var service = new TradeTransferService(Rules);

        var success = service.TryBuildTransferBundle(
            state,
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, child.Id) },
            out var bundle,
            out var error);

        Assert.True(success, error);
        Assert.Single(bundle.Voidlings);
        Assert.Empty(bundle.Eggs);
        var ids = bundle.Lineage.Select(entry => entry.CreatureId).ToHashSet();
        Assert.Contains("child", ids);
        Assert.Contains("parent", ids);
        Assert.Contains("grand", ids);
    }

    [Fact]
    public void BuildTransferBundle_RejectsMalformedLocalAppearance()
    {
        var state = new GameStateData();
        var creature = CreateAdult("local", 10UL, "", "");
        creature.Appearance.VisualTypeId = "res://Assets/Voidlings/body.png";
        state.Voidlings.Add(creature);
        var service = new TradeTransferService(Rules);

        var success = service.TryBuildTransferBundle(
            state,
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, creature.Id) },
            out _,
            out var error);

        Assert.False(success);
        Assert.Contains("invalid transfer state", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_PersistsJournalWithoutMovingAssetsAndLocksOutgoingAsset()
    {
        var state = new GameStateData();
        var local = CreateAdult("local", 11UL, "", "");
        state.Voidlings.Add(local);
        var service = new TradeTransferService(Rules);
        var tradeId = Guid.NewGuid().ToString("N");
        var outgoing = new[] { new TradeAssetReference(TradeAssetKind.Voidling, local.Id) };
        var incoming = CreateRemoteVoidlingBundle("remote", 22UL);

        var result = service.Prepare(state, tradeId, LobbyId, 999UL, "terms-hash", outgoing, incoming);

        Assert.True(result.Success);
        Assert.Contains(state.Voidlings, creature => creature.Id == local.Id);
        var journal = Assert.Single(state.PendingTradeJournal);
        Assert.Equal(LobbyId, journal.LobbyId);
        Assert.True(service.IsAssetLocked(state, outgoing[0]));

        var restored = JsonSerializer.Deserialize<GameStateData>(JsonSerializer.Serialize(state));
        Assert.NotNull(restored);
        Assert.Single(restored!.PendingTradeJournal);
        Assert.Equal(tradeId, restored.PendingTradeJournal[0].TradeId);
        Assert.Equal(LobbyId, restored.PendingTradeJournal[0].LobbyId);
    }

    [Fact]
    public void Prepare_RejectsMalformedIncomingAppearanceBeforeAddingJournal()
    {
        var state = new GameStateData();
        var local = CreateAdult("local", 11UL, "", "");
        state.Voidlings.Add(local);
        var service = new TradeTransferService(Rules);
        var outgoing = new[] { new TradeAssetReference(TradeAssetKind.Voidling, local.Id) };

        var badPath = CreateAdult("bad-path", 31UL, "", "");
        badPath.Appearance = new VoidlingAppearanceData
        {
            VisualTypeId = "res://Assets/Voidlings/body.png",
            PaletteHue = 0.2f
        };
        var badPathBundle = new TradeTransferBundle(
            new[] { badPath },
            Array.Empty<EggData>(),
            new[] { LineageArchiveEntry.FromVoidling(badPath) });
        var pathResult = service.Prepare(
            state,
            Guid.NewGuid().ToString("N"),
            LobbyId,
            999UL,
            "terms-hash",
            outgoing,
            badPathBundle);

        var badHue = CreateAdult("bad-hue", 32UL, "", "");
        badHue.Appearance = new VoidlingAppearanceData
        {
            VisualTypeId = "water",
            PaletteHue = -2.0f
        };
        var badHueBundle = new TradeTransferBundle(
            new[] { badHue },
            Array.Empty<EggData>(),
            new[] { LineageArchiveEntry.FromVoidling(badHue) });
        var hueResult = service.Prepare(
            state,
            Guid.NewGuid().ToString("N"),
            LobbyId,
            999UL,
            "terms-hash",
            outgoing,
            badHueBundle);

        Assert.False(pathResult.Success);
        Assert.False(hueResult.Success);
        Assert.Empty(state.PendingTradeJournal);
        Assert.Contains(state.Voidlings, creature => creature.Id == local.Id);
    }

    [Fact]
    public void AbortPrepared_LeavesOwnedAssetsUntouchedAndUnlocksThem()
    {
        var state = new GameStateData();
        var local = CreateAdult("local", 11UL, "", "");
        state.Voidlings.Add(local);
        var service = new TradeTransferService(Rules);
        var tradeId = Guid.NewGuid().ToString("N");
        var outgoing = new[] { new TradeAssetReference(TradeAssetKind.Voidling, local.Id) };

        Assert.True(service.Prepare(
            state,
            tradeId,
            LobbyId,
            999UL,
            "terms-hash",
            outgoing,
            CreateRemoteVoidlingBundle("remote", 22UL)).Success);

        var aborted = service.AbortPrepared(state, tradeId);

        Assert.True(aborted.Success);
        Assert.Empty(state.PendingTradeJournal);
        Assert.Contains(state.Voidlings, creature => creature.Id == local.Id);
        Assert.False(service.IsAssetLocked(state, outgoing[0]));
    }

    [Fact]
    public void AbortPreparedForLobby_LeavesOtherLobbyJournalIntact()
    {
        var state = new GameStateData();
        state.PendingTradeJournal.Add(new PendingTradeJournalEntry(
            Guid.NewGuid().ToString("N"),
            LobbyId,
            10,
            "one",
            Array.Empty<TradeAssetReference>(),
            TradeTransferBundle.Empty));
        state.PendingTradeJournal.Add(new PendingTradeJournalEntry(
            Guid.NewGuid().ToString("N"),
            88,
            11,
            "two",
            Array.Empty<TradeAssetReference>(),
            TradeTransferBundle.Empty));
        var service = new TradeTransferService(Rules);

        var removed = service.AbortPreparedForLobby(state, LobbyId);

        Assert.Equal(1, removed);
        var remaining = Assert.Single(state.PendingTradeJournal);
        Assert.Equal(88UL, remaining.LobbyId);
    }

    [Fact]
    public void CommitPrepared_AtomicallyMovesAssetsAndIsIdempotent()
    {
        var state = new GameStateData();
        var local = CreateAdult("local", 11UL, "", "");
        local.WorldX = 123;
        local.WorldY = 456;
        state.Voidlings.Add(local);
        var service = new TradeTransferService(Rules);
        var tradeId = Guid.NewGuid().ToString("N");
        var outgoing = new[] { new TradeAssetReference(TradeAssetKind.Voidling, local.Id) };
        var incoming = CreateRemoteVoidlingBundle("remote", 22UL, "remote-parent", "");

        Assert.True(service.Prepare(state, tradeId, LobbyId, 999UL, "terms-hash", outgoing, incoming).Success);

        var committed = service.CommitPrepared(state, tradeId);

        Assert.True(committed.Success);
        Assert.False(committed.AlreadyApplied);
        Assert.DoesNotContain(state.Voidlings, creature => creature.Id == local.Id);
        var received = Assert.Single(state.Voidlings, creature => creature.Id == "remote");
        Assert.Equal(0, received.WorldX);
        Assert.Equal(0, received.WorldY);
        Assert.Empty(state.PendingTradeJournal);
        Assert.Contains(tradeId, state.AppliedTradeIds);
        Assert.Contains(state.LineageArchive, entry => entry.CreatureId == "remote");
        Assert.Contains(state.LineageArchive, entry => entry.CreatureId == "remote-parent");

        var repeated = service.CommitPrepared(state, tradeId);
        Assert.True(repeated.Success);
        Assert.True(repeated.AlreadyApplied);
        Assert.Single(state.Voidlings, creature => creature.Id == "remote");
    }

    [Fact]
    public void CommitPrepared_PreservesSemanticAppearanceAndArchivedPortraitRecipe()
    {
        var state = new GameStateData();
        var local = CreateAdult("local", 40UL, "", "");
        state.Voidlings.Add(local);
        var remote = CreateAdult("remote-appearance", 41UL, "", "");
        remote.Appearance = new VoidlingAppearanceData
        {
            VisualTypeId = "flying",
            PaletteHue = 0.625f,
            LayerIds = new List<string> { "wing.large", "crystal.blue" }
        };
        var incoming = new TradeTransferBundle(
            new[] { remote },
            Array.Empty<EggData>(),
            new[] { LineageArchiveEntry.FromVoidling(remote) });
        var service = new TradeTransferService(Rules);
        var tradeId = Guid.NewGuid().ToString("N");

        Assert.True(service.Prepare(
            state,
            tradeId,
            LobbyId,
            999UL,
            "terms-hash",
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, local.Id) },
            incoming).Success);
        Assert.True(service.CommitPrepared(state, tradeId).Success);

        var received = Assert.Single(state.Voidlings, creature => creature.Id == remote.Id);
        Assert.Equal("flying", received.Appearance.VisualTypeId);
        Assert.Equal(0.625f, received.Appearance.PaletteHue);
        Assert.Equal(new[] { "crystal.blue", "wing.large" }, received.Appearance.LayerIds);
        var archive = Assert.Single(state.LineageArchive, entry => entry.CreatureId == remote.Id);
        Assert.Equal("flying", archive.VisualTypeId);
        Assert.Equal(0.625f, archive.PaletteHue);
        Assert.Equal(new[] { "crystal.blue", "wing.large" }, archive.LayerIds);
    }

    [Fact]
    public void Prepare_RejectsIncomingLineageConflictBeforeAddingJournal()
    {
        var state = new GameStateData();
        var local = CreateAdult("local", 11UL, "", "");
        state.Voidlings.Add(local);
        state.LineageArchive.Add(new LineageArchiveEntry(
            "shared-parent",
            "Known Parent",
            "known-grandparent",
            "",
            1,
            "#AAAAAA",
            false));
        var incomingCreature = CreateAdult("remote", 22UL, "shared-parent", "");
        var incoming = new TradeTransferBundle(
            new[] { incomingCreature },
            Array.Empty<EggData>(),
            new[]
            {
                LineageArchiveEntry.FromVoidling(incomingCreature),
                new LineageArchiveEntry(
                    "shared-parent",
                    "Conflicting Parent",
                    "different-grandparent",
                    "",
                    1,
                    "#BBBBBB",
                    false)
            });
        var service = new TradeTransferService(Rules);

        var prepared = service.Prepare(
            state,
            Guid.NewGuid().ToString("N"),
            LobbyId,
            999UL,
            "terms-hash",
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, local.Id) },
            incoming);

        Assert.False(prepared.Success);
        Assert.Contains("conflict", prepared.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.PendingTradeJournal);
        Assert.Contains(state.Voidlings, creature => creature.Id == local.Id);
    }

    [Fact]
    public void EggBundle_PreservesSeedViabilityAndParentLineage()
    {
        var state = new GameStateData();
        state.LineageArchive.Add(new LineageArchiveEntry("parent-a", "A", "", "", 0, "#AAAAAA", false));
        state.LineageArchive.Add(new LineageArchiveEntry("parent-b", "B", "", "", 0, "#BBBBBB", false));
        var egg = new EggData
        {
            Id = "egg-1",
            Source = EggSource.Bred,
            Seed = 123456UL,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(123456UL),
            ParentAId = "parent-a",
            ParentBId = "parent-b",
            FamilyGeneration = 1,
            IsViable = false,
            FailureResolved = true,
            RequiredIncubationSeconds = 60,
            IncubationSeconds = 22,
            TintHex = "#ABCDEF"
        };
        state.OwnedEggs.Add(egg);
        var service = new TradeTransferService(Rules);

        var success = service.TryBuildTransferBundle(
            state,
            new[] { new TradeAssetReference(TradeAssetKind.Egg, egg.Id) },
            out var bundle,
            out var error);

        Assert.True(success, error);
        var transferred = Assert.Single(bundle.Eggs);
        Assert.Equal(egg.Seed, transferred.Seed);
        Assert.Equal(egg.IsViable, transferred.IsViable);
        Assert.Equal(egg.FailureResolved, transferred.FailureResolved);
        Assert.Contains(bundle.Lineage, entry => entry.CreatureId == "parent-a");
        Assert.Contains(bundle.Lineage, entry => entry.CreatureId == "parent-b");
    }

    private static TradeTransferBundle CreateRemoteVoidlingBundle(
        string id,
        ulong seed,
        string parentAId = "",
        string parentBId = "")
    {
        var creature = CreateAdult(id, seed, parentAId, parentBId);
        var lineage = new List<LineageArchiveEntry>
        {
            LineageArchiveEntry.FromVoidling(creature)
        };
        if (!string.IsNullOrWhiteSpace(parentAId))
            lineage.Add(new LineageArchiveEntry(parentAId, "Remote Parent A", "", "", 0, "#CCCCCC", false));
        if (!string.IsNullOrWhiteSpace(parentBId))
            lineage.Add(new LineageArchiveEntry(parentBId, "Remote Parent B", "", "", 0, "#DDDDDD", false));

        return new TradeTransferBundle(
            new[] { creature },
            Array.Empty<EggData>(),
            lineage.ToArray());
    }

    private static VoidlingData CreateAdult(
        string id,
        ulong seed,
        string parentAId,
        string parentBId)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            ParentAId = parentAId,
            ParentBId = parentBId,
            FamilyGeneration = string.IsNullOrWhiteSpace(parentAId) && string.IsNullOrWhiteSpace(parentBId) ? 0 : 1,
            TintHex = "#ABCDEF"
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }
}
