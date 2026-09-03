using System;
using System.Collections.Generic;
using Voidling.Application.Daily;
using Voidling.Application.Garden;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Domain.Breeding;
using VoidlingGame;

namespace VoidlingGame;

/// <summary>
/// Serialized runtime aggregate. Kept as a DTO-shaped model for backward-compatible JSON.
/// Application services coordinate changes to it; domain services operate on focused inputs.
/// </summary>
public sealed class GameStateData
{
    private Dictionary<string, int> _utilityItems = new(StringComparer.Ordinal);
    private string _shopRareOfferItemId = string.Empty;

    public int SaveVersion { get; set; } = 20;
    public int Coins { get; set; } = 120;
    public double GardenIncomeCoinRemainder { get; set; }
    public double ShopEggRotationElapsedSeconds { get; set; }
    public long SeedCounter { get; set; } = 1;
    public List<VoidlingData> Voidlings { get; set; } = new();
    public List<VoidlingData> DepartedVoidlings { get; set; } = new();
    public List<LineageArchiveEntry> LineageArchive { get; set; } = new();
    public List<EggData> OwnedEggs { get; set; } = new();
    public List<EggData> StoreEggs { get; set; } = new();
    public List<EggShellData> EggShells { get; set; } = new();
    public Dictionary<string, int> TrainingItems { get; set; } = new();

    public Dictionary<string, int> UtilityItems
    {
        get => _utilityItems;
        set => _utilityItems = value ?? new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public string ShopRareOfferItemId
    {
        get => _shopRareOfferItemId;
        set => _shopRareOfferItemId = value ?? string.Empty;
    }

    public List<GardenModuleData> GardenModules { get; set; } = new();

    // Multiplayer transaction durability. These remain harmless empty collections for players who
    // never use multiplayer and do not make Steam/network access part of save loading.
    public List<PendingTradeJournalEntry> PendingTradeJournal { get; set; } = new();
    public List<string> AppliedTradeIds { get; set; } = new();

    // Local multiplayer progress is authoritative locally. Steam leaderboards are only a projection
    // of this data and can be rebuilt/retried if Steam is unavailable.
    public int MultiplayerWins { get; set; }
    public List<string> AppliedMultiplayerRaceIds { get; set; } = new();

    // The daily race is also local-first. Starting an attempt is persisted before the race begins;
    // Steam receives only a completed time for the friends leaderboard when available.
    public List<DailyRaceAttemptData> DailyRaceAttempts { get; set; } = new();
    public DailyLoginStateData DailyLogin { get; set; } = new();
    public DailyMissionStateData DailyMissions { get; set; } = new();

    // Settings remain in the existing save payload during migration for compatibility.
    public float MasterVolume { get; set; } = 1.0f;
    public float SoundEffectVolume { get; set; } = 1.0f;
    public float UiSoundVolume { get; set; } = 1.0f;
    public bool AutoFinishRaces { get; set; } = true;
    public bool EdgePanning { get; set; } = true;

    public bool TutorialCompleted { get; set; }
}
