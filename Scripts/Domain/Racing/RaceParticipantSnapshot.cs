namespace Voidling.Domain.Racing;

/// <summary>
/// Immutable race-entry data. Once a race starts, simulation reads this snapshot rather
/// than the live garden creature, so training/needs/save mutations cannot alter an active race.
/// </summary>
public sealed record RaceParticipantSnapshot(
    string CreatureId,
    string DisplayName,
    string TintHex,
    float Run,
    float Swim,
    float Fly,
    float Power,
    float Stamina);
