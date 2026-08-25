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

public sealed class ConnectedZoneFacadeTests
{
    [Fact]
    public void OfflineFacadeReturnsUnavailableDisconnectedState()
    {
        const string reason = "Steam unavailable";
        var connection = new MultiplayerConnectionService(
            new OfflineIdentity(reason),
            new OfflineLobby(reason),
            new OfflineTransport(reason));
        var zone = new ConnectedZoneService(connection);
        var transient = new ConnectedZoneTransientService(connection, zone);
        var facade = new ConnectedZoneFacade(connection, zone, transient);

        var state = facade.Current;

        Assert.False(state.Availability.IsAvailable);
        Assert.False(state.IsConnected);
        Assert.Null(state.LobbyId);
        Assert.Empty(state.Members);
        Assert.Empty(state.Voidlings);
    }

    [Fact]
    public void FacadeProjectsLobbyMembersHostAndPublishedVoidlings()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var lobby = new MutableLobby(Lobby(host, host, remote));
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(new Identity(host), lobby, transport);
        var zone = new ConnectedZoneService(connection);
        var transient = new ConnectedZoneTransientService(connection, zone);
        var facade = new ConnectedZoneFacade(connection, zone, transient);
        var changes = 0;
        facade.StateChanged += _ => changes++;
        var state = new GameStateData();
        state.Voidlings.Add(new VoidlingData
        {
            Id = "local",
            Name = "Local",
            TintHex = "#ABCDEF",
            Stage = LifeStage.Adult
        });

        var published = facade.PublishVoidling(state, "local", 10, 20);
        var view = facade.Current;

        Assert.True(published.Success, published.Error);
        Assert.True(view.IsConnected);
        Assert.Equal((ulong)77, view.LobbyId);
        Assert.Equal(host.Id, view.HostId);
        Assert.True(view.IsLocalHost);
        Assert.Equal(2, view.Members.Count);
        Assert.Contains(view.Members, member => member.UserId == host.Id && member.IsHost && member.IsLocal);
        Assert.Contains(view.Members, member => member.UserId == remote.Id && !member.IsLocal);
        var shared = Assert.Single(view.Voidlings);
        Assert.Equal("local", shared.CreatureId);
        Assert.True(changes > 0);
    }

    private static PlatformUser User(ulong id, string name)
        => new(new PlatformUserId(id), name);

    private static LobbySnapshot Lobby(PlatformUser owner, params PlatformUser[] members)
        => new(
            77,
            owner.Id,
            members.Select(user => new LobbyMember(user, user.Id == owner.Id)).ToArray());

    private sealed class Identity : IPlatformIdentityService
    {
        public Identity(PlatformUser user) => LocalUser = user;
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public PlatformUser? LocalUser { get; }
    }

    private sealed class MutableLobby : ILobbyService
    {
        public MutableLobby(LobbySnapshot lobby) => CurrentLobby = lobby;
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
    }

    private sealed class FakeTransport : IMultiplayerTransport
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public event Action<NetworkPacket>? PacketReceived;
        public event Action<PlatformUserId>? PeerSessionFailed;
        public bool TrySend(PlatformUserId peer, NetworkChannel channel, ReadOnlyMemory<byte> payload, DeliveryMode delivery) => true;
        public void Poll() { }
        public void Close(PlatformUserId peer) { }
    }

    private sealed class OfflineIdentity : IPlatformIdentityService
    {
        public OfflineIdentity(string reason) => Availability = MultiplayerAvailability.Unavailable(reason);
        public MultiplayerAvailability Availability { get; }
        public PlatformUser? LocalUser => null;
    }

    private sealed class OfflineLobby : ILobbyService
    {
        public OfflineLobby(string reason) => Availability = MultiplayerAvailability.Unavailable(reason);
        public MultiplayerAvailability Availability { get; }
        public LobbySnapshot? CurrentLobby => null;
        public event Action<LobbySnapshot>? LobbyChanged { add { } remove { } }
        public event Action<LobbyJoinRequest>? JoinRequested { add { } remove { } }
        public Task<LobbyOperationResult> CreateFriendsLobbyAsync(int maxMembers, CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Failed(Availability.Reason ?? "unavailable"));
        public Task<LobbyOperationResult> JoinAsync(ulong lobbyId, CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Failed(Availability.Reason ?? "unavailable"));
        public Task LeaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void OpenInviteOverlay() { }
    }

    private sealed class OfflineTransport : IMultiplayerTransport
    {
        public OfflineTransport(string reason) => Availability = MultiplayerAvailability.Unavailable(reason);
        public MultiplayerAvailability Availability { get; }
        public event Action<NetworkPacket>? PacketReceived { add { } remove { } }
        public event Action<PlatformUserId>? PeerSessionFailed { add { } remove { } }
        public bool TrySend(PlatformUserId peer, NetworkChannel channel, ReadOnlyMemory<byte> payload, DeliveryMode delivery) => false;
        public void Poll() { }
        public void Close(PlatformUserId peer) { }
    }
}
