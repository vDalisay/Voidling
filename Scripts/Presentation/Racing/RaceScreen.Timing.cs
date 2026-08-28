using System;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    /// <summary>
    /// Returns the selected player's result time derived only from deterministic simulation steps.
    /// Frame time and wall-clock time never contribute to a leaderboard score.
    /// </summary>
    public bool TryGetPlayerFinishMilliseconds(out int milliseconds)
    {
        milliseconds = 0;
        if (_simulation == null ||
            string.IsNullOrWhiteSpace(_playerId) ||
            !_simulation.TryGetFinishFixedStep(_playerId, out var fixedStep))
        {
            return false;
        }

        try
        {
            milliseconds = RaceTiming.FixedStepsToMilliseconds(fixedStep);
            WriteBalanceTelemetryIfNeeded();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            milliseconds = 0;
            return false;
        }
    }
}
