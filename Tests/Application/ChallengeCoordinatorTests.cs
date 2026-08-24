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

public sealed class ChallengeCoordinatorTests
{
    [Fact]
    public void ClientConstruction_RequestsCanonicalChallengeSyncFromHost()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var lobby = Lobby(host, host, client);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(client),
            new FakeLobby(lobby),
            transport);

        _ = new ChallengeCoordinator(connection);

        var sent = Assert.Single(transport.Sent);
        Assert.Equal(host.Id, sent.Peer);
        Assert.Equal(NetworkChannel.Challenge, sent.Channel);
        Assert.Equal(DeliveryMode.Reliable, sent.Delivery);
        Assert.True(ChallengeProtocol.TryDecodeSyncRequest(
            sent.Payload.Span,
            client.Id,
            out _,
            out var lobbyId));
        Assert.Equal(lobby.LobbyId, lobbyId);
    }

    [Fact]
    public void HostOfferJoinAndStart_UsesCanonicalStateAndEnforcesFourPlayerCap()
    {
        var host = User(1, "Host");
        var second = User(2, "Second");
        var third = User(3, "Third");
        var fourth = User(4, "Fourth");
        var fifth = User(5, "Fifth");
        var lobby = Lobby(host, host, second, third, fourth, fifth);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            new FakeLobby(lobby),
            transport);
        var coordinator = new ChallengeCoordinator(connection);

        var offered = coordinator.OfferChallenge(ChallengeKind.Race, 4);

        Assert.True(offered.Success, offered.Error);
        var snapshot = Assert.Single(coordinator.Challenges);
        Assert.Equal(ChallengePhase.Offered, snapshot.Phase);
        Assert.Equal(new[] { host.Id }, snapshot.Participants);

        Join(transport, second, offered.ChallengeId!);
        Join(transport, third, offered.ChallengeId!);
        Join(transport, fourth, offered.ChallengeId!);
        Join(transport, fifth, offered.ChallengeId!);

        snapshot = Assert.Single(coordinator.Challenges);
        Assert.Equal(ChallengePhase.Forming, snapshot.Phase);
        Assert.Equal(4, snapshot.Participants.Length);
        Assert.Contains(host.Id, snapshot.Participants);
        Assert.Contains(second.Id, snapshot.Participants);
        Assert.Contains(third.Id, snapshot.Participants);
        Assert.Contains(fourth.Id, snapshot.Participants);
        Assert.DoesNotContain(fifth.Id, snapshot.Participants);

        var payload = new byte[] { 7, 8, 9 };
        var started = coordinator.StartChallenge(offered.ChallengeId!, payload);

        Assert.True(started.Success, started.Error);
        snapshot = Assert.Single(coordinator.Challenges);
        Assert.Equal(ChallengePhase.Running, snapshot.Phase);
        Assert.Equal(payload, snapshot.StartPayload);
        Assert.Contains(transport.Sent, sent =>
            sent.Channel == NetworkChannel.Challenge &&
            ChallengeProtocol.TryDecodeState(sent.Payload.Span, host.Id, out var state) &&
            state.Phase == ChallengePhase.Running);
    }

    [Fact]
    public void LateJoinSync_ReplacesClientChallengeState()
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
        transport.Sent.Clear();

        var expected = new ChallengeSnapshot(
            Guid.NewGuid().ToString("N"),
            lobby.LobbyId,
            ChallengeKind.Race,
            other.Id,
            4,
            ChallengePhase.Forming,
            new[] { other.Id, host.Id },
            Array.Empty<byte>());
        transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeSyncState(host, lobby.LobbyId, new[] { expected })));

        var actual = Assert.Single(coordinator.Challenges);
        Assert.Equal(expected.ChallengeId, actual.ChallengeId);
        Assert.Equal(expected.CreatorId, actual.CreatorId);
        Assert.Equal(expected.Participants, actual.Participants);
    }

    [Fact]
    public void SteamHostMigration_CancelsInFlightChallengeAndNewHostBroadcastsCancellation()
    {
        var oldHost = User(1, "Old Host");
        var newHost = User(2, "New Host");
        var third = User(3, "Third");
        var initialLobby = Lobby(oldHost, oldHost, newHost, third);
        var fakeLobby = new FakeLobby(initialLobby);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(newHost),
            fakeLobby,
            transport);
        var coordinator = new ChallengeCoordinator(connection);
        transport.Sent.Clear();

        var active = new ChallengeSnapshot(
            Guid.NewGuid().ToString("N"),
            initialLobby.LobbyId,
            ChallengeKind.Race,
            oldHost.Id,
            4,
            ChallengePhase.Forming,
            new[] { oldHost.Id, newHost.Id, third.Id },
            Array.Empty<byte>());
        transport.Emit(new NetworkPacket(
            oldHost.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeSyncState(oldHost, initialLobby.LobbyId, new[] { active })));
        transport.Sent.Clear();

        fakeLobby.SetLobby(Lobby(newHost, oldHost, newHost, third));

        var cancelled = Assert.Single(coordinator.Challenges);
        Assert.Equal(ChallengePhase.Cancelled, cancelled.Phase);
        Assert.True(connection.IsLocalHost);
        Assert.Contains(transport.Sent, sent =>
            sent.Channel == NetworkChannel.Challenge &&
            ChallengeProtocol.TryDecodeState(sent.Payload.Span, newHost.Id, out var state) &&
            state.ChallengeId == active.ChallengeId &&
            state.Phase == ChallengePhase.Cancelled);
    }

    [Fact]
    public void NonCreatorClient_CannotStartAnotherPlayersChallenge()
    {
        var host = User(1, "Host");
        var creator = User(2, "Creator");
        var client = User(3, "Client");
        var lobby = Lobby(host, host, creator, client);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(client),
            new FakeLobby(lobby),
            transport);
        var coordinator = new ChallengeCoordinator(connection);
        transport.Sent.Clear();

        var active = new ChallengeSnapshot(
            Guid.NewGuid().ToString("N"),
            lobby.LobbyId,
            ChallengeKind.Race,
            creator.Id,
            4,
            ChallengePhase.Forming,
            new[] { creator.Id, client.Id },
            Array.Empty<byte>());
        transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeState(host, active)));

        var result = coordinator.StartChallenge(active.ChallengeId, new byte[] { 1 });

        Assert.False(result.Success);
        Assert.Contains("creator", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(transport.Sent);
    }

    private static void Join(FakeTransport transport, PlatformUser user, string challengeId)
        => transport.Emit(new NetworkPacket(
            user.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeJoinCommand(user, challengeId)));

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

        public void SetLobby(LobbySnapshot lobby)
        {
            CurrentLobby = lobby;
            LobbyChanged?.Invoke(lobby);
        }
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
