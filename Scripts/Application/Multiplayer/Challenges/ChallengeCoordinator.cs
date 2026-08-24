using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Challenges;

/// <summary>
/// Coordinates small 2-4 player activities inside one connected Garden lobby. Steam lobby
/// membership provides identity/discovery; this service owns only transient challenge state.
/// Mode-specific race or auto-battle code consumes the canonical Running StartPayload separately.
/// </summary>
public sealed class ChallengeCoordinator
{
    private const int RecentMessageLimit = 512;

    private readonly MultiplayerConnectionService _connection;
    private readonly Dictionary<string, ChallengeSnapshot> _challenges = new(StringComparer.Ordinal);
    private readonly Queue<Guid> _recentMessageOrder = new();
    private readonly HashSet<Guid> _recentMessageIds = new();
    private ulong _activeLobbyId;
    private PlatformUserId _observedHostId;

    public ChallengeCoordinator(MultiplayerConnectionService connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.PacketReceived += HandlePacket;
        _connection.LobbyChanged += HandleLobbyChanged;
        _connection.LobbyLeft += HandleLobbyLeft;

        var lobby = _connection.CurrentLobby;
        if (lobby == null)
            return;

        _activeLobbyId = lobby.LobbyId;
        _observedHostId = lobby.OwnerId;
        if (!_connection.IsLocalHost)
            RequestSync();
    }

    public IReadOnlyCollection<ChallengeSnapshot> Challenges
        => _challenges.Values
            .OrderBy(snapshot => snapshot.ChallengeId, StringComparer.Ordinal)
            .ToArray();

    public event Action<ChallengeSnapshot>? ChallengeChanged;
    public event Action? ChallengesReset;
    public event Action<string>? ProtocolRejected;

