using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Ports.Multiplayer;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class LeaderboardProjectionServiceTests
{
    [Fact]
    public async Task MultiplayerWinsUsesDescendingNumericBoardAndKeepBest()
    {
        var fake = new FakeLeaderboardService();
        var projection = new LeaderboardProjectionService(fake);

        var result = await projection.UploadMultiplayerWinsAsync(12);

        Assert.True(result.Success, result.Error);
        var upload = Assert.Single(fake.Uploads);
        Assert.Equal("voidling_multiplayer_wins_v1", upload.Definition.Name);
        Assert.Equal(LeaderboardSortDirection.Descending, upload.Definition.SortDirection);
        Assert.Equal(LeaderboardDisplayFormat.Numeric, upload.Definition.DisplayFormat);
        Assert.Equal(12, upload.Score);
        Assert.True(upload.KeepBest);
        Assert.Empty(upload.Details);
    }

    [Fact]
    public async Task NegativeMultiplayerWinsAreRejectedBeforePlatformCall()
    {
        var fake = new FakeLeaderboardService();
        var projection = new LeaderboardProjectionService(fake);

        var result = await projection.UploadMultiplayerWinsAsync(-1);

        Assert.False(result.Success);
        Assert.Empty(fake.Uploads);
    }

    [Fact]
    public async Task FriendsDownloadUsesMultiplayerWinsBoard()
    {
        var fake = new FakeLeaderboardService();
        fake.FriendEntries = new[]
        {
            new LeaderboardEntry(new PlatformUserId(2), 1, 9, Array.Empty<int>()),
            new LeaderboardEntry(new PlatformUserId(3), 2, 4, Array.Empty<int>())
        };
        var projection = new LeaderboardProjectionService(fake);

        var result = await projection.DownloadFriendMultiplayerWinsAsync();

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Entries.Count);
        var definition = Assert.Single(fake.Downloads);
        Assert.Equal(LeaderboardProjectionService.MultiplayerWins, definition);
    }

    [Fact]
    public void CourseAndDailyBoardNamesAreVersionedAndUseMilliseconds()
    {
        var course = LeaderboardProjectionService.CourseBestTime("GardenSprint", 3);
        var daily = LeaderboardProjectionService.DailyRace("2026-08-25", 2);

        Assert.Equal("voidling_course_gardensprint_v3", course.Name);
        Assert.Equal(LeaderboardSortDirection.Ascending, course.SortDirection);
        Assert.Equal(LeaderboardDisplayFormat.Milliseconds, course.DisplayFormat);
        Assert.Equal("voidling_daily_2026-08-25_v2", daily.Name);
        Assert.Equal(LeaderboardSortDirection.Ascending, daily.SortDirection);
        Assert.Equal(LeaderboardDisplayFormat.Milliseconds, daily.DisplayFormat);
    }

    private sealed record UploadCall(
        LeaderboardDefinition Definition,
        int Score,
        bool KeepBest,
        IReadOnlyList<int> Details);

    private sealed class FakeLeaderboardService : ILeaderboardService
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public List<UploadCall> Uploads { get; } = new();
        public List<LeaderboardDefinition> Downloads { get; } = new();
        public IReadOnlyList<LeaderboardEntry> FriendEntries { get; set; } = Array.Empty<LeaderboardEntry>();

        public Task<LeaderboardOperationResult> UploadScoreAsync(
            LeaderboardDefinition leaderboard,
            int score,
            bool keepBest,
            IReadOnlyList<int>? details = null,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add(new UploadCall(
                leaderboard,
                score,
                keepBest,
                details ?? Array.Empty<int>()));
            return Task.FromResult(LeaderboardOperationResult.Succeeded);
        }

        public Task<LeaderboardEntriesResult> DownloadFriendsAsync(
            LeaderboardDefinition leaderboard,
            CancellationToken cancellationToken = default)
        {
            Downloads.Add(leaderboard);
            return Task.FromResult(LeaderboardEntriesResult.Succeeded(FriendEntries));
        }
    }
}
