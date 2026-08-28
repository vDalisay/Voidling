using System;
using Voidling.Presentation.Garden;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class GardenEnvironmentPaletteTests
{
    [Fact]
    public void Resolve_NoonIsBrighterThanMidnightForSameSeason()
    {
        var midnight = GardenEnvironmentPalette.Resolve(new DateTime(2026, 6, 15, 0, 0, 0));
        var noon = GardenEnvironmentPalette.Resolve(new DateTime(2026, 6, 15, 12, 0, 0));

        Assert.True(Brightness(noon) > Brightness(midnight));
    }

    [Fact]
    public void Resolve_SameDaypartChangesWithCalendarSeason()
    {
        var winter = GardenEnvironmentPalette.Resolve(new DateTime(2026, 1, 15, 12, 0, 0));
        var summer = GardenEnvironmentPalette.Resolve(new DateTime(2026, 7, 15, 12, 0, 0));
        var autumn = GardenEnvironmentPalette.Resolve(new DateTime(2026, 10, 15, 12, 0, 0));

        Assert.False(SameRgb(winter, summer));
        Assert.False(SameRgb(summer, autumn));
        Assert.True(winter.B > autumn.B);
        Assert.True(autumn.R >= autumn.B);
    }

    [Fact]
    public void ResolveDaypart_BlendsThroughDawnInsteadOfJumpingDirectlyToDay()
    {
        var night = GardenEnvironmentPalette.ResolveDaypart(5.0f);
        var dawn = GardenEnvironmentPalette.ResolveDaypart(6.0f);
        var day = GardenEnvironmentPalette.ResolveDaypart(9.0f);

        Assert.True(Brightness(dawn) > Brightness(night));
        Assert.True(Brightness(day) > Brightness(dawn));
    }

    private static float Brightness(Godot.Color color)
        => color.R + color.G + color.B;

    private static bool SameRgb(Godot.Color first, Godot.Color second)
        => Godot.Mathf.IsEqualApprox(first.R, second.R) &&
           Godot.Mathf.IsEqualApprox(first.G, second.G) &&
           Godot.Mathf.IsEqualApprox(first.B, second.B);
}
