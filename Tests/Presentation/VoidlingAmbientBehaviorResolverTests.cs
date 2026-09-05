using Voidling.Presentation.Voidlings;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class VoidlingAmbientBehaviorResolverTests
{
    [Fact]
    public void Resolve_HigherRunMovesFasterAndHigherStaminaRestsLess()
    {
        var low = VoidlingAmbientBehaviorResolver.Resolve(run: 0.0f, stamina: 0.0f);
        var high = VoidlingAmbientBehaviorResolver.Resolve(run: 100.0f, stamina: 100.0f);

        Assert.True(high.WalkSpeedMultiplier > low.WalkSpeedMultiplier);
        Assert.True(high.RestSecondsMin < low.RestSecondsMin);
        Assert.True(high.RestSecondsMax < low.RestSecondsMax);
        Assert.True(low.RestSecondsMin >= 0.0f);
        Assert.True(high.RestSecondsMax >= high.RestSecondsMin);
    }

    [Fact]
    public void Resolve_HigherSwimChoosesShorelineMoreOften()
    {
        var low = VoidlingAmbientBehaviorResolver.Resolve(run: 50.0f, stamina: 50.0f, swim: 0.0f);
        var high = VoidlingAmbientBehaviorResolver.Resolve(run: 50.0f, stamina: 50.0f, swim: 100.0f);

        Assert.True(high.ShorelineTargetChance > low.ShorelineTargetChance);
        Assert.InRange(low.ShorelineTargetChance, 0.0f, 1.0f);
        Assert.InRange(high.ShorelineTargetChance, 0.0f, 1.0f);
    }

    [Fact]
    public void Resolve_ClampsMalformedAndOutOfRangeInputs()
    {
        var malformed = VoidlingAmbientBehaviorResolver.Resolve(float.NaN, float.PositiveInfinity, float.NaN);
        var minimum = VoidlingAmbientBehaviorResolver.Resolve(0.0f, 0.0f, 0.0f);
        var maximum = VoidlingAmbientBehaviorResolver.Resolve(100.0f, 100.0f, 100.0f);
        var beyond = VoidlingAmbientBehaviorResolver.Resolve(9999.0f, 9999.0f, 9999.0f);

        Assert.Equal(minimum, malformed);
        Assert.Equal(maximum, beyond);
    }
}
