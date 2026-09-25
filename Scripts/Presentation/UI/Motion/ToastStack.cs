using Godot;
using Voidling.Presentation.UI.Common;
using VoidlingGame;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// Short paper notes that pop in above everything (menus included) and fade away, newest at the
/// bottom, at most three at a time. Used for Garden news the player would otherwise miss because
/// the log is hidden behind a menu or folded to one line.
/// </summary>
public partial class ToastStack : VBoxContainer
{
    private const int MaxToasts = 3;
    private const double HoldSeconds = 2.6;

    public override void _Ready()
    {
        Name = "ToastStack";
        MouseFilter = MouseFilterEnum.Ignore;
        Alignment = AlignmentMode.End;
        AddThemeConstantOverride("separation", 3);
    }

    /// <summary>Fades every note away, for when the log they repeat is back in view.</summary>
    public void DismissAll()
    {
        foreach (var child in GetChildren())
        {
            if (child is not Control toast || toast.IsQueuedForDeletion()) continue;
            var fade = toast.CreateTween();
            fade.TweenProperty(toast, "modulate", new Color(1, 1, 1, 0), UiMotion.Quick);
            fade.TweenCallback(Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(toast)) return;
                if (toast.GetParent() == this) RemoveChild(toast);
                toast.QueueFree();
            }));
        }
    }

    public void Post(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        while (GetChildCount() >= MaxToasts)
        {
            var oldest = GetChild(0);
            RemoveChild(oldest);
            oldest.QueueFree();
        }

        var toast = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        toast.AddThemeStyleboxOverride("panel", UiSkin.Paper());
        var label = UiFactory.CreateLabel(text.Trim(), 7);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(Mathf.Min(220, 40 + text.Length * 4), 0);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore;
        toast.AddChild(label);
        AddChild(toast);
        UiMotion.Appear(toast, 0.0, UiMotion.Quick, 0.14f);

        var life = toast.CreateTween();
        life.TweenInterval(UiMotion.Reduced ? HoldSeconds + 0.5 : HoldSeconds);
        life.TweenProperty(toast, "modulate", new Color(1, 1, 1, 0), UiMotion.Normal);
        life.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(toast)) return;
            if (toast.GetParent() == this) RemoveChild(toast);
            toast.QueueFree();
        }));
    }
}
