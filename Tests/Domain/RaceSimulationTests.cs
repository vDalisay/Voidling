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
        Assert.Equal(RaceTerrain.Ground, Course.TerrainAt(340.0f, false));
        Assert.Equal(RaceTerrain.Swim, Course.TerrainAt(600.0f, false));
        Assert.Equal(RaceTerrain.Glide, Course.TerrainAt(1150.0f, false));
        Assert.Equal(RaceTerrain.FailedGlideSwim, Course.TerrainAt(1150.0f, true));
        Assert.Equal(new[] { 340.0f, 890.0f, 1510.0f, 1660.0f }, Course.Obstacles);
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
