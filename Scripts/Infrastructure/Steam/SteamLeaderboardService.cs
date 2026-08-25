using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Steam;

/// <summary>
/// Serialized asynchronous adapter over GodotSteam leaderboard callbacks. The find callback does not
/// identify the requested name, so one operation is intentionally in flight at a time to prevent
/// callback cross-talk. This is social projection infrastructure only; local saves remain authoritative.
/// </summary>
internal sealed class SteamLeaderboardService : ILeaderboardService
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(15);

    private readonly GodotSteamApi _api;
    private readonly GodotSteamRuntime _runtime;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public SteamLeaderboardService(GodotSteamApi api, GodotSteamRuntime runtime)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

        var steam = api.SteamObject;
        Availability = api.SupportsLeaderboards &&
                       steam.HasSignal("leaderboard_find_result") &&
                       steam.HasSignal("leaderboard_score_uploaded") &&
                       steam.HasSignal("leaderboard_scores_downloaded")
            ? MultiplayerAvailability.Available
            : MultiplayerAvailability.Unavailable(
                "GodotSteam leaderboard methods or callbacks are unavailable. Local progress remains available.");
    }

    public MultiplayerAvailability Availability { get; }

    public async Task<LeaderboardOperationResult> UploadScoreAsync(
        LeaderboardDefinition leaderboard,
        int score,
        bool keepBest,
        IReadOnlyList<int>? details = null,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateDefinition(leaderboard, out var validationError))
            return LeaderboardOperationResult.Failed(validationError!);
        if (!Availability.IsAvailable)
            return LeaderboardOperationResult.Failed(Availability.Reason ?? "Steam leaderboards are unavailable.");

        var detailArray = details?.ToArray() ?? Array.Empty<int>();
        if (detailArray.Length > 64)
            return LeaderboardOperationResult.Failed("Steam leaderboard entries support at most 64 detail integers.");

        using var timeout = CreateOperationToken(cancellationToken);
        try
        {
            await _operationGate.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return LeaderboardOperationResult.Failed("Steam leaderboard operation timed out or was cancelled.");
        }

        try
        {
            var found = await FindOrCreateAsync(leaderboard, timeout.Token).ConfigureAwait(false);
            if (!found.Success)
                return LeaderboardOperationResult.Failed(found.Error!);

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnUploaded(bool success, long handle, int _, bool __, int ___, int ____)
            {
                if (unchecked((ulong)handle) == found.Handle)
                    completion.TrySetResult(success);
            }

            _runtime.LeaderboardScoreUploaded += OnUploaded;
            try
            {
                _api.UploadLeaderboardScore(found.Handle, score, keepBest, detailArray);
                var success = await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                return success
                    ? LeaderboardOperationResult.Succeeded
                    : LeaderboardOperationResult.Failed("Steam rejected the leaderboard score upload.");
            }
            finally
            {
                _runtime.LeaderboardScoreUploaded -= OnUploaded;
            }
        }
        catch (OperationCanceledException)
        {
            return LeaderboardOperationResult.Failed("Steam leaderboard operation timed out or was cancelled.");
        }
        catch (Exception exception)
        {
            return LeaderboardOperationResult.Failed($"Steam leaderboard upload failed: {exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<LeaderboardEntriesResult> DownloadFriendsAsync(
        LeaderboardDefinition leaderboard,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateDefinition(leaderboard, out var validationError))
            return LeaderboardEntriesResult.Failed(validationError!);
        if (!Availability.IsAvailable)
            return LeaderboardEntriesResult.Failed(Availability.Reason ?? "Steam leaderboards are unavailable.");

        using var timeout = CreateOperationToken(cancellationToken);
        try
        {
            await _operationGate.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return LeaderboardEntriesResult.Failed("Steam leaderboard operation timed out or was cancelled.");
        }

        try
        {
            var found = await FindOrCreateAsync(leaderboard, timeout.Token).ConfigureAwait(false);
            if (!found.Success)
                return LeaderboardEntriesResult.Failed(found.Error!);

            var completion = new TaskCompletionSource<IReadOnlyList<LeaderboardEntry>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnDownloaded(string message, long handle, Godot.Collections.Array entries)
            {
                if (unchecked((ulong)handle) != found.Handle)
                    return;

                try
                {
                    completion.TrySetResult(ParseEntries(entries));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(new InvalidOperationException(
                        string.IsNullOrWhiteSpace(message)
                            ? $"Steam leaderboard entries were malformed: {exception.Message}"
                            : $"{message}: {exception.Message}",
                        exception));
                }
            }

            _runtime.LeaderboardScoresDownloaded += OnDownloaded;
            try
            {
                _api.DownloadFriendLeaderboardEntries(found.Handle);
                var entries = await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                return LeaderboardEntriesResult.Succeeded(entries);
            }
            finally
            {
                _runtime.LeaderboardScoresDownloaded -= OnDownloaded;
            }
        }
        catch (OperationCanceledException)
        {
            return LeaderboardEntriesResult.Failed("Steam leaderboard operation timed out or was cancelled.");
        }
        catch (Exception exception)
        {
            return LeaderboardEntriesResult.Failed($"Steam leaderboard download failed: {exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<(bool Success, ulong Handle, string? Error)> FindOrCreateAsync(
        LeaderboardDefinition leaderboard,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<(long Handle, bool Found)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFound(long handle, bool found) => completion.TrySetResult((handle, found));

        _runtime.LeaderboardFound += OnFound;
        try
        {
            _api.FindOrCreateLeaderboard(leaderboard);
            var result = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Found || result.Handle == 0)
                return (false, 0, $"Steam could not find or create leaderboard '{leaderboard.Name}'.");
            return (true, unchecked((ulong)result.Handle), null);
        }
        finally
        {
            _runtime.LeaderboardFound -= OnFound;
        }
    }

    private IReadOnlyList<LeaderboardEntry> ParseEntries(Godot.Collections.Array entries)
    {
        var parsed = new List<LeaderboardEntry>(entries.Count);
        foreach (var raw in entries)
        {
            if (raw.VariantType != Variant.Type.Dictionary)
                continue;

            var dictionary = raw.AsGodotDictionary();
            if (!TryReadInt64(dictionary, "steam_id", out var steamId) || steamId <= 0 ||
                !TryReadInt64(dictionary, "global_rank", out var rank) || rank <= 0 ||
                !TryReadInt64(dictionary, "score", out var score) ||
                rank > int.MaxValue || score is < int.MinValue or > int.MaxValue)
            {
                continue;
            }

            var details = Array.Empty<int>();
            if (dictionary.ContainsKey("details") &&
                dictionary["details"].VariantType == Variant.Type.Array)
            {
                details = dictionary["details"].AsGodotArray()
                    .Take(64)
                    .Select(value => (int)value.AsInt64())
                    .ToArray();
            }

            var userId = unchecked((ulong)steamId);
            parsed.Add(new LeaderboardEntry(
                new PlatformUserId(userId),
                (int)rank,
                (int)score,
                details,
                _api.GetFriendPersonaName(userId)));
        }

        return parsed
            .OrderBy(entry => entry.GlobalRank)
            .ToArray();
    }

    private static bool TryReadInt64(
        Godot.Collections.Dictionary dictionary,
        string key,
        out long value)
    {
        value = 0;
        if (!dictionary.ContainsKey(key))
            return false;
        value = dictionary[key].AsInt64();
        return true;
    }

    private static bool ValidateDefinition(LeaderboardDefinition? leaderboard, out string? error)
    {
        error = null;
        if (leaderboard == null ||
            string.IsNullOrWhiteSpace(leaderboard.Name) ||
            leaderboard.Name.Length > 128 ||
            !Enum.IsDefined(leaderboard.SortDirection) ||
            !Enum.IsDefined(leaderboard.DisplayFormat))
        {
            error = "Leaderboard definition is invalid.";
            return false;
        }
        return true;
    }

    private static CancellationTokenSource CreateOperationToken(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OperationTimeout);
        return timeout;
    }
}
