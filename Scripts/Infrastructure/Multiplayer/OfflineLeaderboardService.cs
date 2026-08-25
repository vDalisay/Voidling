using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Multiplayer;

public sealed class OfflineLeaderboardService : ILeaderboardService
{
    public OfflineLeaderboardService(string reason)
        => Availability = MultiplayerAvailability.Unavailable(reason);

    public MultiplayerAvailability Availability { get; }

    public Task<LeaderboardOperationResult> UploadScoreAsync(
        LeaderboardDefinition leaderboard,
        int score,
        bool keepBest,
        IReadOnlyList<int>? details = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LeaderboardOperationResult.Failed(
            Availability.Reason ?? "Steam leaderboards are unavailable."));

    public Task<LeaderboardEntriesResult> DownloadFriendsAsync(
        LeaderboardDefinition leaderboard,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LeaderboardEntriesResult.Failed(
            Availability.Reason ?? "Steam leaderboards are unavailable."));
}