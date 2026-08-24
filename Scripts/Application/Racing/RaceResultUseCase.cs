using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Racing;

public readonly record struct RaceRewardResult(int Place, int Reward);

/// <summary>
/// Applies race-result economy effects. Placement-to-reward tuning lives in RaceRules so new
/// race tiers can change balance without putting economy constants back into presentation.
/// </summary>
public sealed class RaceResultUseCase
{
    private readonly RaceRules _rules;

    public RaceResultUseCase(GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.Racing;
        if (_rules.PlacementRewards.Count < 4)
            throw new ArgumentException("Race rules require rewards for first, second, third, and fallback placements.", nameof(rules));
    }

    public RaceRewardResult AwardPlacement(GameStateData state, int place)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rewardIndex = place switch
        {
            1 => 0,
            2 => 1,
            3 => 2,
            _ => 3
        };

        var reward = _rules.PlacementRewards[rewardIndex];
        state.Coins += reward;
        return new RaceRewardResult(place, reward);
    }
}
