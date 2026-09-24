namespace Voidling.Application.Garden;

/// <summary>
/// Persisted Garden land. Placed, this is exactly one hex of the island: plain ground when
/// <see cref="BiomeId"/> is blank, a biome that trains one stat otherwise. Unplaced, it is the
/// whole piece the player bought — <see cref="ShapeId"/> says how many hexes it covers — waiting in
/// the inventory to be put down.
/// </summary>
public sealed class GardenModuleData
{
    public string Id { get; set; } = "";

    /// <summary>
    /// Blank on plain ground; a biome (plains, water, …) or, at the top star, its special
    /// environment (swamp, volcano). Added in save version 23; older hexes derive it from the stat.
    /// </summary>
    public string BiomeId { get; set; } = "";

    /// <summary>The stat the biome trains, cached from <see cref="BiomeId"/>; blank on plain ground.</summary>
    public string StatId { get; set; } = "";

    /// <summary>Shape bought from the shop. Only meaningful while the piece is still unplaced.</summary>
    public string ShapeId { get; set; } = "single";

    /// <summary>The biome tile's stars, 1..4. Stacking a matching tile on top raises it one star.</summary>
    public int Level { get; set; } = 1;
    public bool Placed { get; set; }
    public int HexQ { get; set; }
    public int HexR { get; set; }

    /// <summary>
    /// True once a special variant (the Swamp guy) has hatched on this hex. Each special environment
    /// hatches at most one; a new one needs a Swamp that never has.
    /// </summary>
    public bool SpecialVariantHatched { get; set; }

    /// <summary>Pre-hex placement slot. Read once by migration, then left at -1.</summary>
    public int SlotIndex { get; set; } = -1;
}
