using System;
using System.Collections.Generic;
using System.Linq;

namespace Voidling.Domain.Racing;

public sealed record RaceCourseDefinition(string Id, int Version, RaceCourse Course)
{
    public RaceCourseDefinition : this(
        NormalizeId(Id),
        Version > 0 ? Version : throw new ArgumentOutOfRangeException(nameof(Version), "Course version must be positive."),
        Course ?? throw new ArgumentNullException(nameof(Course)))
    {
    }

    private static string NormalizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Course ID is required.", nameof(value));
        if (value.Length > 64)
            throw new ArgumentException("Course ID cannot exceed 64 characters.", nameof(value));
        return value.Trim();
    }
}

/// <summary>
/// One semantic source for authored deterministic race-course data. Course IDs/versions are stable
/// protocol/content identities; coordinates remain pure Domain data and never come from Presentation.
/// Adding a standard course means adding one definition here plus deterministic tests, not branching
/// RaceSimulation or multiplayer rules.
/// </summary>
public static class RaceCourseCatalog
{
    public static RaceCourseDefinition Demo { get; } = new(
        "demo",
        1,
        new RaceCourse(
            startX: 70.0f,
            endX: 1810.0f,
            glideLaunchStartX: 1008.0f,
            segments: new[]
            {
                new RaceCourseSegment("ground-start", 70.0f, 500.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("swim", 500.0f, 760.0f, RaceSegmentKind.Swim),
                new RaceCourseSegment("ground-middle", 760.0f, 1080.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("glide", 1080.0f, 1370.0f, RaceSegmentKind.Glide),
                new RaceCourseSegment("ground-finish", 1370.0f, 1810.0f, RaceSegmentKind.Ground)
            },
            obstacles: new[] { 340.0f, 890.0f, 1510.0f, 1660.0f }));

    /// <summary>
    /// Longer standard-race content using only mechanics already owned by RaceSimulation: more
    /// terrain changes and hurdles, but no new hidden rule, personality modifier, physics outcome,
    /// or economy/cup requirement. Presentation/UX can expose this through a later projection pass.
    /// </summary>
    public static RaceCourseDefinition LongStandard { get; } = new(
        "long-standard",
        1,
        new RaceCourse(
            startX: 70.0f,
            endX: 2250.0f,
            glideLaunchStartX: 1270.0f,
            segments: new[]
            {
                new RaceCourseSegment("ground-start", 70.0f, 430.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("swim-one", 430.0f, 700.0f, RaceSegmentKind.Swim),
                new RaceCourseSegment("ground-middle-one", 700.0f, 980.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("swim-two", 980.0f, 1180.0f, RaceSegmentKind.Swim),
                new RaceCourseSegment("ground-middle-two", 1180.0f, 1350.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("glide", 1350.0f, 1680.0f, RaceSegmentKind.Glide),
                new RaceCourseSegment("ground-finish", 1680.0f, 2250.0f, RaceSegmentKind.Ground)
            },
            obstacles: new[] { 310.0f, 820.0f, 1240.0f, 1780.0f, 1980.0f, 2140.0f }));

    private static readonly IReadOnlyList<RaceCourseDefinition> Definitions = Array.AsReadOnly(new[]
    {
        Demo,
        LongStandard
    });

    public static IReadOnlyList<RaceCourseDefinition> All => Definitions;

    public static bool TryGet(string id, int version, out RaceCourseDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.Ordinal) &&
            candidate.Version == version)!;
        return definition != null;
    }
}
