using System;

namespace Voidling.Presentation.Garden.Atmosphere;

public enum GardenWeatherKind
{
    Clear,
    Overcast,
    Rain,
    Storm
}

/// <summary>
/// How much of each weather ingredient is in the air, each 0..1. Blending two of these is how one
/// kind of weather turns into the next.
/// </summary>
public readonly record struct GardenWeatherState(float Cloud, float Rain, float Storm, float Wind)
{
    public static GardenWeatherState For(GardenWeatherKind kind) => kind switch
    {
        GardenWeatherKind.Overcast => new GardenWeatherState(0.65f, 0.0f, 0.0f, 0.35f),
        GardenWeatherKind.Rain => new GardenWeatherState(0.85f, 0.70f, 0.0f, 0.50f),
        GardenWeatherKind.Storm => new GardenWeatherState(1.00f, 1.00f, 1.0f, 1.00f),
        _ => new GardenWeatherState(0.15f, 0.0f, 0.0f, 0.20f)
    };

    public GardenWeatherState Lerp(GardenWeatherState to, float weight)
    {
        var t = Math.Clamp(weight, 0.0f, 1.0f);
        return new GardenWeatherState(
            Cloud + (to.Cloud - Cloud) * t,
            Rain + (to.Rain - Rain) * t,
            Storm + (to.Storm - Storm) * t,
            Wind + (to.Wind - Wind) * t);
    }
}

/// <summary>
/// Cosmetic Garden weather from the player's local clock. The day is cut into fixed slots and each
/// slot's weather is a hash of the date and the slot, so the sky is the same for everyone looking at
/// the same hour and survives a restart, with no state to save. Each change blends in over
/// <see cref="TransitionSeconds"/>.
///
/// Presentation only, like <see cref="GardenEnvironmentPalette"/>: care, training, racing,
/// genetics, economy and persistence must never read it.
/// </summary>
public static class GardenWeatherSchedule
{
    public const int SlotMinutes = 40;
    public const double TransitionSeconds = 90.0;

    /// <summary>Share of slots for clear, overcast and rain; storms take what is left.</summary>
    private const double ClearShare = 0.56;
    private const double OvercastShare = 0.20;
    private const double RainShare = 0.17;

    public static GardenWeatherKind KindAt(DateTime localTime)
    {
        var slot = (int)(localTime.TimeOfDay.TotalMinutes / SlotMinutes);
        var roll = Roll(localTime.Year, localTime.DayOfYear, slot);
        if (roll < ClearShare) return GardenWeatherKind.Clear;
        if (roll < ClearShare + OvercastShare) return GardenWeatherKind.Overcast;
        if (roll < ClearShare + OvercastShare + RainShare) return GardenWeatherKind.Rain;
        return GardenWeatherKind.Storm;
    }

    /// <summary>
    /// The weather at a moment: the slot's own weather, blending in from the previous slot's over
    /// the first <see cref="TransitionSeconds"/> of the slot.
    /// </summary>
    public static GardenWeatherState Resolve(DateTime localTime)
    {
        var current = GardenWeatherState.For(KindAt(localTime));
        var intoSlot = localTime.TimeOfDay.TotalSeconds % (SlotMinutes * 60.0);
        if (intoSlot >= TransitionSeconds)
            return current;

        var previous = GardenWeatherState.For(KindAt(localTime.AddSeconds(-intoSlot - 1.0)));
        var t = (float)(intoSlot / TransitionSeconds);
        return previous.Lerp(current, t * t * (3.0f - 2.0f * t));
    }

    /// <summary>A stable 0..1 roll per date and slot (FNV-1a, never GetHashCode).</summary>
    private static double Roll(int year, int dayOfYear, int slot)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var value in new[] { year, dayOfYear, slot, 0x5EA7 })
            {
                hash = (hash ^ (uint)(value & 0xFF)) * 16777619u;
                hash = (hash ^ (uint)((value >> 8) & 0xFF)) * 16777619u;
                hash = (hash ^ (uint)((value >> 16) & 0xFF)) * 16777619u;
            }

            hash ^= hash >> 13;
            hash *= 0x5bd1e995u;
            hash ^= hash >> 15;
            return (hash % 100000u) / 100000.0;
        }
    }
}
