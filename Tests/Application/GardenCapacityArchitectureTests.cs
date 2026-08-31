using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Application.Simulation;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class GardenCapacityArchitectureTests
{
    private static readonly GameBalanceRules BaseRules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void ReadyEgg_WaitsForSpaceThenHatchesWhenSlotOpens()
    {
        var rules = BaseRules with { Garden = new GardenRules(MaxPopulation: 1) };
        var state = new GameStateData();
        state.Voidlings.Add(CreateAdult("resident", 1UL));
        var egg = new EggData
        {
            Id = "waiting-egg",
            Genome = new GenomeFactory(rules.Genetics).CreateRandom(2UL),
            IsViable = true,
            FailureResolved = true,
            RequiredIncubationSeconds = 0.1f,
            TintHex = "#ABCDEF"
        };
        state.OwnedEggs.Add(egg);
        var simulation = new AdvanceSimulationUseCase(rules);

        var blocked = simulation.Advance(state, 0.2f);
        var stillBlocked = simulation.Advance(state, 0.2f);

        Assert.Equal(EggState.WaitingForSpace, egg.State);
        Assert.Same(egg, Assert.Single(state.OwnedEggs));
        Assert.Single(state.Voidlings);
        Assert.Empty(state.EggShells);
        Assert.Single(blocked.Events.OfType<EggWaitingForGardenSpaceEvent>());
        Assert.Empty(stillBlocked.Events.OfType<EggWaitingForGardenSpaceEvent>());

        state.Voidlings.Clear();
        var resumed = simulation.Advance(state, 0.01f);

        Assert.Empty(state.OwnedEggs);
        var hatched = Assert.Single(state.Voidlings);
        Assert.Equal("waiting-egg", hatched.Id);
        Assert.Single(state.EggShells);
        Assert.Single(resumed.Events.OfType<CreatureHatchedEvent>());
    }

    [Fact]
    public void TradePrepare_RejectsPopulationOverflowButAllowsOneForOneSwap()
    {
        var rules = BaseRules with { Garden = new GardenRules(MaxPopulation: 1) };
        var state = new GameStateData();
        var local = CreateAdult("local", 10UL);
        state.Voidlings.Add(local);
        var service = new TradeTransferService(rules);
        var incoming = CreateRemoteBundle("remote", 20UL);

        var overflow = service.Prepare(
            state,
            Guid.NewGuid().ToString("N"),
            lobbyId: 77UL,
            counterpartyPlatformUserId: 999UL,
            termsHash: "overflow",
            outgoingAssets: Array.Empty<TradeAssetReference>(),
            incomingBundle: incoming);

        Assert.False(overflow.Success);
        Assert.Contains("Garden is full", overflow.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.PendingTradeJournal);

        var swap = service.Prepare(
            state,
            Guid.NewGuid().ToString("N"),
            lobbyId: 77UL,
            counterpartyPlatformUserId: 999UL,
            termsHash: "swap",
            outgoingAssets: new[] { new TradeAssetReference(TradeAssetKind.Voidling, local.Id) },
            incomingBundle: incoming);

        Assert.True(swap.Success, swap.Error);
        Assert.Single(state.PendingTradeJournal);
    }

    private static TradeTransferBundle CreateRemoteBundle(string id, ulong seed)
    {
        var creature = CreateAdult(id, seed);
        return new TradeTransferBundle(
            new[] { creature },
            Array.Empty<EggData>(),
            new[] { LineageArchiveEntry.FromVoidling(creature) });
    }

    private static VoidlingData CreateAdult(string id, ulong seed)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(BaseRules.Genetics).CreateRandom(seed),
            TintHex = "#ABCDEF"
        };
        foreach (var statId in BaseRules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }
}
