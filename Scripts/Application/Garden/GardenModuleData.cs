namespace Voidling.Application.Garden;

/// <summary>
/// Persisted Garden land. Placed, this is exactly one hex of the island: empty ground when
/// <see cref="StatId"/> is blank, training ground for that stat otherwise. Unplaced, it is the
/// whole piece the player bought — <see cref="ShapeId"/> says how many hexes it covers — waiting in
/// the inventory to be put down.
/// </summary>
public sealed class GardenModuleData
{
    public string Id { get; set; } = "";
    /// <summary>Blank on empty ground; a stat ID once the hex is turned into training ground.</summary>
    public string StatId { get; set; } = "";

    /// <summary>Shape bought from the shop. Only meaningful while the piece is still unplaced.</summary>
    public string ShapeId { get; set; } = "single";
    public int Level { get; set; } = 1;
    public bool Placed { get; set; }
    public int HexQ { get; set; }
    public int HexR { get; set; }

    /// <summary>Pre-hex placement slot. Read once by migration, then left at -1.</summary>
    public int SlotIndex { get; set; } = -1;
}
