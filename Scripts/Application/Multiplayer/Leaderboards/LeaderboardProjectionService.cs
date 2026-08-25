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