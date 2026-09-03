using System;
using Voidling.Domain.Rules;

namespace Voidling.Domain.Care;

/// <summary>
/// Pure care-interaction rules. Care actions change only current care state; they never mutate
/// genetics, training potential, appearance, or race state.
/// </summary>
public sealed class CareInteractionService
{
    public bool Pet(CreatureNeedsState state, CareInteractionRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);

        var changed = false;
        changed |= Set(state.Happiness, value => state.Happiness = value,
            state.Happiness + rules.PetHappinessGain);
        changed |= Set(state.Stress, value => state.Stress = value,
            state.Stress - rules.PetStressReduction);
        changed |= Set(state.Boredom, value => state.Boredom = value,
            state.Boredom - rules.PetBoredomReduction);
        changed |= Set(state.Loneliness, value => state.Loneliness = value,
            state.Loneliness - rules.PetLonelinessReduction);
        return changed;
    }

    public bool Mistreat(CreatureNeedsState state, CareInteractionRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);

        var changed = false;
        changed |= Set(state.Happiness, value => state.Happiness = value,
            state.Happiness - rules.ThrowHappinessLoss);
        changed |= Set(state.Stress, value => state.Stress = value,
            state.Stress + rules.ThrowStressGain);
        return changed;
    }

    private static bool Set(float previous, Action<float> setter, float candidate)
    {
        var normalized = Normalize(candidate);
        if (previous.Equals(normalized))
            return false;

        setter(normalized);
        return true;
    }

    private static float Normalize(float value)
        => float.IsFinite(value) ? Math.Clamp(value, 0.0f, 100.0f) : 0.0f;
}
