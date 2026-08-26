using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Voidling.Application.Ports.Multiplayer;

public readonly record struct PlatformUserId(ulong Value)
{
    public override string ToString() => Value.ToString();
}

public sealed record PlatformUser(PlatformUserId Id, string DisplayName);

public sealed record MultiplayerAvailability(bool IsAvailable, string? Reason)
{
    public static MultiplayerAvailability Available { get; } = new(true, null);

    public static MultiplayerAvailability Unavailable(string reason)
        => new(false, reason);
}

public sealed record LobbyMember(PlatformUser User, bool IsOwner);

public sealed record LobbySnapshot(
    ulong LobbyId,
    PlatformUserId OwnerId,
    IReadOnlyList<LobbyMember> Members);

public sealed record LobbyJoinRequest(ulong LobbyId, PlatformUserId FriendId);

public sealed record LobbyOperationResult(bool Success, LobbySnapshot? Lobby, string? Error)
{
    public static LobbyOperationResult Succeeded(LobbySnapshot lobby)
        => new(true, lobby, null);

    public static LobbyOperationResult Failed(string error)
        => new(false, null, error);
}

/// <summary>
/// Steam Networking Messages channels. Ordering is only assumed within one reliable channel.
/// </summary>
public enum NetworkChannel
{
    Session = 0,
    Zone = 1,
    Challenge = 2,
    Trade = 3,
    GardenTransient = 4
}

public enum DeliveryMode
{
    Unreliable,
    Reliable
}

public sealed record NetworkPacket(
    PlatformUserId Sender,
    NetworkChannel Channel,
    ReadOnlyMemory<byte> Payload);

public interface IPlatformIdentityService
{
    MultiplayerAvailability Availability { get; }
    PlatformUser? LocalUser { get; }
}

public interface ILobbyService
{
    MultiplayerAvailability Availability { get; }
    LobbySnapshot? CurrentLobby { get; }

    event Action<LobbySnapshot>? LobbyChanged;
    event Action<LobbyJoinRequest>? JoinRequested;

    Task<LobbyOperationResult> CreateFriendsLobbyAsync(int maxMembers, CancellationToken cancellationToken = default);
    Task<LobbyOperationResult> JoinAsync(ulong lobbyId, CancellationToken cancellationToken = default);
    Task LeaveAsync(CancellationToken cancellationToken = default);
    void OpenInviteOverlay();
}

public interface IMultiplayerTransport
{
    MultiplayerAvailability Availability { get; }

    event Action<NetworkPacket>? PacketReceived;
    event Action<PlatformUserId>? PeerSessionFailed;

    bool TrySend(
        PlatformUserId peer,
        NetworkChannel channel,
        ReadOnlyMemory<byte> payload,
        DeliveryMode delivery);

    void Poll();
    void Close(PlatformUserId peer);
}
