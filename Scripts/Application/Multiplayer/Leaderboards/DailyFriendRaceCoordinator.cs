using System;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Application.Racing;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Leaderboards;

/// <summary>
/// Transaction boundary around the local daily race. A new attempt is not launchable until its
/// immutable entry has been persisted, and completion is not projectable to Steam until the local
/// completed state has been saved. Steam remains optional and is never part of the save transaction.
/// </summary>
public sealed class DailyFriendRaceCoordinator
{
    private readonly DailyFriendRaceService _daily;
    private readonly IGameStateRepository _repository;
    private readonly LeaderboardProjectionService _projection;

    public DailyFriendRaceCoordinator(
        DailyFriendRaceService daily,
        IGameStateRepository repository,
        LeaderboardProjectionService projection)
    {
        _daily = daily ?? throw new ArgumentNullException(nameof(daily));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
    }

    public MultiplayerAvailability LeaderboardAvailability => _projection.Availability;

    public DailyRaceStartResult BeginAndPersist(
        GameStateData state,
        string creatureId,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = _daily.Begin(state, creatureId, utcNow);
        if (!result.Success || result.AlreadyStarted || result.Attempt == null)
            return result;

        try
        {
            // Persistence is deliberately synchronous here. Presentation must not receive the
            // launchable RaceEntry until the one-attempt marker is durable on disk.
            _repository.Save(state);
            return result;
        }
        catch (Exception exception)
        {
            // Begin added exactly this new attempt. If persistence failed, remove it again so the
            // in-memory state agrees with disk and a later retry is not incorrectly blocked.
            state.DailyRaceAttempts.Remove(result.Attempt);
            return DailyRaceStartResult.Failed(
                $"Could not save the daily race attempt before launch: {exception.Message}");
        }
    }

    public DailyRaceStartResult ResumeToday(GameStateData state, DateTimeOffset utcNow)
        => _daily.ResumeToday(state, utcNow);

    public DailyRaceCompletionResult CompleteAndPersist(
        GameStateData state,
        string dailyKey,
        int finishedMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        var existing = state.DailyRaceAttempts.Find(attempt =>
            string.Equals(attempt.DailyKey, dailyKey, StringComparison.Ordinal));
        var previousState = existing?.State;
        var previousFinishedMilliseconds = existing?.FinishedMilliseconds;

        var result = _daily.Complete(state, dailyKey, finishedMilliseconds);
        if (!result.Success || result.AlreadyCompleted || result.Attempt == null)
            return result;

        try
        {
            _repository.Save(state);
            return result;
        }
        catch (Exception exception)
        {
            // Completion is a local mutation. Restore the exact pre-call values if disk persistence
            // fails so the same finished race can be submitted again rather than being lost locally.
            if (existing != null && previousState.HasValue)
            {
                existing.State = previousState.Value;
                existing.FinishedMilliseconds = previousFinishedMilliseconds;
            }

            return DailyRaceCompletionResult.Failed(
                $"Could not save the completed daily race: {exception.Message}");
        }
    }

    public Task<LeaderboardOperationResult> ProjectCompletedAttemptAsync(
        DailyRaceAttemptData attempt,
        CancellationToken cancellationToken = default)
    {
        if (!DailyFriendRaceService.IsStructurallyValid(
                attempt,
                requireCurrentRules: false,
                out var error) ||
            attempt.State != DailyRaceAttemptState.Completed ||
            !attempt.FinishedMilliseconds.HasValue)
        {
            return Task.FromResult(LeaderboardOperationResult.Failed(
                error ?? "Only a persisted completed daily-race attempt can be projected."));
        }

        return _projection.UploadDailyRaceTimeAsync(
            attempt.DailyKey,
            attempt.RulesVersion,
            attempt.FinishedMilliseconds.Value,
            cancellationToken);
    }

    public Task<LeaderboardEntriesResult> DownloadFriendsForTodayAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
        => _projection.DownloadFriendDailyRaceAsync(
            DailyFriendRaceService.GetDailyKey(utcNow),
            DailyFriendRaceService.CurrentRulesVersion,
            cancellationToken);
}
