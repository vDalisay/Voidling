using System;
using System.Linq;
using Voidling.Presentation.UI.Motion;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class UiMotionMathTests
{
    [Theory]
    [InlineData(0, 120)]
    [InlineData(1250, 1180)]
    [InlineData(5, 6)]
    [InlineData(999, 1_000_000)]
    public void RollValue_StartsAtFromAndEndsExactlyOnTheTarget(long from, long to)
    {
        Assert.Equal(from, UiMotionMath.RollValue(from, to, 0.0f));
        Assert.Equal(to, UiMotionMath.RollValue(from, to, 1.0f));
        Assert.Equal(to, UiMotionMath.RollValue(from, to, 1.5f));
    }

    [Theory]
    [InlineData(0, 120)]
    [InlineData(1250, 1180)]
    public void RollValue_MovesOneWayAndNeverPassesTheTarget(long from, long to)
    {
        var shown = Enumerable.Range(0, 101).Select(step => UiMotionMath.RollValue(from, to, step / 100.0f)).ToArray();
        var direction = Math.Sign(to - from);
        for (var i = 1; i < shown.Length; i++)
        {
            Assert.True(Math.Sign(shown[i] - shown[i - 1]) is 0 || Math.Sign(shown[i] - shown[i - 1]) == direction);
            Assert.InRange(shown[i], Math.Min(from, to), Math.Max(from, to));
        }
    }

    [Fact]
    public void RollSeconds_IsZeroForNoChangeAndStaysWithinItsBounds()
    {
        Assert.Equal(0.0f, UiMotionMath.RollSeconds(40, 40));
        Assert.InRange(UiMotionMath.RollSeconds(40, 41), 0.25f, 0.9f);
        Assert.InRange(UiMotionMath.RollSeconds(0, 1_000_000_000), 0.25f, 0.9f);
        Assert.True(UiMotionMath.RollSeconds(0, 5) < UiMotionMath.RollSeconds(0, 5000));
    }

    [Fact]
    public void StaggerDelay_StepsEachItemAndCompressesLongLists()
    {
        Assert.Equal(0.0f, UiMotionMath.StaggerDelay(0, 5, 0.03f, 0.3f));
        Assert.Equal(0.06f, UiMotionMath.StaggerDelay(2, 5, 0.03f, 0.3f), 5);
        // Forty items at 30 ms would take 1.2 s; the last one still starts by the 0.3 s cap.
        Assert.Equal(0.3f, UiMotionMath.StaggerDelay(39, 40, 0.03f, 0.3f), 5);
        Assert.True(UiMotionMath.StaggerDelay(20, 40, 0.03f, 0.3f) < UiMotionMath.StaggerDelay(21, 40, 0.03f, 0.3f));
    }

    [Theory]
    [InlineData(12, "+12")]
    [InlineData(-8, "-8")]
    [InlineData(0, "0")]
    public void SignedDelta_WritesGainsWithAPlus(long delta, string expected)
        => Assert.Equal(expected, UiMotionMath.SignedDelta(delta));

    [Fact]
    public void Burst_IsDeterministicForASeedAndDiffersBetweenSeeds()
    {
        var first = UiMotionMath.Burst(18, 42u, 55.0f, 120.0f, 4);
        var again = UiMotionMath.Burst(18, 42u, 55.0f, 120.0f, 4);
        var other = UiMotionMath.Burst(18, 43u, 55.0f, 120.0f, 4);

        Assert.Equal(18, first.Count);
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.All(first, particle => Assert.InRange(particle.ColorIndex, 0, 3));
        Assert.Contains(first, particle => particle.Sparkle);
    }

    [Fact]
    public void BurstOffset_IsWholePixelsAndFallsUnderGravity()
    {
        var particle = new BurstParticle(0.0f, -60.0f, 1.0f, 1, 0, false);
        var (_, early) = UiMotionMath.BurstOffset(particle, 0.1f);
        var (_, late) = UiMotionMath.BurstOffset(particle, 0.6f);
        Assert.True(early < 0);
        Assert.True(late > early);
    }

    [Fact]
    public void FlightPoint_StartsAndLandsExactlyAndArcsUpInBetween()
    {
        Assert.Equal((10, 100), UiMotionMath.FlightPoint(10, 100, 200, 20, 30, 0.0f));
        Assert.Equal((200, 20), UiMotionMath.FlightPoint(10, 100, 200, 20, 30, 1.0f));
        var (_, middle) = UiMotionMath.FlightPoint(10, 100, 200, 100, 30, 0.5f);
        Assert.Equal(70, middle);
    }

    [Fact]
    public void BobSteps_AreWholePixelsThatReturnToRest()
    {
        Assert.Equal(0, UiMotionMath.BobSteps[0]);
        Assert.Equal(0, UiMotionMath.BobSteps[^1]);
        Assert.All(UiMotionMath.BobSteps, step => Assert.InRange(step, -3, 0));
    }
}
