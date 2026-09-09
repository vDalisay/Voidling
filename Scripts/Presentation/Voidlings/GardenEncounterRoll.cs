namespace Voidling.Presentation.Voidlings;

/// <summary>What two Voidlings that ran into each other in the Garden decide to do about it.</summary>
public enum GardenEncounterKind
{
    /// <summary>Walk up to each other, stand nose to nose and swap hearts.</summary>
    Greet,

    /// <summary>One gives chase and the other bolts.</summary>
    Chase,

    /// <summary>One pounces on the other, who is startled and turns tail.</summary>
    Pounce
}

/// <summary>
/// Presentation-only pick of which chance encounter plays. Kept apart from the Garden node so the
/// weighting is testable without a scene tree; it never feeds simulation, training or persistence.
/// </summary>
public static class GardenEncounterRoll
{
    private const float GreetWeight = 0.45f;
    private const float ChaseWeight = 0.33f;

    /// <summary>Maps a uniform roll in [0,1) onto an encounter. Out-of-range rolls clamp.</summary>
    public static GardenEncounterKind Pick(float roll)
    {
        if (!float.IsFinite(roll) || roll < GreetWeight)
            return GardenEncounterKind.Greet;
        return roll < GreetWeight + ChaseWeight ? GardenEncounterKind.Chase : GardenEncounterKind.Pounce;
    }
}
