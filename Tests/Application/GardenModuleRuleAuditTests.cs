using System;
using Voidling.Domain.Rules;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class GardenModuleRuleAuditTests
{
    [Fact]
    public void MissingUpgradeCostCannotCreateSilentFreeLevels()
    {
        var rules = new GardenModuleRules(
            SlotCount: 4,
            PurchaseCost: 10,
            UpgradeCosts: Array.AsReadOnly(new[] { 20 }),
            PointsPerMinuteByLevel: Array.AsReadOnly(new[] { 1.0f, 2.0f, 3.0f }));

        Assert.Equal(2, rules.MaxLevel);
        Assert.Equal(20, rules.UpgradeCostForLevel(1));
        Assert.Equal(-1, rules.UpgradeCostForLevel(2));
        Assert.Equal(2.0f, rules.PointsPerMinuteForLevel(99));
    }
}
