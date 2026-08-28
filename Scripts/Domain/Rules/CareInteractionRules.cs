namespace Voidling.Domain.Rules;

/// <summary>
/// Authorable care-interaction tuning kept separate from inherited genetics and race rules.
/// These prototype values can be rebalanced without changing persisted creature state.
/// </summary>
public sealed record CareInteractionRules(
    float PetHappinessGain,
    float PetStressReduction,
    float PetBoredomReduction,
    float PetLonelinessReduction)
{
    public static CareInteractionRules DemoDefaults { get; } = new(
        PetHappinessGain: 2.0f,
        PetStressReduction: 4.0f,
        PetBoredomReduction: 5.0f,
        PetLonelinessReduction: 8.0f);
}
