using System;
using System.Collections.Generic;
using System.Linq;
using VoidlingGame;

namespace Voidling.Application.Daily;

public sealed record DailyLoginStatus(
    bool CanClaim,
    int CurrentStreak,
    int ClaimReward,
    int NextReward,
    IReadOnlyList<int> RewardCycle);

public readonly record struct DailyLoginClaimResult(
    bool Claimed,
    int CoinsAwarded,
    DailyLoginStatus Status);

/// <summary>
/// Deterministic daily check-in logic. Callers provide the local calendar day explicitly; this
/// service never reads wall-clock time. Reward values are supplied by authorable balance data.
/// </summary>
public sealed class DailyLoginUseCase
{
    public DailyLoginStatus GetStatus(
        GameStateData state,
        int currentDayNumber,
        IReadOnlyList<int> coinRewards)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rewards = NormalizeRewards(coinRewards);
        var daily = state.DailyLogin ??= new DailyLoginStateData();
        NormalizeState(daily);

        if (currentDayNumber <= 0 || rewards.Length == 0)
            return new DailyLoginStatus(false, ActiveStreak(daily, currentDayNumber), 0, 0, rewards);

        var alreadyClaimed = daily.LastClaimDayNumber == currentDayNumber;
        var activeStreak = ActiveStreak(daily, currentDayNumber);
        var claimStreak = alreadyClaimed
            ? Math.Max(1, daily.Streak)
            : daily.LastClaimDayNumber == currentDayNumber - 1
                ? Math.Max(0, daily.Streak) + 1
                : 1;
        var claimReward = alreadyClaimed ? 0 : RewardForStreak(rewards, claimStreak);
        var nextStreak = alreadyClaimed
            ? Math.Max(1, daily.Streak) + 1
            : claimStreak + 1;

        return new DailyLoginStatus(
            CanClaim: !alreadyClaimed,
            CurrentStreak: activeStreak,
            ClaimReward: claimReward,
            NextReward: RewardForStreak(rewards, nextStreak),
            RewardCycle: rewards);
    }

    public DailyLoginClaimResult Claim(
        GameStateData state,
        int currentDayNumber,
        IReadOnlyList<int> coinRewards)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rewards = NormalizeRewards(coinRewards);
        var daily = state.DailyLogin ??= new DailyLoginStateData();
        NormalizeState(daily);

        var before = GetStatus(state, currentDayNumber, rewards);
        if (!before.CanClaim || rewards.Length == 0 || currentDayNumber <= 0)
            return new DailyLoginClaimResult(false, 0, before);

        var nextStreak = daily.LastClaimDayNumber == currentDayNumber - 1
            ? Math.Max(0, daily.Streak) + 1
            : 1;
        var reward = RewardForStreak(rewards, nextStreak);
        var availableCoinCapacity = Math.Max(0L, (long)int.MaxValue - state.Coins);
        var awarded = (int)Math.Min(reward, availableCoinCapacity);

        daily.LastClaimDayNumber = currentDayNumber;
        daily.Streak = nextStreak;
        state.Coins += awarded;

        return new DailyLoginClaimResult(
            Claimed: true,
            CoinsAwarded: awarded,
            Status: GetStatus(state, currentDayNumber, rewards));
    }

    private static int ActiveStreak(DailyLoginStateData daily, int currentDayNumber)
    {
        if (currentDayNumber <= 0)
            return 0;
        return daily.LastClaimDayNumber == currentDayNumber ||
               daily.LastClaimDayNumber == currentDayNumber - 1
            ? Math.Max(0, daily.Streak)
            : 0;
    }

    private static int RewardForStreak(IReadOnlyList<int> rewards, int streak)
    {
        if (rewards.Count == 0 || streak <= 0)
            return 0;
        return rewards[(streak - 1) % rewards.Count];
    }

    private static int[] NormalizeRewards(IReadOnlyList<int>? rewards)
        => rewards?.Select(value => Math.Max(0, value)).ToArray() ?? Array.Empty<int>();

    private static void NormalizeState(DailyLoginStateData daily)
    {
        daily.LastClaimDayNumber = Math.Max(0, daily.LastClaimDayNumber);
        daily.Streak = Math.Max(0, daily.Streak);
        if (daily.LastClaimDayNumber == 0)
            daily.Streak = 0;
    }
}
