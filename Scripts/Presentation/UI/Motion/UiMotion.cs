using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// The one place the UI's motion goes through. Every helper is decoration: it starts from the
/// node's current look, always ends on the exact rest value (scale 1, white modulate, laid-out
/// position) and never holds up the action that triggered it.
///
/// Tweens are keyed per node and channel ("scale", "fade", "flash", "nudge") in node metadata, so a
/// new animation replaces the one running on the same channel instead of stacking, and dies with
/// its node. <see cref="Reduced"/> (the Reduce motion setting) skips straight to the rest value.
/// </summary>
public static class UiMotion
{
    public const double Snap = 0.06;
    public const double Quick = 0.12;
    public const double Normal = 0.18;
    public const double Slow = 0.26;

    private static readonly List<Tween> Transient = new();

    /// <summary>When set, animations jump to their end state and idle loops stay still.</summary>
    public static bool Reduced { get; set; }

    /// <summary>
    /// True while a transient (non-idle) animation is still running anywhere. Probes and smokes wait
    /// on it before measuring layout; gameplay never does.
    /// </summary>
    public static bool IsSettling
    {
        get
        {
            Transient.RemoveAll(tween => !GodotObject.IsInstanceValid(tween) || !tween.IsValid() || !tween.IsRunning());
            return Transient.Count > 0;
        }
    }

    /// <summary>Starts a keyed tween, killing whatever ran on that channel before. Null when the node cannot animate.</summary>
    public static Tween? Start(CanvasItem node, string channel, bool transient = true)
    {
        Kill(node, channel);
        if (Reduced || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
            return null;
        var tween = node.CreateTween();
        node.SetMeta(Key(channel), tween);
        if (transient)
            Transient.Add(tween);
        return tween;
    }

    public static void Kill(CanvasItem node, string channel)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        var key = Key(channel);
        if (!node.HasMeta(key)) return;
        if (node.GetMeta(key).AsGodotObject() is Tween tween && GodotObject.IsInstanceValid(tween) && tween.IsValid())
            tween.Kill();
        node.RemoveMeta(key);
    }

    /// <summary>Stops every channel and puts the node back at rest.</summary>
    public static void Rest(CanvasItem node)
    {
        foreach (var channel in new[] { "scale", "fade", "flash", "nudge" })
            Kill(node, channel);
        if (node is Control control) control.Scale = Vector2.One;
        node.SelfModulate = Colors.White;
    }

    /// <summary>Keeps scaling centred; call again whenever the control resizes.</summary>
    public static void CenterPivot(Control control)
        => control.PivotOffset = (control.Size * 0.5f).Round();

    /// <summary>A quick grow past rest and a springy settle back onto it.</summary>
    public static void Pop(Control control, float amount = 0.08f, double duration = Normal)
    {
        var tween = Start(control, "scale");
        if (tween == null) { control.Scale = Vector2.One; return; }
        CenterPivot(control);
        tween.TweenProperty(control, "scale", Vector2.One * (1.0f + amount), Snap)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(control, "scale", Vector2.One, duration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Pressed: wider and shorter, held until <see cref="Release"/>.</summary>
    public static void Squash(Control control)
    {
        var tween = Start(control, "scale");
        if (tween == null) { control.Scale = Vector2.One; return; }
        CenterPivot(control);
        tween.TweenProperty(control, "scale", new Vector2(1.06f, 0.9f), Snap)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Released: a stretch the other way and a bounce home.</summary>
    public static void Release(Control control)
    {
        var tween = Start(control, "scale");
        if (tween == null) { control.Scale = Vector2.One; return; }
        CenterPivot(control);
        tween.TweenProperty(control, "scale", new Vector2(0.96f, 1.07f), Snap)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(control, "scale", Vector2.One, Slow)
            .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>A bright blink of the node's own drawing, fading back to its drawn colours.</summary>
    public static void Flash(CanvasItem node, Color? tint = null, double duration = Normal)
    {
        var tween = Start(node, "flash");
        if (tween == null) { node.SelfModulate = Colors.White; return; }
        node.SelfModulate = tint ?? new Color(1.45f, 1.45f, 1.35f);
        tween.TweenProperty(node, "self_modulate", Colors.White, duration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// A "no": a few whole-pixel shoves sideways, for a click on something that cannot act. Uses a
    /// child-safe offset: the pivot, not the position, so containers keep their layout.
    /// </summary>
    public static void Nudge(Control control)
    {
        // A nudge interrupted by another keeps the first one's rest, not its shoved position.
        var restKey = Key("nudge_rest");
        var rest = control.HasMeta(restKey) && control.HasMeta(Key("nudge"))
            ? control.GetMeta(restKey).AsVector2()
            : control.Position;
        var tween = Start(control, "nudge");
        if (tween == null) return;
        control.SetMeta(restKey, rest);
        foreach (var offset in new[] { -2, 2, -1, 1, 0 })
        {
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(control)) control.Position = new Vector2(rest.X + offset, rest.Y);
            }));
            tween.TweenInterval(0.035);
        }
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(control)) control.RemoveMeta(restKey);
        }));
    }

    /// <summary>Fades the node in after <paramref name="delay"/>, with a small pop, then leaves it at rest.</summary>
    public static void Appear(CanvasItem node, double delay = 0.0, double duration = Quick, float pop = 0.06f)
    {
        var fade = Start(node, "fade");
        if (fade == null) { node.Modulate = Colors.White; if (node is Control rest) rest.Scale = Vector2.One; return; }
        node.Modulate = new Color(1, 1, 1, 0);
        if (delay > 0) fade.TweenInterval(delay);
        fade.TweenProperty(node, "modulate", Colors.White, duration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        if (node is not Control control || pop <= 0) return;

        var scale = Start(control, "scale");
        if (scale == null) return;
        control.Scale = Vector2.One * (1.0f - pop);
        if (delay > 0) scale.TweenInterval(delay);
        scale.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(control)) CenterPivot(control); }));
        scale.TweenProperty(control, "scale", Vector2.One, Normal)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Items appear one after another; long lists compress so the last is never late.</summary>
    public static void StaggerIn(IReadOnlyList<CanvasItem> items, float step = 0.035f, float maxTotal = 0.3f, float pop = 0.06f)
    {
        for (var index = 0; index < items.Count; index++)
            Appear(items[index], UiMotionMath.StaggerDelay(index, items.Count, step, maxTotal), Quick, pop);
    }

    /// <summary>An endless stepped loop (idle bob). Not counted as settling; stopped by <see cref="Kill"/>.</summary>
    public static Tween? Loop(CanvasItem node, string channel)
    {
        var tween = Start(node, channel, transient: false);
        tween?.SetLoops();
        return tween;
    }

    private static StringName Key(string channel) => "ui_motion_" + channel;
}
