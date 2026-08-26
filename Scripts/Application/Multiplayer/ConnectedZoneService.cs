using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer;

/// <summary>
/// Host-coordinated replication for the optional connected Garden. The service owns only transient
/// network state. Local save ownership remains in GameStateData and remote snapshots never enter it.
/// </summary>
public sealed class ConnectedZoneService
{
    private const int RecentCommandLimit = 256;

    private readonly MultiplayerConnectionService _connection;
    private readonly SharedVoidlingSnapshotFactory _snapshotFactory;
    private readonly ConnectedZoneState _state = new();
    private readonly Queue<Guid> _recentCommandOrder = new();
    private readonly HashSet<Guid> _recentCommandIds = new();

    public ConnectedZoneService(
        MultiplayerConnectionService connection,
        SharedVoidlingSnapshotFactory? snapshotFactory = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _snapshotFactory = snapshotFactory ?? new SharedVoidlingSnapshotFactory();

        _connection.LobbyChanged += HandleLobbyChanged;
        _connection.LobbyLeft += HandleLobbyLeft;
        _connection.PacketReceived += HandlePacket;

        if (_connection.CurrentLobby != null)
            HandleLobbyChanged(_connection.CurrentLobby);
    }

    public ConnectedZoneSnapshot? CurrentSnapshot =>
        _state.IsInitialized ? _state.ToSnapshot() : null;

    public bool IsLocalHost => _connection.IsLocalHost;

    public event Action<ConnectedZoneSnapshot?>? StateChanged;
    public event Action<string>? ProtocolRejected;

    public ConnectedZoneOperationResult PublishOwnedVoidling(
        GameStateData localState,
        string creatureId,
        float zoneX,
        float zoneY)
    {
        ArgumentNullException.ThrowIfNull(localState);

        if (!TryGetSessionContext(out var local, out var lobby, out var error))
            return ConnectedZoneOperationResult.Failed(error!);

        if (!_snapshotFactory.TryCreateOwned(
                localState,
                local!,
                creatureId,
                zoneX,
                zoneY,
                out var snapshot,
                out error))
        {
            return ConnectedZoneOperationResult.Failed(error ?? "Could not publish the selected Voidling.");
        }

        if (_connection.IsLocalHost)
        {
            ApplyHostPublish(local!.Id, snapshot);
            return ConnectedZoneOperationResult.Succeeded;
        }

        var payload = MultiplayerProtocol.EncodePublishVoidlingCommand(local!, snapshot);
        return _connection.TrySend(
            lobby!.OwnerId,
            NetworkChannel.Zone,
            payload,
            DeliveryMode.Reliable)
            ? ConnectedZoneOperationResult.Succeeded
            : ConnectedZoneOperationResult.Failed("Could not send the Voidling to the connected Garden host.");
    }

    public ConnectedZoneOperationResult RemoveOwnedVoidling(string creatureId)
    {
        if (!TryGetSessionContext(out var local, out var lobby, out var error))
            return ConnectedZoneOperationResult.Failed(error!);
        if (string.IsNullOrWhiteSpace(creatureId) ||
            creatureId.Length > ConnectedZoneValidation.MaxCreatureIdLength)
        {
            return ConnectedZoneOperationResult.Failed("Creature ID is invalid.");
        }

        if (_connection.IsLocalHost)
        {
            ApplyHostRemove(local!.Id, creatureId);
            return ConnectedZoneOperationResult.Succeeded;
        }

        var payload = MultiplayerProtocol.EncodeRemoveVoidlingCommand(local!, local!.Id, creatureId);
        return _connection.TrySend(
            lobby!.OwnerId,
            NetworkChannel.Zone,
            payload,
            DeliveryMode.Reliable)
            ? ConnectedZoneOperationResult.Succeeded
            : ConnectedZoneOperationResult.Failed("Could not remove the Voidling from the connected Garden.");
    }

