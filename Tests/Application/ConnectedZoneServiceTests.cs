using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class ConnectedZoneServiceTests
{
    [Fact]
    public void HostPublish_AppliesCanonicalStateAndBroadcastsReliableEvent()
    {
        var host = new PlatformUser(new PlatformUserId(1), "Host");
        var remote = new PlatformUser(new PlatformUserId(2), "Remote");
        var lobby = CreateLobby(host, host, remote);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(new FakeIdentity(host), new FakeLobby(lobby), transport);
        var zone = new ConnectedZoneService(connection);
        transport.Sent.Clear();

        var state = new GameStateData();
        state.Voidlings.Add(new VoidlingData
        {
            Id = "owned-1",
            Name = "Pip",
            TintHex = "#AABBCC",
            Stage = LifeStage.Adult,
            FamilyGeneration = 2
        });

        var result = zone.PublishOwnedVoidling(state, "owned-1", 4, 8);

        Assert.True(result.Success);
        var snapshot = Assert.IsType<ConnectedZoneSnapshot>(zone.CurrentSnapshot);
        var shared = Assert.Single(snapshot.Voidlings);
        Assert.Equal(host.Id, shared.OwnerId);
        Assert.Equal("Pip", shared.DisplayName);
        Assert.Equal(1, snapshot.Revision);

        var sent = Assert.Single(transport.Sent);
        Assert.Equal(remote.Id, sent.Peer);
        Assert.Equal(NetworkChannel.Zone, sent.Channel);
        Assert.Equal(DeliveryMode.Reliable, sent.Delivery);
        Assert.True(MultiplayerProtocol.TryDecodeVoidlingPublishedEvent(sent.Payload.Span, host.Id, out _, out var epoch, out var revision, out var canonical));
        Assert.Equal(1, epoch);
        Assert.Equal(1, revision);
        AssertSharedVoidlingEqual(shared, canonical);
    }

    [Fact]
    public void ClientConstruction_RequestsFullSnapshotFromCurrentHost()
    {
        var host = new PlatformUser(new PlatformUserId(1), "Host");
        var client = new PlatformUser(new PlatformUserId(2), "Client");
        var lobby = CreateLobby(host, host, client);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(new FakeIdentity(client), new FakeLobby(lobby), transport);

        _ = new ConnectedZoneService(connection);

        var sent = Assert.Single(transport.Sent);
        Assert.Equal(host.Id, sent.Peer);
        Assert.Equal(NetworkChannel.Zone, sent.Channel);
        Assert.True(MultiplayerProtocol.TryDecodeZoneSnapshotRequest(sent.Payload.Span, client.Id, out _, out var lobbyId));
        Assert.Equal(lobby.LobbyId, lobbyId);
    }

    [Fact]
    public void HostRejectsPublishThatClaimsAnotherOwner()
    {
        var host = new PlatformUser(new PlatformUserId(1), "Host");
        var remote = new PlatformUser(new PlatformUserId(2), "Remote");
        var third = new PlatformUser(new PlatformUserId(3), "Third");
        var lobby = CreateLobby(host, host, remote, third);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(new FakeIdentity(host), new FakeLobby(lobby), transport);
        var zone = new ConnectedZoneService(connection);
        transport.Sent.Clear();

        string? rejection = null;
        zone.ProtocolRejected += reason => rejection = reason;

        var forged = CreateSharedVoidling(third.Id, "forged");
        transport.Emit(new NetworkPacket(remote.Id, NetworkChannel.Zone, MultiplayerProtocol.EncodePublishVoidlingCommand(remote, forged)));

        Assert.NotNull(rejection);
        Assert.Empty(zone.CurrentSnapshot!.Voidlings);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public void ClientAppliesFullSnapshotOnlyFromLobbyHost()
    {
        var host = new PlatformUser(new PlatformUserId(1), "Host");
        var client = new PlatformUser(new PlatformUserId(2), "Client");
        var lobby = CreateLobby(host, host, client);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(new FakeIdentity(client), new FakeLobby(lobby), transport);
        var zone = new ConnectedZoneService(connection);
        transport.Sent.Clear();

        var expected = CreateSharedVoidling(host.Id, "remote-1");
        var full = new ConnectedZoneSnapshot(lobby.LobbyId, host.Id, 1, 1, new[] { expected });
        transport.Emit(new NetworkPacket(host.Id, NetworkChannel.Zone, MultiplayerProtocol.EncodeZoneSnapshot(host, full)));

        var actual = Assert.Single(zone.CurrentSnapshot!.Voidlings);
        AssertSharedVoidlingEqual(expected, actual);
        Assert.Equal(1, zone.CurrentSnapshot!.Revision);
    }

    [Fact]
    public void SteamOwnerMigration_PreservesReplicatedStateAndIncrementsEpoch()
    {
        var oldHost = new PlatformUser(new PlatformUserId(1), "Old Host");
        var newHost = new PlatformUser(new PlatformUserId(2), "New Host");
        var initialLobby = CreateLobby(oldHost, oldHost, newHost);
        var fakeLobby = new FakeLobby(initialLobby);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(new FakeIdentity(newHost), fakeLobby, transport);
        var zone = new ConnectedZoneService(connection);
        transport.Sent.Clear();

        var existing = CreateSharedVoidling(oldHost.Id, "remote-1");
        var full = new ConnectedZoneSnapshot(initialLobby.LobbyId, oldHost.Id, 1, 1, new[] { existing });
        transport.Emit(new NetworkPacket(oldHost.Id, NetworkChannel.Zone, MultiplayerProtocol.EncodeZoneSnapshot(oldHost, full)));
        transport.Sent.Clear();

        var migratedLobby = CreateLobby(newHost, oldHost, newHost);
        fakeLobby.SetLobby(migratedLobby);

        Assert.True(zone.IsLocalHost);
        Assert.Equal(newHost.Id, zone.CurrentSnapshot!.HostId);
        Assert.Equal(2, zone.CurrentSnapshot!.AuthorityEpoch);
        Assert.Single(zone.CurrentSnapshot!.Voidlings);
        Assert.Contains(transport.Sent, message => message.Peer == oldHost.Id && message.Channel == NetworkChannel.Zone);
    }

    private static void AssertSharedVoidlingEqual(SharedVoidlingSnapshot expected, SharedVoidlingSnapshot actual)
    {
        Assert.Equal(expected.CreatureId, actual.CreatureId);
        Assert.Equal(expected.OwnerId, actual.OwnerId);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.TintHex, actual.TintHex);
        Assert.Equal(expected.Stage, actual.Stage);
        Assert.Equal(expected.FamilyGeneration, actual.FamilyGeneration);
        Assert.Equal(expected.ZoneX, actual.ZoneX);
        Assert.Equal(expected.ZoneY, actual.ZoneY);
        Assert.Equal(expected.RareTraitIds, actual.RareTraitIds);
    }

    private static LobbySnapshot CreateLobby(PlatformUser owner, params PlatformUser[] members)
        => new(77, owner.Id, members.Select(user => new LobbyMember(user, user.Id == owner.Id)).ToArray());

    private static SharedVoidlingSnapshot CreateSharedVoidling(PlatformUserId ownerId, string creatureId)
        => new(creatureId, ownerId, "Shared", "#AABBCC", LifeStage.Adult, 0, Array.Empty<string>(), 1, 2);

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

        public Task<LobbyOperationResult> CreateFriendsLobbyAsync(int maxMembers, CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Succeeded(CurrentLobby!));

        public Task<LobbyOperationResult> JoinAsync(ulong lobbyId, CancellationToken cancellationToken = default)
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

    private sealed record SentMessage(PlatformUserId Peer, NetworkChannel Channel, ReadOnlyMemory<byte> Payload, DeliveryMode Delivery);

    private sealed class FakeTransport : IMultiplayerTransport
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public List<SentMessage> Sent { get; } = new();
        public event Action<NetworkPacket>? PacketReceived;
        public event Action<PlatformUserId>? PeerSessionFailed;

        public bool TrySend(PlatformUserId peer, NetworkChannel channel, ReadOnlyMemory<byte> payload, DeliveryMode delivery)
        {
            Sent.Add(new SentMessage(peer, channel, payload, delivery));
            return true;
        }

        public void Poll() { }
        public void Close(PlatformUserId peer) { }
        public void Emit(NetworkPacket packet) => PacketReceived?.Invoke(packet);
    }
}
