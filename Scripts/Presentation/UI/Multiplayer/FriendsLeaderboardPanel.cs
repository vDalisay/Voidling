using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

public sealed record FriendsLeaderboardPanelState(
    bool Available,
    string UnavailableReason,
    FriendsLeaderboardKind Kind,
    string ContextLabel,
    bool Loading,
    FriendsLeaderboardRow[] Rows,
    string Error);

/// <summary>
/// Friends-board view only. It renders presentation-ready rows and emits query intent; it never
/// performs Steam queries or knows about leaderboard handles/platform callbacks.
/// </summary>
public partial class FriendsLeaderboardPanel : VBoxContainer
{
    public event Action? MultiplayerWinsRequested;
    public event Action? TodayDailyRequested;
    public event Action? CourseBestRequested;

    private FriendsLeaderboardPanelState? _state;
    private bool _ready;

    public void Configure(FriendsLeaderboardPanelState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("FriendsLeaderboardPanel must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Render(FriendsLeaderboardPanelState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (_ready)
            Rebuild();
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("FriendsLeaderboardPanel must be configured before AddChild.");

        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _ready = true;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var state = _state!;
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 5);

        var wins = UiFactory.CreateButton(Tr("UI_LEADERBOARD_WINS"));
        wins.CustomMinimumSize = new Vector2(128, 25);
        wins.Disabled = !state.Available || state.Loading;
        wins.Pressed += () => MultiplayerWinsRequested?.Invoke();
        tabs.AddChild(wins);

        var daily = UiFactory.CreateButton(Tr("UI_LEADERBOARD_DAILY"));
        daily.CustomMinimumSize = new Vector2(128, 25);
        daily.Disabled = !state.Available || state.Loading;
        daily.Pressed += () => TodayDailyRequested?.Invoke();
        tabs.AddChild(daily);

        var course = UiFactory.CreateButton(Tr("UI_LEADERBOARD_COURSE"));
        course.CustomMinimumSize = new Vector2(128, 25);
        course.Disabled = !state.Available || state.Loading;
        course.Pressed += () => CourseBestRequested?.Invoke();
        tabs.AddChild(course);
        AddChild(tabs);

        if (!state.Available)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_LEADERBOARD_UNAVAILABLE"), 9));
            var reason = UiFactory.CreateLabel(state.UnavailableReason, 7);
            reason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(reason);
            return;
        }

        var heading = state.Kind switch
        {
            FriendsLeaderboardKind.DailyRace => string.Format(
                Tr("UI_LEADERBOARD_DAILY_CONTEXT"),
                state.ContextLabel),
            FriendsLeaderboardKind.CourseBestTime => string.Format(
                Tr("UI_LEADERBOARD_COURSE_CONTEXT"),
                state.ContextLabel),
            _ => Tr("UI_LEADERBOARD_WINS_CONTEXT")
        };
        AddChild(UiFactory.CreateLabel(heading, 8));

        if (state.Loading)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_LEADERBOARD_LOADING"), 8));
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.Error))
        {
            var error = UiFactory.CreateLabel(
                string.Format(Tr("UI_LEADERBOARD_ERROR"), state.Error),
                7);
            error.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(error);
            return;
        }

        if (state.Rows.Length == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_LEADERBOARD_EMPTY"), 8));
            return;
        }

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(405, 192),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var rows = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        rows.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(rows);
        AddChild(scroll);

        foreach (var rowState in state.Rows)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 5);

            var rank = UiFactory.CreateLabel($"#{rowState.Rank}", 7);
            rank.CustomMinimumSize = new Vector2(42, 18);
            row.AddChild(rank);

            var name = UiFactory.CreateLabel(rowState.DisplayName, 7);
            name.CustomMinimumSize = new Vector2(220, 18);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            row.AddChild(name);

            var score = UiFactory.CreateLabel(FormatScore(state.Kind, rowState.Score), 7);
            score.HorizontalAlignment = HorizontalAlignment.Right;
            score.CustomMinimumSize = new Vector2(105, 18);
            row.AddChild(score);
            rows.AddChild(row);
        }
    }

    private string FormatScore(FriendsLeaderboardKind kind, int score)
    {
        if (kind == FriendsLeaderboardKind.MultiplayerWins)
            return string.Format(Tr("UI_LEADERBOARD_WINS_SCORE"), score);

        if (score <= 0)
            return "—";

        var duration = TimeSpan.FromMilliseconds(score);
        return duration.TotalHours >= 1.0
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}"
            : $"{(int)duration.TotalMinutes}:{duration.Seconds:00}.{duration.Milliseconds:000}";
    }
}