    public void RequestFullSnapshot()
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null || _connection.IsLocalHost)
            return;

        var payload = MultiplayerProtocol.EncodeZoneSnapshotRequest(local, lobby.LobbyId);
        _connection.TrySend(
            lobby.OwnerId,
            NetworkChannel.Zone,
            payload,
            DeliveryMode.Reliable);
    }

    private void HandleLobbyChanged(LobbySnapshot lobby)
    {
        if (!_state.IsInitialized || _state.LobbyId != lobby.LobbyId)
        {
            _state.Reset(lobby);
            ClearCommandHistory();
            RaiseStateChanged();

            if (_connection.IsLocalHost)
                BroadcastFullSnapshot();
            else
                RequestFullSnapshot();
            return;
        }

        var hostChanged = _state.HostId != lobby.OwnerId;
        if (hostChanged)
            _state.Rehost(lobby.OwnerId);

        if (_connection.IsLocalHost)
        {
            var allowedOwners = lobby.Members
                .Select(member => member.User.Id)
                .ToHashSet();
            var removedDepartedOwners = _state.RetainOwners(allowedOwners);

            if (hostChanged || removedDepartedOwners)
                RaiseStateChanged();

            // A membership change means a late joiner may need state. Broadcasting one compact
            // snapshot is simpler and safer than trying to infer which peer missed which deltas.
            BroadcastFullSnapshot();
        }
        else
        {
            if (hostChanged)
                RaiseStateChanged();
            RequestFullSnapshot();
        }
    }

    private void HandleLobbyLeft()
    {
        _state.Clear();
        ClearCommandHistory();
        StateChanged?.Invoke(null);
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Zone || !_state.IsInitialized)
            return;

        if (MultiplayerProtocol.TryDecodeZoneSnapshotRequest(
                packet.Payload.Span,
                packet.Sender,
                out _,
                out var requestedLobbyId))
        {
            HandleSnapshotRequest(packet.Sender, requestedLobbyId);
            return;
        }

        if (MultiplayerProtocol.TryDecodeZoneSnapshot(
                packet.Payload.Span,
                packet.Sender,
                out _,
                out var snapshot))
        {
            HandleFullSnapshot(packet.Sender, snapshot);
            return;
        }

        if (MultiplayerProtocol.TryDecodePublishVoidlingCommand(
                packet.Payload.Span,
                packet.Sender,
                out var publishCommandId,
                out var publishedVoidling))
        {
            if (_connection.IsLocalHost && RememberCommand(publishCommandId))
                ApplyHostPublish(packet.Sender, publishedVoidling);
            return;
        }

        if (MultiplayerProtocol.TryDecodeVoidlingPublishedEvent(
                packet.Payload.Span,
                packet.Sender,
                out _,
                out var publishEpoch,
                out var publishRevision,
                out var canonicalVoidling))
        {
            HandlePublishedEvent(packet.Sender, publishEpoch, publishRevision, canonicalVoidling);
            return;
        }

        if (MultiplayerProtocol.TryDecodeRemoveVoidlingCommand(
                packet.Payload.Span,
                packet.Sender,
                out var removeCommandId,
                out var ownerId,
                out var creatureId))
        {
            if (_connection.IsLocalHost && RememberCommand(removeCommandId))
            {
                if (ownerId != packet.Sender)
                {
                    Reject("Connected-zone remove command claimed another player's ownership.");
                    return;
                }

                ApplyHostRemove(packet.Sender, creatureId);
            }
            return;
        }

        if (MultiplayerProtocol.TryDecodeVoidlingRemovedEvent(
                packet.Payload.Span,
                packet.Sender,
                out _,
                out var removeEpoch,
                out var removeRevision,
                out var removedOwnerId,
                out var removedCreatureId))
        {
            HandleRemovedEvent(
                packet.Sender,
                removeEpoch,
                removeRevision,
                removedOwnerId,
                removedCreatureId);
            return;
        }

        Reject("Connected-zone packet was malformed or used an unsupported message type.");
    }

    private void HandleSnapshotRequest(PlatformUserId sender, ulong requestedLobbyId)
    {
        if (!_connection.IsLocalHost ||
            requestedLobbyId != _state.LobbyId ||
            !_connection.IsLobbyMember(sender))
        {
            return;
        }

        SendFullSnapshot(sender);
    }

    private void HandleFullSnapshot(PlatformUserId sender, ConnectedZoneSnapshot snapshot)
    {
        var lobby = _connection.CurrentLobby;
        if (_connection.IsLocalHost ||
            lobby == null ||
            sender != lobby.OwnerId ||
            snapshot.HostId != lobby.OwnerId ||
            snapshot.LobbyId != lobby.LobbyId)
        {
            Reject("Connected-zone snapshot did not come from the current lobby host.");
            return;
        }

        foreach (var sharedVoidling in snapshot.Voidlings ?? Array.Empty<SharedVoidlingSnapshot>())
        {
            if (!ConnectedZoneValidation.IsValidSharedVoidling(sharedVoidling) ||
                !_connection.IsLobbyMember(sharedVoidling.OwnerId))
            {
                Reject("Connected-zone snapshot contained an invalid or departed owner.");
                return;
            }
        }

        if (_state.TryApplySnapshot(snapshot))
            RaiseStateChanged();
    }

    private void ApplyHostPublish(PlatformUserId sender, SharedVoidlingSnapshot snapshot)
    {
        var local = _connection.LocalUser;
        if (!_connection.IsLocalHost ||
            local == null ||
            !_connection.IsLobbyMember(sender) ||
            snapshot.OwnerId != sender ||
            !ConnectedZoneValidation.IsValidSharedVoidling(snapshot))
        {
            Reject("Connected-zone publish command failed ownership or session validation.");
            return;
        }

        var revision = _state.Publish(snapshot);
        var payload = MultiplayerProtocol.EncodeVoidlingPublishedEvent(
            local,
            _state.AuthorityEpoch,
            revision,
            snapshot);
        _connection.BroadcastToLobby(NetworkChannel.Zone, payload, DeliveryMode.Reliable);
        RaiseStateChanged();
    }

    private void ApplyHostRemove(PlatformUserId sender, string creatureId)
    {
        var local = _connection.LocalUser;
        if (!_connection.IsLocalHost ||
            local == null ||
            !_connection.IsLobbyMember(sender))
        {
            Reject("Connected-zone remove command failed ownership or session validation.");
            return;
        }

        var previousRevision = _state.Revision;
        var revision = _state.Remove(sender, creatureId);
        if (revision == previousRevision)
            return;

        var payload = MultiplayerProtocol.EncodeVoidlingRemovedEvent(
            local,
            _state.AuthorityEpoch,
            revision,
            sender,
            creatureId);
        _connection.BroadcastToLobby(NetworkChannel.Zone, payload, DeliveryMode.Reliable);
        RaiseStateChanged();
    }

    private void HandlePublishedEvent(
        PlatformUserId sender,
        long authorityEpoch,
        long revision,
        SharedVoidlingSnapshot snapshot)
    {
        var lobby = _connection.CurrentLobby;
        if (_connection.IsLocalHost ||
            lobby == null ||
            sender != lobby.OwnerId ||
            !_connection.IsLobbyMember(snapshot.OwnerId))
        {
            Reject("Connected-zone publish event did not come from the current host or referenced a departed owner.");
            return;
        }

        var result = _state.ApplyPublished(authorityEpoch, revision, snapshot);
        if (result == ZoneDeltaApplyResult.Applied)
            RaiseStateChanged();
        else if (result == ZoneDeltaApplyResult.RequiresSnapshot)
            RequestFullSnapshot();
    }

    private void HandleRemovedEvent(
        PlatformUserId sender,
        long authorityEpoch,
        long revision,
        PlatformUserId ownerId,
        string creatureId)
    {
        var lobby = _connection.CurrentLobby;
        if (_connection.IsLocalHost || lobby == null || sender != lobby.OwnerId)
        {
            Reject("Connected-zone remove event did not come from the current lobby host.");
            return;
        }

        var result = _state.ApplyRemoved(authorityEpoch, revision, ownerId, creatureId);
        if (result == ZoneDeltaApplyResult.Applied)
            RaiseStateChanged();
        else if (result == ZoneDeltaApplyResult.RequiresSnapshot)
            RequestFullSnapshot();
    }

    private void SendFullSnapshot(PlatformUserId peer)
    {
        var local = _connection.LocalUser;
        if (!_connection.IsLocalHost || local == null || !_connection.IsLobbyMember(peer))
            return;

        var payload = MultiplayerProtocol.EncodeZoneSnapshot(local, _state.ToSnapshot());
        _connection.TrySend(peer, NetworkChannel.Zone, payload, DeliveryMode.Reliable);
    }

    private void BroadcastFullSnapshot()
    {
        var local = _connection.LocalUser;
        if (!_connection.IsLocalHost || local == null || !_state.IsInitialized)
            return;

        var payload = MultiplayerProtocol.EncodeZoneSnapshot(local, _state.ToSnapshot());
        _connection.BroadcastToLobby(NetworkChannel.Zone, payload, DeliveryMode.Reliable);
    }

    private bool TryGetSessionContext(
        out PlatformUser? local,
        out LobbySnapshot? lobby,
        out string? error)
    {
        local = _connection.LocalUser;
        lobby = _connection.CurrentLobby;
        error = null;

        if (!_connection.IsAvailable)
        {
            error = _connection.UnavailableReason ?? "Multiplayer is unavailable.";
            return false;
        }

        if (local == null || lobby == null || !_state.IsInitialized)
        {
            error = "Join a connected Garden before sharing Voidlings.";
            return false;
        }

        return true;
    }

    private bool RememberCommand(Guid messageId)
    {
        if (messageId == Guid.Empty || !_recentCommandIds.Add(messageId))
            return false;

        _recentCommandOrder.Enqueue(messageId);
        while (_recentCommandOrder.Count > RecentCommandLimit)
        {
            var expired = _recentCommandOrder.Dequeue();
            _recentCommandIds.Remove(expired);
        }

        return true;
    }

    private void ClearCommandHistory()
    {
        _recentCommandOrder.Clear();
        _recentCommandIds.Clear();
    }

    private void RaiseStateChanged()
        => StateChanged?.Invoke(_state.ToSnapshot());

    private void Reject(string reason)
        => ProtocolRejected?.Invoke(reason);
}
