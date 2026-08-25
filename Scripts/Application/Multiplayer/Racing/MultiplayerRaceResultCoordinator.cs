using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Racing;

public sealed record MultiplayerRaceResultChecksumMismatch(
    string ChallengeId,
    long HostTick,
    long LocalTick,
    string HostChecksum,
    string LocalChecksum);

/// <summary>
/// Distributes the host's canonical completed race result and waits for every participant to finish
/// locally and acknowledge it before closing the challenge. A local checksum mismatch is diagnostic:
/// the casual trust model still accepts the host result for presentation/reward handoff, while the
/// mismatch remains visible for deterministic-simulation debugging.
/// </summary>
public sealed class MultiplayerRaceResultCoordinator
{
    private const int RecentMessageLimit = 512;

    private sealed class RaceContext
    {
        public RaceContext(ResolvedMultiplayerRace race, MultiplayerRaceLockstepSession session)
        {
            Race = race;
            Session = session;
        }

        public ResolvedMultiplayerRace Race { get; }
        public MultiplayerRaceLockstepSession Session { get; }
        public MultiplayerRaceResult? PendingHostResult { get; set; }
        public bool HostResultPublished { get; set; }
        public bool LocalResultAccepted { get; set; }
        public HashSet<PlatformUserId> Acknowledged { get; } = new();
    }

    private readonly MultiplayerConnectionService _connection;
    private readonly ChallengeCoordinator _challenges;
    private readonly MultiplayerRaceLockstepCoordinator _lockstep;
    private readonly MultiplayerRaceResultFactory _resultFactory = new();
    private readonly Dictionary<string, RaceContext> _races = new(StringComparer.Ordinal);
    private readonly Queue<Guid> _recentMessageOrder = new();
    private readonly HashSet<Guid> _recentMessageIds = new();

    public MultiplayerRaceResultCoordinator(
        MultiplayerConnectionService connection,
        ChallengeCoordinator challenges,
        MultiplayerRaceLockstepCoordinator lockstep)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _challenges = challenges ?? throw new ArgumentNullException(nameof(challenges));
        _lockstep = lockstep ?? throw new ArgumentNullException(nameof(lockstep));

