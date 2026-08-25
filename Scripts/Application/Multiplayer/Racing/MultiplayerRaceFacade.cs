using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Racing;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Racing;

public sealed record MultiplayerRacePreparationView(
    bool Exists,
    string ChallengeId,
    ChallengePhase Phase,
    int ParticipantCount,
    int MaxParticipants,
    bool CanSelectVoidling,
    string SelectedCreatureId,
    string SelectedCreatureName,
    bool CanRequestStart,
    bool AllSelectionsReady,
    bool IsLocalCreator,
    bool IsLocalHost,
    string? Error);

public sealed record MultiplayerRaceParticipantView(
    string ParticipantId,
    string DisplayName,
    string TintHex,
    bool HasAngelMutation,
    int OtherMutationCount,
    bool IsLocal,
    float X,
    float Progress,
    float CurrentStamina,
    float MaxStamina,
    float DelaySeconds,
    float CheerSeconds,
    float GlideEndX,
    int NextObstacleIndex,
    RaceTerrain Terrain,
    bool Finished,
    int? Placement);

public sealed record MultiplayerRaceFrameView(
    string ChallengeId,
    long CurrentTick,
    bool IsComplete,
    IReadOnlyList<MultiplayerRaceParticipantView> Participants);

/// <summary>
/// Application façade spanning race selection, synchronized start, and deterministic lockstep
/// presentation. It never owns visuals; presentation advances fixed ticks and reads immutable views.
/// </summary>
public sealed class MultiplayerRaceFacade
{
    private readonly MultiplayerConnectionService _connection;
    private readonly ChallengeCoordinator _challenges;
    private readonly MultiplayerRaceStartCoordinator _starts;
    private readonly MultiplayerRaceLockstepCoordinator _lockstep;
    private readonly Func<GameStateData> _stateProvider;
    private readonly Dictionary<string, string> _localSelections = new(StringComparer.Ordinal);
    private readonly HashSet<string> _allSelectionsReady = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResolvedMultiplayerRace> _resolved = new(StringComparer.Ordinal);

