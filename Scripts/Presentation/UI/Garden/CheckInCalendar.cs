using System;

namespace Voidling.Presentation.UI.Garden;

/// <summary>How one day of the check-in reward cycle is stamped.</summary>
public enum CheckInStamp
{
    /// <summary>Claimed earlier in this cycle.</summary>
    Done,
    /// <summary>Today, still waiting to be claimed.</summary>
    Today,
    /// <summary>Today, already claimed.</summary>
    TodayClaimed,
    /// <summary>Later in the cycle.</summary>
    Upcoming
}

/// <summary>
/// Lays the daily check-in's reward cycle out as a row of stamps, from what the status already
/// says (whether today can be claimed and the running streak). Presentation only: the rewards and
/// the streak rules stay in the Application layer's check-in use case.
/// </summary>
public static class CheckInCalendar
{
    /// <summary>The stamp for each day of a <paramref name="cycleLength"/>-day reward cycle.</summary>
    public static CheckInStamp[] Stamps(bool canClaim, int streak, int cycleLength)
    {
        if (cycleLength <= 0) return Array.Empty<CheckInStamp>();
        var streakToday = canClaim ? Math.Max(0, streak) + 1 : Math.Max(1, streak);
        var today = (streakToday - 1) % cycleLength;
        var stamps = new CheckInStamp[cycleLength];
        for (var day = 0; day < cycleLength; day++)
        {
            stamps[day] = day < today ? CheckInStamp.Done
                : day > today ? CheckInStamp.Upcoming
                : canClaim ? CheckInStamp.Today : CheckInStamp.TodayClaimed;
        }
        return stamps;
    }
}
