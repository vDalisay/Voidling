using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Voidling.Application.Ports.Multiplayer;

public enum LeaderboardSortDirection
{
    Ascending,
    Descending
}

public enum LeaderboardDisplayFormat
{
    Numeric,
    Seconds,
    Milliseconds
}

public sealed record LeaderboardDefinition(
    string Name,
    LeaderboardSortDirection SortDirection,
    LeaderboardDisplayFormat DisplayFormat);

public sealed record LeaderboardEntry(
    PlatformUserId UserId,
    int GlobalRank,
    int Score,
    IReadOnlyList<int> Details,
    string? DisplayName = null);

public sealed record LeaderboardOperationResult(bool Success, string? Error)
{
    public static LeaderboardOperationResult Succeeded { get; } = new(true, null);
    public static LeaderboardOperationResult Failed(string error) => new(false, error);
}

public sealed record LeaderboardEntriesResult(
    bool Success,
    IReadOnlyList<LeaderboardEntry> Entries,
    string? Error)
{
    public static LeaderboardEntriesResult Succeeded(IReadOnlyList<LeaderboardEntry> entries)
        => new(true, entries, null);

    public static LeaderboardEntriesResult Failed(string error)
        => new(false, Array.Empty<LeaderboardEntry>(), error);
}

/// <summary>
/// Optional social projection. Local saves remain authoritative; an unavailable leaderboard service
/// must never prevent offline startup, local progression, or multiplayer result persistence.
/// </summary>
public interface ILeaderboardService
{
    MultiplayerAvailability Availability { get; }

    Task<LeaderboardOperationResult> UploadScoreAsync(
        LeaderboardDefinition leaderboard,
        int score,
        bool keepBest,
        IReadOnlyList<int>? details = null,
        CancellationToken cancellationToken = default);

    Task<LeaderboardEntriesResult> DownloadFriendsAsync(
        LeaderboardDefinition leaderboard,
        CancellationToken cancellationToken = default);
}
