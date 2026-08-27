using Godot;
using Voidling.Application.Ports;

namespace Voidling.Infrastructure.Audio;

public sealed class GodotAudioSettingsAdapter : IAudioSettingsAdapter
{
    private readonly string _masterBusName;
    private readonly string _soundEffectBusName;
    private readonly string _uiSoundBusName;

    public GodotAudioSettingsAdapter(
        string masterBusName = "Master",
        string soundEffectBusName = "SFX",
        string uiSoundBusName = "UI")
    {
        _masterBusName = masterBusName;
        _soundEffectBusName = soundEffectBusName;
        _uiSoundBusName = uiSoundBusName;
    }

    public void ApplyMasterVolume(float linearVolume)
        => ApplyBusVolume(_masterBusName, linearVolume);

    public void ApplySoundEffectVolume(float linearVolume)
        => ApplyBusVolume(_soundEffectBusName, linearVolume);

    public void ApplyUiSoundVolume(float linearVolume)
        => ApplyBusVolume(_uiSoundBusName, linearVolume);

    private static void ApplyBusVolume(string busName, float linearVolume)
    {
        var bus = AudioServer.GetBusIndex(busName);
        if (bus < 0)
            return;

        var volume = Mathf.Clamp(linearVolume, 0.0f, 1.0f);
        AudioServer.SetBusMute(bus, volume <= 0.001f);
        if (volume > 0.001f)
            AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(volume));
    }
}
