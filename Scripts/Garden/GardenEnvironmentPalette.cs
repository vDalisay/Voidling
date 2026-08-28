using System;
using Godot;

namespace Voidling.Presentation.Garden;

/// <summary>
/// Pure presentation mapping from the player's local calendar/clock to a cosmetic Garden tint.
/// This must never be consulted by simulation, care, racing, genetics, economy, or persistence.
/// </summary>
public static class GardenEnvironmentPalette
{
    private static readonly Color Night = new(0.58f, 0.66f, 0.78f, 1.0f);
    private static readonly Color Dawn = new(0.93f, 0.82f, 0.76f, 1.0f);
    private static readonly Color Day = Colors.White;
    private static readonly Color Dusk = new(0.92f, 0.75f, 0.70f, 1.0f);

    public static Color Resolve(DateTime localTime)
    {
        var hour = (float)localTime.TimeOfDay.TotalHours;
        var daypart = ResolveDaypart(hour);
        var season = ResolveSeason(localTime.Month);
        return Multiply(daypart, season);
    }

    public static Color ResolveDaypart(float localHour)
    {
        var hour = Mathf.PosMod(localHour, 24.0f);
        if (hour < 5.0f)
            return Night;
        if (hour < 7.0f)
            return Blend(Night, Dawn, (hour - 5.0f) / 2.0f);
        if (hour < 9.0f)
            return Blend(Dawn, Day, (hour - 7.0f) / 2.0f);
        if (hour < 18.0f)
            return Day;
        if (hour < 20.0f)
            return Blend(Day, Dusk, (hour - 18.0f) / 2.0f);
        if (hour < 22.0f)
            return Blend(Dusk, Night, (hour - 20.0f) / 2.0f);
        return Night;
    }

    public static Color ResolveSeason(int month)
        => month switch
        {
            12 or 1 or 2 => new Color(0.94f, 0.97f, 1.00f, 1.0f),
            3 or 4 or 5 => new Color(0.97f, 1.00f, 0.96f, 1.0f),
            6 or 7 or 8 => new Color(1.00f, 0.99f, 0.94f, 1.0f),
            9 or 10 or 11 => new Color(1.00f, 0.93f, 0.84f, 1.0f),
            _ => Colors.White
        };

    private static Color Blend(Color from, Color to, float weight)
    {
        var t = Mathf.Clamp(weight, 0.0f, 1.0f);
        return new Color(
            Mathf.Lerp(from.R, to.R, t),
            Mathf.Lerp(from.G, to.G, t),
            Mathf.Lerp(from.B, to.B, t),
            1.0f);
    }

    private static Color Multiply(Color first, Color second)
        => new(
            first.R * second.R,
            first.G * second.G,
            first.B * second.B,
            1.0f);
}
