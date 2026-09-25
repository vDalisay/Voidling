namespace VoidlingGame;

public partial class GameSession
{
    public void SetEdgePanning(bool enabled)
    {
        if (!_settings!.SetEdgePanning(State, enabled))
            return;

        Save();
        StateChanged?.Invoke();
    }

    /// <summary>Notifies because the Garden re-resolves its cosmetic tint from the same signal.</summary>
    public void SetGardenTint(bool enabled)
    {
        if (!_settings!.SetGardenTint(State, enabled))
            return;

        Save();
        StateChanged?.Invoke();
    }

    /// <summary>Notifies so the root UI switches its motion off or on straight away.</summary>
    public void SetReduceMotion(bool enabled)
    {
        if (!_settings!.SetReduceMotion(State, enabled))
            return;

        Save();
        StateChanged?.Invoke();
    }
}
