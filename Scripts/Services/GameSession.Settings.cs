namespace VoidlingGame;

public partial class GameSession
{
    public void AddRaceReward(int place)
    {
        var reward = place switch
        {
            1 => 30,
            2 => 20,
            3 => 10,
            _ => 5
        };

        State.Coins += reward;
        SaveAndNotify($"Race reward: +{reward} sprouts.");
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
