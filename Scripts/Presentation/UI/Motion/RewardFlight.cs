using System;
using Godot;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// A reward that visibly travels: a few copies of its icon arc from the button that earned it to
/// the counter that holds it. The counter is fed when the first one lands, so the number starts
/// rolling as the rest arrive. The transaction already happened; this only shows where it went.
/// </summary>
public partial class RewardFlight : TextureRect
{
    private const double FlightSeconds = 0.55;

    private Vector2 _from;
    private Vector2 _to;
    private double _delay;
    private float _arc;
    private Action? _landed;

    /// <summary>
    /// Sends <paramref name="pieces"/> icons from <paramref name="fromGlobal"/> to the centre of
    /// <paramref name="target"/>. <paramref name="firstLanded"/> runs once, when the first lands (or
    /// at once with reduced motion).
    /// </summary>
    public static void Launch(Node context, Vector2 fromGlobal, Control target, Texture2D icon, int pieces, Action firstLanded)
    {
        if (UiMotion.Reduced || !GodotObject.IsInstanceValid(target) || !target.IsVisibleInTree())
        {
            firstLanded();
            return;
        }

        var to = target.GetGlobalRect().GetCenter();
        var fired = false;
        void Land()
        {
            if (GodotObject.IsInstanceValid(target))
            {
                UiMotion.Pop(target, 0.12f);
                UiMotion.Flash(target);
            }
            if (fired) return;
            fired = true;
            firstLanded();
        }

        var canvas = UiFxLayer.For(context);
        for (var index = 0; index < Math.Max(1, pieces); index++)
        {
            var spread = (index - (pieces - 1) * 0.5f) * 7.0f;
            canvas.AddChild(new RewardFlight
            {
                Texture = icon,
                Size = new Vector2(16, 16),
                ExpandMode = ExpandModeEnum.IgnoreSize,
                StretchMode = StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                _from = (fromGlobal + new Vector2(spread, 0)).Round() - new Vector2(8, 8),
                _to = to.Round() - new Vector2(8, 8),
                _delay = index * 0.07,
                _arc = 26.0f + 6.0f * (index % 3),
                _landed = Land,
                Position = (fromGlobal + new Vector2(spread, 0)).Round() - new Vector2(8, 8),
                Modulate = new Color(1, 1, 1, 0)
            });
        }
    }

    public override void _Ready()
    {
        var tween = CreateTween();
        if (_delay > 0) tween.TweenInterval(_delay);
        tween.TweenProperty(this, "modulate", Colors.White, 0.06);
        tween.TweenMethod(Callable.From<float>(Fly), 0.0f, 1.0f, FlightSeconds)
            .SetTrans(Tween.TransitionType.Linear);
        tween.TweenCallback(Callable.From(() => _landed?.Invoke()));
        tween.Finished += QueueFree;
    }

    private void Fly(float t)
    {
        var (x, y) = UiMotionMath.FlightPoint(_from.X, _from.Y, _to.X, _to.Y, _arc, t);
        Position = new Vector2(x, y);
    }
}
