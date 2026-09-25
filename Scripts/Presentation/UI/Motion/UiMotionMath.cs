using System;
using System.Collections.Generic;

namespace Voidling.Presentation.UI.Motion;

/// <summary>One pixel of a confirmation burst: where it flies, for how long, and how it looks.</summary>
public readonly record struct BurstParticle(float VelocityX, float VelocityY, float Lifetime, int Size, int ColorIndex, bool Sparkle);

/// <summary>
/// The arithmetic behind the UI's motion, kept free of Godot so it can be tested: easing, counter
/// rolls, stagger timing, burst trajectories and the stepped offsets idle loops use. Everything
/// that ends on screen at rest comes out as a whole pixel or an exact target value.
/// </summary>
public static class UiMotionMath
{
    /// <summary>Gravity for burst pixels, in UI pixels per second squared.</summary>
    public const float BurstGravity = 260.0f;

    /// <summary>Cubic ease-out; 0 at 0, exactly 1 at 1.</summary>
    public static float EaseOutCubic(float t)
    {
        t = Clamp01(t);
        var inverse = 1.0f - t;
        return 1.0f - inverse * inverse * inverse;
    }

    /// <summary>
    /// The number a rolling counter shows at <paramref name="t"/> of its roll: eased, whole, never
    /// past the target and exactly the target once the roll is done.
    /// </summary>
    public static long RollValue(long from, long to, float t)
    {
        if (t >= 1.0f || from == to) return to;
        if (t <= 0.0f) return from;
        var eased = EaseOutCubic(t);
        var value = from + (long)Math.Floor((to - from) * (double)eased);
        return to > from ? Math.Min(value, to) : Math.Max(value, to);
    }

    /// <summary>
    /// How long a counter takes to roll: a small change ticks over quickly, a large one runs a
    /// little longer, and nothing drags past <paramref name="longest"/>.
    /// </summary>
    public static float RollSeconds(long from, long to, float shortest = 0.25f, float longest = 0.9f)
    {
        var distance = Math.Abs(to - from);
        if (distance == 0) return 0.0f;
        var seconds = shortest + 0.12f * (float)Math.Log10(distance + 1);
        return Math.Clamp(seconds, shortest, longest);
    }

    /// <summary>
    /// When the <paramref name="index"/>th item of a list starts to appear. Long lists compress
    /// their step so the last item never waits more than <paramref name="maxTotal"/> seconds.
    /// </summary>
    public static float StaggerDelay(int index, int count, float step, float maxTotal)
    {
        if (index <= 0 || count <= 1) return 0.0f;
        var effectiveStep = Math.Min(step, maxTotal / (count - 1));
        return Math.Min(index, count - 1) * effectiveStep;
    }

    /// <summary>"+12" for a gain, "-8" for a loss, "0" for nothing.</summary>
    public static string SignedDelta(long delta)
        => delta > 0 ? "+" + delta : delta.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// A burst's pixels, spread evenly around the circle with a deterministic wobble so the same
    /// seed always gives the same burst. Upward-biased, like a pop of confetti.
    /// </summary>
    public static IReadOnlyList<BurstParticle> Burst(int count, uint seed, float speedMin, float speedMax, int colorCount, int sparkleEvery = 4)
    {
        var particles = new List<BurstParticle>(Math.Max(0, count));
        var state = seed == 0 ? 0x9E3779B9u : seed;
        for (var index = 0; index < count; index++)
        {
            var angleJitter = Next(ref state) - 0.5f;
            var angle = (index + 0.5f + angleJitter * 0.6f) / count * MathF.Tau;
            var speed = speedMin + (speedMax - speedMin) * Next(ref state);
            var lift = 0.35f * speedMax;
            particles.Add(new BurstParticle(
                MathF.Cos(angle) * speed,
                MathF.Sin(angle) * speed - lift,
                0.45f + 0.3f * Next(ref state),
                Next(ref state) > 0.6f ? 2 : 1,
                colorCount <= 0 ? 0 : (int)(Next(ref state) * colorCount) % colorCount,
                sparkleEvery > 0 && index % sparkleEvery == 0));
        }
        return particles;
    }

    /// <summary>A burst pixel's offset from its origin after <paramref name="seconds"/>, snapped to whole pixels.</summary>
    public static (int X, int Y) BurstOffset(BurstParticle particle, float seconds)
    {
        var x = particle.VelocityX * seconds;
        var y = particle.VelocityY * seconds + 0.5f * BurstGravity * seconds * seconds;
        return ((int)MathF.Round(x), (int)MathF.Round(y));
    }

    /// <summary>
    /// A point along a reward's flight from <paramref name="from"/> to <paramref name="to"/>: an arc
    /// that lifts by <paramref name="arc"/> pixels at its middle, snapped to whole pixels.
    /// </summary>
    public static (int X, int Y) FlightPoint(float fromX, float fromY, float toX, float toY, float arc, float t)
    {
        t = Clamp01(t);
        var eased = t * t * (3.0f - 2.0f * t);
        var x = fromX + (toX - fromX) * eased;
        var y = fromY + (toY - fromY) * eased - arc * 4.0f * t * (1.0f - t);
        return ((int)MathF.Round(x), (int)MathF.Round(y));
    }

    /// <summary>The whole-pixel heights an idle bob steps through, one per beat.</summary>
    public static IReadOnlyList<int> BobSteps { get; } = new[] { 0, -1, -2, -2, -1, 0 };

    private static float Clamp01(float value) => float.IsNaN(value) ? 0.0f : Math.Clamp(value, 0.0f, 1.0f);

    /// <summary>xorshift32 in [0, 1): stable across runtimes, unlike GetHashCode or System.Random.</summary>
    private static float Next(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xFFFFFF) / (float)0x1000000;
    }
}
