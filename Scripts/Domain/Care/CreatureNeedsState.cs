using System;
using Voidling.Domain.Rules;

namespace Voidling.Domain.Care;

/// <summary>
/// Persisted current-care state for a Voidling. Values use a normalized 0..100 scale so tuning can
/// change without save-shape churn. These are current-state values, not inherited genetics and not
/// race stats.
/// </summary>
public sealed class CreatureNeedsState
{
    public float Hunger { get; set; } = 0.0f;
    public float Energy { get; set; } = 100.0f;
    public float Fatigue { get; set; } = 0.0f;
    public float Stress { get; set; } = 0.0f;
    public float Boredom { get; set; } = 0.0f;
    public float Loneliness { get; set; } = 0.0f;
    public float Nourishment { get; set; } = 100.0f;
    public float Condition { get; set; } = 100.0f;
    // Player-facing design fixes the starting value at zero and keeps Happiness completely hidden.
    public float Happiness { get; set; } = 0.0f;
}

/// <summary>
/// Deterministic current-care rules. The service has no wall-clock or presentation dependency:
/// closed-game time cannot punish a creature, and care effects stay independent from genetics and
/// race simulation.
/// </summary>
public sealed class CreatureNeedsService
{
    public bool Advance(CreatureNeedsState state, float elapsedSeconds, NeedsRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0.0f)
            return false;

        var minutes = elapsedSeconds / 60.0f;
        var changed = false;
        changed |= Set(state.Hunger, value => state.Hunger = value,
            state.Hunger + rules.HungerGainPerMinute * minutes);
        changed |= Set(state.Energy, value => state.Energy = value,
            state.Energy - rules.EnergyLossPerMinute * minutes);
        changed |= Set(state.Fatigue, value => state.Fatigue = value,
            state.Fatigue + rules.FatigueGainPerMinute * minutes);
        changed |= Set(state.Stress, value => state.Stress = value,
            state.Stress - rules.StressRecoveryPerMinute * minutes);
        changed |= Set(state.Boredom, value => state.Boredom = value,
            state.Boredom + rules.BoredomGainPerMinute * minutes);
        changed |= Set(state.Loneliness, value => state.Loneliness = value,
            state.Loneliness + rules.LonelinessGainPerMinute * minutes);
        changed |= Set(state.Nourishment, value => state.Nourishment = value,
            state.Nourishment - rules.NourishmentLossPerMinute * minutes);
        changed |= Set(state.Condition, value => state.Condition = value,
            state.Condition - rules.ConditionLossPerMinute * minutes);
        changed |= Set(state.Happiness, value => state.Happiness = value,
            state.Happiness - rules.HappinessLossPerMinute * minutes);
        return changed;
    }

    /// <summary>
    /// Applies the care side of an already-successful active training treat. Training still owns the
    /// stat mutation and inventory transaction; this only updates current feeding/care state. Exact
    /// magnitudes are authorable prototype balance values.
    /// </summary>
    public bool ApplyTrainingTreat(CreatureNeedsState state, NeedsRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);

        var changed = false;
        changed |= Set(state.Hunger, value => state.Hunger = value,
            state.Hunger - rules.TreatHungerReduction);
        changed |= Set(state.Energy, value => state.Energy = value,
            state.Energy + rules.TreatEnergyGain);
        changed |= Set(state.Nourishment, value => state.Nourishment = value,
            state.Nourishment + rules.TreatNourishmentGain);
        changed |= Set(state.Happiness, value => state.Happiness = value,
            state.Happiness + rules.TreatHappinessGain);
        return changed;
    }

    public bool Normalize(CreatureNeedsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var changed = false;
        changed |= Set(state.Hunger, value => state.Hunger = value, state.Hunger);
        changed |= Set(state.Energy, value => state.Energy = value, state.Energy);
        changed |= Set(state.Fatigue, value => state.Fatigue = value, state.Fatigue);
        changed |= Set(state.Stress, value => state.Stress = value, state.Stress);
        changed |= Set(state.Boredom, value => state.Boredom = value, state.Boredom);
        changed |= Set(state.Loneliness, value => state.Loneliness = value, state.Loneliness);
        changed |= Set(state.Nourishment, value => state.Nourishment = value, state.Nourishment);
        changed |= Set(state.Condition, value => state.Condition = value, state.Condition);
        changed |= Set(state.Happiness, value => state.Happiness = value, state.Happiness);
        return changed;
    }

    private static bool Set(float previous, Action<float> setter, float candidate)
    {
        var normalized = NormalizeValue(candidate);
        if (previous.Equals(normalized))
            return false;

        setter(normalized);
        return true;
    }

    private static float NormalizeValue(float value)
    {
        if (!float.IsFinite(value))
            return 0.0f;
        return Math.Clamp(value, 0.0f, 100.0f);
    }
}