using Godot;
using Voidling.Presentation.UI.Common;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// The pack's selector brackets around the focused control: keyboard and controller players get
/// the same "this one" cue a hover gives the mouse. The brackets breathe one pixel in and out on a
/// stepped loop, so they stay crisp; nothing runs while the cursor is hidden.
/// </summary>
public partial class FocusCursor : Control
{
    // Each 16x16 cell holds an 8x9 bracket at (4, 4): white corners in the first two columns,
    // a second breathing frame two rows down.
    private static readonly Rect2[] Corners =
    {
        new(4, 4, 8, 9), new(20, 4, 8, 9), new(4, 20, 8, 9), new(20, 20, 8, 9)
    };

    private int _frame;
    private Control? _target;

    public override void _Ready()
    {
        Name = "FocusCursor";
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        Visible = false;
        _target = GetParent() as Control;
        if (_target != null) _target.Resized += Follow;
        Follow();
    }

    public void ShowCursor()
    {
        _frame = 0;
        Visible = true;
        Follow();
        QueueRedraw();
        var loop = UiMotion.Loop(this, "breathe");
        if (loop == null) return;
        loop.TweenInterval(0.34);
        loop.TweenCallback(Callable.From(Step));
    }

    public void HideCursor()
    {
        UiMotion.Kill(this, "breathe");
        Visible = false;
    }

    private void Step()
    {
        _frame = 1 - _frame;
        QueueRedraw();
    }

    private void Follow()
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target)) return;
        Position = Vector2.Zero;
        Size = _target.Size;
    }

    public override void _Draw()
    {
        // Frame 1 sits a pixel further out, so the brackets pulse around the control.
        var outset = 2 + _frame;
        var rect = new Rect2(-outset, -outset, Mathf.Round(Size.X) + outset * 2, Mathf.Round(Size.Y) + outset * 2);
        var row = _frame * 32;
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(rect.Position, new Vector2(8, 9)),
            Offset(Corners[0], row));
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(new Vector2(rect.End.X - 8, rect.Position.Y), new Vector2(8, 9)),
            Offset(Corners[1], row));
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(new Vector2(rect.Position.X, rect.End.Y - 9), new Vector2(8, 9)),
            Offset(Corners[2], row));
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(new Vector2(rect.End.X - 8, rect.End.Y - 9), new Vector2(8, 9)),
            Offset(Corners[3], row));
    }

    private static Rect2 Offset(Rect2 region, int rowOffset)
        => new(region.Position + new Vector2(0, rowOffset), region.Size);
}
