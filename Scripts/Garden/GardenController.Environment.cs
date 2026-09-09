using System;
using Godot;
using Voidling.Presentation.Garden;

namespace VoidlingGame;

public partial class GardenController
{
    private const double GardenEnvironmentRefreshSeconds = 30.0;
    private const double GardenEnvironmentBlendSeconds = 2.0;

    private Timer? _gardenEnvironmentTimer;
    private Tween? _gardenEnvironmentTween;
    private bool _gardenEnvironmentInstalled;
    public DateTime EnvironmentLocalTime { get; private set; } = DateTime.Now;
    public event Action<DateTime>? EnvironmentTimeChanged;

    private void InstallGardenEnvironmentPresentation()
    {
        if (_gardenEnvironmentTimer != null && GodotObject.IsInstanceValid(_gardenEnvironmentTimer))
            return;

        ApplyGardenEnvironment(DateTime.Now, immediate: true);

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
        => ApplyGardenEnvironment(DateTime.Now, immediate: false);

    private void ApplyGardenEnvironment(DateTime localTime, bool immediate)
    {
        EnvironmentLocalTime = localTime;
        EnvironmentTimeChanged?.Invoke(localTime);
        var target = _session.State.GardenTint ? GardenEnvironmentPalette.Resolve(localTime) : Colors.White;
        if (ColorsApproximatelyEqual(Modulate, target))
            return;

        if (immediate || !IsInsideTree())
        {
            Modulate = target;
            return;
        }

        _gardenEnvironmentTween?.Kill();
        _gardenEnvironmentTween = CreateTween();
        _gardenEnvironmentTween.TweenProperty(this, "modulate", target, GardenEnvironmentBlendSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private static bool ColorsApproximatelyEqual(Color first, Color second)
        => Mathf.IsEqualApprox(first.R, second.R) &&
           Mathf.IsEqualApprox(first.G, second.G) &&
           Mathf.IsEqualApprox(first.B, second.B) &&
           Mathf.IsEqualApprox(first.A, second.A);
}
