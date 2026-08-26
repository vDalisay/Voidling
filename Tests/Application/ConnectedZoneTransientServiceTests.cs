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

public sealed class ConnectedZoneTransientServiceTests
{
    [Fact]
    public void OwnedPublishedVoidlingBroadcastsUnreliableTransformDirectlyToPeers()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var transport = new FakeTransport();
        var connection = Connection(host, Lobby(host, host, remote), transport);
        var zone = new ConnectedZoneService(connection);
        var transient = new ConnectedZoneTransientService(connection, zone);
        var state = StateWith(Voidling("local", "Local"));
        Assert.True(zone.PublishOwnedVoidling(state, "local", 10, 20).Success);
        transport.Sent.Clear();

        var result = transient.PublishOwnedTransform("local", 12, 22, 1, "walk");

        Assert.True(result.Success, result.Error);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal(remote.Id, sent.Peer);
        Assert.Equal(NetworkChannel.GardenTransient, sent.Channel);
        Assert.Equal(DeliveryMode.Unreliable, sent.Delivery);
        Assert.True(ConnectedZoneTransientProtocol.TryDecodeTransform(
            sent.Payload.Span,
            host.Id,
            out _,
            out var transform));
        Assert.Equal(host.Id, transform.OwnerId);
        Assert.Equal("local", transform.CreatureId);
        Assert.Equal(1, transform.Sequence);
        Assert.Equal("walk", transform.AnimationState);
    }

    [Fact]
    public void LocalTransformPublicationIsCoalescedToConfiguredLowFrequency()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var transport = new FakeTransport();
        var connection = Connection(host, Lobby(host, host, remote), transport);
        var zone = new ConnectedZoneService(connection);
        var clock = new ManualTimeProvider();
        var transient = new ConnectedZoneTransientService(
            connection,
            zone,
            clock,
            TimeSpan.FromMilliseconds(100));
        var state = StateWith(Voidling("local", "Local"));
        Assert.True(zone.PublishOwnedVoidling(state, "local", 0, 0).Success);
        transport.Sent.Clear();

        Assert.True(transient.PublishOwnedTransform("local", 1, 1, 1, "walk").Success);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.True(transient.PublishOwnedTransform("local", 2, 2, 1, "walk").Success);
        Assert.Single(transport.Sent);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.True(transient.PublishOwnedTransform("local", 3, 3, 1, "walk").Success);

        Assert.Equal(2, transport.Sent.Count);
        Assert.True(ConnectedZoneTransientProtocol.TryDecodeTransform(
            transport.Sent[1].Payload.Span,
            host.Id,
            out _,
            out var latest));
        Assert.Equal(2, latest.Sequence);
        Assert.Equal(3, latest.ZoneX);
        Assert.Equal(3, latest.ZoneY);
    }

    [Fact]
    public void RemoteTransformMustMatchTransportOwnerAndPublishedVoidling()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var transport = new FakeTransport();
        var connection = Connection(host, Lobby(host, host, remote), transport);
        var zone = new ConnectedZoneService(connection);
        var transient = new ConnectedZoneTransientService(connection, zone);
        PublishRemoteIntoHostZone(transport, remote, "remote");
        var rejected = 0;
        transient.ProtocolRejected += _ => rejected++;

        var forged = new SharedVoidlingTransform(
            host.Id,
            "remote",
            1,
            4,
            5,
            -1,
            "idle");
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.GardenTransient,
            ConnectedZoneTransientProtocol.EncodeTransform(remote, forged)));

        Assert.Equal(1, rejected);
        Assert.Empty(transient.CurrentTransforms);
    }

    [Fact]
    public void OutOfOrderUnreliableTransformsAreIgnoredByPerCreatureSequence()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var transport = new FakeTransport();
        var connection = Connection(host, Lobby(host, host, remote), transport);
        var zone = new ConnectedZoneService(connection);
        var transient = new ConnectedZoneTransientService(connection, zone);
        PublishRemoteIntoHostZone(transport, remote, "remote");
        var changes = 0;
        transient.TransformChanged += _ => changes++;

        EmitTransform(transport, remote, "remote", 2, 20, 20);
        EmitTransform(transport, remote, "remote", 1, 10, 10);

        Assert.Equal(1, changes);
        Assert.True(transient.TryGetTransform(remote.Id, "remote", out var latest));
        Assert.Equal(2, latest.Sequence);
        Assert.Equal(20, latest.ZoneX);
        Assert.Equal(20, latest.ZoneY);
    }

    [Fact]
    public void RemovingReliableZoneEntityPrunesItsTransientTransform()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var transport = new FakeTransport();
        var connection = Connection(host, Lobby(host, host, remote), transport);
        var zone = new ConnectedZoneService(connection);
        var transient = new ConnectedZoneTransientService(connection, zone);
        var state = StateWith(Voidling("local", "Local"));
        Assert.True(zone.PublishOwnedVoidling(state, "local", 0, 0).Success);
        Assert.True(transient.PublishOwnedTransform("local", 3, 4, 1, "walk").Success);
        Assert.True(transient.TryGetTransform(host.Id, "local", out _));

        Assert.True(zone.RemoveOwnedVoidling("local").Success);

        Assert.False(transient.TryGetTransform(host.Id, "local", out _));
    }

    [Fact]
    public void TransformValidationRejectsNonFiniteAndOversizedPresentationData()
    {
        var invalidPosition = new SharedVoidlingTransform(
            new PlatformUserId(1),
            "x",
            1,
            float.NaN,
            0,
            1,
            "idle");
        var invalidAnimation = invalidPosition with
        {
            ZoneX = 0,
            AnimationState = new string('x', ConnectedZoneTransientValidation.MaxAnimationStateLength + 1)
        };

        Assert.False(ConnectedZoneTransientValidation.IsValid(invalidPosition));
        Assert.False(ConnectedZoneTransientValidation.IsValid(invalidAnimation));
    }

    private static void PublishRemoteIntoHostZone(
        FakeTransport transport,
        PlatformUser remote,
        string creatureId)
    {
        var snapshot = new SharedVoidlingSnapshot(
            creatureId,
            remote.Id,
            creatureId,
            "#ABCDEF",
            LifeStage.Adult,
            0,
            Array.Empty<string>(),
            0,
            0);
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Zone,
            MultiplayerProtocol.EncodePublishVoidlingCommand(remote, snapshot)));
    }

    private static void EmitTransform(
        FakeTransport transport,
        PlatformUser remote,
        string creatureId,
        long sequence,
        float x,
        float y)
    {
        var transform = new SharedVoidlingTransform(
            remote.Id,
            creatureId,
            sequence,
            x,
            y,
            1,
            "walk");
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.GardenTransient,
            ConnectedZoneTransientProtocol.EncodeTransform(remote, transform)));
    }

    private static GameStateData StateWith(VoidlingData creature)
    {
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        return state;
    }

    private static VoidlingData Voidling(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            TintHex = "#ABCDEF",
            Stage = LifeStage.Adult
        };

    private static MultiplayerConnectionService Connection(
        PlatformUser local,
        LobbySnapshot lobby,
        FakeTransport transport)
        => new(new FakeIdentity(local), new FakeLobby(lobby), transport);

    private static PlatformUser User(ulong id, string name)
        => new(new PlatformUserId(id), name);

    private static LobbySnapshot Lobby(PlatformUser owner, params PlatformUser[] members)
        => new(
            77,
            owner.Id,
            members.Select(user => new LobbyMember(user, user.Id == owner.Id)).ToArray());

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1_000;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan amount) => _timestamp += (long)amount.TotalMilliseconds;
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
}
