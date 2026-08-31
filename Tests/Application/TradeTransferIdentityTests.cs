using System;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
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

    [Fact]
    public void BuildTransferBundle_StripsSenderLocalPassiveTrainingStateWithoutMutatingOwnedCreature()
    {
        var state = new GameStateData();
        var creature = CreateAdult("portable", 8UL);
        creature.PassiveTrainingStatId = "run";
        creature.PassiveTrainingModuleId = "sender-module";
        creature.PassiveTrainingPointsPerMinute = 9.0f;
        creature.PassiveTrainingPointRemainder = 0.75;
        state.Voidlings.Add(creature);
        var service = new TradeTransferService(Rules);

        var built = service.TryBuildTransferBundle(
            state,
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, creature.Id) },
            out var bundle,
            out var error);

        Assert.True(built, error);
        var transferred = Assert.Single(bundle.Voidlings);
        Assert.Equal(string.Empty, transferred.PassiveTrainingStatId);
        Assert.Equal(string.Empty, transferred.PassiveTrainingModuleId);
        Assert.Equal(0.0f, transferred.PassiveTrainingPointsPerMinute);
        Assert.Equal(0.0, transferred.PassiveTrainingPointRemainder);
        Assert.Equal("sender-module", creature.PassiveTrainingModuleId);
        Assert.Equal(9.0f, creature.PassiveTrainingPointsPerMinute);
    }

    [Fact]
    public void CommitPrepared_NormalizesIncomingCareTrainingAndLocalGardenState()
    {
        var state = new GameStateData();
        var creature = CreateAdult("incoming", 9UL);
        creature.Needs = null!;
        creature.TrainingPoints["run"] = int.MaxValue;
        creature.PassiveTrainingStatId = "run";
        creature.PassiveTrainingModuleId = "remote-module";
        creature.PassiveTrainingPointsPerMinute = 9.0f;
        creature.PassiveTrainingPointRemainder = 0.75;
        creature.DepartureReason = CreatureDepartureReason.Death;
        var incoming = new TradeTransferBundle(
            new[] { creature },
            Array.Empty<EggData>(),
            new[] { LineageArchiveEntry.FromVoidling(creature) });
        var service = new TradeTransferService(Rules);
        var tradeId = Guid.NewGuid().ToString("N");

        var prepared = service.Prepare(
            state,
            tradeId,
            77UL,
            999UL,
            "terms-hash",
            Array.Empty<TradeAssetReference>(),
            incoming);
        Assert.True(prepared.Success, prepared.Error);

        var committed = service.CommitPrepared(state, tradeId);

        Assert.True(committed.Success, committed.Error);
        var received = Assert.Single(state.Voidlings);
        Assert.NotNull(received.Needs);
        Assert.Equal(CreatureDepartureReason.None, received.DepartureReason);
        Assert.Equal(string.Empty, received.PassiveTrainingStatId);
        Assert.Equal(string.Empty, received.PassiveTrainingModuleId);
        Assert.Equal(0.0f, received.PassiveTrainingPointsPerMinute);
        Assert.Equal(0.0, received.PassiveTrainingPointRemainder);
        var cap = new StatCalculator(Rules.Stats).GetTrainingPointCap(received, "run");
        Assert.Equal(cap, received.TrainingPoints["run"]);
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
