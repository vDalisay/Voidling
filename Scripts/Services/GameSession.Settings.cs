using System;
using Voidling.Application.Persistence;

namespace VoidlingGame;

public partial class GameSession
{
    public void AddRaceReward(int place)
    {
        var result = _raceResults!.AwardPlacement(State, place);
        SaveAndNotify($"Race reward: +{result.Reward} sprouts.");
    }

    public void SetMasterVolume(float value)
    {
        if (!_settings!.SetMasterVolume(State, value))
            return;

        _audioSettings!.ApplyMasterVolume(State.MasterVolume);
        Save(showFeedback: true);
    }

    public void SetSoundEffectVolume(float value)
    {
        if (!_settings!.SetSoundEffectVolume(State, value))
            return;

        _audioSettings!.ApplySoundEffectVolume(State.SoundEffectVolume);
        Save(showFeedback: true);
    }

    public void SetUiSoundVolume(float value)
    {
        if (!_settings!.SetUiSoundVolume(State, value))
            return;

        _audioSettings!.ApplyUiSoundVolume(State.UiSoundVolume);
        Save(showFeedback: true);
    }

    public void SetAutoFinishRaces(bool enabled)
    {
        if (_settings!.SetAutoFinishRaces(State, enabled))
            Save(showFeedback: true);
    }

    public ulong CreateRaceSeed()
    {
        var seed = NextSeed();
        Save();
        return seed;
    }

    /// <summary>
    /// Renames the island. The name is the player's own text, so it is stored as typed and never
    /// translated; only surrounding whitespace and length are normalized.
    /// </summary>
    public bool SetGardenName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length > GameStateMigrationService.GardenNameMaxLength)
            trimmed = trimmed[..GameStateMigrationService.GardenNameMaxLength];
        if (string.Equals(trimmed, State.GardenName, StringComparison.Ordinal))
            return false;

        State.GardenName = trimmed;
        SaveAndNotify(trimmed.Length == 0 ? "Garden name cleared." : $"Garden renamed to {trimmed}.");
        return true;
    }
}
