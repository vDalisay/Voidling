using System;
using System.Globalization;
using System.Linq;
using Godot;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private bool _telemetryReported;

    public override void _Notification(int what)
    {
        if (what != NotificationProcess ||
            _telemetryReported ||
            !_resultsShown ||
            _simulation == null ||
            _entry == null)
        {
            return;
        }

        _telemetryReported = true;
        var telemetry = _simulation.GetTelemetrySnapshot();
        var entrants = _entry.Entrants.ToDictionary(
            entrant => entrant.Participant.CreatureId,
            StringComparer.Ordinal);

        foreach (var metric in telemetry.Participants.OrderBy(value => value.Placement == 0 ? int.MaxValue : value.Placement))
        {
            if (!entrants.TryGetValue(metric.ParticipantId, out var entrant))
                continue;

            var participant = entrant.Participant;
            var finishSeconds = metric.FinishFixedStep > 0
                ? metric.FinishFixedStep * RaceSimulation.FixedStepSeconds
                : 0.0;
            var safeName = participant.DisplayName.Replace('"', '\'');
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"[race-telemetry] course={_entry.CourseDefinition.Id}@{_entry.CourseDefinition.Version} " +
                $"seed={_entry.SimulationSeed} steps={telemetry.FixedStepCount} " +
                $"participant={participant.CreatureId} name=\"{safeName}\" player={(participant.CreatureId == _playerId ? 1 : 0)} " +
                $"placement={metric.Placement} finish_s={finishSeconds:0.000} " +
                $"run={participant.Run:0.00} swim={participant.Swim:0.00} fly={participant.Fly:0.00} " +
                $"power={participant.Power:0.00} stamina={participant.Stamina:0.00} " +
                $"max_speed={metric.MaxObservedSpeed:0.000} min_stamina={metric.MinimumObservedStamina:0.000} " +
                $"obstacle_avoids={metric.ObstacleAvoids} obstacle_failures={metric.ObstacleFailures} cheers={metric.CheerActivations}");
            GD.Print(line);
        }
    }
}
