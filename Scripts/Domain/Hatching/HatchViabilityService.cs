using System;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;

namespace Voidling.Domain.Hatching;

public sealed class HatchViabilityService
{
    private readonly BreedingRules _rules;

    public HatchViabilityService(BreedingRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public int FailurePercent(int burdenLevel)
    {
        var index = Math.Clamp(burdenLevel, 0, _rules.HatchFailurePercentByBurden.Count - 1);
        return _rules.HatchFailurePercentByBurden[index];
    }

    public bool RollViability(ulong seed, int burdenLevel)
    {
        var failurePercent = FailurePercent(burdenLevel);
        if (failurePercent <= 0)
            return true;
        if (failurePercent >= 100)
            return false;

        return StableRandom.Create(seed, "inbreeding:viability").Next(100) >= failurePercent;
    }
}
