namespace Voidling.Application.Ports;

public interface IAudioSettingsAdapter
{
    void ApplyMasterVolume(float linearVolume);
    void ApplySoundEffectVolume(float linearVolume);
    void ApplyUiSoundVolume(float linearVolume);
}
