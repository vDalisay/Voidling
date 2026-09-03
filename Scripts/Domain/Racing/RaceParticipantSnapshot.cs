using System;
using System.Text.Json.Serialization;

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
    string LayerIdsKey = "")
{
    private const char LayerSeparator = '|';

    [JsonIgnore]
    public string[] LayerIds => string.IsNullOrWhiteSpace(LayerIdsKey)
        ? Array.Empty<string>()
        : LayerIdsKey.Split(LayerSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string BuildLayerIdsKey(System.Collections.Generic.IEnumerable<string>? layerIds)
    {
        if (layerIds == null)
            return string.Empty;

        return string.Join(
            LayerSeparator,
            layerIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
    }
}
