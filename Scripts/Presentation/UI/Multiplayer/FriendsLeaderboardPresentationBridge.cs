using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Presentation.UI.Multiplayer;

public enum FriendsLeaderboardKind
{
    MultiplayerWins,
    DailyRace,
    CourseBestTime
}

public sealed record FriendsLeaderboardRow(
    int Rank,
    string DisplayName,
    int Score);

public sealed record FriendsLeaderboardViewResult(
    bool Success,
    FriendsLeaderboardKind Kind,
    string ContextLabel,
    FriendsLeaderboardRow[] Rows,
    string? Error)
{
    public static FriendsLeaderboardViewResult Failed(
        FriendsLeaderboardKind kind,
        string contextLabel,
        string error)
        => new(false, kind, contextLabel, Array.Empty<FriendsLeaderboardRow>(), error);
}

/// <summary>
/// Godot presentation boundary for optional friends leaderboards. Steam-specific IDs and adapter
/// callbacks stay below this node; screens receive only rank, display name and score.
/// </summary>
public partial class FriendsLeaderboardPresentationBridge : Node
{
    private LeaderboardProjectionService? _projection;

    public MultiplayerAvailability Availability
        => RequireProjection().Availability;

    public void Configure(LeaderboardProjectionService projection)
    {
        if (_projection != null)
            throw new InvalidOperationException("Friends leaderboard presentation bridge is already configured.");
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
    }

    public async Task<FriendsLeaderboardViewResult> LoadMultiplayerWinsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await RequireProjection()
            .DownloadFriendMultiplayerWinsAsync(cancellationToken)
            .ConfigureAwait(false);
        return Map(
            FriendsLeaderboardKind.MultiplayerWins,
            "Multiplayer wins",
            result);
    }

    public async Task<FriendsLeaderboardViewResult> LoadTodayDailyRaceAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var key = DailyFriendRaceService.GetDailyKey(utcNow.ToUniversalTime());
        var result = await RequireProjection()
            .DownloadFriendDailyRaceAsync(
                key,
                DailyFriendRaceService.CurrentRulesVersion,
                cancellationToken)
            .ConfigureAwait(false);
        return Map(
            FriendsLeaderboardKind.DailyRace,
            key,
            result);
    }

    public async Task<FriendsLeaderboardViewResult> LoadCourseBestTimeAsync(
        string stableCourseId,
        int rulesVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await RequireProjection()
            .DownloadFriendCourseBestTimeAsync(
                stableCourseId,
                rulesVersion,
                cancellationToken)
            .ConfigureAwait(false);
        return Map(
            FriendsLeaderboardKind.CourseBestTime,
            stableCourseId,
            result);
    }

    private static FriendsLeaderboardViewResult Map(
        FriendsLeaderboardKind kind,
        string contextLabel,
        LeaderboardEntriesResult result)
    {
        if (!result.Success)
        {
            return FriendsLeaderboardViewResult.Failed(
                kind,
                contextLabel,
                result.Error ?? "Leaderboard query failed.");
        }

        var rows = result.Entries
            .OrderBy(entry => entry.GlobalRank)
            .Select(entry => new FriendsLeaderboardRow(
                entry.GlobalRank,
                string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? $"Friend {entry.UserId.Value}"
                    : entry.DisplayName.Trim(),
                entry.Score))
            .ToArray();
        return new FriendsLeaderboardViewResult(
            true,
            kind,
            contextLabel,
            rows,
            null);
    }

    private LeaderboardProjectionService RequireProjection()
        => _projection ?? throw new InvalidOperationException("Friends leaderboard presentation bridge is not configured.");
}
