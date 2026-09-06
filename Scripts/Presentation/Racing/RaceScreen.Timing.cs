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
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            milliseconds = 0;
            return false;
        }
    }

    /// <summary>
    /// The one spelling of a race time. The results card, the course-record card and the Garden log
    /// all read it, so a recorded time and the time the player just saw cannot disagree.
    /// </summary>
    public static string FormatMilliseconds(int milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return span.TotalMinutes >= 1.0
            ? $"{(int)span.TotalMinutes}:{span.Seconds:00}.{span.Milliseconds:000}"
            : $"{span.Seconds}.{span.Milliseconds:000}s";
    }
}
