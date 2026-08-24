namespace Voidling.Application.Ports;

public interface IAudioSettingsAdapter
{
    void ApplyMasterVolume(float linearVolume);
}
