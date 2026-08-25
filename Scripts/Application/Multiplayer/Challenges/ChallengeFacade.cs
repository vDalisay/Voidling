using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Challenges;

public sealed record ChallengeParticipantView(
    string DisplayName,
    bool IsLocal,
    bool IsCreator,
    bool IsHost);

public sealed record ChallengeView(
    string ChallengeId,
    ChallengeKind Kind,
    ChallengePhase Phase,
    string CreatorDisplayName,
    int MaxParticipants,
    IReadOnlyList<ChallengeParticipantView> Participants,
    bool LocalParticipating,
    bool CanJoin,
    bool CanLeave,
    bool CanCancel);

public sealed record ChallengeHubViewState(
    MultiplayerAvailability Availability,
    bool IsConnected,
    bool CanOffer,
    IReadOnlyList<ChallengeView> Challenges);

/// <summary>
/// Presentation-safe façade for connected-Garden challenge discovery and participation. Raw platform
/// IDs remain below this boundary; the UI receives display names, permissions and typed player intent.
/// </summary>
public sealed class ChallengeFacade
{
    private readonly MultiplayerConnectionService _connection;
    private readonly ChallengeCoordinator _challenges;

    public ChallengeFacade(
        MultiplayerConnectionService connection,
        ChallengeCoordinator challenges)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _challenges = challenges ?? throw new ArgumentNullException(nameof(challenges));

        _connection.LobbyChanged += _ => RaiseStateChanged();
        _connection.LobbyLeft += RaiseStateChanged;
        _challenges.ChallengeChanged += _ => RaiseStateChanged();
        _challenges.ChallengesReset += RaiseStateChanged;
    }

    public event Action<ChallengeHubViewState>? StateChanged;

    public ChallengeHubViewState Current => BuildState();

    public ChallengeOperationResult OfferRace(int maxParticipants = ChallengeValidation.MaxParticipants)
        => _challenges.OfferChallenge(ChallengeKind.Race, maxParticipants);

    public ChallengeOperationResult Join(string challengeId)
        => _challenges.JoinChallenge(challengeId);

    public ChallengeOperationResult Leave(string challengeId)
        => _challenges.LeaveChallenge(challengeId);

    public ChallengeOperationResult Cancel(string challengeId)
        => _challenges.CancelChallenge(challengeId);

    private ChallengeHubViewState BuildState()
    {
        var availability = _connection.IsAvailable
            ? MultiplayerAvailability.Available
            : MultiplayerAvailability.Unavailable(
                _connection.UnavailableReason ?? "Multiplayer is unavailable.");
        var lobby = _connection.CurrentLobby;
        var local = _connection.LocalUser;
        if (lobby == null || local == null)
        {
            return new ChallengeHubViewState(
                availability,
                IsConnected: false,
                CanOffer: false,
                Array.Empty<ChallengeView>());
        }

        var active = _challenges.Challenges
            .Where(challenge => challenge.Phase is not (ChallengePhase.Completed or ChallengePhase.Cancelled))
            .ToArray();
        var localActive = active.Any(challenge => challenge.Contains(local.Id));
        var names = lobby.Members.ToDictionary(
            member => member.User.Id,
            member => member.User.DisplayName);

        var views = active
            .Select(challenge =>
            {
                var localParticipating = challenge.Contains(local.Id);
                var participantIds = challenge.Participants ?? Array.Empty<PlatformUserId>();
                var participants = participantIds
                    .Select(userId => new ChallengeParticipantView(
                        DisplayNameFor(userId, names),
                        IsLocal: userId == local.Id,
                        IsCreator: userId == challenge.CreatorId,
                        IsHost: userId == lobby.OwnerId))
                    .ToArray();
                var joinablePhase = challenge.Phase is ChallengePhase.Offered or ChallengePhase.Forming;
                var canJoin = joinablePhase &&
                              !localParticipating &&
                              !localActive &&
                              participantIds.Length < challenge.MaxParticipants;
                var canLeave = localParticipating &&
                               challenge.Phase is not (ChallengePhase.Completed or ChallengePhase.Cancelled);
                var canCancel = challenge.Phase is not (ChallengePhase.Completed or ChallengePhase.Cancelled) &&
                                (challenge.CreatorId == local.Id || lobby.OwnerId == local.Id);

                return new ChallengeView(
                    challenge.ChallengeId,
                    challenge.Kind,
                    challenge.Phase,
                    DisplayNameFor(challenge.CreatorId, names),
                    challenge.MaxParticipants,
                    participants,
                    localParticipating,
                    canJoin,
                    canLeave,
                    canCancel);
            })
            .OrderBy(view => view.Phase)
            .ThenBy(view => view.ChallengeId, StringComparer.Ordinal)
            .ToArray();

        return new ChallengeHubViewState(
            availability,
            IsConnected: true,
            CanOffer: !localActive,
            views);
    }

    private static string DisplayNameFor(
        PlatformUserId userId,
        IReadOnlyDictionary<PlatformUserId, string> names)
        => names.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Player {userId.Value}";

    private void RaiseStateChanged()
        => StateChanged?.Invoke(BuildState());
}
