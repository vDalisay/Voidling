using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>
/// Session-only garden history presented like an MMO chat log: transparent over the world,
/// compact timestamped lines, auto-following new activity, and still manually scrollable.
/// </summary>
public partial class GardenEventLog : Control
{
    private const int MaxEntries = 80;

    private readonly Queue<string> _entries = new();
    private RichTextLabel _history = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;

        _history = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = false,
            ScrollActive = true,
            ScrollFollowing = true,
            SelectionEnabled = true,
            Position = Vector2.Zero,
            Size = Size,
            CustomMinimumSize = CustomMinimumSize,
            MouseFilter = MouseFilterEnum.Stop
        };
        _history.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        UiFactory.ApplyPixelFont(_history, 6);

        // MMO-chat style: no window chrome/background. Keep a small outline/shadow so text stays
        // readable over water, grass and decorations without putting a beige panel behind it.
        _history.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _history.AddThemeColorOverride("default_color", Color.FromHtml("#31473C"));
        _history.AddThemeColorOverride("font_outline_color", new Color(0.92f, 0.96f, 0.86f, 0.82f));
        _history.AddThemeColorOverride("font_shadow_color", new Color(0.08f, 0.14f, 0.11f, 0.32f));
        _history.AddThemeConstantOverride("outline_size", 1);
        _history.AddThemeConstantOverride("shadow_offset_x", 1);
        _history.AddThemeConstantOverride("shadow_offset_y", 1);
        _history.AddThemeConstantOverride("line_separation", 1);
        AddChild(_history);

        CallDeferred(MethodName.StyleScrollbar);
        RefreshText();
    }

    public void Append(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var timestamp = DateTime.Now.ToString("HH:mm");
        _entries.Enqueue($"[{timestamp}]  {message.Trim()}");
        while (_entries.Count > MaxEntries)
            _entries.Dequeue();

        RefreshText();
    }

    private void RefreshText()
    {
        if (_history == null || !GodotObject.IsInstanceValid(_history))
            return;

        _history.Text = string.Join("\n", _entries);
        _history.ScrollToLine(Math.Max(0, _entries.Count - 1));
    }

    private void StyleScrollbar()
    {
        if (_history == null || !GodotObject.IsInstanceValid(_history))
            return;

        var scrollbar = _history.GetVScrollBar();
        scrollbar.CustomMinimumSize = new Vector2(5, 0);
        scrollbar.AddThemeStyleboxOverride("scroll", new StyleBoxEmpty());
        scrollbar.AddThemeStyleboxOverride("scroll_focus", new StyleBoxEmpty());

        var grabber = new StyleBoxFlat
        {
            BgColor = new Color(0.20f, 0.29f, 0.24f, 0.45f),
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2
        };
        scrollbar.AddThemeStyleboxOverride("grabber", grabber);
        scrollbar.AddThemeStyleboxOverride("grabber_highlight", grabber);
        scrollbar.AddThemeStyleboxOverride("grabber_pressed", grabber);
    }
}
