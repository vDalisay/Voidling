using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Presentation.UI.Multiplayer;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Presentation-only bridge between live Garden actors and the connected-zone façade. Local shared
/// actors are sampled for lossy transform publication; remote shared actors are separate visual nodes
/// that never enter the local Garden actor dictionary, selection model, roster, or save.
/// </summary>
public partial class ConnectedZoneGardenSync : Node2D
{
    private readonly Dictionary<SharedVoidlingKey, RemoteVoidlingActor> _remoteActors = new();
    private ConnectedZonePresentationBridge? _bridge;
    private GardenController? _garden;

    public void Configure(
        ConnectedZonePresentationBridge bridge,
        GardenController garden)
    {
        if (_bridge != null || _garden != null)
            throw new InvalidOperationException("ConnectedZoneGardenSync is already configured.");

        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _garden = garden ?? throw new ArgumentNullException(nameof(garden));
    }

    public override void _Ready()
    {
        if (_bridge == null || _garden == null)
            throw new InvalidOperationException("ConnectedZoneGardenSync must be configured before AddChild.");

        _bridge.StateChanged += HandleStateChanged;
        ApplyState(_bridge.Current);
    }

    public override void _ExitTree()
    {
        if (_bridge != null && GodotObject.IsInstanceValid(_bridge))
            _bridge.StateChanged -= HandleStateChanged;
    }

    public override void _Process(double delta)
    {
        var bridge = _bridge;
        var garden = _garden;
        if (bridge == null || garden == null)
            return;

        var state = bridge.Current;
        var local = state.LocalUser;
        if (!state.IsConnected || local == null)
            return;

        foreach (var shared in state.Voidlings)
        {
            if (shared.OwnerId != local.Id)
                continue;

            if (garden.TryGetActorConnectedGardenPresentation(
                    shared.CreatureId,
                    out var position,
                    out var facingX,
                    out var animationState))
            {
                // The Application service owns the 10 Hz rate limit. Calling every frame lets the
                // freshest sample win without adding timing state to presentation.
                bridge.PublishTransform(
                    shared.CreatureId,
                    position.X,
                    position.Y,
                    facingX,
                    animationState);
            }
        }

        foreach (var pair in _remoteActors)
        {
            if (bridge.TryGetTransientTransform(
                    pair.Key.OwnerId,
                    pair.Key.CreatureId,
                    out var transform))
            {
                pair.Value.ApplyTransform(transform);
            }
        }
    }

    private void HandleStateChanged(ConnectedZoneViewState state)
        => ApplyState(state);

    private void ApplyState(ConnectedZoneViewState state)
    {
        var local = state.LocalUser;
        var desired = local == null
            ? Array.Empty<SharedVoidlingSnapshot>()
            : state.Voidlings
                .Where(shared => shared.OwnerId != local.Id)
                .ToArray();
        var desiredKeys = desired.Select(shared => shared.Key).ToHashSet();

        foreach (var key in _remoteActors.Keys.Where(key => !desiredKeys.Contains(key)).ToArray())
        {
            var actor = _remoteActors[key];
            _remoteActors.Remove(key);
            if (GodotObject.IsInstanceValid(actor))
                actor.QueueFree();
        }

        foreach (var shared in desired)
        {
            var ownerName = state.Members
                .FirstOrDefault(member => member.UserId == shared.OwnerId)
                ?.DisplayName ?? "Friend";

            if (_remoteActors.TryGetValue(shared.Key, out var existing) &&
                GodotObject.IsInstanceValid(existing))
            {
                existing.ApplySnapshot(shared, ownerName);
                continue;
            }

            var actor = new RemoteVoidlingActor();
            actor.Setup(shared, ownerName);
            _remoteActors[shared.Key] = actor;
            AddChild(actor);
        }
    }
}
