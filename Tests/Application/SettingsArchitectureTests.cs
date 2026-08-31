using Voidling.Application.Settings;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class SettingsArchitectureTests
{
    [Fact]
    public void AudioVolumes_ClampIntoPersistedRangeIndependently()
    {
        var settings = new SettingsUseCase();
        var state = new GameStateData
        {
            MasterVolume = 0.5f,
            SoundEffectVolume = 0.4f,
            UiSoundVolume = 0.3f
        };

        Assert.True(settings.SetMasterVolume(state, 2.0f));
        Assert.True(settings.SetSoundEffectVolume(state, -0.5f));
        Assert.True(settings.SetUiSoundVolume(state, 0.75f));

        Assert.Equal(1.0f, state.MasterVolume);
        Assert.Equal(0.0f, state.SoundEffectVolume);
        Assert.Equal(0.75f, state.UiSoundVolume);
    }

    [Fact]
    public void AudioVolumes_NormalizeNonFiniteInputWithoutPoisoningLiveState()
    {
        var settings = new SettingsUseCase();
        var state = new GameStateData
        {
            MasterVolume = 0.25f,
            SoundEffectVolume = 0.5f,
            UiSoundVolume = 0.75f
        };

        Assert.True(settings.SetMasterVolume(state, float.NaN));
        Assert.True(settings.SetSoundEffectVolume(state, float.PositiveInfinity));
        Assert.True(settings.SetUiSoundVolume(state, float.NegativeInfinity));

        Assert.Equal(1.0f, state.MasterVolume);
        Assert.Equal(1.0f, state.SoundEffectVolume);
        Assert.Equal(1.0f, state.UiSoundVolume);
    }

    [Fact]
    public void Settings_SameValueIsANoOp()
    {
        var settings = new SettingsUseCase();
        var state = new GameStateData
        {
            MasterVolume = 0.5f,
            SoundEffectVolume = 0.6f,
            UiSoundVolume = 0.7f,
            AutoFinishRaces = true,
            EdgePanning = true
        };

        Assert.False(settings.SetMasterVolume(state, 0.5f));
        Assert.False(settings.SetSoundEffectVolume(state, 0.6f));
        Assert.False(settings.SetUiSoundVolume(state, 0.7f));
        Assert.False(settings.SetAutoFinishRaces(state, true));
        Assert.False(settings.SetEdgePanning(state, true));
    }

    [Fact]
    public void ToggleSettings_ChangeOnlyTheirOwnedValues()
    {
        var settings = new SettingsUseCase();
        var state = new GameStateData
        {
            MasterVolume = 0.4f,
            SoundEffectVolume = 0.3f,
            UiSoundVolume = 0.2f,
            AutoFinishRaces = true,
            EdgePanning = true
        };

        Assert.True(settings.SetAutoFinishRaces(state, false));
        Assert.True(settings.SetEdgePanning(state, false));

        Assert.False(state.AutoFinishRaces);
        Assert.False(state.EdgePanning);
        Assert.Equal(0.4f, state.MasterVolume);
        Assert.Equal(0.3f, state.SoundEffectVolume);
        Assert.Equal(0.2f, state.UiSoundVolume);
    }
}
