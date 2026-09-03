namespace Voidling.Domain.Racing;

/// <summary>
/// Immutable race-entry data. Once a race starts, simulation reads this snapshot rather than the
/// live garden creature. Cosmetic appearance is frozen alongside result-affecting stats, but the
/// simulator never reads the appearance fields to determine outcomes.
/// </summary>
public sealed record RaceParticipantSnapshot(
    string CreatureId,
    string DisplayName,
    string TintHex,
    float Run,
    float Swim,
    float Fly,
    float Power,
    float Stamina,
    string VisualTypeId = "normal",
    float PaletteHue = -1.0f,
    string[]? LayerIds = null);
