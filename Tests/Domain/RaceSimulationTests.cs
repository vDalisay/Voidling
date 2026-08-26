using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class RaceSimulationTests
{
    private static readonly RaceRules Rules = GameBalanceRules.DemoDefaults.Racing;
    private static readonly RaceCourse Course = RaceCourse.Demo;

    [Fact]
    public void DemoCourse_MapsCurrentRaceSectionsToData()
    {
        Assert.Equal(70.0f, Course.StartX, 3);
        Assert.Equal(1810.0f, Course.EndX, 3);
        Assert.Equal(4.0f, Course.ObstacleTriggerOffsetX, 3);
        Assert.Equal(RaceTerrain.Ground, Course.TerrainAt(340.0f, false));
        Assert.Equal(RaceTerrain.Swim, Course.TerrainAt(600.0f, false));
        Assert.Equal(RaceTerrain.Glide, Course.TerrainAt(1150.0f, false));
        Assert.Equal(RaceTerrain.FailedGlideSwim, Course.TerrainAt(1150.0f, true));
        Assert.Equal(new[] { 340.0f, 890.0f, 1510.0f, 1660.0f }, Course.Obstacles);
        Assert.Same(RaceCourseCatalog.Demo.Course, Course);
    }

    [Fact]
    public void RaceCourseCatalog_ProvidesStableSemanticIdsAndLongerStandardContent()
    {
        Assert.True(RaceCourseCatalog.TryGet("demo", 1, out var demo));
        Assert.Same(RaceCourseCatalog.Demo, demo);
        Assert.True(RaceCourseCatalog.TryGet("long-standard", 1, out var longer));
        Assert.Same(RaceCourseCatalog.LongStandard, longer);
        Assert.False(RaceCourseCatalog.TryGet("long-standard", 2, out _));

        Assert.True(longer.Course.EndX > demo.Course.EndX);
        Assert.True(longer.Course.Segments.Count > demo.Course.Segments.Count);
        Assert.True(longer.Course.Obstacles.Count > demo.Course.Obstacles.Count);
        Assert.Equal(2, longer.Course.Segments.Count(segment => segment.Kind == RaceSegmentKind.Swim));
        Assert.Equal(1, longer.Course.Segments.Count(segment => segment.Kind == RaceSegmentKind.Glide));
    }

    [Fact]
    public void RaceCourse_CanonicalizesSegmentAndObstacleAuthoringOrder()
    {
        var course = new RaceCourse(
            startX: 0.0f,
            endX: 300.0f,
            glideLaunchStartX: 180.0f,
            segments: new[]
            {
                new RaceCourseSegment("finish", 240.0f, 300.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("glide", 200.0f, 240.0f, RaceSegmentKind.Glide),
                new RaceCourseSegment("start", 0.0f, 100.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("swim", 100.0f, 200.0f, RaceSegmentKind.Swim)
            },
            obstacles: new[] { 275.0f, 25.0f, 160.0f });

        Assert.Equal(new[] { "start", "swim", "glide", "finish" }, course.Segments.Select(segment => segment.Id));
        Assert.Equal(new[] { 25.0f, 160.0f, 275.0f }, course.Obstacles);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void RaceCourse_RejectsInvalidResultAffectingGeometry(
        bool createGap,
        bool createOverlap,
        bool duplicateIds)
    {
        var secondStart = createGap ? 110.0f : createOverlap ? 90.0f : 100.0f;
        var secondId = duplicateIds ? "first" : "second";

        Assert.Throws<ArgumentException>(() => new RaceCourse(
            startX: 0.0f,
            endX: 200.0f,
            glideLaunchStartX: 50.0f,
            segments: new[]
            {
                new RaceCourseSegment("first", 0.0f, 100.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment(secondId, secondStart, 200.0f, RaceSegmentKind.Ground)
            },
            obstacles: Array.Empty<float>()));
    }

    [Fact]
    public void RaceCourse_RejectsMultipleGlideSegmentsUntilSimulatorSupportsThemExplicitly()
    {
        Assert.Throws<ArgumentException>(() => new RaceCourse(
            startX: 0.0f,
            endX: 300.0f,
            glideLaunchStartX: 80.0f,
            segments: new[]
            {
                new RaceCourseSegment("ground", 0.0f, 100.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("glide-a", 100.0f, 150.0f, RaceSegmentKind.Glide),
                new RaceCourseSegment("middle", 150.0f, 200.0f, RaceSegmentKind.Ground),
                new RaceCourseSegment("glide-b", 200.0f, 250.0f, RaceSegmentKind.Glide),
                new RaceCourseSegment("finish", 250.0f, 300.0f, RaceSegmentKind.Ground)
            },
            obstacles: Array.Empty<float>()));
    }

    [Fact]
    public void ObstacleTriggerOffset_IsCourseOwnedAndChangesResolutionTiming()
    {
        var earlier = FirstObstacleResolutionStep(-25.0f);
        var later = FirstObstacleResolutionStep(25.0f);

        Assert.True(later > earlier);
    }

    [Fact]
    public void RaceCourse_RejectsObstacleTriggerPositionsOutsideCourseBounds()
    {
        Assert.Throws<ArgumentException>(() => new RaceCourse(
            startX: 0.0f,
            endX: 200.0f,
            glideLaunchStartX: 50.0f,
            segments: new[]
            {
                new RaceCourseSegment("ground", 0.0f, 200.0f, RaceSegmentKind.Ground)
            },
            obstacles: new[] { 195.0f },
            obstacleTriggerOffsetX: 10.0f));
    }

    [Fact]
    public void SameSeedAndParticipants_AreIndependentOfFrameChunking()
    {
        var fine = new RaceSimulation(Course, Rules, Participants(), 424242UL);
        var coarse = new RaceSimulation(Course, Rules, Participants(), 424242UL);

        var fineEvents = RunWithElapsedChunks(fine, RaceSimulation.FixedStepSeconds);
        var coarseEvents = RunWithElapsedChunks(coarse, 0.10);

        Assert.Equal(fine.FinishOrder, coarse.FinishOrder);
        Assert.Equal(fineEvents, coarseEvents);

        foreach (var participant in Participants())
        {
            var fineState = fine.GetState(participant.CreatureId);
            var coarseState = coarse.GetState(participant.CreatureId);
            Assert.Equal(fineState.X, coarseState.X, 3);
            Assert.Equal(fineState.CurrentStamina, coarseState.CurrentStamina, 3);
            Assert.True(fineState.Finished);
            Assert.True(coarseState.Finished);
        }
    }

    [Fact]
    public void EveryAuthoredCourse_ReplaysIdenticallyAcrossElapsedChunking()
    {
        foreach (var definition in RaceCourseCatalog.All)
        {
            var fine = new RaceSimulation(definition.Course, Rules, Participants(), 7331UL);
            var coarse = new RaceSimulation(definition.Course, Rules, Participants(), 7331UL);

            var fineEvents = RunWithElapsedChunks(fine, RaceSimulation.FixedStepSeconds);
            var coarseEvents = RunWithElapsedChunks(coarse, 0.137);

            Assert.Equal(fine.FinishOrder, coarse.FinishOrder);
            Assert.Equal(fineEvents, coarseEvents);
            Assert.Equal(fine.FixedStepCount, coarse.FixedStepCount);
            foreach (var participant in Participants())
            {
                var fineState = fine.GetDeterministicStateSnapshot().Participants
                    .Single(value => value.ParticipantId == participant.CreatureId);
                var coarseState = coarse.GetDeterministicStateSnapshot().Participants
                    .Single(value => value.ParticipantId == participant.CreatureId);
                Assert.Equal(fineState, coarseState);
            }
        }
    }

    [Fact]
    public void FastForward_UsesSameFixedStepSimulationAsNormalProgression()
    {
        var normal = new RaceSimulation(Course, Rules, Participants(), 99117UL);
        var fast = new RaceSimulation(Course, Rules, Participants(), 99117UL);

        var normalEvents = new List<string>();
        var guard = 0;
        while (!normal.IsComplete && guard++ < 120000)
        {
            normalEvents.AddRange(normal.AdvanceFixedSteps(1).Select(Describe));
        }

        var fastEvents = fast.FastForwardToFinish().Select(Describe).ToArray();

        Assert.True(normal.IsComplete);
        Assert.Equal(normal.FinishOrder, fast.FinishOrder);
        Assert.Equal(normalEvents, fastEvents);
    }

    [Fact]
    public void Cheer_IsAResultAffectingSimulationCommandNotPresentationState()
    {
        var participant = new RaceParticipantSnapshot("player", "Player", "#FFFFFF", 50, 40, 30, 20, 60);
        var cheered = new RaceSimulation(Course, Rules, new[] { participant }, 7UL);
        var plain = new RaceSimulation(Course, Rules, new[] { participant }, 7UL);

        var before = cheered.GetState("player");
        Assert.True(cheered.TryCheer("player"));
        var afterCheer = cheered.GetState("player");
        Assert.Equal(before.CurrentStamina - Rules.CheerCost, afterCheer.CurrentStamina, 3);
        Assert.Equal(Rules.CheerDurationSeconds, afterCheer.CheerSeconds, 3);
        Assert.False(cheered.TryCheer("player"));

        cheered.AdvanceFixedSteps(60);
        plain.AdvanceFixedSteps(60);

        Assert.True(cheered.GetState("player").X > plain.GetState("player").X);
    }

    [Fact]
    public void CompleteParticipantAsLast_RecordsFinishInsideSimulation()
    {
        var participants = Participants().Take(2).ToArray();
        var simulation = new RaceSimulation(Course, Rules, participants, 8UL);

        var first = simulation.CompleteParticipantAsLast(participants[1].CreatureId);
        var second = simulation.CompleteParticipantAsLast(participants[0].CreatureId);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, first!.Placement);
        Assert.Equal(2, second!.Placement);
        Assert.True(simulation.IsComplete);
        Assert.Equal(
            new[] { participants[1].CreatureId, participants[0].CreatureId },
            simulation.FinishOrder);
    }

    private static IReadOnlyList<RaceParticipantSnapshot> Participants()
        => new[]
        {
            new RaceParticipantSnapshot("player", "Player", "#E7A6B6", 72, 48, 62, 20, 68),
            new RaceParticipantSnapshot("cpu-a", "Fern", "#A9D5C0", 65, 80, 28, 25, 52),
            new RaceParticipantSnapshot("cpu-b", "Moss", "#B7B2E8", 84, 35, 44, 40, 45),
            new RaceParticipantSnapshot("cpu-c", "Puck", "#F0C778", 58, 55, 85, 35, 74)
        };

    private static int FirstObstacleResolutionStep(float triggerOffset)
    {
        var course = new RaceCourse(
            startX: 0.0f,
            endX: 500.0f,
            glideLaunchStartX: 250.0f,
            segments: new[]
            {
                new RaceCourseSegment("ground", 0.0f, 500.0f, RaceSegmentKind.Ground)
            },
            obstacles: new[] { 200.0f },
            obstacleTriggerOffsetX: triggerOffset);
        var participant = new RaceParticipantSnapshot(
            "runner",
            "Runner",
            "#FFFFFF",
            100,
            0,
            0,
            0,
            100);
        var simulation = new RaceSimulation(course, Rules, new[] { participant }, 12345UL);

        for (var step = 1; step <= 20000; step++)
        {
            if (simulation.AdvanceFixedSteps(1).OfType<RaceObstacleResolvedEvent>().Any())
                return simulation.FixedStepCount;
        }

        throw new InvalidOperationException("Obstacle was not resolved within deterministic test guard.");
    }

    private static IReadOnlyList<string> RunWithElapsedChunks(RaceSimulation simulation, double chunk)
    {
        var events = new List<string>();
        var guard = 0;
        while (!simulation.IsComplete && guard++ < 120000)
        {
            events.AddRange(simulation.Advance(chunk).Select(Describe));
        }

        Assert.True(simulation.IsComplete);
        return events;
    }

    private static string Describe(RaceSimulationEvent raceEvent)
        => raceEvent switch
        {
            RaceObstacleResolvedEvent obstacle =>
                $"obstacle:{obstacle.ParticipantId}:{obstacle.ObstacleIndex}:{obstacle.Avoided}",
            RaceParticipantFinishedEvent finished =>
                $"finish:{finished.ParticipantId}:{finished.Placement}",
            _ => raceEvent.ToString() ?? string.Empty
        };
}
