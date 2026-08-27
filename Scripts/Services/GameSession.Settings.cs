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
        Save();
    }

    public void SetSoundEffectVolume(float value)
    {
        if (!_settings!.SetSoundEffectVolume(State, value))
            return;

        _audioSettings!.ApplySoundEffectVolume(State.SoundEffectVolume);
        Save();
    }

    public void SetUiSoundVolume(float value)
    {
        if (!_settings!.SetUiSoundVolume(State, value))
            return;

        _audioSettings!.ApplyUiSoundVolume(State.UiSoundVolume);
        Save();
    }

    public void SetAutoFinishRaces(bool enabled)
    {
        if (_settings!.SetAutoFinishRaces(State, enabled))
            Save();
    }

    public ulong CreateRaceSeed()
    {
        var seed = NextSeed();
        Save();
        return seed;
    }
}
