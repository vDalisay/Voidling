using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer;

/// <summary>
/// Lossy, presentation-only movement replication for Voidlings already published in the connected
/// Garden. It never mutates ConnectedZoneState or GameStateData. Reliable zone snapshots remain the
/// fallback position; transient transforms only improve what connected peers see between snapshots.
/// </summary>
public sealed class ConnectedZoneTransientService
{
    public static readonly TimeSpan DefaultMinimumPublishInterval = TimeSpan.FromMilliseconds(100);

    private readonly MultiplayerConnectionService _connection;
    private readonly ConnectedZoneService _zone;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _minimumPublishInterval;
    private readonly Dictionary<SharedVoidlingKey, SharedVoidlingTransform> _latest = new();
    private readonly Dictionary<SharedVoidlingKey, long> _nextLocalSequence = new();
    private readonly Dictionary<SharedVoidlingKey, long> _lastLocalPublishTimestamp = new();

    public ConnectedZoneTransientService(
        MultiplayerConnectionService connection,
        ConnectedZoneService zone,
        TimeProvider? timeProvider = null,
        TimeSpan? minimumPublishInterval = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _minimumPublishInterval = minimumPublishInterval ?? DefaultMinimumPublishInterval;
        if (_minimumPublishInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumPublishInterval));

        _connection.PacketReceived += HandlePacket;
        _zone.StateChanged += HandleZoneStateChanged;
        _connection.LobbyLeft += Clear;
    }

    public event Action<SharedVoidlingTransform>? TransformChanged;
    public event Action<string>? ProtocolRejected;

    public IReadOnlyCollection<SharedVoidlingTransform> CurrentTransforms
        => _latest.Values.ToArray();

    public ConnectedZoneOperationResult PublishOwnedTransform(
        string creatureId,
        float zoneX,
        float zoneY,
        float facingX,
        string animationState)
    {
        var local = _connection.LocalUser;
        var zone = _zone.CurrentSnapshot;
        if (!_connection.IsAvailable || local == null || zone == null || _connection.CurrentLobby == null)
            return ConnectedZoneOperationResult.Failed("Join a connected Garden before publishing movement.");
        if (string.IsNullOrWhiteSpace(creatureId) ||
            creatureId.Length > ConnectedZoneValidation.MaxCreatureIdLength)
        {
            return ConnectedZoneOperationResult.Failed("Creature ID is invalid.");
        }

        var key = new SharedVoidlingKey(local.Id, creatureId);
        if (!ContainsPublishedVoidling(zone, key))
            return ConnectedZoneOperationResult.Failed("Only a locally owned Voidling already published in this zone can move remotely.");

        var sequence = _nextLocalSequence.TryGetValue(key, out var previous)
            ? previous + 1
            : 1;
        var transform = new SharedVoidlingTransform(
            local.Id,
            creatureId,
            sequence,
            zoneX,
            zoneY,
            facingX,
            (animationState ?? string.Empty).Trim());
        if (!ConnectedZoneTransientValidation.IsValid(transform))
            return ConnectedZoneOperationResult.Failed("Connected-zone transform is invalid.");

        var now = _timeProvider.GetTimestamp();
        if (_lastLocalPublishTimestamp.TryGetValue(key, out var last) &&
            _timeProvider.GetElapsedTime(last, now) < _minimumPublishInterval)
        {
            // A throttled sample is not an error; a later sample supersedes it by design.
            return ConnectedZoneOperationResult.Succeeded;
        }

        _lastLocalPublishTimestamp[key] = now;
        _nextLocalSequence[key] = sequence;
        _latest[key] = transform;

        // Transforms are ephemeral and superseded by newer samples. Sending directly to every lobby
        // member avoids a host relay and is safe because recipients authenticate transport ownership.
        var payload = ConnectedZoneTransientProtocol.EncodeTransform(local, transform);
        _connection.BroadcastToLobby(
            NetworkChannel.GardenTransient,
            payload,
            DeliveryMode.Unreliable);
        TransformChanged?.Invoke(transform);
        return ConnectedZoneOperationResult.Succeeded;
    }

    public bool TryGetTransform(
        PlatformUserId ownerId,
        string creatureId,
        out SharedVoidlingTransform transform)
        => _latest.TryGetValue(new SharedVoidlingKey(ownerId, creatureId), out transform!);

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.GardenTransient)
            return;

        if (!ConnectedZoneTransientProtocol.TryDecodeTransform(
                packet.Payload.Span,
                packet.Sender,
                out _,
                out var transform))
        {
            Reject("Connected-zone transient packet was malformed or unsupported.");
            return;
        }

        var snapshot = _zone.CurrentSnapshot;
        if (snapshot == null ||
            !_connection.IsLobbyMember(packet.Sender) ||
            transform.OwnerId != packet.Sender ||
            !ContainsPublishedVoidling(snapshot, transform.Key))
        {
            Reject("Connected-zone transient transform failed lobby or ownership validation.");
            return;
        }

        if (_latest.TryGetValue(transform.Key, out var previous) &&
            transform.Sequence <= previous.Sequence)
        {
            return; // expected with unreliable/reordered delivery
        }

        _latest[transform.Key] = transform;
        TransformChanged?.Invoke(transform);
    }

    private void HandleZoneStateChanged(ConnectedZoneSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            Clear();
            return;
        }

        var published = snapshot.Voidlings
            .Select(value => value.Key)
            .ToHashSet();
        foreach (var key in _latest.Keys.Where(key => !published.Contains(key)).ToArray())
            _latest.Remove(key);
        foreach (var key in _nextLocalSequence.Keys.Where(key => !published.Contains(key)).ToArray())
            _nextLocalSequence.Remove(key);
        foreach (var key in _lastLocalPublishTimestamp.Keys.Where(key => !published.Contains(key)).ToArray())
            _lastLocalPublishTimestamp.Remove(key);
    }

    private static bool ContainsPublishedVoidling(
        ConnectedZoneSnapshot snapshot,
        SharedVoidlingKey key)
        => snapshot.Voidlings.Any(value => value.Key == key);

    private void Clear()
    {
        _latest.Clear();
        _nextLocalSequence.Clear();
        _lastLocalPublishTimestamp.Clear();
    }

    private void Reject(string reason)
        => ProtocolRejected?.Invoke(reason);
}
