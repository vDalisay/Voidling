using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Application.Racing;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Leaderboards;

public sealed record DailyFriendRaceStatus(
    string DailyKey,
    bool HasAttempt,
    bool CanStart,
    bool CanResume,
    bool Completed,
    string SelectedCreatureId,
    string SelectedDisplayName,
    int? FinishedMilliseconds,
    string? Error);

public sealed record DailyFriendRaceLaunchResult(
    bool Success,
    bool Resumed,
    string DailyKey,
    RaceEntry? Entry,
    string? Error)
{
    public static DailyFriendRaceLaunchResult Failed(string dailyKey, string error)
        => new(false, false, dailyKey, null, error);
}

public sealed record DailyFriendRaceCompleteResult(
    bool Success,
    bool AlreadyCompleted,
    string DailyKey,
    int? FinishedMilliseconds,
    string? Error);

/// <summary>
/// Application-facing daily-race façade for presentation. It keeps the one-attempt local save as
/// authority, preserves save-before-launch/save-before-projection ordering, and exposes no Steam API.
/// </summary>
public sealed class DailyFriendRaceFacade
{
    private readonly DailyFriendRaceCoordinator _coordinator;
    private readonly Func<GameStateData> _stateProvider;
    private readonly Action _localStateChanged;

    public DailyFriendRaceFacade(
        DailyFriendRaceCoordinator coordinator,
        Func<GameStateData> stateProvider,
        Action localStateChanged)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        _localStateChanged = localStateChanged ?? throw new ArgumentNullException(nameof(localStateChanged));
    }

    public MultiplayerAvailability LeaderboardAvailability => _coordinator.LeaderboardAvailability;

    public DailyFriendRaceStatus GetToday(DateTimeOffset utcNow)
    {
        var dailyKey = DailyFriendRaceService.GetDailyKey(utcNow.ToUniversalTime());
        var attempt = _stateProvider().DailyRaceAttempts.FirstOrDefault(value =>
            string.Equals(value.DailyKey, dailyKey, StringComparison.Ordinal));
        if (attempt == null)
        {
            return new DailyFriendRaceStatus(
                dailyKey,
                HasAttempt: false,
                CanStart: true,
                CanResume: false,
                Completed: false,
                SelectedCreatureId: string.Empty,
                SelectedDisplayName: string.Empty,
                FinishedMilliseconds: null,
                Error: null);
        }

        if (!DailyFriendRaceService.IsStructurallyValid(
                attempt,
                requireCurrentRules: true,
                out var error))
        {
            return new DailyFriendRaceStatus(
                dailyKey,
                HasAttempt: true,
                CanStart: false,
                CanResume: false,
                Completed: false,
                SelectedCreatureId: attempt.SelectedEntrant?.Participant?.CreatureId ?? string.Empty,
                SelectedDisplayName: attempt.SelectedEntrant?.Participant?.DisplayName ?? string.Empty,
                FinishedMilliseconds: attempt.FinishedMilliseconds,
                Error: error);
        }

        var completed = attempt.State == DailyRaceAttemptState.Completed;
        return new DailyFriendRaceStatus(
            dailyKey,
            HasAttempt: true,
            CanStart: false,
            CanResume: !completed,
            Completed: completed,
            SelectedCreatureId: attempt.SelectedEntrant!.Participant.CreatureId,
            SelectedDisplayName: attempt.SelectedEntrant.Participant.DisplayName,
            FinishedMilliseconds: attempt.FinishedMilliseconds,
            Error: null);
    }

    public DailyFriendRaceLaunchResult BeginOrResume(
        string creatureId,
        DateTimeOffset utcNow)
    {
        var now = utcNow.ToUniversalTime();
        var dailyKey = DailyFriendRaceService.GetDailyKey(now);
        var status = GetToday(now);
        if (!string.IsNullOrWhiteSpace(status.Error))
            return DailyFriendRaceLaunchResult.Failed(dailyKey, status.Error!);
        if (status.Completed)
            return DailyFriendRaceLaunchResult.Failed(dailyKey, "Today's daily race is already completed.");

        var result = status.CanResume
            ? _coordinator.ResumeToday(_stateProvider(), now)
            : _coordinator.BeginAndPersist(_stateProvider(), creatureId, now);
        if (!result.Success || result.Entry == null || result.Attempt == null)
        {
            return DailyFriendRaceLaunchResult.Failed(
                dailyKey,
                result.Error ?? "The daily race could not be prepared.");
        }

        if (!result.AlreadyStarted)
            _localStateChanged();

        return new DailyFriendRaceLaunchResult(
            Success: true,
            Resumed: result.AlreadyStarted,
            DailyKey: result.Attempt.DailyKey,
            Entry: result.Entry,
            Error: null);
    }

    public DailyFriendRaceCompleteResult Complete(
        string dailyKey,
        int finishedMilliseconds)
    {
        var result = _coordinator.CompleteAndPersist(
            _stateProvider(),
            dailyKey,
            finishedMilliseconds);
        if (!result.Success || result.Attempt == null)
        {
            return new DailyFriendRaceCompleteResult(
                false,
                false,
                dailyKey,
                null,
                result.Error ?? "The daily race result could not be saved.");
        }

        if (!result.AlreadyCompleted)
            _localStateChanged();

        return new DailyFriendRaceCompleteResult(
            true,
            result.AlreadyCompleted,
            result.Attempt.DailyKey,
            result.Attempt.FinishedMilliseconds,
            null);
    }

    public Task<LeaderboardOperationResult> ProjectTodayAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var dailyKey = DailyFriendRaceService.GetDailyKey(utcNow.ToUniversalTime());
        var attempt = _stateProvider().DailyRaceAttempts.FirstOrDefault(value =>
            string.Equals(value.DailyKey, dailyKey, StringComparison.Ordinal));
        if (attempt == null || attempt.State != DailyRaceAttemptState.Completed)
        {
            return Task.FromResult(LeaderboardOperationResult.Failed(
                "Today's daily race has no completed local result to project."));
        }

        return _coordinator.ProjectCompletedAttemptAsync(attempt, cancellationToken);
    }
}
