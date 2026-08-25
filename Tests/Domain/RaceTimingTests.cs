using System;
using Voidling.Domain.Racing;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class RaceTimingTests
{
    [Theory]
    [InlineData(1, 17)]
    [InlineData(30, 500)]
    [InlineData(60, 1000)]
    [InlineData(90, 1500)]
    [InlineData(61, 1017)]
    public void FixedStepsConvertToStableRoundedMilliseconds(int fixedSteps, int expectedMilliseconds)
        => Assert.Equal(expectedMilliseconds, RaceTiming.FixedStepsToMilliseconds(fixedSteps));

    [Fact]
    public void NonPositiveFixedStepsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RaceTiming.FixedStepsToMilliseconds(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RaceTiming.FixedStepsToMilliseconds(-1));
    }
}
