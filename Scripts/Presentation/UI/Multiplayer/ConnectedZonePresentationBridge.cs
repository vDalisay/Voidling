using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Godot-owned presentation bridge over the Application connected-zone façade. The main scene can
/// resolve this stable node without becoming a service locator for raw Steam/network dependencies.
/// </summary>
public partial class ConnectedZonePresentationBridge : Node
{
    private ConnectedZoneFacade? _facade;

    public ConnectedZoneViewState Current
        => _facade?.Current ?? throw new InvalidOperationException("Connected-zone presentation bridge is not configured.");

    public event Action<ConnectedZoneViewState>? StateChanged;

    public void Configure(ConnectedZoneFacade facade)
    {
        if (_facade != null)
            throw new InvalidOperationException("Connected-zone presentation bridge is already configured.");

        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        _facade.StateChanged += HandleStateChanged;
    }

    public Task<LobbyOperationResult> CreateAsync(CancellationToken cancellationToken = default)
        => RequireFacade().CreateAsync(cancellationToken);

    public Task<LobbyOperationResult> JoinAsync(
        ulong lobbyId,
        CancellationToken cancellationToken = default)
        => RequireFacade().JoinAsync(lobbyId, cancellationToken);

    public Task LeaveAsync(CancellationToken cancellationToken = default)
        => RequireFacade().LeaveAsync(cancellationToken);

    public void OpenInviteOverlay()
        => RequireFacade().OpenInviteOverlay();

    public ConnectedZoneOperationResult PublishVoidling(
        GameStateData localState,
        string creatureId,
        float zoneX,
        float zoneY)
        => RequireFacade().PublishVoidling(localState, creatureId, zoneX, zoneY);

    public ConnectedZoneOperationResult RemoveVoidling(string creatureId)
        => RequireFacade().RemoveVoidling(creatureId);

    public ConnectedZoneOperationResult PublishTransform(
        string creatureId,
        float zoneX,
        float zoneY,
        float facingX,
        string animationState)
        => RequireFacade().PublishTransform(creatureId, zoneX, zoneY, facingX, animationState);

    public bool TryGetTransientTransform(
        PlatformUserId ownerId,
        string creatureId,
        out SharedVoidlingTransform transform)
        => RequireFacade().TryGetTransientTransform(ownerId, creatureId, out transform);

    public override void _ExitTree()
    {
        if (_facade != null)
            _facade.StateChanged -= HandleStateChanged;
    }

    private ConnectedZoneFacade RequireFacade()
        => _facade ?? throw new InvalidOperationException("Connected-zone presentation bridge is not configured.");

    private void HandleStateChanged(ConnectedZoneViewState state)
        => StateChanged?.Invoke(state);
}
