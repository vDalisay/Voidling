using Godot;

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
        State.MasterVolume = Mathf.Clamp(value, 0.0f, 1.0f);
        ApplyAudioSettings();
        Save();
    }

    public void SetAutoFinishRaces(bool enabled)
    {
        State.AutoFinishRaces = enabled;
        Save();
    }

    public ulong CreateRaceSeed()
    {
        var seed = NextSeed();
        Save();
        return seed;
    }
}
