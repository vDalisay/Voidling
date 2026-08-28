using System;
using System.Globalization;
using System.Text;
using Godot;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private bool _balanceTelemetryWritten;

    /// <summary>
    /// Emits one local debug record per completed deterministic single-player race. The record is
    /// intentionally derived from immutable race-entry data plus deterministic simulation state so
    /// it cannot affect gameplay and can be aggregated later to inspect build/placement curves.
    /// </summary>
    private void WriteBalanceTelemetryIfNeeded()
    {
        if (_balanceTelemetryWritten || _simulation == null || _entry == null || !_simulation.IsComplete)
            return;

        var courseDefinition = _entry.CourseDefinition;
        var courseDistance = Math.Max(0.0f, courseDefinition.Course.EndX - courseDefinition.Course.StartX);
        var line = new StringBuilder(512);
        line.Append("[RACE_BALANCE]")
            .Append(" course=").Append(courseDefinition.Id)
            .Append(" version=").Append(courseDefinition.Version)
            .Append(" seed=").Append(_entry.SimulationSeed.ToString(CultureInfo.InvariantCulture));

        foreach (var entrant in _entry.Entrants)
        {
            var participant = entrant.Participant;
            if (!_simulation.TryGetFinishFixedStep(participant.CreatureId, out var fixedStep))
                continue;

            int finishMilliseconds;
            try
            {
                finishMilliseconds = RaceTiming.FixedStepsToMilliseconds(fixedStep);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            var placement = 0;
            for (var i = 0; i < _simulation.FinishOrder.Count; i++)
            {
                if (!string.Equals(_simulation.FinishOrder[i], participant.CreatureId, StringComparison.Ordinal))
                    continue;

                placement = i + 1;
                break;
            }

            var finishSeconds = finishMilliseconds / 1000.0f;
            var effectiveAverageSpeed = finishSeconds > 0.0f ? courseDistance / finishSeconds : 0.0f;
            var finalState = _simulation.GetState(participant.CreatureId);

            line.Append(" | id=").Append(participant.CreatureId)
                .Append(" player=").Append(string.Equals(participant.CreatureId, _playerId, StringComparison.Ordinal) ? 1 : 0)
                .Append(" place=").Append(placement)
                .Append(" finish_ms=").Append(finishMilliseconds)
                .Append(" avg_speed=").Append(effectiveAverageSpeed.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" final_stamina=").Append(finalState.CurrentStamina.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" run=").Append(participant.Run.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" swim=").Append(participant.Swim.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" fly=").Append(participant.Fly.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" power=").Append(participant.Power.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" stamina=").Append(participant.Stamina.ToString("0.###", CultureInfo.InvariantCulture));
        }

        _balanceTelemetryWritten = true;
        GD.Print(line.ToString());
    }
}
