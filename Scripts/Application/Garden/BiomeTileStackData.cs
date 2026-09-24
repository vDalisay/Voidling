namespace Voidling.Application.Garden;

/// <summary>
/// Biome tiles waiting in the inventory: bought at one star from the shop, or picked back up off
/// the island with the stars they had. Tiles of one biome and star are interchangeable.
/// </summary>
public sealed class BiomeTileStackData
{
    public string BiomeId { get; set; } = "";
    public int Stars { get; set; } = 1;
    public int Count { get; set; }
}
