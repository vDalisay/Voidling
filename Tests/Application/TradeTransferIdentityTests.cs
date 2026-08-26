using System;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class TradeTransferIdentityTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Prepare_RejectsIncomingVoidlingWhoseIdentityMatchesExistingEgg()
    {
        var state = new GameStateData();
        state.OwnedEggs.Add(CreateEgg("shared-id", 1UL));
        var incoming = CreateVoidlingBundle("shared-id", 2UL);

        var result = Prepare(state, incoming);

        Assert.False(result.Success);
        Assert.Contains("identity", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.PendingTradeJournal);
    }

    [Fact]
    public void Prepare_RejectsIncomingEggWhoseIdentityMatchesExistingVoidling()
    {
        var state = new GameStateData();
        state.Voidlings.Add(CreateAdult("shared-id", 3UL));
        var incoming = new TradeTransferBundle(
            Array.Empty<VoidlingData>(),
            new[] { CreateEgg("shared-id", 4UL) },
            Array.Empty<LineageArchiveEntry>());

        var result = Prepare(state, incoming);

        Assert.False(result.Success);
        Assert.Contains("identity", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.PendingTradeJournal);
    }

    [Fact]
    public void Prepare_RejectsCrossKindDuplicateIdentityInsideIncomingBundle()
    {
        var state = new GameStateData();
        var creature = CreateAdult("shared-id", 5UL);
        var incoming = new TradeTransferBundle(
            new[] { creature },
            new[] { CreateEgg("shared-id", 6UL) },
            new[] { LineageArchiveEntry.FromVoidling(creature) });

        var result = Prepare(state, incoming);

        Assert.False(result.Success);
        Assert.Contains("duplicate", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.PendingTradeJournal);
    }

    [Fact]
    public void Prepare_RejectsIncomingEggWhoseIdentityMatchesHistoricalLineage()
    {
        var state = new GameStateData();
        state.LineageArchive.Add(new LineageArchiveEntry(
            "historical-id",
            "Historical",
            "",
            "",
            0,
            "#AAAAAA",
            false));
        var incoming = new TradeTransferBundle(
            Array.Empty<VoidlingData>(),
            new[] { CreateEgg("historical-id", 7UL) },
            Array.Empty<LineageArchiveEntry>());

        var result = Prepare(state, incoming);

        Assert.False(result.Success);
        Assert.Contains("identity", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.PendingTradeJournal);
    }

    private static TradeLocalOperationResult Prepare(GameStateData state, TradeTransferBundle incoming)
        => new TradeTransferService(Rules).Prepare(
            state,
            Guid.NewGuid().ToString("N"),
            77UL,
            999UL,
            "terms-hash",
            Array.Empty<TradeAssetReference>(),
            incoming);

    private static TradeTransferBundle CreateVoidlingBundle(string id, ulong seed)
    {
        var creature = CreateAdult(id, seed);
        return new TradeTransferBundle(
            new[] { creature },
            Array.Empty<EggData>(),
            new[] { LineageArchiveEntry.FromVoidling(creature) });
    }

    private static VoidlingData CreateAdult(string id, ulong seed)
        => new()
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            FamilyGeneration = 0,
            TintHex = "#ABCDEF"
        };

    private static EggData CreateEgg(string id, ulong seed)
        => new()
        {
            Id = id,
            Source = EggSource.Store,
            State = EggState.Incubating,
            Seed = seed,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            FamilyGeneration = 0,
            RequiredIncubationSeconds = 60.0f,
            IncubationSeconds = 0.0f,
            IsViable = true,
            FailureResolved = true,
            TintHex = "#ABCDEF"
        };
}
