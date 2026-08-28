using System.Collections.Generic;
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
    public int SaveVersion { get; set; } = 15;
    public int Coins { get; set; } = 120;
    public double GardenIncomeCoinRemainder { get; set; }
    public long SeedCounter { get; set; } = 1;
    public List<VoidlingData> Voidlings { get; set; } = new();
    public List<VoidlingData> DepartedVoidlings { get; set; } = new();
    public List<LineageArchiveEntry> LineageArchive { get; set; } = new();
    public List<EggData> OwnedEggs { get; set; } = new();
    public List<EggData> StoreEggs { get; set; } = new();
    public List<EggShellData> EggShells { get; set; } = new();
    public Dictionary<string, int> TrainingItems { get; set; } = new();

    public List<PendingTradeJournalEntry> PendingTradeJournal { get; set; } = new();
    public List<string> AppliedTradeIds { get; set; } = new();
    public int MultiplayerWins { get; set; }
    public List<string> AppliedMultiplayerRaceIds { get; set; } = new();
    public List<DailyRaceAttemptData> DailyRaceAttempts { get; set; } = new();

    public float MasterVolume { get; set; } = 1.0f;
    public float SoundEffectVolume { get; set; } = 1.0f;
    public float UiSoundVolume { get; set; } = 1.0f;
    public bool AutoFinishRaces { get; set; } = true;
    public bool EdgePanning { get; set; } = true;

    // New installs begin with the lightweight guided tour. Migration marks pre-tutorial saves as
    // completed so an update never surprises existing players with first-launch onboarding.
    public bool TutorialCompleted { get; set; }
}