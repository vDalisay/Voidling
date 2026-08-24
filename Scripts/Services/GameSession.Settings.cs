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

        ApplyAudioSettings();
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
