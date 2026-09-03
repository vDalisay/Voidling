namespace Voidling.Application.Garden;

/// <summary>
/// Persisted Garden land tile. A placed tile lives at an axial hex coordinate on the Garden grid;
/// an unplaced one sits in the player's inventory waiting to be put down.
/// </summary>
public sealed class GardenModuleData
{
    public string Id { get; set; } = "";
    public string StatId { get; set; } = "";
    public int Level { get; set; } = 1;
    public bool Placed { get; set; }
    public int HexQ { get; set; }
    public int HexR { get; set; }

    /// <summary>Pre-hex placement slot. Read once by migration, then left at -1.</summary>
    public int SlotIndex { get; set; } = -1;
}
