using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>
/// Session-only garden journal with timestamped, scrollable activity and actionable invitations.
/// </summary>
public partial class GardenEventLog : Control
{
    public event Action? ActivitiesRequested;
    private const int MaxEntries = 300;
    private const float ExpandedHeight = 80;
    private const float CompactHeight = 45;

    private sealed record Entry(string Id, string Text, Action? Action);

    private readonly Queue<Entry> _entries = new();
    private RichTextLabel _history = null!;
    private Button _heightToggle = null!;
    private Tween? _heightTween;
    private float _bottom;
    private int _nextActionId;
    public bool IsCompact { get; private set; }

    public override void _Ready()
    {
        _bottom = Position.Y + Size.Y;
        MouseFilter = MouseFilterEnum.Pass;

        var panel = UiFactory.CreatePanel(Vector2.Zero);
        var background = (StyleBoxTexture)panel.GetThemeStylebox("panel").Duplicate();
        background.ModulateColor = new Color(1, 1, 1, 0.5f);
        background.ContentMarginTop = background.ContentMarginBottom = 6;
        background.ContentMarginLeft = background.ContentMarginRight = 9;
        panel.AddThemeStyleboxOverride("panel", background);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        panel.AddChild(column);
        var heading = new HBoxContainer();
        _heightToggle = UiFactory.CreateButton("−");
        _heightToggle.Name = "ToggleHeight";
        _heightToggle.CustomMinimumSize = new Vector2(18, 18);
        _heightToggle.TooltipText = Tr("UI_GARDEN_LOG_COLLAPSE");
        _heightToggle.Pressed += ToggleHeight;
        UiFactory.ApplyPixelFont(_heightToggle, 8);
        heading.AddChild(_heightToggle);
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var activities = UiFactory.CreateButton(Tr("UI_GARDEN_ACTIVITIES"));
        activities.Name = "Activities";
        activities.CustomMinimumSize = new Vector2(62, 18);
        UiFactory.ApplyPixelFont(activities, 8);
        activities.Pressed += () => ActivitiesRequested?.Invoke();
        heading.AddChild(activities);
        column.AddChild(heading);
        _history = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = false,
            ScrollActive = true,
            ScrollFollowing = true,
            SelectionEnabled = true,
            CustomMinimumSize = new Vector2(0, 24),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        _history.MetaClicked += HandleMetaClicked;
        UiFactory.ApplyPixelFont(_history, 6);

        _history.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _history.AddThemeColorOverride("default_color", Color.FromHtml("#31473C"));
        _history.AddThemeConstantOverride("line_separation", 3);
        column.AddChild(_history);

        CallDeferred(MethodName.StyleScrollbar);
        RefreshText();
    }

    private void ToggleHeight()
    {
        IsCompact = !IsCompact;
        _heightToggle.Text = IsCompact ? "+" : "−";
        _heightToggle.TooltipText = Tr(IsCompact ? "UI_GARDEN_LOG_EXPAND" : "UI_GARDEN_LOG_COLLAPSE");
        _heightToggle.GrabFocus();
        _history.ScrollActive = !IsCompact;
        RefreshText();
        _heightTween?.Kill();
        var height = IsCompact ? CompactHeight : ExpandedHeight;
        _heightTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _heightTween.TweenProperty(this, "position:y", _bottom - height, 0.18);
        _heightTween.TweenProperty(this, "size:y", height, 0.18);
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
        foreach (var entry in IsCompact ? _entries.TakeLast(1) : _entries)
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
