using Godot;
using Voidling.Presentation.UI.Common;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// "+12" or "-8" in the pack's pixel font, thrown up from a counter when it changes. It rises in
/// whole-pixel steps, fades and frees itself; it lives on the effect layer, so a counter that is
/// rebuilt the same frame does not take it along.
/// </summary>
public partial class FloatingNumber : Label
{
    private const float Rise = 14.0f;
    private const double Lifetime = 0.85;

    private Vector2 _origin;

    public static void Spawn(Node context, Vector2 globalCenter, long delta, Color? color = null)
    {
        if (UiMotion.Reduced || delta == 0 || !GodotObject.IsInstanceValid(context) || !context.IsInsideTree())
            return;
        var label = new FloatingNumber
        {
            Text = UiMotionMath.SignedDelta(delta),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(64, 12),
            AutoTranslateMode = AutoTranslateModeEnum.Disabled
        };
        UiSkin.ApplyNumberFont(label, 1, color ?? (delta > 0 ? UiSkin.Gain : UiSkin.Loss), UiSkin.Cream);
        label._origin = (globalCenter - new Vector2(32, 6)).Round();
        label.Position = label._origin;
        UiFxLayer.For(context).AddChild(label);
    }

    public override void _Ready()
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(Step), 0.0f, 1.0f, Lifetime);
        tween.Finished += QueueFree;
    }

    private void Step(float t)
    {
        Position = _origin + new Vector2(0, -Mathf.Round(Rise * UiMotionMath.EaseOutCubic(t)));
        Modulate = new Color(1, 1, 1, t < 0.6f ? 1.0f : 1.0f - (t - 0.6f) / 0.4f);
    }
}
