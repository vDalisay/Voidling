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
    private LobbySnapshot? _currentLobby;
    private bool _localWasHost;

    public MultiplayerConnectionService(
        IPlatformIdentityService identity,
        ILobbyService lobbies,
        IMultiplayerTransport transport)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _lobbies = lobbies ?? throw new ArgumentNullException(nameof(lobbies));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _currentLobby = _lobbies.CurrentLobby;
        _localWasHost = _currentLobby?.OwnerId == _identity.LocalUser?.Id;

        _lobbies.LobbyChanged += HandleLobbyChanged;
        _lobbies.JoinRequested += request => JoinRequested?.Invoke(request);
        _transport.PacketReceived += OnPacketReceived;
        _transport.PeerSessionFailed += OnPeerSessionFailed;
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
    public LobbySnapshot? CurrentLobby => _currentLobby;
    public bool IsLocalHost =>
        LocalUser != null &&
        CurrentLobby != null &&
        CurrentLobby.OwnerId == LocalUser.Id;

    public event Action<LobbySnapshot>? LobbyChanged;
    public event Action<LobbyJoinRequest>? JoinRequested;
    public event Action? LobbyLeft;
    public event Action<PlatformUser>? PeerHelloReceived;
    public event Action<NetworkPacket>? PacketReceived;
    public event Action<PlatformUserId>? PeerSessionFailed;

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

    public async Task LeaveConnectedZoneAsync(CancellationToken cancellationToken = default)
    {
        await _lobbies.LeaveAsync(cancellationToken);
        _currentLobby = null;
        _localWasHost = false;
        LobbyLeft?.Invoke();
    }

    public void OpenInviteOverlay()
    {
        if (IsAvailable && CurrentLobby != null)
            _lobbies.OpenInviteOverlay();
    }

    public bool IsLobbyMember(PlatformUserId userId)
    {
        var lobby = CurrentLobby;
        if (lobby == null || userId.Value == 0)
            return false;

        foreach (var member in lobby.Members)
        {
            if (member.User.Id == userId)
                return true;
        }

        return false;
    }

    public bool TrySend(
        PlatformUserId peer,
        NetworkChannel channel,
        ReadOnlyMemory<byte> payload,
        DeliveryMode delivery)
    {
        if (!IsAvailable || !IsLobbyMember(peer))
            return false;

        return _transport.TrySend(peer, channel, payload, delivery);
    }

    public int BroadcastToLobby(
        NetworkChannel channel,
        ReadOnlyMemory<byte> payload,
        DeliveryMode delivery)
    {
        var local = LocalUser;
        var lobby = CurrentLobby;
        if (!IsAvailable || local == null || lobby == null)
            return 0;

        var sent = 0;
        foreach (var member in lobby.Members)
        {
            if (member.User.Id == local.Id)
                continue;

            if (_transport.TrySend(member.User.Id, channel, payload, delivery))
                sent++;
        }

        return sent;
    }

    public void SendHelloToLobbyMembers()
    {
        var local = LocalUser;
        if (!IsAvailable || local == null)
            return;

        var payload = MultiplayerProtocol.EncodeHello(local);
        BroadcastToLobby(NetworkChannel.Session, payload, DeliveryMode.Reliable);
    }

    public void Poll() => _transport.Poll();

    private void OnPacketReceived(NetworkPacket packet)
    {
        PacketReceived?.Invoke(packet);

        if (packet.Channel != NetworkChannel.Session)
            return;

        if (MultiplayerProtocol.TryDecodeHello(packet.Payload.Span, packet.Sender, out var sender))
            PeerHelloReceived?.Invoke(sender);
    }

    private void HandleLobbyChanged(LobbySnapshot lobby)
    {
        _currentLobby = lobby;
        _localWasHost = lobby.OwnerId == _identity.LocalUser?.Id;
        LobbyChanged?.Invoke(lobby);
    }

    private void OnPeerSessionFailed(PlatformUserId peer)
    {
        var ownerDisconnected = _currentLobby?.OwnerId == peer && !_localWasHost;
        PeerSessionFailed?.Invoke(peer);
        if (!ownerDisconnected)
            return;

        _currentLobby = null;
        _localWasHost = false;
        LobbyLeft?.Invoke();
    }
}
