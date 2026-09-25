using Voidling.Presentation.UI.Garden;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class CheckInCalendarTests
{
    [Fact]
    public void FirstEverCheckIn_LightsTheFirstStamp()
        => Assert.Equal(
            new[] { CheckInStamp.Today, CheckInStamp.Upcoming, CheckInStamp.Upcoming },
            CheckInCalendar.Stamps(canClaim: true, streak: 0, cycleLength: 3));

    [Fact]
    public void ContinuingAStreak_ChecksThePastDaysAndLightsTheNext()
        => Assert.Equal(
            new[] { CheckInStamp.Done, CheckInStamp.Done, CheckInStamp.Today, CheckInStamp.Upcoming },
            CheckInCalendar.Stamps(canClaim: true, streak: 2, cycleLength: 4));

    [Fact]
    public void AlreadyClaimedToday_MarksTodayAsClaimed()
        => Assert.Equal(
            new[] { CheckInStamp.Done, CheckInStamp.TodayClaimed, CheckInStamp.Upcoming },
            CheckInCalendar.Stamps(canClaim: false, streak: 2, cycleLength: 3));

    [Fact]
    public void AStreakLongerThanTheCycle_WrapsIntoANewRow()
        => Assert.Equal(
            new[] { CheckInStamp.Today, CheckInStamp.Upcoming, CheckInStamp.Upcoming },
            CheckInCalendar.Stamps(canClaim: true, streak: 3, cycleLength: 3));

    [Fact]
    public void AnEmptyCycle_HasNoStamps()
        => Assert.Empty(CheckInCalendar.Stamps(canClaim: true, streak: 5, cycleLength: 0));
}
