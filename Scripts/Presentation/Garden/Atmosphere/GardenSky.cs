using System;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// How dark, how golden and how misty the Garden is at a local hour. Pure presentation, like
/// <see cref="GardenEnvironmentPalette"/>: nothing that decides an outcome may read it.
/// </summary>
public static class GardenSky
{
    /// <summary>0 in full day, 1 in full night, easing through dawn (5-8) and dusk (18-22).</summary>
    public static float Night(float localHour)
    {
        var hour = Wrap(localHour);
        if (hour < 5.0f) return 1.0f;
        if (hour < 8.0f) return 1.0f - Smooth((hour - 5.0f) / 3.0f);
        if (hour < 18.0f) return 0.0f;
        if (hour < 22.0f) return Smooth((hour - 18.0f) / 4.0f);
        return 1.0f;
    }

    /// <summary>Warm low sun: peaks shortly after sunrise and before sunset.</summary>
    public static float GoldenHour(float localHour)
    {
        var hour = Wrap(localHour);
        return Math.Max(Bump(hour, 7.2f, 1.4f), Bump(hour, 18.6f, 1.5f));
    }

    /// <summary>Mist on the water around dawn.</summary>
    public static float Mist(float localHour) => Bump(Wrap(localHour), 6.3f, 1.8f);

    /// <summary>Fireflies come out as the light goes and stay until the small hours.</summary>
    public static float Fireflies(float localHour)
    {
        var hour = Wrap(localHour);
        if (hour >= 19.0f) return Smooth(Math.Min(1.0f, (hour - 19.0f) / 1.5f));
        if (hour < 3.0f) return 1.0f;
        if (hour < 5.0f) return 1.0f - Smooth((hour - 3.0f) / 2.0f);
        return 0.0f;
    }

    private static float Bump(float hour, float center, float halfWidth)
    {
        var t = 1.0f - Math.Abs(hour - center) / halfWidth;
        return t <= 0.0f ? 0.0f : Smooth(t);
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private static float Wrap(float hour)
    {
        var wrapped = hour % 24.0f;
        return wrapped < 0.0f ? wrapped + 24.0f : wrapped;
    }
}
