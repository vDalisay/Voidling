using System;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Leaderboards;

/// <summary>
/// Names/versioning for Steam social projections. None of these values are authoritative game state;
/// callers can retry uploads from the local save whenever Steam becomes available again.
/// </summary>
public sealed class LeaderboardProjectionService
{
    public static LeaderboardDefinition MultiplayerWins { get; } = new(
        "voidling_multiplayer_wins_v1",
        LeaderboardSortDirection.Descending,
        LeaderboardDisplayFormat.Numeric);

    private readonly ILeaderboardService _leaderboards;

    public LeaderboardProjectionService(ILeaderboardService leaderboards)
        => _leaderboards = leaderboards ?? throw new ArgumentNullException(nameof(leaderboards));

    public MultiplayerAvailability Availability => _leaderboards.Availability;

    public Task<LeaderboardOperationResult> UploadMultiplayerWinsAsync(
        int totalWins,
        CancellationToken cancellationToken = default)
    {
        if (totalWins < 0)
            return Task.FromResult(LeaderboardOperationResult.Failed("Multiplayer win total cannot be negative."));

        // The local total only increases, so KeepBest is appropriate for the descending board.
        return _leaderboards.UploadScoreAsync(
            MultiplayerWins,
            totalWins,
            keepBest: true,
            details: null,
            cancellationToken);
    }

    public Task<LeaderboardEntriesResult> DownloadFriendMultiplayerWinsAsync(
        CancellationToken cancellationToken = default)
        => _leaderboards.DownloadFriendsAsync(MultiplayerWins, cancellationToken);

    public Task<LeaderboardOperationResult> UploadDailyRaceTimeAsync(
        string utcDailyKey,
        int rulesVersion,
        int finishedMilliseconds,
        CancellationToken cancellationToken = default)
    {
        if (finishedMilliseconds <= 0)
            return Task.FromResult(LeaderboardOperationResult.Failed("Daily race time must be positive."));

        LeaderboardDefinition definition;
        try
        {
            definition = DailyRace(utcDailyKey, rulesVersion);
        }
        catch (ArgumentException exception)
        {
            return Task.FromResult(LeaderboardOperationResult.Failed(exception.Message));
        }

        // One local attempt is allowed, but KeepBest remains defensive/idempotent if the same
        // completed attempt is projected more than once after a transient Steam failure/restart.
        return _leaderboards.UploadScoreAsync(
            definition,
            finishedMilliseconds,
            keepBest: true,
            details: null,
            cancellationToken);
    }

    public Task<LeaderboardEntriesResult> DownloadFriendDailyRaceAsync(
        string utcDailyKey,
        int rulesVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return _leaderboards.DownloadFriendsAsync(
                DailyRace(utcDailyKey, rulesVersion),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Task.FromResult(LeaderboardEntriesResult.Failed(exception.Message));
        }
    }

    public static LeaderboardDefinition CourseBestTime(string stableCourseId, int rulesVersion)
    {
        if (string.IsNullOrWhiteSpace(stableCourseId))
            throw new ArgumentException("Course ID is required.", nameof(stableCourseId));
        if (rulesVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(rulesVersion));

        var safeId = stableCourseId.Trim().ToLowerInvariant();
        return new LeaderboardDefinition(
            $"voidling_course_{safeId}_v{rulesVersion}",
            LeaderboardSortDirection.Ascending,
            LeaderboardDisplayFormat.Milliseconds);
    }

    public static LeaderboardDefinition DailyRace(string utcDailyKey, int rulesVersion)
    {
        if (!DateOnly.TryParseExact(utcDailyKey, "yyyy-MM-dd", out _))
            throw new ArgumentException("Daily key must use yyyy-MM-dd.", nameof(utcDailyKey));
        if (rulesVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(rulesVersion));

        return new LeaderboardDefinition(
            $"voidling_daily_{utcDailyKey}_v{rulesVersion}",
            LeaderboardSortDirection.Ascending,
            LeaderboardDisplayFormat.Milliseconds);
    }
}
