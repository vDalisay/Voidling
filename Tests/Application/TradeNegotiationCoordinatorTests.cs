using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Application.Ports;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class TradeNegotiationCoordinatorTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void BothPlayersMustAccept_BeforeDurableExchangeCommits()
    {
        var pair = CreatePair();
        var invited = pair.Host.Negotiation.Invite(pair.Remote.User.Id);
        Assert.True(invited.Success, invited.Error);
        var id = Assert.IsType<string>(invited.NegotiationId);

        Assert.Equal(TradeNegotiationPhase.Invited, pair.Host.Negotiation.Get(id)!.Phase);
        Assert.Equal(TradeNegotiationPhase.Invited, pair.Remote.Negotiation.Get(id)!.Phase);

        Assert.True(pair.Remote.Facade.AcceptInvite(id).Success);
        Assert.Equal(TradeNegotiationPhase.Negotiating, pair.Host.Negotiation.Get(id)!.Phase);
        Assert.Equal(TradeNegotiationPhase.Negotiating, pair.Remote.Negotiation.Get(id)!.Phase);

        Assert.True(pair.Host.Facade.SelectVoidling(id, pair.Host.Primary.Id).Success);
        Assert.True(pair.Remote.Facade.SelectVoidling(id, pair.Remote.Primary.Id).Success);

        var hostRoom = Assert.IsType<TradeNegotiationView>(pair.Host.Facade.Current.ActiveNegotiation);
        var remoteRoom = Assert.IsType<TradeNegotiationView>(pair.Remote.Facade.Current.ActiveNegotiation);
        Assert.Equal(pair.Remote.Primary.Name, hostRoom.RemoteOffer!.DisplayName);
        Assert.Equal(pair.Host.Primary.Name, remoteRoom.RemoteOffer!.DisplayName);
        Assert.Equal(
            AppearancePhenotypeResolver.ResolveSemantic(pair.Remote.Primary.Genome),
            hostRoom.RemoteOffer.Appearance);
        Assert.Equal(
            AppearancePhenotypeResolver.ResolveSemantic(pair.Host.Primary.Genome),
            remoteRoom.RemoteOffer.Appearance);

        Assert.True(pair.Host.Facade.SetAccepted(id, true).Success);
        Assert.True(pair.Host.State.Voidlings.Any(value => value.Id == pair.Host.Primary.Id));
        Assert.True(pair.Remote.State.Voidlings.Any(value => value.Id == pair.Remote.Primary.Id));
        Assert.Equal(0, pair.Host.Repository.SaveCount);
        Assert.Equal(0, pair.Remote.Repository.SaveCount);

        Assert.True(pair.Remote.Facade.SetAccepted(id, true).Success);

        Assert.Equal(TradeNegotiationPhase.Completed, pair.Host.Negotiation.Get(id)!.Phase);
        Assert.Equal(TradeNegotiationPhase.Completed, pair.Remote.Negotiation.Get(id)!.Phase);
        Assert.DoesNotContain(pair.Host.State.Voidlings, value => value.Id == pair.Host.Primary.Id);
        Assert.Contains(pair.Host.State.Voidlings, value => value.Id == pair.Remote.Primary.Id);
        Assert.DoesNotContain(pair.Remote.State.Voidlings, value => value.Id == pair.Remote.Primary.Id);
        Assert.Contains(pair.Remote.State.Voidlings, value => value.Id == pair.Host.Primary.Id);
        Assert.Equal(2, pair.Host.Repository.SaveCount);
        Assert.Equal(2, pair.Remote.Repository.SaveCount);
        Assert.Single(pair.Host.State.AppliedTradeIds);
        Assert.Single(pair.Remote.State.AppliedTradeIds);
    }

    [Fact]
    public void ChangingEitherOffer_RevokesBothAcceptStatesAndUpdatesPartnerPreview()
    {
        var pair = CreatePair(remoteVoidlingCount: 2);
        var id = StartNegotiating(pair);

        Assert.True(pair.Host.Facade.SelectVoidling(id, pair.Host.Primary.Id).Success);
        Assert.True(pair.Remote.Facade.SelectVoidling(id, pair.Remote.Primary.Id).Success);
        Assert.True(pair.Host.Facade.SetAccepted(id, true).Success);
        Assert.True(pair.Host.Negotiation.Get(id)!.InitiatorAccepted);

        var replacement = pair.Remote.State.Voidlings.Single(value => value.Id != pair.Remote.Primary.Id);
        Assert.True(pair.Remote.Facade.SelectVoidling(id, replacement.Id).Success);

        var state = pair.Host.Negotiation.Get(id)!;
        Assert.False(state.InitiatorAccepted);
        Assert.False(state.CounterpartyAccepted);
        Assert.Equal(replacement.Id, state.CounterpartyAsset!.AssetId);
        Assert.Equal(replacement.Name, pair.Host.Facade.Current.ActiveNegotiation!.RemoteOffer!.DisplayName);
        Assert.Contains(pair.Host.State.Voidlings, value => value.Id == pair.Host.Primary.Id);
        Assert.Contains(pair.Remote.State.Voidlings, value => value.Id == pair.Remote.Primary.Id);
        Assert.Contains(pair.Remote.State.Voidlings, value => value.Id == replacement.Id);
    }

    [Fact]
    public void EitherPlayerCanCancelBeforeFinalization_WithoutChangingOwnership()
    {
        var pair = CreatePair();
        var id = StartNegotiating(pair);

        Assert.True(pair.Host.Facade.SelectVoidling(id, pair.Host.Primary.Id).Success);
        Assert.True(pair.Remote.Facade.SelectVoidling(id, pair.Remote.Primary.Id).Success);
        Assert.True(pair.Host.Facade.SetAccepted(id, true).Success);

        Assert.True(pair.Remote.Facade.Cancel(id).Success);

        var hostState = pair.Host.Negotiation.Get(id)!;
        var remoteState = pair.Remote.Negotiation.Get(id)!;
        Assert.Equal(TradeNegotiationPhase.Cancelled, hostState.Phase);
        Assert.Equal(TradeNegotiationPhase.Cancelled, remoteState.Phase);
        Assert.False(hostState.InitiatorAccepted);
        Assert.False(hostState.CounterpartyAccepted);
        Assert.Contains(pair.Host.State.Voidlings, value => value.Id == pair.Host.Primary.Id);
        Assert.Contains(pair.Remote.State.Voidlings, value => value.Id == pair.Remote.Primary.Id);
        Assert.Empty(pair.Host.State.AppliedTradeIds);
        Assert.Empty(pair.Remote.State.AppliedTradeIds);
        Assert.Equal(0, pair.Host.Repository.SaveCount);
        Assert.Equal(0, pair.Remote.Repository.SaveCount);
    }

    private static string StartNegotiating(PeerPair pair)
    {
        var invited = pair.Host.Negotiation.Invite(pair.Remote.User.Id);
        Assert.True(invited.Success, invited.Error);
        var id = Assert.IsType<string>(invited.NegotiationId);
        Assert.True(pair.Remote.Facade.AcceptInvite(id).Success);
        return id;
    }

    private static PeerPair CreatePair(int remoteVoidlingCount = 1)
    {
        var hostUser = new PlatformUser(new PlatformUserId(101), "Host player");
        var remoteUser = new PlatformUser(new PlatformUserId(202), "Remote player");
        var lobby = new LobbySnapshot(
            77,
            hostUser.Id,
            new[]
            {
                new LobbyMember(hostUser, true),
                new LobbyMember(remoteUser, false)
            });

        var hostTransport = new LinkedTransport(hostUser.Id);
        var remoteTransport = new LinkedTransport(remoteUser.Id);
        hostTransport.Connect(remoteTransport);
        remoteTransport.Connect(hostTransport);

        var host = CreatePeer(hostUser, lobby, hostTransport, "host-voidling", 1100UL, 1);
        var remote = CreatePeer(remoteUser, lobby, remoteTransport, "remote-voidling", 2200UL, remoteVoidlingCount);
        return new PeerPair(host, remote);
    }

    private static PeerHarness CreatePeer(
        PlatformUser user,
        LobbySnapshot lobby,
        LinkedTransport transport,
        string primaryId,
        ulong seed,
        int voidlingCount)
    {
        var state = new GameStateData();
        VoidlingData? primary = null;
        for (var index = 0; index < voidlingCount; index++)
        {
            var creature = CreateAdult(
                index == 0 ? primaryId : $"{primaryId}-{index + 1}",
                seed + (ulong)index);
            creature.Name = index == 0 ? $"{user.DisplayName} Voidling" : $"{user.DisplayName} Backup {index + 1}";
            state.Voidlings.Add(creature);
            primary ??= creature;
        }

        var connection = new MultiplayerConnectionService(
            new FakeIdentity(user),
            new FakeLobby(lobby),
            transport);
        var repository = new FakeRepository();
        var durable = new TradeNetworkCoordinator(
            connection,
            new TradeTransferService(Rules),
            repository,
            () => state);
        var negotiation = new TradeNegotiationCoordinator(connection, durable);
        var previews = new TradeOfferPreviewCoordinator(connection, negotiation);
        var facade = new TradeNegotiationFacade(connection, negotiation, previews, () => state);
        return new PeerHarness(user, state, primary!, repository, negotiation, facade);
    }

    private static VoidlingData CreateAdult(string id, ulong seed)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            TintHex = seed % 2 == 0 ? "#A4C8E8" : "#E8B7C5"
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }

    private sealed record PeerPair(PeerHarness Host, PeerHarness Remote);

    private sealed record PeerHarness(
        PlatformUser User,
        GameStateData State,
        VoidlingData Primary,
        FakeRepository Repository,
        TradeNegotiationCoordinator Negotiation,
        TradeNegotiationFacade Facade);

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

    private sealed class LinkedTransport : IMultiplayerTransport
    {
        private LinkedTransport? _peer;

        public LinkedTransport(PlatformUserId localId) => LocalId = localId;
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public event Action<NetworkPacket>? PacketReceived;
        public event Action<PlatformUserId>? PeerSessionFailed;

        public void Connect(LinkedTransport peer) => _peer = peer;

        public bool TrySend(
            PlatformUserId peer,
            NetworkChannel channel,
            ReadOnlyMemory<byte> payload,
            DeliveryMode delivery)
        {
            if (_peer == null || _peer.LocalId != peer)
                return false;
            _peer.PacketReceived?.Invoke(new NetworkPacket(LocalId, channel, payload.ToArray()));
            return true;
        }

        public void Poll() { }
        public void Close(PlatformUserId peer) { }
    }

    private sealed class FakeRepository : IGameStateRepository
    {
        public int SaveCount { get; private set; }
        public GameStateData? Load() => null;
        public void Save(GameStateData state) => SaveCount++;
    }
}
