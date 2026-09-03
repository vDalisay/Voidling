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
    }

    private void RefreshGardenEnvironmentFromSystemClock()
        => ApplyGardenEnvironment(DateTime.Now, immediate: false);

    private void ApplyGardenEnvironment(DateTime localTime, bool immediate)
    {
        var target = GardenEnvironmentPalette.Resolve(localTime);
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
