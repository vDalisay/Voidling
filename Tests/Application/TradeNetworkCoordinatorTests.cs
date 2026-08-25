using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Application.Ports;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class TradeNetworkCoordinatorTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void HostParticipant_OnlyCommitsAfterBothPreparedAndReady()
    {
        var host = new PlatformUser(new PlatformUserId(1), "Host");
        var remote = new PlatformUser(new PlatformUserId(2), "Remote");
        var lobby = CreateLobby(host, host, remote);
        var fakeLobby = new FakeLobby(lobby);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(new FakeIdentity(host), fakeLobby, transport);

        var state = new GameStateData();
        var localCreature = CreateAdult("local-creature", 100UL);
        state.Voidlings.Add(localCreature);
        var repository = new FakeRepository();
        var coordinator = new TradeNetworkCoordinator(
            connection,
            new TradeTransferService(Rules),
            repository,
            () => state);
        var statuses = new List<TradeStatusUpdate>();
        TradeTerms? locallyCommitted = null;
        coordinator.TradeStatusChanged += statuses.Add;
        coordinator.LocalTradeCommitted += terms => locallyCommitted = terms;

        var localReference = new TradeAssetReference(TradeAssetKind.Voidling, localCreature.Id);
        var offered = coordinator.OfferTrade(remote.Id, new[] { localReference });

        Assert.True(offered.Success, offered.Error);
        Assert.NotNull(offered.TradeId);
        transport.Sent.Clear();

        var remoteReference = new TradeAssetReference(TradeAssetKind.Voidling, "remote-creature");
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Trade,
            TradeProtocol.EncodeAcceptCommand(remote, offered.TradeId!, new[] { remoteReference })));

        var prepareMessage = Assert.Single(transport.Sent.Where(message =>
            message.Peer == remote.Id && IsPrepareRequest(message.Payload, host.Id)));
        Assert.True(TradeProtocol.TryDecodePrepareRequest(
            prepareMessage.Payload.Span,
            host.Id,
            out var terms,
            out var termsHash));
        Assert.Equal(offered.TradeId, terms.TradeId);
        Assert.Empty(state.PendingTradeJournal);
        Assert.Equal(0, repository.SaveCount);

        transport.Sent.Clear();
        var remoteBundle = CreateRemoteBundle("remote-creature", 200UL);
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Trade,
            TradeProtocol.EncodeBundlePrepared(remote, terms.TradeId, termsHash, remoteBundle)));

        Assert.Single(state.PendingTradeJournal);
        Assert.Equal(terms.TradeId, state.PendingTradeJournal[0].TradeId);
        Assert.Equal(lobby.LobbyId, state.PendingTradeJournal[0].LobbyId);
        Assert.Equal(1, repository.SaveCount);
        Assert.Contains(transport.Sent, message =>
            message.Peer == remote.Id && IsPersistRequest(message.Payload, host.Id));

        transport.Sent.Clear();
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Trade,
            TradeProtocol.EncodeReady(remote, terms.TradeId, termsHash, true, null)));

        Assert.Empty(state.PendingTradeJournal);
        Assert.DoesNotContain(state.Voidlings, creature => creature.Id == localCreature.Id);
        Assert.Contains(state.Voidlings, creature => creature.Id == "remote-creature");
        Assert.Contains(terms.TradeId, state.AppliedTradeIds);
        Assert.NotNull(locallyCommitted);
        Assert.Equal(terms.TradeId, locallyCommitted!.TradeId);
        Assert.Equal(terms.InitiatorAssets, locallyCommitted.InitiatorAssets);
        Assert.Equal(terms.CounterpartyAssets, locallyCommitted.CounterpartyAssets);
        Assert.Equal(2, repository.SaveCount);
        Assert.Contains(transport.Sent, message =>
            message.Peer == remote.Id && IsCommit(message.Payload, host.Id));

        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Trade,
            TradeProtocol.EncodeCommitted(remote, terms.TradeId, termsHash, true, null)));

        Assert.Contains(statuses, status =>
            status.TradeId == terms.TradeId && status.Status == TradeSessionStatus.Completed);
    }

    [Fact]
    public void PreparationFailure_AbortsDuringBundlePhaseInsteadOfHanging()
    {
        var host = new PlatformUser(new PlatformUserId(1), "Host");
        var remote = new PlatformUser(new PlatformUserId(2), "Remote");
        var lobby = CreateLobby(host, host, remote);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            new FakeLobby(lobby),
            transport);

        var state = new GameStateData();
        var localCreature = CreateAdult("local-creature", 300UL);
        state.Voidlings.Add(localCreature);
        var coordinator = new TradeNetworkCoordinator(
            connection,
            new TradeTransferService(Rules),
            new FakeRepository(),
            () => state);
        var statuses = new List<TradeStatusUpdate>();
        coordinator.TradeStatusChanged += statuses.Add;

        var offered = coordinator.OfferTrade(
            remote.Id,
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, localCreature.Id) });
        Assert.True(offered.Success, offered.Error);
        transport.Sent.Clear();

        var remoteReference = new TradeAssetReference(TradeAssetKind.Voidling, "remote-creature");
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Trade,
            TradeProtocol.EncodeAcceptCommand(remote, offered.TradeId!, new[] { remoteReference })));

        var prepareMessage = Assert.Single(transport.Sent.Where(message =>
            message.Peer == remote.Id && IsPrepareRequest(message.Payload, host.Id)));
        Assert.True(TradeProtocol.TryDecodePrepareRequest(
            prepareMessage.Payload.Span,
            host.Id,
            out var terms,
            out var termsHash));

        transport.Sent.Clear();
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Trade,
            TradeProtocol.EncodeReady(
                remote,
                terms.TradeId,
                termsHash,
                false,
                "Remote transfer bundle could not be prepared.")));

        Assert.Empty(state.PendingTradeJournal);
        Assert.Contains(state.Voidlings, creature => creature.Id == localCreature.Id);
        Assert.Contains(statuses, status =>
            status.TradeId == terms.TradeId && status.Status == TradeSessionStatus.Aborted);

        var abort = Assert.Single(transport.Sent.Where(message =>
            message.Peer == remote.Id && IsAbort(message.Payload, host.Id)));
        Assert.True(TradeProtocol.TryDecodeAbort(
            abort.Payload.Span,
            host.Id,
            out var abortedTradeId,
            out var reason));
        Assert.Equal(terms.TradeId, abortedTradeId);
        Assert.Contains("could not be prepared", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LeavingLobby_AbortsOnlyPreparedTradesForThatLobbyAndPersistsUnlock()
    {
        var host = new PlatformUser(new PlatformUserId(1), "Host");
        var remote = new PlatformUser(new PlatformUserId(2), "Remote");
        var lobby = CreateLobby(host, host, remote);
        var fakeLobby = new FakeLobby(lobby);
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            fakeLobby,
            new FakeTransport());

        var state = new GameStateData();
        var localCreature = CreateAdult("local-creature", 400UL);
        state.Voidlings.Add(localCreature);
        var transfers = new TradeTransferService(Rules);
        var tradeId = Guid.NewGuid().ToString("N");
        var outgoing = new[] { new TradeAssetReference(TradeAssetKind.Voidling, localCreature.Id) };
        Assert.True(transfers.Prepare(
            state,
            tradeId,
            lobby.LobbyId,
            remote.Id.Value,
            new string('a', 64),
            outgoing,
            CreateRemoteBundle("remote-creature", 401UL)).Success);

        var repository = new FakeRepository();
        _ = new TradeNetworkCoordinator(connection, transfers, repository, () => state);

        await connection.LeaveConnectedZoneAsync();

        Assert.Empty(state.PendingTradeJournal);
        Assert.Contains(state.Voidlings, creature => creature.Id == localCreature.Id);
        Assert.Equal(1, repository.SaveCount);
    }

    private static bool IsPrepareRequest(ReadOnlyMemory<byte> payload, PlatformUserId sender)
        => TradeProtocol.TryDecodePrepareRequest(payload.Span, sender, out _, out _);

    private static bool IsPersistRequest(ReadOnlyMemory<byte> payload, PlatformUserId sender)
        => TradeProtocol.TryDecodePersistRequest(payload.Span, sender, out _, out _, out _);

    private static bool IsCommit(ReadOnlyMemory<byte> payload, PlatformUserId sender)
        => TradeProtocol.TryDecodeCommit(payload.Span, sender, out _, out _);

    private static bool IsAbort(ReadOnlyMemory<byte> payload, PlatformUserId sender)
        => TradeProtocol.TryDecodeAbort(payload.Span, sender, out _, out _);

    private static LobbySnapshot CreateLobby(PlatformUser owner, params PlatformUser[] members)
        => new(
            77,
            owner.Id,
            members.Select(user => new LobbyMember(user, user.Id == owner.Id)).ToArray());

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
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            TintHex = "#ABCDEF"
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }

    private sealed class FakeIdentity : IPlatformIdentityService
    {
        public FakeIdentity(PlatformUser local) => LocalUser = local;
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public PlatformUser? LocalUser { get; }
    }

    private sealed class FakeLobby : ILobbyService
    {
        public FakeLobby(LobbySnapshot lobby) => CurrentLobby = lobby;
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public LobbySnapshot? CurrentLobby { get; private set; }
        public event Action<LobbySnapshot>? LobbyChanged;
        public event Action<LobbyJoinRequest>? JoinRequested;

        public Task<LobbyOperationResult> CreateFriendsLobbyAsync(
            int maxMembers,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Succeeded(CurrentLobby!));

        public Task<LobbyOperationResult> JoinAsync(
            ulong lobbyId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Succeeded(CurrentLobby!));

        public Task LeaveAsync(CancellationToken cancellationToken = default)
        {
            CurrentLobby = null;
            return Task.CompletedTask;
        }

        public void OpenInviteOverlay() { }
    }

    private sealed record SentMessage(
        PlatformUserId Peer,
        NetworkChannel Channel,
        ReadOnlyMemory<byte> Payload,
        DeliveryMode Delivery);

    private sealed class FakeTransport : IMultiplayerTransport
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public List<SentMessage> Sent { get; } = new();
        public event Action<NetworkPacket>? PacketReceived;
        public event Action<PlatformUserId>? PeerSessionFailed;

        public bool TrySend(
            PlatformUserId peer,
            NetworkChannel channel,
            ReadOnlyMemory<byte> payload,
            DeliveryMode delivery)
        {
            Sent.Add(new SentMessage(peer, channel, payload, delivery));
            return true;
        }

        public void Poll() { }
        public void Close(PlatformUserId peer) { }
        public void Emit(NetworkPacket packet) => PacketReceived?.Invoke(packet);
    }

    private sealed class FakeRepository : IGameStateRepository
    {
        public int SaveCount { get; private set; }
        public GameStateData? Load() => null;
        public void Save(GameStateData state) => SaveCount++;
    }
}
