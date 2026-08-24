using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Infrastructure.Multiplayer;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class MultiplayerConnectionServiceTests
{
    [Fact]
    public async Task OfflineAdapters_DoNotThrowAndReportUnavailable()
    {
        const string reason = "Steam is offline.";
        var service = new MultiplayerConnectionService(
            new OfflinePlatformIdentityService(reason),
            new OfflineLobbyService(reason),
            new OfflineMultiplayerTransport(reason));

        Assert.False(service.IsAvailable);
        Assert.Equal(reason, service.UnavailableReason);

        var create = await service.CreateConnectedZoneAsync();
        var join = await service.JoinConnectedZoneAsync(1234);
        await service.LeaveConnectedZoneAsync();

        Assert.False(create.Success);
        Assert.False(join.Success);
    }

    [Fact]
    public void HelloProtocol_RejectsSenderSpoofing()
    {
        var claimed = new PlatformUser(new PlatformUserId(42), "Pip");
        var payload = MultiplayerProtocol.EncodeHello(claimed);

        var accepted = MultiplayerProtocol.TryDecodeHello(
            payload,
            new PlatformUserId(99),
            out _);

        Assert.False(accepted);
    }

    [Fact]
    public void ConnectionService_ReceivesValidHelloFromLobbyPeer()
    {
        var local = new PlatformUser(new PlatformUserId(1), "Local");
        var remote = new PlatformUser(new PlatformUserId(2), "Remote");
        var lobby = new LobbySnapshot(
            10,
            local.Id,
            new List<LobbyMember>
            {
                new(local, true),
                new(remote, false)
            });

        var identity = new FakeIdentity(local);
        var lobbies = new FakeLobby(lobby);
        var transport = new FakeTransport();
        var service = new MultiplayerConnectionService(identity, lobbies, transport);
        PlatformUser? received = null;
        service.PeerHelloReceived += user => received = user;

        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Session,
            MultiplayerProtocol.EncodeHello(remote)));

        Assert.Equal(remote, received);
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
        public LobbySnapshot? CurrentLobby { get; }
        public event Action<LobbySnapshot>? LobbyChanged;
        public event Action<LobbyJoinRequest>? JoinRequested;

        public Task<LobbyOperationResult> CreateFriendsLobbyAsync(int maxMembers, CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Succeeded(CurrentLobby!));

        public Task<LobbyOperationResult> JoinAsync(ulong lobbyId, CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Succeeded(CurrentLobby!));

        public Task LeaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void OpenInviteOverlay() { }
    }

    private sealed class FakeTransport : IMultiplayerTransport
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public event Action<NetworkPacket>? PacketReceived;
        public event Action<PlatformUserId>? PeerSessionFailed;

        public bool TrySend(PlatformUserId peer, NetworkChannel channel, ReadOnlyMemory<byte> payload, DeliveryMode delivery)
            => true;

        public void Poll() { }
        public void Close(PlatformUserId peer) { }
        public void Emit(NetworkPacket packet) => PacketReceived?.Invoke(packet);
    }
}
