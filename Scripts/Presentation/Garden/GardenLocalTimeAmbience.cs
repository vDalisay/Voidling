using System;

namespace Voidling.Presentation.Garden;

/// <summary>
/// Pure presentation policy for mapping the computer's local time to a cosmetic Garden night tint.
/// It owns no progression or save state; closing/reopening the game simply re-evaluates local time.
/// </summary>
public static class GardenLocalTimeAmbience
{
    public const float MaximumNightOverlayAlpha = 0.30f;

    public static float NightOverlayAlpha(TimeSpan localTime)
    {
        var hours = localTime.TotalHours % 24.0;
        if (hours < 0.0)
            hours += 24.0;

        if (hours < 5.0)
            return MaximumNightOverlayAlpha;
        if (hours < 7.0)
            return MaximumNightOverlayAlpha * (float)((7.0 - hours) / 2.0);
        if (hours < 18.0)
            return 0.0f;
        if (hours < 20.0)
            return MaximumNightOverlayAlpha * (float)((hours - 18.0) / 2.0);
        return MaximumNightOverlayAlpha;
    }
}
