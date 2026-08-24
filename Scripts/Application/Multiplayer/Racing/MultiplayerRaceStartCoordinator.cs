using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Racing;

/// <summary>
/// Freezes one locally owned Voidling per challenge participant, lets the lobby host assemble the
/// immutable deterministic race entry, and requires every participant to validate/acknowledge the
/// exact same start bytes before ChallengeCoordinator transitions the race to Running.
/// </summary>
public sealed class MultiplayerRaceStartCoordinator
{
    private const int RecentMessageLimit = 512;

    private sealed class HostedPreparation
    {
        public Dictionary<PlatformUserId, MultiplayerRaceEntrant> Selections { get; } = new();
        public byte[]? ProposedBytes { get; set; }
        public string? ProposedHash { get; set; }
        public HashSet<PlatformUserId> Acknowledged { get; } = new();
    }

    private readonly MultiplayerConnectionService _connection;
    private readonly ChallengeCoordinator _challenges;
    private readonly MultiplayerRaceSelectionFactory _selectionFactory;
    private readonly MultiplayerRaceEntryFactory _entryFactory;
    private readonly Dictionary<string, HostedPreparation> _hosted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Hash, ResolvedMultiplayerRace Race)> _validatedStarts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _launchedHashes = new(StringComparer.Ordinal);
    private readonly Queue<Guid> _recentMessageOrder = new();
    private readonly HashSet<Guid> _recentMessageIds = new();

    public MultiplayerRaceStartCoordinator(
        MultiplayerConnectionService connection,
        ChallengeCoordinator challenges,
        GameBalanceRules rules)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _challenges = challenges ?? throw new ArgumentNullException(nameof(challenges));
        ArgumentNullException.ThrowIfNull(rules);
        _selectionFactory = new MultiplayerRaceSelectionFactory(rules);
        _entryFactory = new MultiplayerRaceEntryFactory(rules);

        _connection.PacketReceived += HandlePacket;
        _connection.LobbyLeft += Reset;
        _challenges.ChallengeChanged += HandleChallengeChanged;
        _challenges.ChallengesReset += Reset;
    }

    public event Action<string, PlatformUserId>? SelectionAccepted;
    public event Action<string>? RacePreparationReady;
    public event Action<ResolvedMultiplayerRace>? RaceReadyToLaunch;
    public event Action<string, string>? RacePreparationFailed;
    public event Action<string>? ProtocolRejected;

    public MultiplayerRaceOperationResult SubmitSelection(
        GameStateData state,
        string challengeId,
        string creatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var context = FindLocalRaceChallenge(challengeId);
        if (context.Error != null)
            return MultiplayerRaceOperationResult.Failed(context.Error);
        if (context.Snapshot!.Phase is not (ChallengePhase.Offered or ChallengePhase.Forming))
            return MultiplayerRaceOperationResult.Failed("Race selections are locked once start preparation begins.");
        if (!context.Snapshot.Contains(context.Local!.Id))
            return MultiplayerRaceOperationResult.Failed("Local player is not participating in this race challenge.");
        if (!_selectionFactory.TryCreate(
                state,
                context.Local.Id,
                creatureId,
                out var entrant,
                out var error))
        {
            return MultiplayerRaceOperationResult.Failed(error!);
        }

        if (_connection.IsLocalHost)
        {
            if (!HandleHostSelection(context.Local.Id, challengeId, entrant))
                return MultiplayerRaceOperationResult.Failed("Lobby host rejected the race selection.");
        }
        else if (!_connection.TrySend(
                     context.Lobby!.OwnerId,
                     NetworkChannel.Challenge,
                     MultiplayerRaceProtocol.EncodeSelection(context.Local, challengeId, entrant),
                     DeliveryMode.Reliable))
        {
            return MultiplayerRaceOperationResult.Failed("Could not send the race selection to the lobby host.");
        }

        return MultiplayerRaceOperationResult.Succeeded;
    }

    public MultiplayerRaceOperationResult RequestStart(string challengeId)
    {
        var context = FindLocalRaceChallenge(challengeId);
        if (context.Error != null)
            return MultiplayerRaceOperationResult.Failed(context.Error);
        if (context.Snapshot!.CreatorId != context.Local!.Id && !_connection.IsLocalHost)
            return MultiplayerRaceOperationResult.Failed("Only the challenge creator or lobby host can request race start.");
        if (context.Snapshot.Phase is not (ChallengePhase.Offered or ChallengePhase.Forming))
            return MultiplayerRaceOperationResult.Failed("Race start preparation has already begun.");

        if (_connection.IsLocalHost)
        {
            return TryHostPrepareStart(context.Local.Id, challengeId, out var error)
                ? MultiplayerRaceOperationResult.Succeeded
                : MultiplayerRaceOperationResult.Failed(error ?? "Race is not ready to start.");
        }

        if (!_connection.TrySend(
                context.Lobby!.OwnerId,
                NetworkChannel.Challenge,
                MultiplayerRaceProtocol.EncodeStartRequest(context.Local, challengeId),
                DeliveryMode.Reliable))
        {
            return MultiplayerRaceOperationResult.Failed("Could not send the race start request to the lobby host.");
        }

        return MultiplayerRaceOperationResult.Succeeded;
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Challenge || _connection.CurrentLobby == null)
            return;

        if (MultiplayerRaceProtocol.TryDecodeSelection(
                packet.Payload.Span,
                packet.Sender,
                out var selectionMessageId,
                out var challengeId,
                out var entrant))
        {
            if (_connection.IsLocalHost && RememberMessage(selectionMessageId))
                HandleHostSelection(packet.Sender, challengeId, entrant);
            return;
        }

        if (MultiplayerRaceProtocol.TryDecodeStartRequest(
                packet.Payload.Span,
                packet.Sender,
                out var startMessageId,
                out challengeId))
        {
            if (_connection.IsLocalHost && RememberMessage(startMessageId) &&
                !TryHostPrepareStart(packet.Sender, challengeId, out var error))
            {
                RacePreparationFailed?.Invoke(challengeId, error ?? "Race is not ready to start.");
            }
            return;
        }

        if (MultiplayerRaceProtocol.TryDecodeStartProposal(
                packet.Payload.Span,
                packet.Sender,
                out var proposalMessageId,
                out challengeId,
                out var startHash,
                out var startBytes))
        {
            if (IsPacketFromCurrentHost(packet.Sender) && RememberMessage(proposalMessageId))
                HandleStartProposal(challengeId, startHash, startBytes);
            return;
        }

        if (MultiplayerRaceProtocol.TryDecodeStartAck(
                packet.Payload.Span,
                packet.Sender,
                out var ackMessageId,
                out challengeId,
                out startHash,
                out var success,
                out var ackError))
        {
            if (_connection.IsLocalHost && RememberMessage(ackMessageId))
                HandleHostAck(packet.Sender, challengeId, startHash, success, ackError);
            return;
        }

        if (MultiplayerProtocol.TryPeekMessageType(packet.Payload.Span, out var messageType) &&
            messageType.StartsWith("race.", StringComparison.Ordinal))
        {
            ProtocolRejected?.Invoke("Race packet was malformed or used an unsupported race message type.");
        }
    }

    private bool HandleHostSelection(
        PlatformUserId sender,
        string challengeId,
        MultiplayerRaceEntrant entrant)
    {
        if (!_connection.IsLocalHost ||
            entrant.OwnerId != sender ||
            !MultiplayerRaceValidation.IsValidEntrant(entrant, out _) ||
            !TryGetRaceChallenge(challengeId, out var challenge) ||
            challenge.Phase is not (ChallengePhase.Offered or ChallengePhase.Forming) ||
            !challenge.Contains(sender) ||
            !_connection.IsLobbyMember(sender))
        {
            return false;
        }

        var preparation = GetOrCreatePreparation(challengeId);
        preparation.Selections[sender] = entrant;
        preparation.ProposedBytes = null;
        preparation.ProposedHash = null;
        preparation.Acknowledged.Clear();
        SelectionAccepted?.Invoke(challengeId, sender);

        if (challenge.Participants.All(preparation.Selections.ContainsKey))
            RacePreparationReady?.Invoke(challengeId);
        return true;
    }

    private bool TryHostPrepareStart(
        PlatformUserId requester,
        string challengeId,
        out string? error)
    {
        error = null;
        if (!_connection.IsLocalHost ||
            !TryGetRaceChallenge(challengeId, out var challenge) ||
            challenge.Phase is not (ChallengePhase.Offered or ChallengePhase.Forming))
        {
            error = "Race challenge is not in a startable phase.";
            return false;
        }

        var host = _connection.LocalUser;
        if (host == null || (requester != challenge.CreatorId && requester != host.Id))
        {
            error = "Only the challenge creator or lobby host can request race start.";
            return false;
        }
        if (challenge.Participants.Length < 2 ||
            challenge.Participants.Any(user => !_connection.IsLobbyMember(user)))
        {
            error = "Every race participant must still be connected.";
            return false;
        }

        var preparation = GetOrCreatePreparation(challengeId);
        if (challenge.Participants.Any(user => !preparation.Selections.ContainsKey(user)))
        {
            error = "Every race participant must select a Voidling before the race can start.";
            return false;
        }

        var entrants = challenge.Participants
            .Select(user => preparation.Selections[user])
            .ToArray();
        MultiplayerRaceStartPayload start;
        try
        {
            start = _entryFactory.CreateStartPayload(challengeId, entrants);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }

        var bytes = MultiplayerRaceStartCodec.Encode(start);
        if (bytes.Length > ChallengeValidation.MaxStartPayloadBytes)
        {
            error = "Canonical race start payload exceeds the challenge packet budget.";
            return false;
        }
        if (!_entryFactory.TryResolve(start, out _, out error))
            return false;

        var ready = _challenges.MarkChallengeReady(challengeId);
        if (!ready.Success)
        {
            error = ready.Error;
            return false;
        }

        preparation.ProposedBytes = bytes;
        preparation.ProposedHash = MultiplayerRaceStartCodec.ComputeHash(bytes);
        preparation.Acknowledged.Clear();

        foreach (var participant in challenge.Participants)
        {
            if (host.Id == participant)
            {
                HandleStartProposal(challengeId, preparation.ProposedHash, bytes);
                continue;
            }

            if (!_connection.TrySend(
                    participant,
                    NetworkChannel.Challenge,
                    MultiplayerRaceProtocol.EncodeStartProposal(
                        host,
                        challengeId,
                        preparation.ProposedHash,
                        bytes),
                    DeliveryMode.Reliable))
            {
                error = "Could not send the canonical race start proposal to every participant.";
                AbortRacePreparation(challengeId, error);
                return false;
            }
        }

        return true;
    }

    private void HandleStartProposal(
        string challengeId,
        string startHash,
        byte[] startBytes)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null ||
            !TryGetRaceChallenge(challengeId, out var challenge) ||
            challenge.Phase != ChallengePhase.Ready ||
            !challenge.Contains(local.Id))
        {
            return;
        }

        var computedHash = MultiplayerRaceStartCodec.ComputeHash(startBytes);
        if (!string.Equals(startHash, computedHash, StringComparison.Ordinal) ||
            !MultiplayerRaceStartCodec.TryDecode(startBytes, out var start, out var error) ||
            !string.Equals(start.ChallengeId, challengeId, StringComparison.Ordinal) ||
            !ParticipantSetsMatch(challenge.Participants, start.Entrants) ||
            !_entryFactory.TryResolve(start, out var resolved, out error))
        {
            SendAckToHost(
                challengeId,
                startHash,
                false,
                error ?? "Race start payload failed local validation.");
            return;
        }

        _validatedStarts[challengeId] = (startHash, resolved);
        SendAckToHost(challengeId, startHash, true, null);
    }

    private void SendAckToHost(
        string challengeId,
        string startHash,
        bool success,
        string? error)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null)
            return;

        if (_connection.IsLocalHost)
        {
            HandleHostAck(local.Id, challengeId, startHash, success, error);
            return;
        }

        _connection.TrySend(
            lobby.OwnerId,
            NetworkChannel.Challenge,
            MultiplayerRaceProtocol.EncodeStartAck(local, challengeId, startHash, success, error),
            DeliveryMode.Reliable);
    }

    private void HandleHostAck(
        PlatformUserId sender,
        string challengeId,
        string startHash,
        bool success,
        string? error)
    {
        if (!_connection.IsLocalHost ||
            !TryGetRaceChallenge(challengeId, out var challenge) ||
            challenge.Phase != ChallengePhase.Ready ||
            !challenge.Contains(sender) ||
            !_hosted.TryGetValue(challengeId, out var preparation) ||
            preparation.ProposedBytes == null ||
            preparation.ProposedHash == null ||
            !string.Equals(preparation.ProposedHash, startHash, StringComparison.Ordinal))
        {
            return;
        }

        if (!success)
        {
            AbortRacePreparation(
                challengeId,
                error ?? "A race participant rejected the canonical start payload.");
            return;
        }

        preparation.Acknowledged.Add(sender);
        if (!challenge.Participants.All(preparation.Acknowledged.Contains))
            return;

        var started = _challenges.StartChallenge(challengeId, preparation.ProposedBytes);
        if (!started.Success)
            AbortRacePreparation(challengeId, started.Error ?? "Challenge could not enter the running phase.");
    }

    private void HandleChallengeChanged(ChallengeSnapshot snapshot)
    {
        if (snapshot.Kind != ChallengeKind.Race)
            return;

        if (_connection.IsLocalHost &&
            snapshot.Phase is ChallengePhase.Offered or ChallengePhase.Forming &&
            _hosted.TryGetValue(snapshot.ChallengeId, out var preparation))
        {
            var participants = snapshot.Participants.ToHashSet();
            foreach (var stale in preparation.Selections.Keys.Where(user => !participants.Contains(user)).ToArray())
                preparation.Selections.Remove(stale);
        }

        if (snapshot.Phase is ChallengePhase.Cancelled or ChallengePhase.Completed)
        {
            _hosted.Remove(snapshot.ChallengeId);
            _validatedStarts.Remove(snapshot.ChallengeId);
            _launchedHashes.Remove(snapshot.ChallengeId);
            return;
        }

        if (snapshot.Phase != ChallengePhase.Running)
            return;

        var bytes = snapshot.StartPayload ?? Array.Empty<byte>();
        var hash = MultiplayerRaceStartCodec.ComputeHash(bytes);
        if (_launchedHashes.TryGetValue(snapshot.ChallengeId, out var launched) &&
            string.Equals(launched, hash, StringComparison.Ordinal))
        {
            return;
        }

        if (!MultiplayerRaceStartCodec.TryDecode(bytes, out var start, out var error) ||
            !string.Equals(start.ChallengeId, snapshot.ChallengeId, StringComparison.Ordinal) ||
            !ParticipantSetsMatch(snapshot.Participants, start.Entrants) ||
            !_entryFactory.TryResolve(start, out var resolved, out error))
        {
            RacePreparationFailed?.Invoke(
                snapshot.ChallengeId,
                error ?? "Running race payload could not be resolved by this client.");
            return;
        }

        if (_validatedStarts.TryGetValue(snapshot.ChallengeId, out var validated) &&
            !string.Equals(validated.Hash, hash, StringComparison.Ordinal))
        {
            RacePreparationFailed?.Invoke(
                snapshot.ChallengeId,
                "Running race payload differs from the payload this client acknowledged.");
            return;
        }

        _validatedStarts[snapshot.ChallengeId] = (hash, resolved);
        _launchedHashes[snapshot.ChallengeId] = hash;
        RaceReadyToLaunch?.Invoke(resolved);
    }

    private void AbortRacePreparation(string challengeId, string reason)
    {
        _hosted.Remove(challengeId);
        _validatedStarts.Remove(challengeId);
        RacePreparationFailed?.Invoke(challengeId, reason);

        if (_connection.IsLocalHost)
            _challenges.CancelChallenge(challengeId);
    }

    private (PlatformUser? Local, LobbySnapshot? Lobby, ChallengeSnapshot? Snapshot, string? Error)
        FindLocalRaceChallenge(string challengeId)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null)
            return (local, lobby, null, "Join a connected Garden before preparing a multiplayer race.");
        if (!TryGetRaceChallenge(challengeId, out var snapshot))
            return (local, lobby, null, "Race challenge is not available in the current connected Garden.");
        if (snapshot.Phase is ChallengePhase.Cancelled or ChallengePhase.Completed or ChallengePhase.Running)
            return (local, lobby, snapshot, "Race challenge is no longer accepting preparation changes.");
        return (local, lobby, snapshot, null);
    }

    private bool TryGetRaceChallenge(string challengeId, out ChallengeSnapshot snapshot)
    {
        snapshot = _challenges.Challenges.FirstOrDefault(value =>
            string.Equals(value.ChallengeId, challengeId, StringComparison.Ordinal))!;
        return snapshot != null && snapshot.Kind == ChallengeKind.Race;
    }

    private HostedPreparation GetOrCreatePreparation(string challengeId)
    {
        if (_hosted.TryGetValue(challengeId, out var existing))
            return existing;
        var created = new HostedPreparation();
        _hosted.Add(challengeId, created);
        return created;
    }

    private bool IsPacketFromCurrentHost(PlatformUserId sender)
        => _connection.CurrentLobby?.OwnerId == sender;

    private static bool ParticipantSetsMatch(
        IEnumerable<PlatformUserId> challengeParticipants,
        IEnumerable<MultiplayerRaceEntrant> entrants)
    {
        var expected = challengeParticipants.ToHashSet();
        var actual = entrants.Select(value => value.OwnerId).ToHashSet();
        return expected.SetEquals(actual) && expected.Count == actual.Count;
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

    private void Reset()
    {
        _hosted.Clear();
        _validatedStarts.Clear();
        _launchedHashes.Clear();
        _recentMessageOrder.Clear();
        _recentMessageIds.Clear();
    }
}
