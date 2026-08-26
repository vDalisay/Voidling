using System;
using System.Collections.Generic;
using System.Linq;

namespace Voidling.Domain.Racing;

public enum RaceSegmentKind
{
    Ground,
    Swim,
    Glide
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
/// Pure, immutable result-affecting race geometry. Decorative rendering remains presentation-owned.
/// Course data is validated and canonicalized here so deterministic simulation/fingerprinting never
/// depends on incidental authoring order.
/// </summary>
public sealed class RaceCourse
{
    private const float CoordinateTolerance = 0.001f;

    /// <summary>
    /// Compatibility alias for the current standard course. New code that needs semantic course
    /// identity should use RaceCourseCatalog instead of inventing another hard-coded course source.
    /// </summary>
    public static RaceCourse Demo => RaceCourseCatalog.Demo.Course;

    public RaceCourse(
        float startX,
        float endX,
        float glideLaunchStartX,
        IEnumerable<RaceCourseSegment> segments,
        IEnumerable<float> obstacles)
    {
        if (!float.IsFinite(startX) || !float.IsFinite(endX) || endX <= startX)
            throw new ArgumentOutOfRangeException(nameof(endX), "Race end must be finite and after race start.");
        if (!float.IsFinite(glideLaunchStartX) || glideLaunchStartX < startX || glideLaunchStartX >= endX)
            throw new ArgumentOutOfRangeException(nameof(glideLaunchStartX), "Glide launch marker must remain inside the course bounds.");

        StartX = startX;
        EndX = endX;
        GlideLaunchStartX = glideLaunchStartX;

        var authoredSegments = (segments ?? throw new ArgumentNullException(nameof(segments))).ToArray();
        if (authoredSegments.Length == 0)
            throw new ArgumentException("Race course must contain at least one segment.", nameof(segments));
        if (authoredSegments.Any(segment =>
                string.IsNullOrWhiteSpace(segment.Id) ||
                !float.IsFinite(segment.StartX) ||
                !float.IsFinite(segment.EndX) ||
                segment.EndX <= segment.StartX))
        {
            throw new ArgumentException("Race course segments require IDs and finite positive lengths.", nameof(segments));
        }
        if (authoredSegments.Select(segment => segment.Id).Distinct(StringComparer.Ordinal).Count() != authoredSegments.Length)
            throw new ArgumentException("Race course segment IDs must be unique.", nameof(segments));

        var orderedSegments = authoredSegments
            .OrderBy(segment => segment.StartX)
            .ThenBy(segment => segment.EndX)
            .ThenBy(segment => segment.Id, StringComparer.Ordinal)
            .ToArray();
        if (orderedSegments.Any(segment => segment.StartX < StartX || segment.EndX > EndX))
            throw new ArgumentException("Race course segments must remain inside the course bounds.", nameof(segments));
        if (!ApproximatelyEqual(orderedSegments[0].StartX, StartX) ||
            !ApproximatelyEqual(orderedSegments[^1].EndX, EndX))
        {
            throw new ArgumentException("Race course segments must cover the full course from start to finish.", nameof(segments));
        }
        for (var i = 1; i < orderedSegments.Length; i++)
        {
            if (!ApproximatelyEqual(orderedSegments[i - 1].EndX, orderedSegments[i].StartX))
                throw new ArgumentException("Race course segments must be contiguous and non-overlapping.", nameof(segments));
        }

        var glideSegments = orderedSegments.Where(segment => segment.Kind == RaceSegmentKind.Glide).ToArray();
        if (glideSegments.Length > 1)
        {
            throw new ArgumentException(
                "The current deterministic glide mechanic supports at most one glide segment per course.",
                nameof(segments));
        }
        if (glideSegments.Length == 1 && GlideLaunchStartX > glideSegments[0].StartX + CoordinateTolerance)
        {
            throw new ArgumentException(
                "Glide launch marker must be at or before the glide segment start.",
                nameof(glideLaunchStartX));
        }

        var orderedObstacles = (obstacles ?? throw new ArgumentNullException(nameof(obstacles)))
            .OrderBy(x => x)
            .ToArray();
        if (orderedObstacles.Any(x => !float.IsFinite(x) || x < StartX || x >= EndX))
            throw new ArgumentException("Race obstacles must be finite and remain inside the course bounds.", nameof(obstacles));

        Segments = Array.AsReadOnly(orderedSegments);
        Obstacles = Array.AsReadOnly(orderedObstacles);
        GlideSegment = glideSegments.SingleOrDefault();
    }

    public float StartX { get; }
    public float EndX { get; }
    public float GlideLaunchStartX { get; }
    public IReadOnlyList<RaceCourseSegment> Segments { get; }
    public IReadOnlyList<float> Obstacles { get; }
    public RaceCourseSegment GlideSegment { get; }
    public bool HasGlideSegment => GlideSegment.Kind == RaceSegmentKind.Glide;

    public RaceSegmentKind SegmentKindAt(float x)
        => Segments.FirstOrDefault(segment => segment.Contains(x)).Kind;

    public RaceTerrain TerrainAt(float x, bool glideFailed)
    {
        return SegmentKindAt(x) switch
        {
            RaceSegmentKind.Swim => RaceTerrain.Swim,
            RaceSegmentKind.Glide when glideFailed => RaceTerrain.FailedGlideSwim,
            RaceSegmentKind.Glide => RaceTerrain.Glide,
            _ => RaceTerrain.Ground
        };
    }

    private static bool ApproximatelyEqual(float left, float right)
        => Math.Abs(left - right) <= CoordinateTolerance;
}
