using Voidling.Application.Settings;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class SettingsArchitectureTests
{
    [Fact]
    public void MasterVolume_ClampsIntoPersistedRange()
    {
        var settings = new SettingsUseCase();
        var state = new GameStateData { MasterVolume = 0.5f };

        Assert.True(settings.SetMasterVolume(state, 2.0f));
        Assert.Equal(1.0f, state.MasterVolume);
        Assert.True(settings.SetMasterVolume(state, -0.5f));
        Assert.Equal(0.0f, state.MasterVolume);
    }

    [Fact]
    public void Settings_SameValueIsANoOp()
    {
        var settings = new SettingsUseCase();
        var state = new GameStateData
        {
            MasterVolume = 0.5f,
            AutoFinishRaces = true,
            EdgePanning = true
        };

        Assert.False(settings.SetMasterVolume(state, 0.5f));
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
            AutoFinishRaces = true,
            EdgePanning = true
        };

        Assert.True(settings.SetAutoFinishRaces(state, false));
        Assert.True(settings.SetEdgePanning(state, false));

        Assert.False(state.AutoFinishRaces);
        Assert.False(state.EdgePanning);
        Assert.Equal(0.4f, state.MasterVolume);
    }
}
