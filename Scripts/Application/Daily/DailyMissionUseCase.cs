using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Daily;

public sealed record DailyMissionView(
    string MissionId,
    int Progress,
    int Target,
    int CoinReward,
    bool CanClaim,
    bool Claimed);

public sealed record DailyMissionStatus(
    int DayNumber,
    IReadOnlyList<DailyMissionView> Missions);

public readonly record struct DailyMissionClaimResult(
    bool Claimed,
    int CoinsAwarded,
    DailyMissionStatus Status);

/// <summary>
/// Deterministic daily-mission state machine. The local calendar day is supplied explicitly and the
/// selected mission IDs are frozen into the save for that day. Definition/target/reward content is
/// authorable balance data and can be retuned without changing the persistence model.
/// </summary>
public sealed class DailyMissionUseCase
{
    public bool EnsureDay(GameStateData state, int currentDayNumber, DailyMissionRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        state.DailyMissions ??= new DailyMissionStateData();
        state.DailyMissions.Missions ??= new List<DailyMissionProgressData>();

        if (currentDayNumber <= 0)
            return false;

        if (state.DailyMissions.DayNumber == currentDayNumber && state.DailyMissions.Missions.Count > 0)
        {
            var normalized = NormalizeProgress(state.DailyMissions, rules);
            if (state.DailyMissions.Missions.Count > 0)
                return normalized;
        }

        var definitions = ValidDefinitions(rules);
        state.DailyMissions.DayNumber = currentDayNumber;
        state.DailyMissions.Missions.Clear();
        if (definitions.Length == 0)
            return true;

        var count = Math.Clamp(rules.MissionsPerDay, 1, definitions.Length);
        var start = currentDayNumber % definitions.Length;
        for (var i = 0; i < count; i++)
        {
            var definition = definitions[(start + i) % definitions.Length];
            state.DailyMissions.Missions.Add(new DailyMissionProgressData
            {
                MissionId = definition.Id,
                Progress = 0,
                Claimed = false
            });
        }

        return true;
    }

    public DailyMissionStatus GetStatus(GameStateData state, int currentDayNumber, DailyMissionRules rules)
    {
        EnsureDay(state, currentDayNumber, rules);
        var definitions = ValidDefinitions(rules).ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        var views = state.DailyMissions.Missions
            .Where(progress => definitions.ContainsKey(progress.MissionId))
            .Select(progress =>
            {
                var definition = definitions[progress.MissionId];
                var target = Math.Max(1, definition.Target);
                var normalizedProgress = Math.Clamp(progress.Progress, 0, target);
                return new DailyMissionView(
                    progress.MissionId,
                    normalizedProgress,
                    target,
                    Math.Max(0, definition.CoinReward),
                    CanClaim: !progress.Claimed && normalizedProgress >= target,
                    Claimed: progress.Claimed);
            })
            .ToArray();
        return new DailyMissionStatus(state.DailyMissions.DayNumber, views);
    }

    public bool RecordEvent(
        GameStateData state,
        int currentDayNumber,
        DailyMissionRules rules,
        DailyMissionEventKind eventKind,
        int amount = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        var changed = EnsureDay(state, currentDayNumber, rules);
        if (amount <= 0)
            return changed;

        var definitions = ValidDefinitions(rules).ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        foreach (var progress in state.DailyMissions.Missions)
        {
            if (!definitions.TryGetValue(progress.MissionId, out var definition) ||
                definition.EventKind != eventKind ||
                progress.Claimed)
            {
                continue;
            }

            var target = Math.Max(1, definition.Target);
            var next = Math.Clamp((long)Math.Max(0, progress.Progress) + amount, 0L, target);
            if (progress.Progress == next)
                continue;
            progress.Progress = (int)next;
            changed = true;
        }

        return changed;
    }

    public DailyMissionClaimResult Claim(
        GameStateData state,
        int currentDayNumber,
        DailyMissionRules rules,
        string missionId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        EnsureDay(state, currentDayNumber, rules);

        var definition = ValidDefinitions(rules).FirstOrDefault(candidate =>
            string.Equals(candidate.Id, missionId, StringComparison.Ordinal));
        var progress = state.DailyMissions.Missions.FirstOrDefault(candidate =>
            string.Equals(candidate.MissionId, missionId, StringComparison.Ordinal));
        if (definition == null || progress == null || progress.Claimed ||
            progress.Progress < Math.Max(1, definition.Target))
        {
            return new DailyMissionClaimResult(false, 0, GetStatus(state, currentDayNumber, rules));
        }

        var reward = Math.Max(0, definition.CoinReward);
        var availableCoinCapacity = Math.Max(0L, (long)int.MaxValue - state.Coins);
        var awarded = (int)Math.Min(reward, availableCoinCapacity);
        progress.Claimed = true;
        state.Coins += awarded;
        return new DailyMissionClaimResult(true, awarded, GetStatus(state, currentDayNumber, rules));
    }

    private static bool NormalizeProgress(DailyMissionStateData state, DailyMissionRules rules)
    {
        var changed = false;
        var definitions = ValidDefinitions(rules).ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = state.Missions.Count - 1; i >= 0; i--)
        {
            var mission = state.Missions[i];
            if (mission == null || !definitions.TryGetValue(mission.MissionId ?? string.Empty, out var definition) ||
                !seen.Add(mission.MissionId))
            {
                state.Missions.RemoveAt(i);
                changed = true;
                continue;
            }

            var normalized = Math.Clamp(mission.Progress, 0, Math.Max(1, definition.Target));
            if (mission.Progress != normalized)
            {
                mission.Progress = normalized;
                changed = true;
            }
        }
        return changed;
    }

    private static DailyMissionDefinition[] ValidDefinitions(DailyMissionRules rules)
        => rules.Definitions?
            .Where(definition => definition != null &&
                                 !string.IsNullOrWhiteSpace(definition.Id) &&
                                 definition.Id.Length <= 128)
            .GroupBy(definition => definition.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray() ?? Array.Empty<DailyMissionDefinition>();
}
