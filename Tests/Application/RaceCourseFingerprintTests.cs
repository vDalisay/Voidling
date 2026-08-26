using Voidling.Application.Multiplayer.Racing;
using Voidling.Domain.Racing;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class RaceCourseFingerprintTests
{
    [Fact]
    public void Fingerprint_ChangesWhenResultAffectingObstacleTriggerOffsetChanges()
    {
        var first = CreateCourse(4.0f);
        var second = CreateCourse(5.0f);

        Assert.NotEqual(
            RaceCourseFingerprint.Compute(first),
            RaceCourseFingerprint.Compute(second));
    }

    private static RaceCourse CreateCourse(float obstacleTriggerOffsetX)
        => new(
            startX: 0.0f,
            endX: 300.0f,
            glideLaunchStartX: 150.0f,
            segments: new[]
            {
                new RaceCourseSegment("ground", 0.0f, 300.0f, RaceSegmentKind.Ground)
            },
            obstacles: new[] { 120.0f },
            obstacleTriggerOffsetX: obstacleTriggerOffsetX);
}
