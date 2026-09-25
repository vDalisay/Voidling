using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Presentation.Garden.Atmosphere;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class GardenWeatherScheduleTests
{
    private static readonly DateTime Day = new(2026, 9, 25);

    [Fact]
    public void KindAt_IsTheSameForTheSameMoment()
    {
        var moment = Day.AddHours(14.3);

        Assert.Equal(GardenWeatherSchedule.KindAt(moment), GardenWeatherSchedule.KindAt(moment));
        Assert.Equal(GardenWeatherSchedule.Resolve(moment), GardenWeatherSchedule.Resolve(moment));
    }

    [Fact]
    public void KindAt_HoldsForAWholeSlot()
    {
        var slotStart = Day.AddMinutes(GardenWeatherSchedule.SlotMinutes * 7);
        var kind = GardenWeatherSchedule.KindAt(slotStart);

        for (var minute = 0; minute < GardenWeatherSchedule.SlotMinutes; minute++)
            Assert.Equal(kind, GardenWeatherSchedule.KindAt(slotStart.AddMinutes(minute)));
    }

    [Fact]
    public void KindAt_SpreadsEveryKindOfWeatherOverAYear()
    {
        var counts = new Dictionary<GardenWeatherKind, int>();
        var slots = 0;
        for (var day = 0; day < 365; day++)
        {
            for (var slot = 0; slot < 24 * 60 / GardenWeatherSchedule.SlotMinutes; slot++)
            {
                var kind = GardenWeatherSchedule.KindAt(Day.AddDays(day).AddMinutes(slot * GardenWeatherSchedule.SlotMinutes));
                counts[kind] = counts.GetValueOrDefault(kind) + 1;
                slots++;
            }
        }

        Assert.Equal(Enum.GetValues<GardenWeatherKind>().Length, counts.Count);
        Assert.InRange(counts[GardenWeatherKind.Clear] / (double)slots, 0.50, 0.62);
        Assert.InRange(counts[GardenWeatherKind.Rain] / (double)slots, 0.13, 0.21);
        Assert.InRange(counts[GardenWeatherKind.Storm] / (double)slots, 0.04, 0.10);
    }

    [Fact]
    public void Resolve_SettlesOnTheSlotsOwnWeatherAfterTheTransition()
    {
        var slotStart = Day.AddMinutes(GardenWeatherSchedule.SlotMinutes * 11);
        var settled = slotStart.AddSeconds(GardenWeatherSchedule.TransitionSeconds + 1.0);

        Assert.Equal(
            GardenWeatherState.For(GardenWeatherSchedule.KindAt(settled)),
            GardenWeatherSchedule.Resolve(settled));
    }

    [Fact]
    public void Resolve_NeverJumpsBetweenNeighbouringSeconds()
    {
        var start = Day.AddHours(6);
        var previous = GardenWeatherSchedule.Resolve(start);
        for (var second = 1; second < 6 * 3600; second += 7)
        {
            var current = GardenWeatherSchedule.Resolve(start.AddSeconds(second));
            Assert.True(MathF.Abs(current.Rain - previous.Rain) < 0.2f, $"Rain jumped at {second}s.");
            Assert.True(MathF.Abs(current.Cloud - previous.Cloud) < 0.2f, $"Cloud jumped at {second}s.");
            previous = current;
        }
    }

    [Fact]
    public void For_RainIsWetterThanOvercastAndStormIsTheWettest()
    {
        var clear = GardenWeatherState.For(GardenWeatherKind.Clear);
        var overcast = GardenWeatherState.For(GardenWeatherKind.Overcast);
        var rain = GardenWeatherState.For(GardenWeatherKind.Rain);
        var storm = GardenWeatherState.For(GardenWeatherKind.Storm);

        Assert.Equal(0.0f, clear.Rain);
        Assert.Equal(0.0f, overcast.Rain);
        Assert.True(rain.Rain > 0.0f);
        Assert.True(storm.Rain >= rain.Rain);
        Assert.True(new[] { clear, overcast, rain, storm }.All(state => state.Storm <= 1.0f && state.Wind <= 1.0f));
    }
}
