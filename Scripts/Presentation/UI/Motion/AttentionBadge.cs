using Godot;
using Voidling.Presentation.UI.Common;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// A small "!" on a round pack button that sits on a control's corner while it has something to
/// claim. It pops in, then bobs in whole-pixel steps on a stepped loop; hidden, it does nothing.
/// </summary>
public partial class AttentionBadge : Control
{
    private static readonly Rect2 SalmonButton = new(1, 11 * 16, 14, 16);

    private Control _host = null!;
    private bool _active;
    private int _bob;

    public static AttentionBadge Attach(Control host)
    {
        foreach (var child in host.GetChildren(includeInternal: true))
            if (child is AttentionBadge existing) return existing;
        var badge = new AttentionBadge { Name = "AttentionBadge" };
        host.AddChild(badge, false, InternalMode.Back);
        return badge;
    }

    public override void _Ready()
    {
        _host = GetParent<Control>();
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(14, 16);
        Visible = false;
        _host.Resized += Place;
        Place();

        var mark = new Label
        {
            Text = "!",
            Size = new Vector2(14, 13),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled
        };
        UiSkin.ApplyNumberFont(mark, 1, Colors.White, UiSkin.InkOnRed);
        AddChild(mark);
    }

    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            Visible = value;
            if (!value)
            {
                UiMotion.Kill(this, "bob");
                _bob = 0;
                Place();
                return;
            }
            Place();
            UiMotion.Pop(this, 0.3f, UiMotion.Slow);
            var loop = UiMotion.Loop(this, "bob");
            if (loop == null) return;
            // Hop twice, then rest, so it catches the eye without buzzing.
            foreach (var step in UiMotionMath.BobSteps)
            {
                var captured = step;
                loop.TweenCallback(Callable.From(() => { _bob = captured; Place(); }));
                loop.TweenInterval(0.07);
            }
            loop.TweenInterval(1.1);
        }
    }

    public override void _Draw() => DrawTextureRectRegion(UiSkin.RoundButtons, new Rect2(Vector2.Zero, Size), SalmonButton);

    private void Place()
    {
        if (!GodotObject.IsInstanceValid(_host)) return;
        Position = new Vector2(Mathf.Round(_host.Size.X - 9), -6 + _bob);
    }
}
