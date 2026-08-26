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
    // Product design intentionally keeps a substantial session history so players can catch up on
    // important Garden events after leaving the idle game unattended for a while.
    private const int MaxEntries = 300;

    private sealed record Entry(string Id, string Text, Action? Action);

    private readonly Queue<Entry> _entries = new();
    private RichTextLabel _history = null!;
    private int _nextActionId;

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
        _history.MetaClicked += HandleMetaClicked;
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
        => Append(message, null);

    public void AppendAction(string message, Action action)
        => Append(message, action ?? throw new ArgumentNullException(nameof(action)));

    private void Append(string message, Action? action)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var timestamp = DateTime.Now.ToString("HH:mm");
        var id = action == null ? string.Empty : $"action-{++_nextActionId}";
        _entries.Enqueue(new Entry(id, $"[{timestamp}]  {message.Trim()}", action));
        while (_entries.Count > MaxEntries)
            _entries.Dequeue();

        RefreshText();
    }

    private void RefreshText()
    {
        if (_history == null || !GodotObject.IsInstanceValid(_history))
            return;

        _history.Clear();
        foreach (var entry in _entries)
        {
            if (entry.Action == null)
            {
                _history.AddText(entry.Text);
            }
            else
            {
                _history.PushColor(Color.FromHtml("#315F85"));
                _history.PushMeta(entry.Id);
                _history.AddText(entry.Text);
                _history.Pop();
                _history.Pop();
            }
            _history.Newline();
        }
        _history.ScrollToLine(Math.Max(0, _entries.Count - 1));
    }

    private void HandleMetaClicked(Variant meta)
    {
        var id = meta.AsString();
        _entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal))?.Action?.Invoke();
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
