using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer;

public sealed record ConnectedZoneMemberView(
    PlatformUserId UserId,
    string DisplayName,
    bool IsHost,
    bool IsLocal);

public sealed record ConnectedZoneViewState(
    MultiplayerAvailability Availability,
    PlatformUser? LocalUser,
    ulong? LobbyId,
    PlatformUserId? HostId,
    bool IsLocalHost,
    IReadOnlyList<ConnectedZoneMemberView> Members,
    IReadOnlyList<SharedVoidlingSnapshot> Voidlings)
{
    public bool IsConnected => LobbyId.HasValue;
}

/// <summary>
/// Focused Application façade intended for presentation composition. UI gets one capability-oriented
/// API and never sees GodotSteam, transport packets, or lobby callbacks directly.
/// </summary>
public sealed class ConnectedZoneFacade
{
    private readonly MultiplayerConnectionService _connection;
    private readonly ConnectedZoneService _zone;
    private readonly ConnectedZoneTransientService _transient;

    public ConnectedZoneFacade(
        MultiplayerConnectionService connection,
        ConnectedZoneService zone,
        ConnectedZoneTransientService transient)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _transient = transient ?? throw new ArgumentNullException(nameof(transient));

        _connection.LobbyChanged += _ => RaiseStateChanged();
        _connection.LobbyLeft += RaiseStateChanged;
        _zone.StateChanged += _ => RaiseStateChanged();
    }

    public event Action<ConnectedZoneViewState>? StateChanged;

    public ConnectedZoneViewState Current => BuildState();

    public Task<LobbyOperationResult> CreateAsync(CancellationToken cancellationToken = default)
        => _connection.CreateConnectedZoneAsync(cancellationToken);

    public Task<LobbyOperationResult> JoinAsync(
        ulong lobbyId,
        CancellationToken cancellationToken = default)
        => _connection.JoinConnectedZoneAsync(lobbyId, cancellationToken);

    public Task LeaveAsync(CancellationToken cancellationToken = default)
        => _connection.LeaveConnectedZoneAsync(cancellationToken);

    public void OpenInviteOverlay() => _connection.OpenInviteOverlay();

    public ConnectedZoneOperationResult PublishVoidling(
        GameStateData localState,
        string creatureId,
        float zoneX,
        float zoneY)
        => _zone.PublishOwnedVoidling(localState, creatureId, zoneX, zoneY);

    public ConnectedZoneOperationResult RemoveVoidling(string creatureId)
        => _zone.RemoveOwnedVoidling(creatureId);

    public ConnectedZoneOperationResult PublishTransform(
        string creatureId,
        float zoneX,
        float zoneY,
        float facingX,
        string animationState)
        => _transient.PublishOwnedTransform(
            creatureId,
            zoneX,
            zoneY,
            facingX,
            animationState);

    public bool TryGetTransientTransform(
        PlatformUserId ownerId,
        string creatureId,
        out SharedVoidlingTransform transform)
        => _transient.TryGetTransform(ownerId, creatureId, out transform);

    private ConnectedZoneViewState BuildState()
    {
        var lobby = _connection.CurrentLobby;
        var local = _connection.LocalUser;
        var zone = _zone.CurrentSnapshot;
        var availability = _connection.IsAvailable
            ? MultiplayerAvailability.Available
            : MultiplayerAvailability.Unavailable(
                _connection.UnavailableReason ?? "Multiplayer is unavailable.");

        if (lobby == null)
        {
            return new ConnectedZoneViewState(
                availability,
                local,
                null,
                null,
                false,
                Array.Empty<ConnectedZoneMemberView>(),
                Array.Empty<SharedVoidlingSnapshot>());
        }

        var members = new ConnectedZoneMemberView[lobby.Members.Count];
        for (var i = 0; i < lobby.Members.Count; i++)
        {
            var member = lobby.Members[i];
            members[i] = new ConnectedZoneMemberView(
                member.User.Id,
                member.User.DisplayName,
                member.User.Id == lobby.OwnerId,
                local != null && member.User.Id == local.Id);
        }

        return new ConnectedZoneViewState(
            availability,
            local,
            lobby.LobbyId,
            lobby.OwnerId,
            _connection.IsLocalHost,
            members,
            zone?.Voidlings ?? Array.Empty<SharedVoidlingSnapshot>());
    }

    private void RaiseStateChanged()
        => StateChanged?.Invoke(BuildState());
}
