using Godot;
using Voidling.Application.Ports;

namespace Voidling.Infrastructure.Audio;

public sealed class GodotAudioSettingsAdapter : IAudioSettingsAdapter
{
    private readonly string _masterBusName;

    public GodotAudioSettingsAdapter(string masterBusName = "Master")
    {
        _masterBusName = masterBusName;
    }

    public void ApplyMasterVolume(float linearVolume)
    {
        var bus = AudioServer.GetBusIndex(_masterBusName);
        if (bus < 0)
            return;

        var volume = Mathf.Clamp(linearVolume, 0.0f, 1.0f);
        AudioServer.SetBusMute(bus, volume <= 0.001f);
        if (volume > 0.001f)
            AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(volume));
    }
}
