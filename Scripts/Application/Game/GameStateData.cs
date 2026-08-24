using System.Collections.Generic;
using Voidling.Domain.Breeding;
using VoidlingGame;

namespace VoidlingGame;

/// <summary>
/// Serialized runtime aggregate. Kept as a DTO-shaped model for backward-compatible JSON.
/// Application services coordinate changes to it; domain services operate on focused inputs.
/// </summary>
public sealed class GameStateData
{
    public int SaveVersion { get; set; } = 5;
    public int Coins { get; set; } = 120;
    public long SeedCounter { get; set; } = 1;
    public List<VoidlingData> Voidlings { get; set; } = new();
    public List<VoidlingData> DepartedVoidlings { get; set; } = new();
    public List<LineageArchiveEntry> LineageArchive { get; set; } = new();
    public List<EggData> OwnedEggs { get; set; } = new();
    public List<EggData> StoreEggs { get; set; } = new();
    public Dictionary<string, int> TrainingItems { get; set; } = new();

    // Settings remain in the existing save payload during migration for compatibility.
    public float MasterVolume { get; set; } = 1.0f;
    public bool AutoFinishRaces { get; set; } = true;
    public bool EdgePanning { get; set; } = true;
}
