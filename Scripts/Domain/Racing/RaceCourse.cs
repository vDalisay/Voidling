using System;
using System.Collections.Generic;
using System.Linq;

namespace Voidling.Domain.Racing;

public enum RaceSegmentKind
{
    Ground,
    Swim,
    Glide,
    Climb
}

public readonly record struct RaceCourseSegment(
    string Id,
    float StartX,
    float EndX,
    RaceSegmentKind Kind)
{
    public bool Contains(float x) => x >= StartX && x < EndX;
}

/// <summary>
/// Pure course definition for the current demo race. It contains result-affecting geometry only;
/// decorative track rendering remains presentation-owned. Future route/shortcut work can add
/// authored branches without putting result rules in RaceController.
/// </summary>
public sealed class RaceCourse
{
    public static RaceCourse Demo { get; } = new(
        startX: 70.0f,
        endX: 1810.0f,
        glideLaunchStartX: 1008.0f,
        segments: new[]
        {
            new RaceCourseSegment("ground-start", 70.0f, 500.0f, RaceSegmentKind.Ground),
            new RaceCourseSegment("swim", 500.0f, 760.0f, RaceSegmentKind.Swim),
            new RaceCourseSegment("climb", 760.0f, 860.0f, RaceSegmentKind.Climb),
            new RaceCourseSegment("ground-middle", 860.0f, 1080.0f, RaceSegmentKind.Ground),
            new RaceCourseSegment("glide", 1080.0f, 1370.0f, RaceSegmentKind.Glide),
            new RaceCourseSegment("ground-finish", 1370.0f, 1810.0f, RaceSegmentKind.Ground)
        },
        obstacles: new[] { 340.0f, 890.0f, 1510.0f, 1660.0f });

    public RaceCourse(
        float startX,
        float endX,
        float glideLaunchStartX,
        IEnumerable<RaceCourseSegment> segments,
        IEnumerable<float> obstacles)
    {
        if (endX <= startX)
            throw new ArgumentOutOfRangeException(nameof(endX), "Race end must be after race start.");

        StartX = startX;
        EndX = endX;
        GlideLaunchStartX = glideLaunchStartX;
        Segments = Array.AsReadOnly((segments ?? throw new ArgumentNullException(nameof(segments))).ToArray());
        Obstacles = Array.AsReadOnly((obstacles ?? throw new ArgumentNullException(nameof(obstacles))).OrderBy(x => x).ToArray());

        if (Segments.Count == 0)
            throw new ArgumentException("Race course must contain at least one segment.", nameof(segments));
        if (Segments.Any(segment => segment.EndX <= segment.StartX))
            throw new ArgumentException("Race course segments must have positive length.", nameof(segments));
        if (Segments.Any(segment => segment.StartX < StartX || segment.EndX > EndX))
            throw new ArgumentException("Race course segments must remain inside the course bounds.", nameof(segments));
        if (Obstacles.Any(x => x < StartX || x >= EndX))
            throw new ArgumentException("Race obstacles must remain inside the course bounds.", nameof(obstacles));

        GlideSegment = Segments.SingleOrDefault(segment => segment.Kind == RaceSegmentKind.Glide);
    }

    public float StartX { get; }
    public float EndX { get; }
    public float GlideLaunchStartX { get; }
    public IReadOnlyList<RaceCourseSegment> Segments { get; }
    public IReadOnlyList<float> Obstacles { get; }
    public RaceCourseSegment GlideSegment { get; }

    public RaceSegmentKind SegmentKindAt(float x)
        => Segments.FirstOrDefault(segment => segment.Contains(x)).Kind;

    public RaceTerrain TerrainAt(float x, bool glideFailed)
    {
        return SegmentKindAt(x) switch
        {
            RaceSegmentKind.Swim => RaceTerrain.Swim,
            RaceSegmentKind.Glide when glideFailed => RaceTerrain.FailedGlideSwim,
            RaceSegmentKind.Glide => RaceTerrain.Glide,
            RaceSegmentKind.Climb => RaceTerrain.Climb,
            _ => RaceTerrain.Ground
        };
    }
}