        _connection.PacketReceived += HandlePacket;
        _connection.LobbyLeft += Reset;
        _challenges.ChallengeChanged += HandleChallengeChanged;
        _challenges.ChallengesReset += Reset;
        _lockstep.LocalChecksumReported += HandleLocalChecksumReported;
    }

    public event Action<MultiplayerRaceResult>? ValidatedResultReady;
    public event Action<MultiplayerRaceResultChecksumMismatch>? ChecksumMismatch;
    public event Action<string, string>? ResultHandshakeIssue;
    public event Action<string>? ResultConsensusCompleted;
    public event Action<string>? ProtocolRejected;

    public bool AttachRace(ResolvedMultiplayerRace race, out string? error)
    {
        ArgumentNullException.ThrowIfNull(race);
        error = null;
        if (_races.ContainsKey(race.Start.ChallengeId))
            return true;
        if (!_lockstep.TryGetSession(race.Start.ChallengeId, out var session))
        {
            error = "Race lockstep session must be attached before result coordination.";
            return false;
        }

        var local = _connection.LocalUser;
        if (local == null || !race.Start.Entrants.Any(value => value.OwnerId == local.Id))
        {
            error = "Local player is not an entrant in this multiplayer race.";
            return false;
        }

        var context = new RaceContext(race, session);
        _races.Add(race.Start.ChallengeId, context);
        if (session.IsComplete)
            HandleLocalCompletion(context);
        return true;
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Challenge || _connection.CurrentLobby == null)
            return;

        if (MultiplayerRaceResultProtocol.TryDecodeFinalResult(
                packet.Payload.Span,
                packet.Sender,
                out var resultMessageId,
                out var result))
        {
            if (IsPacketFromCurrentHost(packet.Sender) && RememberMessage(resultMessageId))
                HandleHostResult(result);
            return;
        }

        if (MultiplayerRaceResultProtocol.TryDecodeResultAck(
                packet.Payload.Span,
                packet.Sender,
                out var ackMessageId,
                out var challengeId,
                out var accepted,
                out var error))
        {
            if (_connection.IsLocalHost && RememberMessage(ackMessageId))
                HandleHostAck(packet.Sender, challengeId, accepted, error);
            return;
        }

        if (MultiplayerProtocol.TryPeekMessageType(packet.Payload.Span, out var messageType) &&
            messageType.StartsWith("result.race.", StringComparison.Ordinal))
        {
            ProtocolRejected?.Invoke("Race result packet was malformed or unsupported.");
        }
    }

    private void HandleLocalChecksumReported(string challengeId, long _, string __)
    {
        if (!_races.TryGetValue(challengeId, out var context) || !context.Session.IsComplete)
            return;
        HandleLocalCompletion(context);
    }

    private void HandleLocalCompletion(RaceContext context)
    {
        if (_connection.IsLocalHost)
        {
            PublishHostResult(context);
            return;
        }

        if (context.PendingHostResult != null)
            AcceptHostResult(context, context.PendingHostResult);
    }

    private void PublishHostResult(RaceContext context)
    {
        if (context.HostResultPublished)
            return;

        var host = _connection.LocalUser;
        if (host == null)
        {
            ResultHandshakeIssue?.Invoke(context.Race.Start.ChallengeId, "Race host identity is unavailable at completion.");
            return;
        }

        MultiplayerRaceResult result;
        try
        {
            result = _resultFactory.Create(context.Race, context.Session);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ResultHandshakeIssue?.Invoke(context.Race.Start.ChallengeId, exception.Message);
            return;
        }

        context.HostResultPublished = true;
        context.PendingHostResult = result;
        context.LocalResultAccepted = true;
        context.Acknowledged.Add(host.Id);
        ValidatedResultReady?.Invoke(result);

        foreach (var entrant in context.Race.Start.Entrants)
        {
            if (entrant.OwnerId == host.Id)
                continue;

            if (!_connection.TrySend(
                    entrant.OwnerId,
                    NetworkChannel.Challenge,
                    MultiplayerRaceResultProtocol.EncodeFinalResult(host, result),
                    DeliveryMode.Reliable))
            {
                ResultHandshakeIssue?.Invoke(
                    result.ChallengeId,
                    $"Could not send final race result to participant {entrant.OwnerId.Value}.");
            }
        }

        TryCompleteConsensus(context);
    }

    private void HandleHostResult(MultiplayerRaceResult result)
    {
        if (!_races.TryGetValue(result.ChallengeId, out var context))
            return;
        if (!ResultMatchesRace(result, context.Race))
        {
            SendAck(result.ChallengeId, false, "Host result entrants do not match the immutable race start.");
            ResultHandshakeIssue?.Invoke(result.ChallengeId, "Host result entrants do not match the immutable race start.");
            return;
        }

        context.PendingHostResult = result;
        if (context.Session.IsComplete)
            AcceptHostResult(context, result);
    }

    private void AcceptHostResult(RaceContext context, MultiplayerRaceResult result)
    {
        if (context.LocalResultAccepted)
        {
            SendAck(result.ChallengeId, true, null);
            return;
        }
        if (!ResultMatchesRace(result, context.Race))
        {
            SendAck(result.ChallengeId, false, "Host result does not match the immutable race start.");
            return;
        }

        var localChecksum = context.Session.ComputeDeterministicChecksum();
        if (result.FinalTick != context.Session.CurrentTick ||
            !string.Equals(result.FinalChecksum, localChecksum, StringComparison.Ordinal))
        {
            ChecksumMismatch?.Invoke(new MultiplayerRaceResultChecksumMismatch(
                result.ChallengeId,
                result.FinalTick,
                context.Session.CurrentTick,
                result.FinalChecksum,
                localChecksum));
        }

        context.LocalResultAccepted = true;
        ValidatedResultReady?.Invoke(result);
        SendAck(result.ChallengeId, true, null);
    }

    private void SendAck(string challengeId, bool accepted, string? error)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null || _connection.IsLocalHost)
            return;

        if (!_connection.TrySend(
                lobby.OwnerId,
                NetworkChannel.Challenge,
                MultiplayerRaceResultProtocol.EncodeResultAck(local, challengeId, accepted, error),
                DeliveryMode.Reliable))
        {
            ResultHandshakeIssue?.Invoke(challengeId, "Could not acknowledge final race result to the host.");
        }
    }

    private void HandleHostAck(
        PlatformUserId sender,
        string challengeId,
        bool accepted,
        string? error)
    {
        if (!_connection.IsLocalHost ||
            !_races.TryGetValue(challengeId, out var context) ||
            !context.HostResultPublished ||
            !context.Race.Start.Entrants.Any(value => value.OwnerId == sender))
        {
            return;
        }

        context.Acknowledged.Add(sender);
        if (!accepted)
        {
            ResultHandshakeIssue?.Invoke(
                challengeId,
                $"Participant {sender.Value} rejected the host result: {error ?? "no reason supplied"}");
        }
        TryCompleteConsensus(context);
    }

    private void TryCompleteConsensus(RaceContext context)
    {
        if (!_connection.IsLocalHost ||
            context.Race.Start.Entrants.Any(value => !context.Acknowledged.Contains(value.OwnerId)))
        {
            return;
        }

        var completed = _challenges.CompleteChallenge(context.Race.Start.ChallengeId);
        if (!completed.Success)
        {
            ResultHandshakeIssue?.Invoke(
                context.Race.Start.ChallengeId,
                completed.Error ?? "Challenge could not enter the completed phase.");
            return;
        }

        ResultConsensusCompleted?.Invoke(context.Race.Start.ChallengeId);
    }

    private static bool ResultMatchesRace(
        MultiplayerRaceResult result,
        ResolvedMultiplayerRace race)
    {
        if (!string.Equals(result.ChallengeId, race.Start.ChallengeId, StringComparison.Ordinal) ||
            result.Placements.Length != race.Start.Entrants.Length)
        {
            return false;
        }

        var expected = race.Start.Entrants.ToDictionary(
            value => value.OwnerId,
            value => value.Participant.CreatureId);
        foreach (var placement in result.Placements)
        {
            if (!expected.TryGetValue(placement.OwnerId, out var participantId) ||
                !string.Equals(participantId, placement.ParticipantId, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private void HandleChallengeChanged(ChallengeSnapshot snapshot)
    {
        if (snapshot.Kind != ChallengeKind.Race ||
            snapshot.Phase is not (ChallengePhase.Cancelled or ChallengePhase.Completed))
        {
            return;
        }
        _races.Remove(snapshot.ChallengeId);
    }

    private bool IsPacketFromCurrentHost(PlatformUserId sender)
        => _connection.CurrentLobby?.OwnerId == sender;

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
        _races.Clear();
        _recentMessageOrder.Clear();
        _recentMessageIds.Clear();
    }
}