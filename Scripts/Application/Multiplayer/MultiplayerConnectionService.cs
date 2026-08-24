using System;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer;

/// <summary>
/// Godot-free orchestration for the optional multiplayer capability.
/// Single-player gameplay never depends on this service being available.
/// </summary>
public sealed class MultiplayerConnectionService
{
    private readonly IPlatformIdentityService _identity;
    private readonly ILobbyService _lobbies;
    private readonly IMultiplayerTransport _transport;

    public MultiplayerConnectionService(
        IPlatformIdentityService identity,
        ILobbyService lobbies,
        IMultiplayerTransport transport)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _lobbies = lobbies ?? throw new ArgumentNullException(nameof(lobbies));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));

        _lobbies.LobbyChanged += lobby => LobbyChanged?.Invoke(lobby);
        _lobbies.JoinRequested += request => JoinRequested?.Invoke(request);
        _transport.PacketReceived += OnPacketReceived;
    }

    public bool IsAvailable =>
        _identity.Availability.IsAvailable &&
        _lobbies.Availability.IsAvailable &&
        _transport.Availability.IsAvailable;

    public string? UnavailableReason =>
        _identity.Availability.Reason ??
        _lobbies.Availability.Reason ??
        _transport.Availability.Reason;

    public PlatformUser? LocalUser => _identity.LocalUser;
    public LobbySnapshot? CurrentLobby => _lobbies.CurrentLobby;

    public event Action<LobbySnapshot>? LobbyChanged;
    public event Action<LobbyJoinRequest>? JoinRequested;
    public event Action<PlatformUser>? PeerHelloReceived;

    public Task<LobbyOperationResult> CreateConnectedZoneAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.FromResult(LobbyOperationResult.Failed(UnavailableReason ?? "Multiplayer is unavailable."));

        return _lobbies.CreateFriendsLobbyAsync(16, cancellationToken);
    }

    public Task<LobbyOperationResult> JoinConnectedZoneAsync(
        ulong lobbyId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.FromResult(LobbyOperationResult.Failed(UnavailableReason ?? "Multiplayer is unavailable."));

        return _lobbies.JoinAsync(lobbyId, cancellationToken);
    }

    public Task LeaveConnectedZoneAsync(CancellationToken cancellationToken = default)
        => _lobbies.LeaveAsync(cancellationToken);

    public void OpenInviteOverlay()
    {
        if (IsAvailable && CurrentLobby != null)
            _lobbies.OpenInviteOverlay();
    }

    public void SendHelloToLobbyMembers()
    {
        var local = LocalUser;
        var lobby = CurrentLobby;
        if (!IsAvailable || local == null || lobby == null)
            return;

        var payload = MultiplayerProtocol.EncodeHello(local);
        foreach (var member in lobby.Members)
        {
            if (member.User.Id == local.Id)
                continue;

            _transport.TrySend(
                member.User.Id,
                NetworkChannel.Session,
                payload,
                DeliveryMode.Reliable);
        }
    }

    public void Poll() => _transport.Poll();

    private void OnPacketReceived(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Session)
            return;

        if (MultiplayerProtocol.TryDecodeHello(packet.Payload.Span, packet.Sender, out var sender))
            PeerHelloReceived?.Invoke(sender);
    }
}
