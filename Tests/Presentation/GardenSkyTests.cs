using Voidling.Presentation.Garden.Atmosphere;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class GardenSkyTests
{
    [Theory]
    [InlineData(12.0f, 0.0f)]
    [InlineData(2.0f, 1.0f)]
    [InlineData(23.5f, 1.0f)]
    public void Night_IsFullDayAtNoonAndFullNightAfterDark(float hour, float expected)
        => Assert.Equal(expected, GardenSky.Night(hour), 3);

    [Fact]
    public void Night_BrightensThroughTheDawn()
    {
        var previous = GardenSky.Night(5.0f);
        for (var hour = 5.1f; hour <= 8.0f; hour += 0.1f)
        {
            var current = GardenSky.Night(hour);
            Assert.True(current <= previous + 1e-4f);
            previous = current;
        }
    }

    [Fact]
    public void Night_WrapsAroundMidnight()
        => Assert.Equal(GardenSky.Night(1.0f), GardenSky.Night(25.0f), 4);

    [Fact]
    public void GoldenHour_PeaksAtSunriseAndSunsetOnly()
    {
        Assert.True(GardenSky.GoldenHour(7.2f) > 0.9f);
        Assert.True(GardenSky.GoldenHour(18.6f) > 0.9f);
        Assert.Equal(0.0f, GardenSky.GoldenHour(13.0f));
        Assert.Equal(0.0f, GardenSky.GoldenHour(1.0f));
    }

    [Fact]
    public void Fireflies_ComeOutAfterDuskAndLeaveBeforeDay()
    {
        Assert.Equal(0.0f, GardenSky.Fireflies(12.0f));
        Assert.Equal(1.0f, GardenSky.Fireflies(23.0f), 3);
        Assert.Equal(1.0f, GardenSky.Fireflies(1.0f), 3);
        Assert.Equal(0.0f, GardenSky.Fireflies(6.0f));
    }

    [Fact]
    public void Mist_OnlyHangsAroundDawn()
    {
        Assert.True(GardenSky.Mist(6.3f) > 0.9f);
        Assert.Equal(0.0f, GardenSky.Mist(12.0f));
        Assert.Equal(0.0f, GardenSky.Mist(21.0f));
    }
}
