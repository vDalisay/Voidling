using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class ChallengeFacadeTests
{
    [Fact]
    public void HostOfferProducesNamedPresentationStateAndBlocksSecondOffer()
    {
        var host = User(1, "Host");
        var friend = User(2, "Friend");
        var lobby = Lobby(host, host, friend);
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            new FakeLobby(lobby),
            new FakeTransport());
        var coordinator = new ChallengeCoordinator(connection);
        var facade = new ChallengeFacade(connection, coordinator);

        Assert.True(facade.Current.CanOffer);
        var offered = facade.OfferRace(3);
        var state = facade.Current;

        Assert.True(offered.Success, offered.Error);
        Assert.False(state.CanOffer);
        var challenge = Assert.Single(state.Challenges);
        Assert.Equal(ChallengeKind.Race, challenge.Kind);
        Assert.Equal(ChallengePhase.Offered, challenge.Phase);
        Assert.Equal("Host", challenge.CreatorDisplayName);
        Assert.Equal(3, challenge.MaxParticipants);
        Assert.True(challenge.LocalParticipating);
        Assert.False(challenge.CanJoin);
        Assert.True(challenge.CanLeave);
        Assert.True(challenge.CanCancel);
        var participant = Assert.Single(challenge.Participants);
        Assert.Equal("Host", participant.DisplayName);
        Assert.True(participant.IsLocal);
        Assert.True(participant.IsCreator);
        Assert.True(participant.IsHost);
    }

    [Fact]
    public void ClientSyncMapsFriendNamesAndExposesJoinPermission()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var other = User(3, "Other");
        var lobby = Lobby(host, host, client, other);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(client),
            new FakeLobby(lobby),
            transport);
        var coordinator = new ChallengeCoordinator(connection);
        transport.Sent.Clear(); // discard construction sync request
        var facade = new ChallengeFacade(connection, coordinator);
        var challengeId = Guid.NewGuid().ToString("N");
        var snapshot = new ChallengeSnapshot(
            challengeId,
            lobby.LobbyId,
            ChallengeKind.Race,
            host.Id,
            4,
            ChallengePhase.Forming,
            new[] { host.Id, other.Id },
            Array.Empty<byte>());

        transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeSyncState(host, lobby.LobbyId, new[] { snapshot })));

        var view = Assert.Single(facade.Current.Challenges);
        Assert.Equal("Host", view.CreatorDisplayName);
        Assert.Equal(new[] { "Host", "Other" }, view.Participants.Select(value => value.DisplayName));
        Assert.False(view.LocalParticipating);
        Assert.True(view.CanJoin);
        Assert.False(view.CanLeave);
        Assert.False(view.CanCancel);
        Assert.True(facade.Current.CanOffer);

        var joined = facade.Join(challengeId);
        Assert.True(joined.Success, joined.Error);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal(host.Id, sent.Peer);
        Assert.True(ChallengeProtocol.TryDecodeJoinCommand(
            sent.Payload.Span,
            client.Id,
            out _,
            out var sentChallengeId));
        Assert.Equal(challengeId, sentChallengeId);
    }

    private static PlatformUser User(ulong id, string name)
        => new(new PlatformUserId(id), name);

    private static LobbySnapshot Lobby(PlatformUser owner, params PlatformUser[] members)
        => new(
            77,
            owner.Id,
            members.Select(user => new LobbyMember(user, user.Id == owner.Id)).ToArray());

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
}
