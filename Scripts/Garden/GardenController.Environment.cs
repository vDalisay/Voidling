using System;
using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    private const double GardenEnvironmentRefreshSeconds = 30.0;

    private Timer? _gardenEnvironmentTimer;
    private bool _gardenEnvironmentInstalled;
    public DateTime EnvironmentLocalTime { get; private set; } = DateTime.Now;
    public event Action<DateTime>? EnvironmentTimeChanged;

    private void InstallGardenEnvironmentPresentation()
    {
        if (_gardenEnvironmentTimer != null && GodotObject.IsInstanceValid(_gardenEnvironmentTimer))
            return;

        RefreshGardenEnvironmentFromSystemClock();

        _gardenEnvironmentTimer = new Timer
        {
            Name = "GardenEnvironmentClock",
            WaitTime = GardenEnvironmentRefreshSeconds,
            OneShot = false,
            Autostart = true,
            ProcessMode = ProcessModeEnum.Always
        };
        _gardenEnvironmentTimer.Timeout += RefreshGardenEnvironmentFromSystemClock;
        AddChild(_gardenEnvironmentTimer);

        // The player can switch the tint off in Settings, so the same signal that refreshes the
        // rest of the Garden re-resolves it; the equality guard below keeps unrelated saves free.
        _gardenEnvironmentInstalled = true;
        _session.StateChanged += RefreshGardenEnvironmentFromSystemClock;
        TreeExiting += DetachGardenEnvironmentPresentation;
    }

    private void DetachGardenEnvironmentPresentation()
    {
        if (!_gardenEnvironmentInstalled)
            return;

        _gardenEnvironmentInstalled = false;
        if (GodotObject.IsInstanceValid(_session))
            _session.StateChanged -= RefreshGardenEnvironmentFromSystemClock;
        TreeExiting -= DetachGardenEnvironmentPresentation;
    }

    private void RefreshGardenEnvironmentFromSystemClock()
        => ApplyGardenEnvironment(DateTime.Now.AddSeconds(_session.DeveloperClockOffsetSeconds));

    public void RefreshDeveloperEnvironment() => RefreshGardenEnvironmentFromSystemClock();

    /// <summary>
    /// The day, dusk and night light is the atmosphere's ambient light now rather than a tint on this
    /// node, so lights in the Garden can glow on top of it. The atmosphere eases between colours
    /// itself.
    /// </summary>
    private void ApplyGardenEnvironment(DateTime localTime)
    {
        EnvironmentLocalTime = localTime;
        EnvironmentTimeChanged?.Invoke(localTime);
        Modulate = Colors.White;
        _atmosphere.SetClock(localTime, _session.State.GardenTint);
    }
}
