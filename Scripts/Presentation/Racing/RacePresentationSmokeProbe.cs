using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Headless CI probe for race presentation contracts that only hold once Godot is running.
///
/// It guards authored terrain, shared creature grounding and the manually driven flower launch,
/// as well as section/course labels that would otherwise show raw localization keys to players.
/// </summary>
public partial class RacePresentationSmokeProbe : Node
{
    public override async void _Ready()
    {
        try
        {
            ValidateLocalizedSectionAndCourseStrings();
            ValidateEverySegmentKindIsDrawn();
            ValidateGroundPivotMatchesGardenAtRaceScale();
            await ValidateFlowerSprings();

            GD.Print(
                "[race-presentation-smoke] RACE_PRESENTATION_SMOKE_SUCCESS " +
                $"courses={string.Join(',', RaceCourseCatalog.All.Select(course => course.Id))}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[race-presentation-smoke] RACE_PRESENTATION_SMOKE_FAILED: {exception.Message}");
            GetTree().Quit(6);
        }
    }

    // A key that translates to itself is a string that was never authored, which is exactly how the
    // course picker ended up showing UI_RACE_COURSE_LONG_NAME to players.
    private void ValidateLocalizedSectionAndCourseStrings()
    {
        foreach (var key in RaceCoursePresentationCatalog.AllKeys().Distinct(StringComparer.Ordinal))
        {
            var translated = Tr(key);
            if (string.IsNullOrWhiteSpace(translated) || string.Equals(translated, key, StringComparison.Ordinal))
                throw new InvalidOperationException($"Localization key '{key}' has no translated string.");
        }
    }

    // Every segment kind an authored course actually contains must produce world geometry. Climb
    // was authored on both courses but drawn by nothing, so players never saw the Power stretch.
    private void ValidateEverySegmentKindIsDrawn()
    {
        foreach (var definition in RaceCourseCatalog.All)
        {
            foreach (var kind in definition.Course.Segments.Select(segment => segment.Kind).Distinct())
            {
                // Throws for any kind the renderer has no declaration for.
                var visual = RaceScreen.VisualFor(kind);
                if (kind != RaceSegmentKind.Ground && !visual.Water && !visual.Climb && !visual.Spring)
                {
                    throw new InvalidOperationException(
                        $"Course '{definition.Id}' contains {kind} segments that race presentation never draws.");
                }
            }
        }
    }