    public MultiplayerRaceFacade(
        MultiplayerConnectionService connection,
        ChallengeCoordinator challenges,
        MultiplayerRaceStartCoordinator starts,
        MultiplayerRaceLockstepCoordinator lockstep,
        Func<GameStateData> stateProvider)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _challenges = challenges ?? throw new ArgumentNullException(nameof(challenges));
        _starts = starts ?? throw new ArgumentNullException(nameof(starts));
        _lockstep = lockstep ?? throw new ArgumentNullException(nameof(lockstep));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));

        _starts.SelectionAccepted += HandleSelectionAccepted;
        _starts.RacePreparationReady += HandlePreparationReady;
        _starts.RaceReadyToLaunch += HandleRaceReady;
        _starts.RacePreparationFailed += HandlePreparationFailed;
        _challenges.ChallengeChanged += HandleChallengeChanged;
        _challenges.ChallengesReset += Reset;
        _connection.LobbyLeft += Reset;
    }

    public event Action<string>? PreparationChanged;
    public event Action<ResolvedMultiplayerRace>? RaceReadyToLaunch;
    public event Action<string, string>? RacePreparationFailed;

    public MultiplayerRacePreparationView GetPreparation(string challengeId)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        var challenge = FindRaceChallenge(challengeId);
        if (local == null || lobby == null || challenge == null)
        {
            return new MultiplayerRacePreparationView(
                false,
                challengeId ?? string.Empty,
                ChallengePhase.Cancelled,
                0,
                0,
                false,
                string.Empty,
                string.Empty,
                false,
                false,
                false,
                false,
                "Race challenge is not available in the current connected Garden.");
        }

        var localParticipating = challenge.Contains(local.Id);
        var selectablePhase = challenge.Phase is ChallengePhase.Offered or ChallengePhase.Forming;
        _localSelections.TryGetValue(challenge.ChallengeId, out var selectedId);
        selectedId ??= string.Empty;
        var selectedName = _stateProvider().Voidlings.FirstOrDefault(value =>
            string.Equals(value.Id, selectedId, StringComparison.Ordinal))?.Name ?? string.Empty;
        var isCreator = challenge.CreatorId == local.Id;
        var isHost = lobby.OwnerId == local.Id;
        var canRequestStart = localParticipating &&
                              selectablePhase &&
                              challenge.Participants.Length >= 2 &&
                              (isCreator || isHost) &&
                              selectedId.Length > 0;
        if (isHost && !_allSelectionsReady.Contains(challenge.ChallengeId))
            canRequestStart = false;

        return new MultiplayerRacePreparationView(
            true,
            challenge.ChallengeId,
            challenge.Phase,
            challenge.Participants.Length,
            challenge.MaxParticipants,
            localParticipating && selectablePhase,
            selectedId,
            selectedName,
            canRequestStart,
            _allSelectionsReady.Contains(challenge.ChallengeId),
            isCreator,
            isHost,
            null);
    }

    public MultiplayerRaceOperationResult SubmitSelection(string challengeId, string creatureId)
    {
        var result = _starts.SubmitSelection(_stateProvider(), challengeId, creatureId);
        if (!result.Success)
            return result;

        _localSelections[challengeId] = creatureId;
        PreparationChanged?.Invoke(challengeId);
        return result;
    }

    public MultiplayerRaceOperationResult RequestStart(string challengeId)
        => _starts.RequestStart(challengeId);

    public MultiplayerRaceOperationResult RequestCheer(string challengeId)
        => _lockstep.RequestCheer(challengeId);

    public MultiplayerRaceOperationResult AdvanceFixedSteps(string challengeId, int stepCount)
    {
        if (_lockstep.TryAdvanceFixedSteps(challengeId, stepCount, out _, out var error))
            return MultiplayerRaceOperationResult.Succeeded;
        return MultiplayerRaceOperationResult.Failed(error ?? "Multiplayer race could not advance.");
    }

    public bool TryGetFrame(string challengeId, out MultiplayerRaceFrameView frame)
    {
        frame = default!;
        if (!_resolved.TryGetValue(challengeId, out var race) ||
            !_lockstep.TryGetSession(challengeId, out var session))
        {
            return false;
        }

        var local = _connection.LocalUser;
        var finishOrder = session.Simulation.FinishOrder;
        var finishPlacements = finishOrder
            .Select((participantId, index) => (participantId, placement: index + 1))
            .ToDictionary(value => value.participantId, value => value.placement, StringComparer.Ordinal);
        var distance = Math.Max(1.0f, race.Course.EndX - race.Course.StartX);
        var participants = race.Start.Entrants
            .Select(entrant =>
            {
                var state = session.Simulation.GetState(entrant.Participant.CreatureId);
                var hasPlacement = finishPlacements.TryGetValue(
                    entrant.Participant.CreatureId,
                    out var placement);
                return new MultiplayerRaceParticipantView(
                    entrant.Participant.CreatureId,
                    entrant.Participant.DisplayName,
                    entrant.Participant.TintHex,
                    entrant.HasAngelMutation,
                    entrant.OtherMutationCount,
                    local != null && entrant.OwnerId == local.Id,
                    state.X,
                    Math.Clamp((state.X - race.Course.StartX) / distance, 0.0f, 1.0f),
                    state.CurrentStamina,
                    state.MaxStamina,
                    state.DelaySeconds,
                    state.CheerSeconds,
                    state.GlideEndX,
                    state.NextObstacleIndex,
                    state.Terrain,
                    state.Finished,
                    hasPlacement ? placement : null);
            })
            .ToArray();

        frame = new MultiplayerRaceFrameView(
            challengeId,
            session.CurrentTick,
            session.IsComplete,
            participants);
        return true;
    }

    private ChallengeSnapshot? FindRaceChallenge(string challengeId)
        => _challenges.Challenges.FirstOrDefault(value =>
            value.Kind == ChallengeKind.Race &&
            string.Equals(value.ChallengeId, challengeId, StringComparison.Ordinal));

    private void HandleSelectionAccepted(string challengeId, PlatformUserId ownerId)
    {
        if (_connection.LocalUser?.Id == ownerId && !_localSelections.ContainsKey(challengeId))
            _localSelections[challengeId] = string.Empty;
        PreparationChanged?.Invoke(challengeId);
    }

    private void HandlePreparationReady(string challengeId)
    {
        _allSelectionsReady.Add(challengeId);
        PreparationChanged?.Invoke(challengeId);
    }

    private void HandleRaceReady(ResolvedMultiplayerRace race)
    {
        _resolved[race.Start.ChallengeId] = race;
        RaceReadyToLaunch?.Invoke(race);
    }

    private void HandlePreparationFailed(string challengeId, string error)
    {
        RacePreparationFailed?.Invoke(challengeId, error);
        PreparationChanged?.Invoke(challengeId);
    }

    private void HandleChallengeChanged(ChallengeSnapshot challenge)
    {
        if (challenge.Kind != ChallengeKind.Race)
            return;

        if (challenge.Phase is ChallengePhase.Completed or ChallengePhase.Cancelled)
        {
            _localSelections.Remove(challenge.ChallengeId);
            _allSelectionsReady.Remove(challenge.ChallengeId);
            _resolved.Remove(challenge.ChallengeId);
        }
        PreparationChanged?.Invoke(challenge.ChallengeId);
    }

    private void Reset()
    {
        _localSelections.Clear();
        _allSelectionsReady.Clear();
        _resolved.Clear();
    }
}
