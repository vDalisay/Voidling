using System;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    /// <summary>
    /// Returns the selected player's result time derived only from deterministic 60 Hz simulation
    /// steps. Frame time and wall-clock time never contribute to a leaderboard score.
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

        // RaceSimulation is exactly 60 Hz. Round to the nearest millisecond using integer math so
        // the same fixed step always maps to the same uploaded score on every platform.
        var rounded = ((long)fixedStep * 1000L + 30L) / 60L;
        if (rounded <= 0 || rounded > int.MaxValue)
            return false;

        milliseconds = (int)rounded;
        return true;
    }
}
