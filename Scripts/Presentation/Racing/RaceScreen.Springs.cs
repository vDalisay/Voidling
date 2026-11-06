using System;
using Godot;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    internal const float SpringHopDistance = 18.0f;
    internal const float SpringRiseSeconds = 0.34f;
    private const float SpringCatchUpSeconds = 0.28f;

    internal readonly record struct SpringLaunchPose(
        float X, float Elevation, float Rotation, bool Holding, bool Released);

    private void AdvanceFlowerSprings(float delta)
    {
        foreach (var spring in _flowerSprings)
            spring.Advance(delta);
    }

    /// <summary>
    /// Shared by local and multiplayer snapshot rendering. Contact briefly holds the drawing at
    /// the flower while the authoritative race keeps moving, then catches up during the launch.
    /// No animation event feeds back into the simulation or its finish times.
    /// </summary>
    private SpringLaunchPose? UpdateFlowerLaunch(
        RacerVisual visual, RaceParticipantStateSnapshot state, float trackX, float delta)
    {
        var previousX = visual.PreviousTrackX;
        visual.PreviousTrackX = trackX;
        var spring = visual.FlowerSpring;
        if (spring == null || state.Finished ||
            state.Terrain is RaceTerrain.Swim or RaceTerrain.FailedGlideSwim ||
            trackX >= Course.GlideSegment.EndX)
        {
            return null;
        }

        var springX = spring.Position.X;
        var groundLift = visual.BaseY - spring.Position.Y;
        if (!spring.HasTriggered)
        {
            if (trackX < springX)
            {
                if (trackX < springX - SpringHopDistance || visual.RecoverySeconds > 0.0f)
                    return null;

                return SampleSpringApproach(trackX, springX, groundLift);
            }

            // Account for the part of this frame after crossing the contact point. A late first
            // multiplayer snapshot starts already settled instead of replaying an old take-off.
            var travel = trackX - previousX;
            var elapsed = delta > 0.0f && travel > 0.0f && previousX < springX
                ? delta * Mathf.Clamp((trackX - springX) / travel, 0.0f, 1.0f)
                : RaceFlowerSpring.ResponseDurationSeconds + SpringRiseSeconds;
            spring.Trigger(elapsed);
            visual.JumpSeconds = 0.0f;
            visual.RecoverySeconds = 0.0f;
        }

        if (spring.CurrentFrame < 3)
        {
            return new SpringLaunchPose(
                springX, groundLift - spring.SupportOffset.Y, 0.0f, Holding: true, Released: false);
        }

        var launchedSeconds = (float)(spring.ElapsedSeconds - RaceFlowerSpring.LaunchDelaySeconds);
        if (launchedSeconds >= SpringRiseSeconds && trackX >= Course.GlideSegment.StartX)
            return null;

        var glide = GlidePoseFor(visual, state);
        return SampleSpringRelease(trackX, springX, groundLift,
            glide.Elevation, glide.Rotation, launchedSeconds);
    }

    internal static SpringLaunchPose SampleSpringApproach(float x, float springX, float groundLift)
    {
        var progress = Mathf.Clamp((x - (springX - SpringHopDistance)) / SpringHopDistance, 0.0f, 1.0f);
        var supportHeight = -RaceFlowerSpring.SupportOffsetForFrame(0).Y;
        var height = supportHeight * progress + 4.0f * 12.0f * progress * (1.0f - progress);
        return new SpringLaunchPose(x, groundLift + height,
            -0.10f * Mathf.Sin(progress * Mathf.Pi), Holding: false, Released: false);
    }

    internal static SpringLaunchPose SampleSpringRelease(
        float snapshotX, float springX, float groundLift,
        float glideElevation, float glideRotation, float launchedSeconds)
    {
        var rise = EaseOut(Mathf.Clamp(launchedSeconds / SpringRiseSeconds, 0.0f, 1.0f));
        var catchUp = EaseOut(Mathf.Clamp(launchedSeconds / SpringCatchUpSeconds, 0.0f, 1.0f));
        var releaseHeight = groundLift - RaceFlowerSpring.SupportOffsetForFrame(3).Y;
        return new SpringLaunchPose(
            Mathf.Lerp(springX, Math.Max(springX, snapshotX), catchUp),
            Mathf.Lerp(releaseHeight, glideElevation, rise),
            Mathf.Lerp(-0.24f, glideRotation, rise), Holding: false, Released: true);
    }

    private static float EaseOut(float progress) => 1.0f - (1.0f - progress) * (1.0f - progress);

    private (float Elevation, float Rotation, float Progress) GlidePoseFor(
        RacerVisual visual, RaceParticipantStateSnapshot state)
    {
        var span = Math.Max(1.0f, state.GlideEndX - Course.GlideSegment.StartX);
        var progress = Mathf.Clamp((state.X - Course.GlideSegment.StartX) / span, 0.0f, 1.0f);
        var descent = Mathf.Pow(progress, 1.35f);
        var destination = state.GlideEndX < Course.GlideSegment.EndX - 1.0f ? -7.0f : 0.0f;
        var seconds = (float)Time.GetTicksMsec() / 1000.0f;
        var gust = Mathf.Sin(seconds * 5.1f + visual.BaseY) * 0.6f +
                   Mathf.Sin(seconds * 11.3f + visual.BaseY * 0.5f) * 0.3f;
        return (
            Mathf.Lerp(RaceTrackArt.LaunchHeight(Course), destination, descent) -
                gust * 2.2f * (1.0f - progress * 0.6f),
            Mathf.Lerp(-0.20f, 0.06f, descent) + gust * 0.045f,
            progress);
    }
}
