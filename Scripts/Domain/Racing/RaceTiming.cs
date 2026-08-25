using System;

namespace Voidling.Domain.Racing;

/// <summary>
/// Canonical conversion between deterministic simulation steps and externally displayed/projected
/// race time. Keeping this in Domain prevents presentation frame time from becoming a result input.
/// </summary>
public static class RaceTiming
{
    public const int FixedStepsPerSecond = 60;

    public static int FixedStepsToMilliseconds(int fixedSteps)
    {
        if (fixedSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(fixedSteps));

        var rounded = ((long)fixedSteps * 1000L + FixedStepsPerSecond / 2L) /
                      FixedStepsPerSecond;
        if (rounded <= 0 || rounded > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(fixedSteps));
        return (int)rounded;
    }
}
