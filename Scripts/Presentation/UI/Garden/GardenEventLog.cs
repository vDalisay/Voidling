using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>
/// Session-only garden history. The coordinator supplies already worded presentation events;
/// this component owns display, bounded history, and follow-to-latest behavior only.
/// </summary>
public partial class GardenEventLog : VBoxContainer
{
    private const int MaxEntries = 80;

    private readonly System.Collections.Generic.Queue<string> _entries = new();
    private RichTextLabel _history = null!;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 2);

        var title = UiFactory.CreateLabel(Tr("UI_GARDEN_LOG_TITLE"), 8);
        AddChild(title);

        _history = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = false,
            ScrollActive = true,
            ScrollFollowing = true,
            SelectionEnabled = true,
            CustomMinimumSize = new Vector2(356, 48),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        UiFactory.ApplyPixelFont(_history, 6);
        _history.AddThemeColorOverride("default_color", Color.FromHtml("#465247"));
        AddChild(_history);
    }

    public void Append(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var normalized = message.Trim();
        _entries.Enqueue(normalized);
        while (_entries.Count > MaxEntries)
            _entries.Dequeue();

        if (_history == null || !GodotObject.IsInstanceValid(_history))
            return;

        _history.Text = string.Join("\n", _entries.Select(entry => $"• {entry}"));
        // Godot follows the latest line when new events arrive. The scroll bar remains enabled,
        // so the player can manually scroll back through the current session between events.
        _history.ScrollToLine(Math.Max(0, _entries.Count - 1));
    }
}
