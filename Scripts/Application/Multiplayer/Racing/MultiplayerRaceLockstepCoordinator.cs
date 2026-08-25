using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Racing;

namespace Voidling.Application.Multiplayer.Racing;

public sealed record MultiplayerRaceDesync(
    string ChallengeId,
    long Tick,
    PlatformUserId PeerId,
    string HostChecksum,
    string PeerChecksum);

/// <summary>
/// Network orchestration around the deterministic lockstep session. The lobby host schedules
/// result-affecting Cheer commands a small number of ticks into the future and broadcasts the
/// exact command to every peer. Each client advances its own deterministic simulation locally;
/// periodic checksums are reported to the host for diagnostics only. V1 deliberately has no
/// rollback or host state correction.
/// </summary>
public sealed class MultiplayerRaceLockstepCoordinator
{
    public const int ChecksumIntervalTicks = 120;
    private const int RecentMessageLimit = 1024;

    private sealed class ActiveRace
    {
        public ActiveRace(MultiplayerRaceLockstepSession session)
            => Session = session;

        public MultiplayerRaceLockstepSession Session { get; }
        public long NextLocalInputSequence { get; set; } = 1;
        public HashSet<long> LocalChecksumTicks { get; } = new();
        public Dictionary<long, string> HostChecksums { get; } = new();
        public Dictionary<long, Dictionary<PlatformUserId, string>> PeerChecksums { get; } = new();
    }

    private readonly MultiplayerConnectionService _connection;
    private readonly ChallengeCoordinator _challenges;
    private readonly Dictionary<string, ActiveRace> _active = new(StringComparer.Ordinal);
    private readonly Queue<Guid> _recentMessageOrder = new();
    private readonly HashSet<Guid> _recentMessageIds = new();

    public MultiplayerRaceLockstepCoordinator(
        MultiplayerConnectionService connection,
        ChallengeCoordinator challenges)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _challenges = challenges ?? throw new ArgumentNullException(nameof(challenges));

