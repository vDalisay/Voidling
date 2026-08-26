using System;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Multiplayer;

public sealed class OfflinePlatformIdentityService : IPlatformIdentityService
{
    public OfflinePlatformIdentityService(string reason)
        => Availability = MultiplayerAvailability.Unavailable(reason);

    public MultiplayerAvailability Availability { get; }
    public PlatformUser? LocalUser => null;
}

public sealed class OfflineLobbyService : ILobbyService
{
    public OfflineLobbyService(string reason)
        => Availability = MultiplayerAvailability.Unavailable(reason);

    public MultiplayerAvailability Availability { get; }
    public LobbySnapshot? CurrentLobby => null;

    public event Action<LobbySnapshot>? LobbyChanged
    {
        add { }
        remove { }
    }

    public event Action<LobbyJoinRequest>? JoinRequested
    {
        add { }
        remove { }
    }

    public Task<LobbyOperationResult> CreateFriendsLobbyAsync(
        int maxMembers,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LobbyOperationResult.Failed(Availability.Reason ?? "Multiplayer is unavailable."));

    public Task<LobbyOperationResult> JoinAsync(
        ulong lobbyId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LobbyOperationResult.Failed(Availability.Reason ?? "Multiplayer is unavailable."));

    public Task LeaveAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void OpenInviteOverlay()
    {
    }
}

public sealed class OfflineMultiplayerTransport : IMultiplayerTransport
{
    public OfflineMultiplayerTransport(string reason)
        => Availability = MultiplayerAvailability.Unavailable(reason);

    public MultiplayerAvailability Availability { get; }

    public event Action<NetworkPacket>? PacketReceived
    {
        add { }
        remove { }
    }

    public event Action<PlatformUserId>? PeerSessionFailed
    {
        add { }
        remove { }
    }

    public bool TrySend(
        PlatformUserId peer,
        NetworkChannel channel,
        ReadOnlyMemory<byte> payload,
        DeliveryMode delivery)
        => false;

    public void Poll()
    {
    }

    public void Close(PlatformUserId peer)
    {
    }
}
