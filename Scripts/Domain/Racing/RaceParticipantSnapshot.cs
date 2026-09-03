using System.Text.Json.Serialization;
using VoidlingGame;

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
    string VisualTypeId = VoidlingAppearanceData.DefaultVisualTypeId,
    float PaletteHue = VoidlingAppearanceData.LegacyUninitializedPaletteHue,
    string LayerIdsKey = "")
{
    [JsonIgnore]
    public string[] LayerIds => VoidlingAppearanceData.ParseLayerIdsKey(LayerIdsKey);

    public static string BuildLayerIdsKey(System.Collections.Generic.IEnumerable<string>? layerIds)
        => VoidlingAppearanceData.BuildLayerIdsKey(layerIds);
}
