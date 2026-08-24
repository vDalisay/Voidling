using System;
using VoidlingGame;

namespace Voidling.Application.Settings;

/// <summary>
/// Applies persisted player settings without Godot APIs. Platform side effects such as changing
/// an AudioServer bus remain in Infrastructure and are invoked by the presentation/lifetime shell.
/// </summary>
public sealed class SettingsUseCase
{
    public bool SetMasterVolume(GameStateData state, float value)
    {
        ArgumentNullException.ThrowIfNull(state);
        var clamped = Math.Clamp(value, 0.0f, 1.0f);
        if (Math.Abs(state.MasterVolume - clamped) < 0.0001f)
            return false;

        state.MasterVolume = clamped;
        return true;
    }

    public bool SetAutoFinishRaces(GameStateData state, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.AutoFinishRaces == enabled)
            return false;
        state.AutoFinishRaces = enabled;
        return true;
    }

    public bool SetEdgePanning(GameStateData state, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.EdgePanning == enabled)
            return false;
        state.EdgePanning = enabled;
        return true;
    }
}
