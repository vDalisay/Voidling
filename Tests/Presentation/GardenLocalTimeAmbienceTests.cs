using System;
using Voidling.Presentation.Garden;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class GardenLocalTimeAmbienceTests
{
    [Theory]
    [InlineData(0, 0, GardenLocalTimeAmbience.MaximumNightOverlayAlpha)]
    [InlineData(4, 59, GardenLocalTimeAmbience.MaximumNightOverlayAlpha)]
    [InlineData(7, 0, 0.0f)]
    [InlineData(12, 0, 0.0f)]
    [InlineData(18, 0, 0.0f)]
    [InlineData(20, 0, GardenLocalTimeAmbience.MaximumNightOverlayAlpha)]
    [InlineData(23, 59, GardenLocalTimeAmbience.MaximumNightOverlayAlpha)]
    public void NightOverlayAlpha_UsesExpectedDayAndNightAnchors(int hour, int minute, float expected)
    {
        var alpha = GardenLocalTimeAmbience.NightOverlayAlpha(new TimeSpan(hour, minute, 0));

        Assert.Equal(expected, alpha, 3);
    }

    [Fact]
    public void NightOverlayAlpha_FadesThroughDawnAndDusk()
    {
        var dawn = GardenLocalTimeAmbience.NightOverlayAlpha(new TimeSpan(6, 0, 0));
        var dusk = GardenLocalTimeAmbience.NightOverlayAlpha(new TimeSpan(19, 0, 0));

        Assert.Equal(GardenLocalTimeAmbience.MaximumNightOverlayAlpha * 0.5f, dawn, 3);
        Assert.Equal(GardenLocalTimeAmbience.MaximumNightOverlayAlpha * 0.5f, dusk, 3);
    }
}
