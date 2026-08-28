using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class RaceSimulationTelemetryTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void CompletedRace_ExposesBalancingMetricsWithoutChangingOutcomeState()
    {
        var participant = new RaceParticipantSnapshot(
            CreatureId: "telemetry",
            DisplayName: "Telemetry",
            TintHex: "#FFFFFF",
            Run: 72.0f,
            Swim: 48.0f,
            Fly: 36.0f,
            Power: 40.0f,
            Stamina: 64.0f);
        var simulation = new RaceSimulation(
            RaceCourse.Demo,
            Rules.Racing,
            new[] { participant },
            424242UL);

        Assert.True(simulation.TryCheer(participant.CreatureId));
        simulation.FastForwardToFinish();

        var telemetry = simulation.GetTelemetrySnapshot();
        var metric = Assert.Single(telemetry.Participants);
        var deterministic = simulation.GetDeterministicStateSnapshot();

        Assert.Equal(1, metric.Placement);
        Assert.True(metric.FinishFixedStep > 0);
        Assert.True(metric.MaxObservedSpeed > 0.0f);
        Assert.True(metric.MinimumObservedStamina < new RacePerformanceModel(Rules.Racing).GetMaxStamina(participant));
        Assert.Equal(RaceCourse.Demo.Obstacles.Count, metric.ObstacleAvoids);
        Assert.True(metric.ObstacleFailures >= 0);
        Assert.Equal(1, metric.CheerActivations);
        Assert.Equal(simulation.FixedStepCount, telemetry.FixedStepCount);
        Assert.Equal(new[] { participant.CreatureId }, simulation.FinishOrder);
        Assert.Equal(new[] { participant.CreatureId }, deterministic.FinishOrder);
    }

    [Fact]
    public void ReadingTelemetry_DoesNotPerturbDeterministicReplay()
    {
        var participants = new[]
        {
            new RaceParticipantSnapshot("a", "A", "#FFFFFF", 42, 35, 28, 30, 46),
            new RaceParticipantSnapshot("b", "B", "#FFFFFF", 38, 41, 33, 30, 44)
        };
        var baseline = new RaceSimulation(RaceCourse.Demo, Rules.Racing, participants, 9001UL);
        var observed = new RaceSimulation(RaceCourse.Demo, Rules.Racing, participants, 9001UL);

        baseline.FastForwardToFinish();
        while (!observed.IsComplete)
        {
            _ = observed.GetTelemetrySnapshot();
            observed.AdvanceFixedSteps(17);
        }

        Assert.Equal(baseline.FinishOrder, observed.FinishOrder);
        Assert.Equal(baseline.GetDeterministicStateSnapshot(), observed.GetDeterministicStateSnapshot());
    }
}
