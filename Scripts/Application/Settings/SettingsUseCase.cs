using System;
using VoidlingGame;

namespace Voidling.Application.Settings;

/// <summary>
/// Applies persisted player settings without Godot APIs. Platform side effects such as changing
/// AudioServer buses remain in Infrastructure and are invoked by the lifetime shell.
/// </summary>
public sealed class SettingsUseCase
{
    public bool SetMasterVolume(GameStateData state, float value)
        => SetVolume(state, value, static current => current.MasterVolume, static (current, volume) => current.MasterVolume = volume);

    public bool SetSoundEffectVolume(GameStateData state, float value)
        => SetVolume(state, value, static current => current.SoundEffectVolume, static (current, volume) => current.SoundEffectVolume = volume);

    public bool SetUiSoundVolume(GameStateData state, float value)
        => SetVolume(state, value, static current => current.UiSoundVolume, static (current, volume) => current.UiSoundVolume = volume);

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

    private static bool SetVolume(
        GameStateData state,
        float value,
        Func<GameStateData, float> read,
        Action<GameStateData, float> write)
    {
        ArgumentNullException.ThrowIfNull(state);
        // Match save migration: a non-finite external value must never poison the live state.
        var clamped = float.IsFinite(value) ? Math.Clamp(value, 0.0f, 1.0f) : 1.0f;
        if (Math.Abs(read(state) - clamped) < 0.0001f)
            return false;

        write(state, clamped);
        return true;
    }
}