    public ChallengeOperationResult OfferChallenge(
        ChallengeKind kind,
        int maxParticipants = ChallengeValidation.MaxParticipants)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null)
            return ChallengeOperationResult.Failed("Join a connected Garden before offering a challenge.");
        if (!Enum.IsDefined(kind))
            return ChallengeOperationResult.Failed("Challenge kind is invalid.");
        if (maxParticipants is < 2 or > ChallengeValidation.MaxParticipants)
            return ChallengeOperationResult.Failed("Challenges support between 2 and 4 participants.");
        if (HasActiveChallenge(local.Id))
            return ChallengeOperationResult.Failed("Leave or finish the current challenge before offering another.");

        var challengeId = Guid.NewGuid().ToString("N");
        if (_connection.IsLocalHost)
        {
            HandleHostOffer(local.Id, challengeId, lobby.LobbyId, kind, maxParticipants);
        }
        else if (!_connection.TrySend(
                     lobby.OwnerId,
                     NetworkChannel.Challenge,
                     ChallengeProtocol.EncodeOfferCommand(local, challengeId, lobby.LobbyId, kind, maxParticipants),
                     DeliveryMode.Reliable))
        {
            return ChallengeOperationResult.Failed("Could not send the challenge offer to the lobby host.");
        }

        return ChallengeOperationResult.Succeeded(challengeId);
    }

    public ChallengeOperationResult JoinChallenge(string challengeId)
    {
        var context = ValidateKnownChallenge(challengeId);
        if (context.Error != null)
            return ChallengeOperationResult.Failed(context.Error);
        if (HasActiveChallenge(context.Local!.Id) && !context.Snapshot!.Contains(context.Local.Id))
            return ChallengeOperationResult.Failed("Leave or finish the current challenge before joining another.");

        if (_connection.IsLocalHost)
        {
            HandleHostJoin(context.Local.Id, challengeId);
        }
        else if (!_connection.TrySend(
                     context.Lobby!.OwnerId,
                     NetworkChannel.Challenge,
                     ChallengeProtocol.EncodeJoinCommand(context.Local, challengeId),
                     DeliveryMode.Reliable))
        {
            return ChallengeOperationResult.Failed("Could not send the challenge join request.");
        }

        return ChallengeOperationResult.Succeeded(challengeId);
    }

    public ChallengeOperationResult LeaveChallenge(string challengeId)
    {
        var context = ValidateKnownChallenge(challengeId);
        if (context.Error != null)
            return ChallengeOperationResult.Failed(context.Error);
        if (!context.Snapshot!.Contains(context.Local!.Id))
            return ChallengeOperationResult.Failed("Local player is not participating in this challenge.");

        if (_connection.IsLocalHost)
        {
            HandleHostLeave(context.Local.Id, challengeId);
        }
        else if (!_connection.TrySend(
                     context.Lobby!.OwnerId,
                     NetworkChannel.Challenge,
                     ChallengeProtocol.EncodeLeaveCommand(context.Local, challengeId),
                     DeliveryMode.Reliable))
        {
            return ChallengeOperationResult.Failed("Could not send the challenge leave request.");
        }

        return ChallengeOperationResult.Succeeded(challengeId);
    }

    public ChallengeOperationResult CancelChallenge(string challengeId)
    {
        var context = ValidateKnownChallenge(challengeId);
        if (context.Error != null)
            return ChallengeOperationResult.Failed(context.Error);
        if (!_connection.IsLocalHost && context.Snapshot!.CreatorId != context.Local!.Id)
            return ChallengeOperationResult.Failed("Only the challenge creator or lobby host can cancel it.");

        if (_connection.IsLocalHost)
        {
            HandleHostCancel(context.Local!.Id, challengeId);
        }
        else if (!_connection.TrySend(
                     context.Lobby!.OwnerId,
                     NetworkChannel.Challenge,
                     ChallengeProtocol.EncodeCancelCommand(context.Local!, challengeId),
                     DeliveryMode.Reliable))
        {
            return ChallengeOperationResult.Failed("Could not send the challenge cancellation.");
        }

        return ChallengeOperationResult.Succeeded(challengeId);
    }

    /// <summary>
    /// Starts a mode after its caller has built immutable mode-specific start data. Multiplayer race
    /// synchronization will add a payload-hash acknowledgement before invoking this boundary.
    /// </summary>
    public ChallengeOperationResult StartChallenge(
        string challengeId,
        ReadOnlyMemory<byte> startPayload)
    {
        var context = ValidateKnownChallenge(challengeId);
        if (context.Error != null)
            return ChallengeOperationResult.Failed(context.Error);
        if (startPayload.Length > ChallengeValidation.MaxStartPayloadBytes)
            return ChallengeOperationResult.Failed("Challenge start payload is too large.");
        if (!_connection.IsLocalHost && context.Snapshot!.CreatorId != context.Local!.Id)
            return ChallengeOperationResult.Failed("Only the challenge creator or lobby host can start it.");

        var bytes = startPayload.ToArray();
        if (_connection.IsLocalHost)
        {
            HandleHostStart(context.Local!.Id, challengeId, bytes);
        }
        else if (!_connection.TrySend(
                     context.Lobby!.OwnerId,
                     NetworkChannel.Challenge,
                     ChallengeProtocol.EncodeStartCommand(context.Local!, challengeId, bytes),
                     DeliveryMode.Reliable))
        {
            return ChallengeOperationResult.Failed("Could not send the challenge start request.");
        }

        return ChallengeOperationResult.Succeeded(challengeId);
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Challenge || _connection.CurrentLobby == null)
            return;

        if (ChallengeProtocol.TryDecodeOfferCommand(
                packet.Payload.Span,
                packet.Sender,
                out var offerMessageId,
                out var challengeId,
                out var lobbyId,
                out var kind,
                out var maxParticipants))
        {
            if (_connection.IsLocalHost && RememberMessage(offerMessageId))
                HandleHostOffer(packet.Sender, challengeId, lobbyId, kind, maxParticipants);
            return;
        }

        if (ChallengeProtocol.TryDecodeJoinCommand(
                packet.Payload.Span,
                packet.Sender,
                out var joinMessageId,
                out challengeId))
        {
            if (_connection.IsLocalHost && RememberMessage(joinMessageId))
                HandleHostJoin(packet.Sender, challengeId);
            return;
        }

        if (ChallengeProtocol.TryDecodeLeaveCommand(
                packet.Payload.Span,
                packet.Sender,
                out var leaveMessageId,
                out challengeId))
        {
            if (_connection.IsLocalHost && RememberMessage(leaveMessageId))
                HandleHostLeave(packet.Sender, challengeId);
            return;
        }

        if (ChallengeProtocol.TryDecodeCancelCommand(
                packet.Payload.Span,
                packet.Sender,
                out var cancelMessageId,
                out challengeId))
        {
            if (_connection.IsLocalHost && RememberMessage(cancelMessageId))
                HandleHostCancel(packet.Sender, challengeId);
            return;
        }

        if (ChallengeProtocol.TryDecodeStartCommand(
                packet.Payload.Span,
                packet.Sender,
                out var startMessageId,
                out challengeId,
                out var startPayload))
        {
            if (_connection.IsLocalHost && RememberMessage(startMessageId))
                HandleHostStart(packet.Sender, challengeId, startPayload);
            return;
        }

        if (ChallengeProtocol.TryDecodeSyncRequest(
                packet.Payload.Span,
                packet.Sender,
                out var syncMessageId,
                out lobbyId))
        {
            if (_connection.IsLocalHost && RememberMessage(syncMessageId))
                HandleHostSyncRequest(packet.Sender, lobbyId);
            return;
        }

        if (ChallengeProtocol.TryDecodeSyncState(
                packet.Payload.Span,
                packet.Sender,
                out lobbyId,
                out var challenges))
        {
            if (IsPacketFromCurrentHost(packet.Sender))
                ApplySyncState(lobbyId, challenges);
            return;
        }

        if (ChallengeProtocol.TryDecodeState(packet.Payload.Span, packet.Sender, out var snapshot))
        {
            if (IsPacketFromCurrentHost(packet.Sender))
                ApplyHostState(snapshot);
            return;
        }

        ProtocolRejected?.Invoke("Challenge packet was malformed or used an unsupported message type.");
    }

    private void HandleHostOffer(
        PlatformUserId creatorId,
        string challengeId,
        ulong lobbyId,
        ChallengeKind kind,
        int maxParticipants)
    {
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsLocalHost ||
            lobby == null ||
            lobby.LobbyId != lobbyId ||
            !_connection.IsLobbyMember(creatorId) ||
            !ChallengeValidation.IsValidChallengeId(challengeId) ||
            !Enum.IsDefined(kind) ||
            maxParticipants is < 2 or > ChallengeValidation.MaxParticipants ||
            _challenges.Count >= ChallengeValidation.MaxChallengesPerLobby ||
            _challenges.ContainsKey(challengeId) ||
            HasActiveChallenge(creatorId))
        {
            return;
        }

        var snapshot = new ChallengeSnapshot(
            challengeId,
            lobby.LobbyId,
            kind,
            creatorId,
            maxParticipants,
            ChallengePhase.Offered,
            new[] { creatorId },
            Array.Empty<byte>());
        PublishHostState(snapshot);
    }

    private void HandleHostJoin(PlatformUserId sender, string challengeId)
    {
        if (!_connection.IsLocalHost ||
            !_connection.IsLobbyMember(sender) ||
            !_challenges.TryGetValue(challengeId, out var current) ||
            current.Phase is not (ChallengePhase.Offered or ChallengePhase.Forming) ||
            current.Contains(sender) ||
            current.Participants.Length >= current.MaxParticipants ||
            HasActiveChallenge(sender))
        {
            return;
        }

        var participants = current.Participants.Concat(new[] { sender }).ToArray();
        PublishHostState(current with
        {
            Phase = ChallengePhase.Forming,
            Participants = participants
        });
    }

    private void HandleHostLeave(PlatformUserId sender, string challengeId)
    {
        if (!_connection.IsLocalHost ||
            !_challenges.TryGetValue(challengeId, out var current) ||
            !current.Contains(sender) ||
            current.Phase is ChallengePhase.Completed or ChallengePhase.Cancelled)
        {
            return;
        }

        if (sender == current.CreatorId || current.Phase == ChallengePhase.Running)
        {
            PublishHostState(Cancelled(current));
            return;
        }

        var participants = current.Participants.Where(user => user != sender).ToArray();
        PublishHostState(current with
        {
            Phase = participants.Length == 1 ? ChallengePhase.Offered : ChallengePhase.Forming,
            Participants = participants
        });
    }

    private void HandleHostCancel(PlatformUserId sender, string challengeId)
    {
        if (!_connection.IsLocalHost ||
            !_challenges.TryGetValue(challengeId, out var current) ||
            current.Phase is ChallengePhase.Completed or ChallengePhase.Cancelled)
        {
            return;
        }

        var host = _connection.LocalUser;
        if (host == null || (sender != current.CreatorId && sender != host.Id))
            return;

        PublishHostState(Cancelled(current));
    }

    private void HandleHostStart(
        PlatformUserId sender,
        string challengeId,
        byte[] startPayload)
    {
        if (!_connection.IsLocalHost ||
            !_challenges.TryGetValue(challengeId, out var current) ||
            current.Phase is not (ChallengePhase.Offered or ChallengePhase.Forming or ChallengePhase.Ready) ||
            current.Participants.Length < 2 ||
            startPayload == null ||
            startPayload.Length > ChallengeValidation.MaxStartPayloadBytes)
        {
            return;
        }

        var host = _connection.LocalUser;
        if (host == null || (sender != current.CreatorId && sender != host.Id))
            return;
        if (current.Participants.Any(user => !_connection.IsLobbyMember(user)))
        {
            PublishHostState(Cancelled(current));
            return;
        }

        PublishHostState(current with
        {
            Phase = ChallengePhase.Running,
            StartPayload = startPayload.ToArray()
        });
    }

    private void HandleHostSyncRequest(PlatformUserId sender, ulong lobbyId)
    {
        var lobby = _connection.CurrentLobby;
        var host = _connection.LocalUser;
        if (!_connection.IsLocalHost ||
            lobby == null ||
            host == null ||
            lobby.LobbyId != lobbyId ||
            !_connection.IsLobbyMember(sender) ||
            sender == host.Id)
        {
            return;
        }

        var snapshots = _challenges.Values
            .OrderBy(snapshot => snapshot.ChallengeId, StringComparer.Ordinal)
            .ToArray();
        _connection.TrySend(
            sender,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeSyncState(host, lobby.LobbyId, snapshots),
            DeliveryMode.Reliable);
    }

    private void PublishHostState(ChallengeSnapshot snapshot)
    {
        if (!ChallengeValidation.IsValidSnapshot(snapshot))
            throw new InvalidOperationException("Host attempted to publish an invalid challenge snapshot.");

        _challenges[snapshot.ChallengeId] = snapshot;
        ChallengeChanged?.Invoke(snapshot);

        var host = _connection.LocalUser;
        if (host == null)
            return;
        _connection.BroadcastToLobby(
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeState(host, snapshot),
            DeliveryMode.Reliable);
    }

    private void ApplyHostState(ChallengeSnapshot snapshot)
    {
        var lobby = _connection.CurrentLobby;
        if (lobby == null || snapshot.LobbyId != lobby.LobbyId)
            return;
        if (snapshot.Phase != ChallengePhase.Cancelled &&
            snapshot.Participants.Any(user => !_connection.IsLobbyMember(user)))
        {
            return;
        }

        _challenges[snapshot.ChallengeId] = snapshot;
        ChallengeChanged?.Invoke(snapshot);
    }

    private void ApplySyncState(ulong lobbyId, ChallengeSnapshot[] snapshots)
    {
        var lobby = _connection.CurrentLobby;
        if (lobby == null || lobby.LobbyId != lobbyId)
            return;

        _challenges.Clear();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Phase != ChallengePhase.Cancelled &&
                snapshot.Participants.Any(user => !_connection.IsLobbyMember(user)))
            {
                continue;
            }
            _challenges[snapshot.ChallengeId] = snapshot;
        }

        ChallengesReset?.Invoke();
        foreach (var snapshot in _challenges.Values.OrderBy(value => value.ChallengeId, StringComparer.Ordinal))
            ChallengeChanged?.Invoke(snapshot);
    }

    private void HandleLobbyChanged(LobbySnapshot lobby)
    {
        var sameLobby = _activeLobbyId != 0 && _activeLobbyId == lobby.LobbyId;
        var hostChanged = sameLobby && _observedHostId.Value != 0 && _observedHostId != lobby.OwnerId;
        var previousLobbyId = _activeLobbyId;
        _activeLobbyId = lobby.LobbyId;
        _observedHostId = lobby.OwnerId;

        if (!sameLobby && previousLobbyId != 0)
            ResetChallenges();

        if (hostChanged)
        {
            var active = _challenges.Values.Where(snapshot =>
                snapshot.Phase is not (ChallengePhase.Completed or ChallengePhase.Cancelled)).ToArray();
            foreach (var snapshot in active)
            {
                var cancelled = Cancelled(snapshot);
                _challenges[cancelled.ChallengeId] = cancelled;
                ChallengeChanged?.Invoke(cancelled);
            }

            if (_connection.IsLocalHost)
            {
                foreach (var snapshot in active.Select(Cancelled))
                    PublishHostState(snapshot);
            }
        }

        if (_connection.IsLocalHost)
        {
            foreach (var snapshot in _challenges.Values.ToArray())
            {
                if (snapshot.Phase is ChallengePhase.Completed or ChallengePhase.Cancelled)
                    continue;

                var creatorPresent = _connection.IsLobbyMember(snapshot.CreatorId);
                var remaining = snapshot.Participants.Where(_connection.IsLobbyMember).ToArray();
                if (!creatorPresent || snapshot.Phase == ChallengePhase.Running && remaining.Length != snapshot.Participants.Length)
                {
                    PublishHostState(Cancelled(snapshot));
                    continue;
                }

                if (remaining.Length != snapshot.Participants.Length)
                {
                    PublishHostState(snapshot with
                    {
                        Participants = remaining,
                        Phase = remaining.Length == 1 ? ChallengePhase.Offered : ChallengePhase.Forming
                    });
                }
            }
        }
        else
        {
            RequestSync();
        }
    }

    private void HandleLobbyLeft()
    {
        ResetChallenges();
        _activeLobbyId = 0;
        _observedHostId = default;
        ClearRecentMessages();
    }

    private void RequestSync()
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null || _connection.IsLocalHost)
            return;

        _connection.TrySend(
            lobby.OwnerId,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeSyncRequest(local, lobby.LobbyId),
            DeliveryMode.Reliable);
    }

    private (PlatformUser? Local, LobbySnapshot? Lobby, ChallengeSnapshot? Snapshot, string? Error)
        ValidateKnownChallenge(string challengeId)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null)
            return (local, lobby, null, "Join a connected Garden before using challenges.");
        if (!ChallengeValidation.IsValidChallengeId(challengeId) ||
            !_challenges.TryGetValue(challengeId, out var snapshot))
        {
            return (local, lobby, null, "Challenge is not available in the current connected Garden.");
        }
        if (snapshot.Phase is ChallengePhase.Completed or ChallengePhase.Cancelled)
            return (local, lobby, snapshot, "Challenge is no longer active.");
        return (local, lobby, snapshot, null);
    }

    private bool HasActiveChallenge(PlatformUserId userId)
        => _challenges.Values.Any(snapshot =>
            snapshot.Contains(userId) &&
            snapshot.Phase is not (ChallengePhase.Completed or ChallengePhase.Cancelled));

    private bool IsPacketFromCurrentHost(PlatformUserId sender)
        => _connection.CurrentLobby?.OwnerId == sender;

    private static ChallengeSnapshot Cancelled(ChallengeSnapshot snapshot)
        => snapshot with
        {
            Phase = ChallengePhase.Cancelled,
            StartPayload = Array.Empty<byte>()
        };

    private void ResetChallenges()
    {
        if (_challenges.Count == 0)
            return;
        _challenges.Clear();
        ChallengesReset?.Invoke();
    }

    private bool RememberMessage(Guid messageId)
    {
        if (messageId == Guid.Empty || !_recentMessageIds.Add(messageId))
            return false;
        _recentMessageOrder.Enqueue(messageId);
        while (_recentMessageOrder.Count > RecentMessageLimit)
            _recentMessageIds.Remove(_recentMessageOrder.Dequeue());
        return true;
    }

    private void ClearRecentMessages()
    {
        _recentMessageOrder.Clear();
        _recentMessageIds.Clear();
    }
}
