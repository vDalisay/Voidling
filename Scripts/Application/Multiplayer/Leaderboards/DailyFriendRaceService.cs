using System;
using System.Buffers.Binary;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Voidling.Application.Racing;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Leaderboards;

public enum DailyRaceAttemptState
{
    Started,
    Completed
}

/// <summary>
/// Persisted local authority for the friends-only daily race. Steam only receives the finished time
/// as a social projection; starting/completing an attempt never requires network access.
/// </summary>
public sealed class DailyRaceAttemptData
{
    public string DailyKey { get; set; } = string.Empty;
    public int RulesVersion { get; set; }
    public ulong SimulationSeed { get; set; }
    public RaceEntrant? SelectedEntrant { get; set; }
    public DailyRaceAttemptState State { get; set; }
    public int? FinishedMilliseconds { get; set; }
}

public sealed record DailyRaceStartResult(
    bool Success,
    bool AlreadyStarted,
    DailyRaceAttemptData? Attempt,
    RaceEntry? Entry,
    string? Error)
{
    public static DailyRaceStartResult Started(DailyRaceAttemptData attempt, RaceEntry entry)
        => new(true, false, attempt, entry, null);

    public static DailyRaceStartResult Existing(DailyRaceAttemptData attempt, RaceEntry? entry, string? error = null)
        => new(entry != null, true, attempt, entry, error);

    public static DailyRaceStartResult Failed(string error)
        => new(false, false, null, null, error);
}

public sealed record DailyRaceCompletionResult(
    bool Success,
    bool AlreadyCompleted,
    DailyRaceAttemptData? Attempt,
    string? Error)
{
    public static DailyRaceCompletionResult Completed(DailyRaceAttemptData attempt)
        => new(true, false, attempt, null);

    public static DailyRaceCompletionResult Existing(DailyRaceAttemptData attempt)
        => new(true, true, attempt, null);

    public static DailyRaceCompletionResult Failed(string error)
        => new(false, false, null, error);
}

/// <summary>
/// Owns deterministic UTC daily identity and the local one-attempt state transition. The caller must
/// persist immediately after Begin succeeds and before presenting the race. That ordering makes a crash
/// consume/resume the same attempt instead of granting another roll.
/// </summary>
public sealed class DailyFriendRaceService
{
    public const int CurrentRulesVersion = 1;
    public const int MaxAttemptHistory = 32;

    private readonly RaceEntryFactory _entries;

    public DailyFriendRaceService(RaceEntryFactory entries)
        => _entries = entries ?? throw new ArgumentNullException(nameof(entries));

    public DailyRaceStartResult Begin(
        GameStateData state,
        string creatureId,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(creatureId))
            return DailyRaceStartResult.Failed("A Voidling must be selected for the daily race.");

        var dailyKey = GetDailyKey(utcNow);
        var existing = state.DailyRaceAttempts.FirstOrDefault(attempt =>
            string.Equals(attempt.DailyKey, dailyKey, StringComparison.Ordinal));
        if (existing != null)
            return ResumeExisting(existing);

        var selected = state.Voidlings.FirstOrDefault(voidling =>
            string.Equals(voidling.Id, creatureId, StringComparison.Ordinal));
        if (selected == null)
            return DailyRaceStartResult.Failed("The selected Voidling is not owned by this save.");

        var frozen = _entries.CreateOwnedEntrant(selected);
        var seed = ComputeDailySeed(dailyKey, CurrentRulesVersion);
        var attempt = new DailyRaceAttemptData
        {
            DailyKey = dailyKey,
            RulesVersion = CurrentRulesVersion,
            SimulationSeed = seed,
            SelectedEntrant = frozen,
            State = DailyRaceAttemptState.Started,
            FinishedMilliseconds = null
        };