        _connection.PacketReceived += HandlePacket;
        _connection.LobbyLeft += Reset;
        _challenges.ChallengeChanged += HandleChallengeChanged;
        _challenges.ChallengesReset += Reset;
    }

    public event Action<string, MultiplayerRaceLockstepSession>? RaceSessionStarted;
    public event Action<ScheduledRaceCommand>? CommandScheduled;
    public event Action<string, long, string>? LocalChecksumReported;
    public event Action<string, long, PlatformUserId>? PeerChecksumMatched;
    public event Action<MultiplayerRaceDesync>? DesyncDetected;
    public event Action<string, string>? SyncIssue;
    public event Action<string>? ProtocolRejected;

    /// <summary>
    /// Attaches the exact immutable race that passed the start-payload acknowledgement handshake.
    /// This method is idempotent for a challenge and is intentionally separate from presentation.
    /// </summary>
    public bool AttachRace(ResolvedMultiplayerRace race, out string? error)
    {
        ArgumentNullException.ThrowIfNull(race);
        error = null;

        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null)
        {
            error = "Join a connected Garden before starting multiplayer race lockstep.";
            return false;
        }

        if (!_challenges.Challenges.Any(snapshot =>
                string.Equals(snapshot.ChallengeId, race.Start.ChallengeId, StringComparison.Ordinal) &&
                snapshot.Kind == ChallengeKind.Race &&
                snapshot.Phase == ChallengePhase.Running &&
                snapshot.Contains(local.Id)))
        {
            error = "Race challenge is not running for the local player.";
            return false;
        }

        var entrantOwners = race.Start.Entrants.Select(value => value.OwnerId).ToHashSet();
        if (!entrantOwners.Contains(local.Id) || entrantOwners.Any(user => !_connection.IsLobbyMember(user)))
        {
            error = "Race entrants do not match the currently connected lobby participants.";
            return false;
        }

        if (_active.ContainsKey(race.Start.ChallengeId))
            return true;

        var active = new ActiveRace(new MultiplayerRaceLockstepSession(race));
        _active.Add(race.Start.ChallengeId, active);
        RaceSessionStarted?.Invoke(race.Start.ChallengeId, active.Session);
        PublishChecksumIfDue(active, force: true);
        return true;
    }

    public bool TryGetSession(string challengeId, out MultiplayerRaceLockstepSession session)
    {
        if (_active.TryGetValue(challengeId, out var active))
        {
            session = active.Session;
            return true;
        }

        session = default!;
        return false;
    }

    public MultiplayerRaceOperationResult RequestCheer(string challengeId)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null)
            return MultiplayerRaceOperationResult.Failed("Join a connected Garden before cheering in a multiplayer race.");
        if (!_active.TryGetValue(challengeId, out var active) || active.Session.IsComplete)
            return MultiplayerRaceOperationResult.Failed("Multiplayer race is not active.");
        if (!active.Session.TryGetParticipantId(local.Id, out _))
            return MultiplayerRaceOperationResult.Failed("Local player does not own an entrant in this race.");

        var inputSequence = active.NextLocalInputSequence++;
        if (_connection.IsLocalHost)
        {
            return TryHostScheduleCheer(local.Id, challengeId, inputSequence, out var error)
                ? MultiplayerRaceOperationResult.Succeeded
                : MultiplayerRaceOperationResult.Failed(error ?? "Host could not schedule Cheer.");
        }

        return _connection.TrySend(
            lobby.OwnerId,
            NetworkChannel.Challenge,
            MultiplayerRaceLockstepProtocol.EncodeCheerRequest(local, challengeId, inputSequence),
            DeliveryMode.Reliable)
            ? MultiplayerRaceOperationResult.Succeeded
            : MultiplayerRaceOperationResult.Failed("Could not send Cheer request to the race host.");
    }

    public bool TryAdvanceFixedSteps(
        string challengeId,
        int stepCount,
        out RaceLockstepAdvanceResult result,
        out string? error)
    {
        result = EmptyAdvanceResult();
        error = null;
        if (stepCount <= 0)
            return true;
        if (!_active.TryGetValue(challengeId, out var active))
        {
            error = "Multiplayer race lockstep session is not active.";
            return false;
        }

        var simulationEvents = new List<RaceSimulationEvent>();
        var applications = new List<RaceCommandApplication>();
        for (var i = 0; i < stepCount && !active.Session.IsComplete; i++)
        {
            var advanced = active.Session.AdvanceFixedSteps(1);
            simulationEvents.AddRange(advanced.SimulationEvents);
            applications.AddRange(advanced.CommandApplications);
            PublishChecksumIfDue(active, force: active.Session.IsComplete);
        }

        result = new RaceLockstepAdvanceResult(
            simulationEvents.AsReadOnly(),
            applications.AsReadOnly());
        return true;
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Challenge || _connection.CurrentLobby == null)
            return;

        if (MultiplayerRaceLockstepProtocol.TryDecodeCheerRequest(
                packet.Payload.Span,
                packet.Sender,
                out var cheerMessageId,
                out var challengeId,
                out var inputSequence))
        {
            if (_connection.IsLocalHost && RememberMessage(cheerMessageId) &&
                !TryHostScheduleCheer(packet.Sender, challengeId, inputSequence, out var error))
            {
                SyncIssue?.Invoke(challengeId, error ?? "Host rejected a Cheer request.");
            }
            return;
        }

        if (MultiplayerRaceLockstepProtocol.TryDecodeScheduledCommand(
                packet.Payload.Span,
                packet.Sender,
                out var scheduledMessageId,
                out var command))
        {
            if (IsPacketFromCurrentHost(packet.Sender) && RememberMessage(scheduledMessageId))
                HandleScheduledCommand(command);
            return;
        }

        if (MultiplayerRaceLockstepProtocol.TryDecodeChecksum(
                packet.Payload.Span,
                packet.Sender,
                out var checksumMessageId,
                out challengeId,
                out var tick,
                out var checksum))
        {
            if (_connection.IsLocalHost && RememberMessage(checksumMessageId))
                HandlePeerChecksum(packet.Sender, challengeId, tick, checksum);
            return;
        }

        if (MultiplayerProtocol.TryPeekMessageType(packet.Payload.Span, out var messageType) &&
            IsLockstepMessageType(messageType))
        {
            ProtocolRejected?.Invoke("Race lockstep packet was malformed or unsupported.");
        }
    }

    private bool TryHostScheduleCheer(
        PlatformUserId ownerId,
        string challengeId,
        long inputSequence,
        out string? error)
    {
        error = null;
        if (!_connection.IsLocalHost || !_connection.IsLobbyMember(ownerId))
        {
            error = "Only the connected Garden host can schedule race commands.";
            return false;
        }
        if (!_active.TryGetValue(challengeId, out var active) || active.Session.IsComplete)
        {
            error = "Race is not active on the host.";
            return false;
        }
        if (!active.Session.TryGetParticipantId(ownerId, out var participantId))
        {
            error = "Cheer sender does not own a participant in this race.";
            return false;
        }

        var command = new ScheduledRaceCommand(
            challengeId,
            active.Session.CurrentTick + MultiplayerRaceLockstepSession.DefaultInputDelayTicks,
            inputSequence,
            ownerId,
            participantId,
            MultiplayerRaceCommandKind.Cheer);
        var schedule = active.Session.Schedule(command);
        if (schedule == RaceCommandScheduleResult.Duplicate)
            return true;
        if (schedule != RaceCommandScheduleResult.Scheduled)
        {
            error = $"Cheer command could not be scheduled ({schedule}).";
            return false;
        }

        var host = _connection.LocalUser;
        if (host == null)
        {
            error = "Race host identity is unavailable.";
            return false;
        }

        _connection.BroadcastToLobby(
            NetworkChannel.Challenge,
            MultiplayerRaceLockstepProtocol.EncodeScheduledCommand(host, command),
            DeliveryMode.Reliable);
        CommandScheduled?.Invoke(command);
        return true;
    }

    private void HandleScheduledCommand(ScheduledRaceCommand command)
    {
        if (!_active.TryGetValue(command.ChallengeId, out var active))
            return;

        var schedule = active.Session.Schedule(command);
        switch (schedule)
        {
            case RaceCommandScheduleResult.Scheduled:
                CommandScheduled?.Invoke(command);
                break;
            case RaceCommandScheduleResult.Duplicate:
                break;
            case RaceCommandScheduleResult.TooLate:
                SyncIssue?.Invoke(command.ChallengeId, $"Race command arrived after tick {command.Tick}.");
                break;
            default:
                SyncIssue?.Invoke(command.ChallengeId, "Race host sent an invalid scheduled command.");
                break;
        }
    }

    private void PublishChecksumIfDue(ActiveRace active, bool force)
    {
        var tick = active.Session.CurrentTick;
        if (!force && (tick == 0 || tick % ChecksumIntervalTicks != 0))
            return;
        if (!active.LocalChecksumTicks.Add(tick))
            return;

        var checksum = active.Session.ComputeDeterministicChecksum();
        LocalChecksumReported?.Invoke(active.Session.ChallengeId, tick, checksum);

        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null)
            return;

        if (_connection.IsLocalHost)
        {
            active.HostChecksums[tick] = checksum;
            CompareQueuedPeerChecksums(active, tick, checksum);
            return;
        }

        if (!_connection.TrySend(
                lobby.OwnerId,
                NetworkChannel.Challenge,
                MultiplayerRaceLockstepProtocol.EncodeChecksum(local, active.Session.ChallengeId, tick, checksum),
                DeliveryMode.Reliable))
        {
            SyncIssue?.Invoke(active.Session.ChallengeId, $"Could not report race checksum for tick {tick}.");
        }
    }

    private void HandlePeerChecksum(
        PlatformUserId peerId,
        string challengeId,
        long tick,
        string checksum)
    {
        if (!_connection.IsLocalHost ||
            !_active.TryGetValue(challengeId, out var active) ||
            !active.Session.TryGetParticipantId(peerId, out _))
        {
            return;
        }

        if (tick > active.Session.CurrentTick + MultiplayerRaceLockstepSession.MaxFutureCommandTicks)
        {
            SyncIssue?.Invoke(challengeId, "Peer reported a checksum implausibly far ahead of the host.");
            return;
        }

        if (!active.PeerChecksums.TryGetValue(tick, out var peers))
        {
            peers = new Dictionary<PlatformUserId, string>();
            active.PeerChecksums.Add(tick, peers);
        }
        peers[peerId] = checksum;

        if (active.HostChecksums.TryGetValue(tick, out var hostChecksum))
            ComparePeerChecksum(active, tick, peerId, checksum, hostChecksum);
    }

    private void CompareQueuedPeerChecksums(ActiveRace active, long tick, string hostChecksum)
    {
        if (!active.PeerChecksums.TryGetValue(tick, out var peers))
            return;

        foreach (var pair in peers)
            ComparePeerChecksum(active, tick, pair.Key, pair.Value, hostChecksum);
    }

    private void ComparePeerChecksum(
        ActiveRace active,
        long tick,
        PlatformUserId peerId,
        string peerChecksum,
        string hostChecksum)
    {
        if (string.Equals(peerChecksum, hostChecksum, StringComparison.Ordinal))
        {
            PeerChecksumMatched?.Invoke(active.Session.ChallengeId, tick, peerId);
            return;
        }

        DesyncDetected?.Invoke(new MultiplayerRaceDesync(
            active.Session.ChallengeId,
            tick,
            peerId,
            hostChecksum,
            peerChecksum));
    }

    private void HandleChallengeChanged(ChallengeSnapshot snapshot)
    {
        if (snapshot.Kind != ChallengeKind.Race ||
            snapshot.Phase is not (ChallengePhase.Cancelled or ChallengePhase.Completed))
        {
            return;
        }

        _active.Remove(snapshot.ChallengeId);
    }

    private bool IsPacketFromCurrentHost(PlatformUserId sender)
        => _connection.CurrentLobby?.OwnerId == sender;

    private static bool IsLockstepMessageType(string messageType)
        => string.Equals(messageType, MultiplayerRaceLockstepProtocol.CheerRequestType, StringComparison.Ordinal) ||
           string.Equals(messageType, MultiplayerRaceLockstepProtocol.ScheduledCommandType, StringComparison.Ordinal) ||
           string.Equals(messageType, MultiplayerRaceLockstepProtocol.ChecksumType, StringComparison.Ordinal);

    private static RaceLockstepAdvanceResult EmptyAdvanceResult()
        => new(Array.Empty<RaceSimulationEvent>(), Array.Empty<RaceCommandApplication>());

    private bool RememberMessage(Guid messageId)
    {
        if (messageId == Guid.Empty || !_recentMessageIds.Add(messageId))
            return false;
        _recentMessageOrder.Enqueue(messageId);
        while (_recentMessageOrder.Count > RecentMessageLimit)
            _recentMessageIds.Remove(_recentMessageOrder.Dequeue());
        return true;
    }

    private void Reset()
    {
        _active.Clear();
        _recentMessageOrder.Clear();
        _recentMessageIds.Clear();
    }
}
