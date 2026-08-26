using System;
using System.Collections.Generic;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private readonly Dictionary<string, string> _appearanceModes = new(StringComparer.Ordinal);

    private void BeginAppearanceBinding()
        => _appearanceModes.Clear();

    /// <summary>
    /// Keeps the cosmetic material in sync with the currently displayed authored atlas. This runs
    /// from the existing lightweight presentation driver, but only reapplies a material when a racer
    /// changes visual mode. RaceSimulation never sees or depends on these appearance values.
    /// </summary>
    private void RefreshRaceAppearanceContexts()
    {
        foreach (var pair in _visuals)
        {
            var visual = pair.Value;
            var mode = visual.VisualMode;
            if (_appearanceModes.TryGetValue(pair.Key, out var previous) &&
                string.Equals(previous, mode, StringComparison.Ordinal))
            {
                continue;
            }

            _appearanceModes[pair.Key] = mode;
            var context = string.Equals(mode, "swim", StringComparison.Ordinal)
                ? VoidlingAppearanceContext.RaceSwim
                : VoidlingAppearanceContext.RaceRun;
            VoidlingAppearancePresenter.Apply(
                visual.Sprite,
                visual.Entrant.Appearance,
                visual.Entrant.Participant.TintHex,
                context);
        }
    }
}