        state.DailyRaceAttempts.Add(attempt);
        TrimHistory(state);
        return DailyRaceStartResult.Started(attempt, _entries.Create(frozen, seed));
    }

    public DailyRaceStartResult ResumeToday(GameStateData state, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(state);
        var dailyKey = GetDailyKey(utcNow);
        var existing = state.DailyRaceAttempts.FirstOrDefault(attempt =>
            string.Equals(attempt.DailyKey, dailyKey, StringComparison.Ordinal));
        return existing == null
            ? DailyRaceStartResult.Failed("No daily race attempt has been started today.")
            : ResumeExisting(existing);
    }

    public DailyRaceCompletionResult Complete(
        GameStateData state,
        string dailyKey,
        int finishedMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(dailyKey))
            return DailyRaceCompletionResult.Failed("Daily race key is required.");
        if (finishedMilliseconds <= 0)
            return DailyRaceCompletionResult.Failed("Daily race finish time must be positive.");

        var attempt = state.DailyRaceAttempts.FirstOrDefault(value =>
            string.Equals(value.DailyKey, dailyKey, StringComparison.Ordinal));
        if (attempt == null)
            return DailyRaceCompletionResult.Failed("Daily race attempt was not found in the local save.");
        if (attempt.State == DailyRaceAttemptState.Completed)
            return DailyRaceCompletionResult.Existing(attempt);
        if (!IsStructurallyValid(attempt, requireCurrentRules: true, out var error))
            return DailyRaceCompletionResult.Failed(error!);

        attempt.State = DailyRaceAttemptState.Completed;
        attempt.FinishedMilliseconds = finishedMilliseconds;
        return DailyRaceCompletionResult.Completed(attempt);
    }

    public static string GetDailyKey(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static ulong ComputeDailySeed(string dailyKey, int rulesVersion)
    {
        if (!DateOnly.TryParseExact(dailyKey, "yyyy-MM-dd", out _))
            throw new ArgumentException("Daily key must use yyyy-MM-dd.", nameof(dailyKey));
        if (rulesVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(rulesVersion));

        var bytes = Encoding.UTF8.GetBytes($"voidling:daily-race:v1|{dailyKey}|{rulesVersion}");
        var digest = SHA256.HashData(bytes);
        var seed = BinaryPrimitives.ReadUInt64LittleEndian(digest);
        return seed == 0 ? 1UL : seed;
    }

    public static bool IsStructurallyValid(
        DailyRaceAttemptData? attempt,
        bool requireCurrentRules,
        out string? error)
    {
        error = null;
        if (attempt == null ||
            !DateOnly.TryParseExact(attempt.DailyKey, "yyyy-MM-dd", out _) ||
            attempt.RulesVersion < 1 ||
            attempt.SimulationSeed == 0 ||
            attempt.SelectedEntrant?.Participant == null ||
            string.IsNullOrWhiteSpace(attempt.SelectedEntrant.Participant.CreatureId) ||
            !Enum.IsDefined(attempt.State) ||
            (attempt.State == DailyRaceAttemptState.Completed &&
             (!attempt.FinishedMilliseconds.HasValue || attempt.FinishedMilliseconds.Value <= 0)) ||
            (attempt.State == DailyRaceAttemptState.Started && attempt.FinishedMilliseconds.HasValue))
        {
            error = "Daily race attempt data is malformed.";
            return false;
        }

        if (requireCurrentRules && attempt.RulesVersion != CurrentRulesVersion)
        {
            error = "This daily race attempt belongs to an incompatible daily-race rules version.";
            return false;
        }

        return true;
    }

    private DailyRaceStartResult ResumeExisting(DailyRaceAttemptData attempt)
    {
        if (!IsStructurallyValid(attempt, requireCurrentRules: true, out var error))
            return DailyRaceStartResult.Existing(attempt, null, error);
        if (attempt.State == DailyRaceAttemptState.Completed)
            return DailyRaceStartResult.Existing(attempt, null, "Today's daily race attempt is already completed.");

        return DailyRaceStartResult.Existing(
            attempt,
            _entries.Create(attempt.SelectedEntrant!, attempt.SimulationSeed));
    }

    private static void TrimHistory(GameStateData state)
    {
        if (state.DailyRaceAttempts.Count <= MaxAttemptHistory)
            return;

        state.DailyRaceAttempts = state.DailyRaceAttempts
            .OrderByDescending(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .Take(MaxAttemptHistory)
            .OrderBy(attempt => attempt.DailyKey, StringComparer.Ordinal)
            .ToList();
    }
}
