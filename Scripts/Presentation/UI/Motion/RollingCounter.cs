using System;
using Godot;
using Voidling.Presentation.UI.Common;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// Makes a label a counter that rolls to each new value instead of snapping: whole numbers only,
/// eased, never past the target, with a pop, a green (gain) or red (loss) ink flash and a thrown
/// "+N"/"-N". The value is set immediately; only what the label shows catches up.
/// </summary>
public partial class RollingCounter : Node
{
    private Label _label = null!;
    private long _value;
    private long _shown;
    private Func<long, string> _format = value => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private Color _ink;

    /// <summary>Whether a change throws a floating "+N". On by default.</summary>
    public bool ThrowDelta { get; set; } = true;

    /// <summary>What pops on a change; the label itself unless a surrounding plate is nicer.</summary>
    public Control? PopTarget { get; set; }

    public long Value => _value;

    public static RollingCounter Attach(Label label, long value, Func<long, string>? format = null)
    {
        var counter = new RollingCounter { Name = "Counter", _value = value, _shown = value };
        if (format != null) counter._format = format;
        label.Text = counter._format(value);
        label.AddChild(counter, false, InternalMode.Front);
        return counter;
    }

    public override void _Ready()
    {
        _label = GetParent<Label>();
        _ink = _label.GetThemeColor("font_color");
    }

    /// <summary>Sets the value; with <paramref name="animate"/> the label rolls there.</summary>
    public void SetValue(long value, bool animate = true)
    {
        if (value == _value) return;
        var from = _shown;
        var delta = value - _value;
        _value = value;
        if (!animate || !IsInsideTree() || UiMotion.Reduced)
        {
            UiMotion.Kill(_label, "roll");
            Show(value);
            return;
        }

        var roll = UiMotion.Start(_label, "roll");
        if (roll == null) { Show(value); return; }
        roll.TweenMethod(Callable.From<float>(t => Show(UiMotionMath.RollValue(from, value, t))),
            0.0f, 1.0f, UiMotionMath.RollSeconds(from, value));

        var target = PopTarget ?? _label;
        UiMotion.Pop(target, 0.14f);
        FlashInk(delta > 0 ? UiSkin.Gain : UiSkin.Loss);
        if (ThrowDelta)
            FloatingNumber.Spawn(_label, target.GetGlobalRect().GetCenter() + new Vector2(0, -12), delta);
    }

    private void Show(long value)
    {
        _shown = value;
        if (GodotObject.IsInstanceValid(_label)) _label.Text = _format(value);
    }

    private void FlashInk(Color color)
    {
        var tween = UiMotion.Start(_label, "ink");
        if (tween == null) return;
        tween.TweenMethod(Callable.From<Color>(ink => _label.AddThemeColorOverride("font_color", ink)), color, _ink, 0.55)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }
}