    // The Garden and the race share one shadow offset, so both must place the sprite on the same
    // ground pivot. Measured in sprite-local pixels the foot-to-shadow distance has to match, or the
    // race sprite floats above its own shadow the way it did when the race offset stopped scaling.
    private static void ValidateGroundPivotMatchesGardenAtRaceScale()
    {
        foreach (var visualTypeId in VoidlingVisualFactory.VisualTypeIds)
        {
            var adultScale = VoidlingVisualFactory.WorldScale(adult: true, visualTypeId);
            var childScale = VoidlingVisualFactory.WorldScale(adult: false, visualTypeId);
            var raceScale = VoidlingVisualFactory.RaceScaleFor(visualTypeId);

            var adultFootGap = FootGapInSpritePixels(
                adultScale,
                VoidlingVisualFactory.WorldSpriteCenterYOffset(adultScale, visualTypeId),
                visualTypeId);
            var childFootGap = FootGapInSpritePixels(
                childScale,
                VoidlingVisualFactory.WorldSpriteCenterYOffset(childScale, visualTypeId),
                visualTypeId);
            var raceFootGap = FootGapInSpritePixels(
                raceScale,
                VoidlingVisualFactory.RaceSpriteCenterYOffset(visualTypeId),
                visualTypeId);

            if (Mathf.Abs(adultFootGap - raceFootGap) > 0.001f ||
                Mathf.Abs(adultFootGap - childFootGap) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Voidling '{visualTypeId}' does not share one ground pivot across contexts " +
                    $"(adult {adultFootGap:0.###}, child {childFootGap:0.###}, race {raceFootGap:0.###} " +
                    "px in sprite space).");
            }
        }
    }

    // Build the production layers, rather than a test-only flower arrangement. The course preview
    // and the live race must both receive four independent flowers on the actual lane surfaces.
    private async Task ValidateFlowerSprings()
    {
        var layout = new RaceTrackLayout(126.0f, 244.0f, 640.0f, 360.0f, RaceTrackArt.ClimbHeight);
        foreach (var definition in RaceCourseCatalog.All)
        {
            var course = definition.Course;
            var world = new Node2D { Name = "SpringSmoke_" + definition.Id };
            AddChild(world);
            try
            {
                var springs = RaceTrackLayers.Build(world, course, layout);
                var expectedCount = course.HasGlideSegment ? 4 : 0;
                Require(springs.Count == expectedCount &&
                        world.GetChildren().OfType<RaceFlowerSpring>().Count() == expectedCount,
                    $"Course '{definition.Id}' must build one flower for each of its four lanes.");
                if (!course.HasGlideSegment)
                    continue;

                var springX = RaceTrackLayers.SpringXFor(course);
                var groundLift = RaceTrackArt.SurfaceLift(course, springX);
                for (var lane = 0; lane < springs.Count; lane++)
                {
                    var spring = springs[lane];
                    RequireNear(spring.Position.X, springX, "Flower horizontal registration");
                    RequireNear(spring.Position.Y + groundLift, 154.0f + lane * 20.0f,
                        $"Course '{definition.Id}' lane {lane + 1} ground anchor");
                    ValidateFlowerSprite(spring);
                }

                ValidateFlowerSupport(springs[0]);
                ValidateFlowerTiming(springs[0], springs[1]);
                ValidateSpringTrajectory(course, springX, groundLift);

                // Both idle previews and triggered race flowers must remain unchanged when the
                // owning screen does not advance them, even while the scene tree processes frames.
                springs[0].Trigger(0.10);
                var elapsed = springs[0].ElapsedSeconds;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                RequireNear(springs[0].ElapsedSeconds, elapsed, "Flower autonomous clock", 0.0000001);
                Require(springs[0].CurrentFrame == 2 && springs[0].GetNode<Sprite2D>("Flower").Frame == 2 &&
                        springs.Skip(1).All(spring => !spring.HasTriggered && spring.CurrentFrame == 0),
                    "Flower poses must remain unchanged without an explicit Advance call.");
            }
            finally
            {
                world.QueueFree();
            }
        }

        var groundOnly = new RaceCourse(70.0f, 400.0f, 400.0f,
            new[] { new RaceCourseSegment("ground-only", 70.0f, 400.0f, RaceSegmentKind.Ground) },
            Array.Empty<float>());
        var groundWorld = new Node2D { Name = "GroundOnlySpringSmoke" };
        AddChild(groundWorld);
        try
        {
            Require(RaceTrackLayers.Build(groundWorld, groundOnly, layout).Count == 0 &&
                    !groundWorld.GetChildren().OfType<RaceFlowerSpring>().Any(),
                "A course without a glide crossing must not create flower springs.");
        }
        finally
        {
            groundWorld.QueueFree();
        }

        // Let deferred disposal finish before the probe quits the engine.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void ValidateFlowerSprite(RaceFlowerSpring spring)
    {
        var sprite = spring.GetNode<Sprite2D>("Flower");
        Require(sprite.Texture != null && sprite.Texture.GetWidth() == 320 && sprite.Texture.GetHeight() == 128 &&
                sprite.Hframes == 4 && sprite.Vframes == 2 && sprite.GetRect().Size == new Vector2(80.0f, 64.0f),
            "The flower must slice the original sheet into eight 80 by 64 frames.");
        Require(spring.TextureFilter == CanvasItem.TextureFilterEnum.Nearest &&
                sprite.TextureFilter == CanvasItem.TextureFilterEnum.Nearest && !sprite.Centered &&
                sprite.Offset == new Vector2(-40.5f, -52.0f) && sprite.Position == Vector2.Zero &&
                sprite.Scale == new Vector2(0.5f, 0.5f) && spring.Scale == Vector2.One,
            "Flower pixels must use nearest filtering and the authored ground pivot at half scale.");
        Require(!spring.IsProcessing() && !spring.IsPhysicsProcessing(),
            "Flower presentation must not own an autonomous process clock.");
    }

    // Measure the imported art itself: a metadata mistake must not let the racer's feet float
    // above the yellow pad or sink into it, even when every caller shares the same bad constant.
    private static void ValidateFlowerSupport(RaceFlowerSpring spring)
    {
        using var image = spring.GetNode<Sprite2D>("Flower").Texture.GetImage();
        for (var frame = 0; frame < 8; frame++)
        {
            var minX = 80;
            var maxX = -1;
            var minY = 64;
            var maxY = -1;
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 80; x++)
                {
                    var color = image.GetPixel(frame % 4 * 80 + x, frame / 4 * 64 + y);
                    if (color.A < 0.5f || color.R <= color.B + 15.0f / 255.0f ||
                        color.G <= color.B + 15.0f / 255.0f)
                        continue;

                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            Require(maxX >= minX && maxY >= minY, $"Flower frame {frame} has no yellow support pad.");
            var support = RaceFlowerSpring.SupportOffsetForFrame(frame);
            RequireNear(support.X, ((minX + maxX + 1) * 0.5f - 40.5f) * 0.5f,
                $"Flower frame {frame} horizontal support");
            RequireNear(support.Y, ((minY + maxY + 1) * 0.5f - 52.0f) * 0.5f,
                $"Flower frame {frame} vertical support");
        }
    }

    private static void ValidateFlowerTiming(RaceFlowerSpring spring, RaceFlowerSpring neighbour)
    {
        Require(!spring.HasTriggered && !spring.IsAnimating && spring.CurrentFrame == 0,
            "A newly built flower must wait on its idle frame.");
        spring.Advance(1.0);
        Require(!spring.HasTriggered && spring.CurrentFrame == 0 && spring.ElapsedSeconds == 0.0,
            "Advancing an idle preview must not trigger its spring.");

        spring.Trigger();
        Require(spring.HasTriggered && spring.IsAnimating && spring.CurrentFrame == 1 &&
                spring.GetNode<Sprite2D>("Flower").Frame == 1,
            "Contact must immediately display the first compression frame.");
        var durationsMilliseconds = new[] { 60, 80, 40, 60, 80, 120 };
        var elapsedMilliseconds = 0;
        for (var frame = 1; frame <= durationsMilliseconds.Length; frame++)
        {
            elapsedMilliseconds += durationsMilliseconds[frame - 1];
            var boundary = elapsedMilliseconds / 1000.0;
            Require(RaceFlowerSpring.FrameAtElapsed(boundary - 0.000001) == frame &&
                    RaceFlowerSpring.FrameAtElapsed(boundary) == frame + 1,
                $"Flower frame {frame} must end at {elapsedMilliseconds} ms.");
            spring.Advance(durationsMilliseconds[frame - 1] / 1000.0);
            Require(spring.CurrentFrame == frame + 1 && spring.GetNode<Sprite2D>("Flower").Frame == frame + 1,
                "The displayed flower frame must follow its manually advanced clock.");
            Require(!neighbour.HasTriggered && neighbour.CurrentFrame == 0,
                "Triggering one lane must not animate a neighbouring flower.");
        }

        RequireNear(RaceFlowerSpring.LaunchDelaySeconds, 0.14, "Flower launch cue");
        RequireNear(RaceFlowerSpring.ResponseDurationSeconds, 0.44, "Flower response duration");
        Require(spring.HasTriggered && !spring.IsAnimating && spring.CurrentFrame == 7,
            "The flower must settle on its final idle frame.");
        spring.Advance(0.50);
        RequireNear(spring.ElapsedSeconds, 0.94, "Post-settle launch clock");
        spring.Trigger(0.205);
        Require(spring.CurrentFrame == 4, "Contact overshoot must select the already elapsed pose.");
        var elapsed = spring.ElapsedSeconds;
        spring.Advance(0.0);
        RequireNear(spring.ElapsedSeconds, elapsed, "Paused flower clock", 0.0000001);
        spring.Reset();
        Require(!spring.HasTriggered && !spring.IsAnimating && spring.CurrentFrame == 0 &&
                spring.ElapsedSeconds == 0.0 && spring.GetNode<Sprite2D>("Flower").Frame == 0,
            "Reset must restore an untriggered flower and its displayed idle pose.");
    }

    private static void ValidateSpringTrajectory(RaceCourse course, float springX, float groundLift)
    {
        // The former ramp span stays at its original plateau height all the way to the lake.
        var beforeLaunchZone = RaceTrackArt.SurfaceLift(course, course.GlideLaunchStartX - 1.0f);
        foreach (var x in new[] { course.GlideLaunchStartX, (course.GlideLaunchStartX + springX) * 0.5f,
                     springX, course.GlideSegment.StartX - 0.01f })
        {
            RequireNear(RaceTrackArt.SurfaceLift(course, x), beforeLaunchZone,
                "Former ramp zone must contain only its plateau ground");
        }
        RequireNear(RaceTrackArt.LaunchHeight(course), groundLift + 38.0f,
            "Spring launch elevation must include the plateau exactly once");

        var hopStart = RaceScreen.SampleSpringApproach(springX - RaceScreen.SpringHopDistance, springX, groundLift);
        var hopMiddle = RaceScreen.SampleSpringApproach(springX - RaceScreen.SpringHopDistance * 0.5f, springX, groundLift);
        var contact = RaceScreen.SampleSpringApproach(springX, springX, groundLift);
        RequireNear(hopStart.Elevation, groundLift, "Spring approach starts on the ground");
        RequireNear(contact.Elevation, groundLift - RaceFlowerSpring.SupportOffsetForFrame(0).Y,
            "Spring approach ends on the idle pad");
        RequireNear(contact.X, springX, "Spring contact horizontal position");
        Require(hopMiddle.Elevation > Math.Max(hopStart.Elevation, contact.Elevation) &&
                !contact.Holding && !contact.Released,
            "The approach must arc above the pad before contact, without launching early.");

        var targetX = course.GlideSegment.StartX + 24.0f;
        var targetElevation = RaceTrackArt.LaunchHeight(course);
        const float targetRotation = -0.20f;
        var release = RaceScreen.SampleSpringRelease(targetX, springX, groundLift, targetElevation, targetRotation, 0.0f);
        RequireNear(release.X, springX, "Release starts at the flower");
        RequireNear(release.Elevation, groundLift - RaceFlowerSpring.SupportOffsetForFrame(3).Y,
            "Release starts on the authored frame 3 support");
        Require(release.Released && !release.Holding, "The release pose must permit flight.");

        var middle = RaceScreen.SampleSpringRelease(targetX, springX, groundLift,
            targetElevation, targetRotation, RaceScreen.SpringRiseSeconds * 0.5f);
        var nearlyGliding = RaceScreen.SampleSpringRelease(targetX, springX, groundLift,
            targetElevation, targetRotation, RaceScreen.SpringRiseSeconds - 0.001f);
        var gliding = RaceScreen.SampleSpringRelease(targetX, springX, groundLift,
            targetElevation, targetRotation, RaceScreen.SpringRiseSeconds);
        Require(middle.X > release.X && middle.X <= targetX &&
                middle.Elevation > release.Elevation && middle.Elevation < targetElevation,
            "Spring release must rise and catch up without overshooting the glide.");
        RequireNear(gliding.X, targetX, "Glide horizontal handoff");
        RequireNear(gliding.Elevation, targetElevation, "Glide elevation handoff");
        RequireNear(gliding.Rotation, targetRotation, "Glide rotation handoff");
        RequireNear(nearlyGliding.Elevation, gliding.Elevation, "Smooth glide elevation handoff", 0.01);
        RequireNear(nearlyGliding.Rotation, gliding.Rotation, "Smooth glide rotation handoff", 0.01);

        // Elevation is absolute above the base track band. Crossing the edge of a raised plateau
        // must not add its height again or suddenly drop the launched body to the lake's surface.
        var shoreBefore = RaceScreen.SampleSpringRelease(course.GlideSegment.StartX - 0.01f, springX,
            groundLift, targetElevation, targetRotation, 0.30f);
        var shoreAfter = RaceScreen.SampleSpringRelease(course.GlideSegment.StartX + 0.01f, springX,
            groundLift, targetElevation, targetRotation, 0.30f);
        RequireNear(shoreBefore.Elevation, shoreAfter.Elevation, "Shoreline launch continuity");
        Require(shoreAfter.Elevation <= targetElevation && shoreAfter.Elevation >= release.Elevation,
            "Shoreline launch must retain one absolute elevation, including the plateau once.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireNear(double actual, double expected, string message, double tolerance = 0.001)
        => Require(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance,
            $"{message}: expected {expected:0.######}, found {actual:0.######}.");

    private static float FootGapInSpritePixels(float spriteScale, float spriteCenterY, string visualTypeId)
        => (VoidlingVisualFactory.ShadowCenterYOffset(spriteScale, visualTypeId) - spriteCenterY) / spriteScale;
}
