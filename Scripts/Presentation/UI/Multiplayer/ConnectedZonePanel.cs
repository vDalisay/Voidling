using System;
using System.Collections.Generic;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

public sealed record ConnectedZonePanelMemberState(
    string DisplayName,
    bool IsHost,
    bool IsLocal);

public sealed record ConnectedZonePanelState(
    bool Available,
    string UnavailableReason,
    bool Connected,
    string LobbyId,
    IReadOnlyList<ConnectedZonePanelMemberState> Members,
    string SelectedVoidlingName,
    bool SelectedVoidlingShared);

/// <summary>
/// Connected-Garden view only. It renders presentation-ready state and emits player intent; it has
/// no reference to Steam, networking services, GameSession, or persisted game state.
/// </summary>
public partial class ConnectedZonePanel : VBoxContainer
{
    public event Action? CreateRequested;
    public event Action<ulong>? JoinRequested;
    public event Action? InviteRequested;
    public event Action? LeaveRequested;
    public event Action? FriendsLeaderboardRequested;
    public event Action? DailyRaceRequested;
    public event Action? ShareSelectedRequested;
    public event Action? RemoveSelectedRequested;

    private ConnectedZonePanelState? _state;
    private bool _ready;

    public void Configure(ConnectedZonePanelState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("ConnectedZonePanel must be configured before it enters the scene tree.");

        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Render(ConnectedZonePanelState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (_ready)
            Rebuild();
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("ConnectedZonePanel must be configured before AddChild.");

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
        if (!state.Available)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_ONLINE_UNAVAILABLE"), 9));
            var reason = UiFactory.CreateLabel(state.UnavailableReason, 7);
            reason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(reason);
            AddChild(UiFactory.CreateLabel(Tr("UI_ONLINE_OFFLINE_SAFE"), 7));
            AddChild(BuildDailyRaceButton());
            return;
        }

        if (!state.Connected)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_ONLINE_READY"), 8));

            var create = UiFactory.CreateButton(Tr("UI_ONLINE_CREATE"));
            create.CustomMinimumSize = new Vector2(190, 25);
            create.Pressed += () => CreateRequested?.Invoke();
            AddChild(create);

            var joinRow = new HBoxContainer();
            joinRow.AddThemeConstantOverride("separation", 5);
            var lobbyId = new LineEdit
            {
                PlaceholderText = Tr("UI_ONLINE_LOBBY_ID_PLACEHOLDER"),
                CustomMinimumSize = new Vector2(215, 25),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            UiFactory.ApplyPixelFont(lobbyId, 8);
            joinRow.AddChild(lobbyId);

            var join = UiFactory.CreateButton(Tr("UI_ONLINE_JOIN"));
            join.CustomMinimumSize = new Vector2(78, 25);
            join.Pressed += () =>
            {
                if (ulong.TryParse(lobbyId.Text.Trim(), out var parsed) && parsed > 0)
                    JoinRequested?.Invoke(parsed);
            };
            joinRow.AddChild(join);
            AddChild(joinRow);

            AddChild(UiFactory.CreateLabel(Tr("UI_ONLINE_JOIN_HINT"), 6));
            var social = new HBoxContainer();
            social.AddThemeConstantOverride("separation", 6);
            social.AddChild(BuildDailyRaceButton());
            social.AddChild(BuildFriendsBoardButton());
            AddChild(social);
            return;
        }

        AddChild(UiFactory.CreateLabel(string.Format(Tr("UI_ONLINE_CONNECTED"), state.LobbyId), 8));
        AddChild(UiFactory.CreateLabel(Tr("UI_ONLINE_MEMBERS"), 8));

        foreach (var member in state.Members)
        {
            var suffix = member.IsHost
                ? Tr("UI_ONLINE_MEMBER_HOST")
                : member.IsLocal
                    ? Tr("UI_ONLINE_MEMBER_YOU")
                    : string.Empty;
            var text = suffix.Length == 0
                ? member.DisplayName
                : $"{member.DisplayName}  {suffix}";
            AddChild(UiFactory.CreateLabel(text, 7));
        }

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        var invite = UiFactory.CreateButton(Tr("UI_ONLINE_INVITE"));
        invite.CustomMinimumSize = new Vector2(105, 25);
        invite.Pressed += () => InviteRequested?.Invoke();
        actions.AddChild(invite);

        var daily = BuildDailyRaceButton();
        daily.CustomMinimumSize = new Vector2(91, 25);
        actions.AddChild(daily);

        var boards = BuildFriendsBoardButton();
        boards.CustomMinimumSize = new Vector2(105, 25);
        actions.AddChild(boards);

        var leave = UiFactory.CreateButton(Tr("UI_ONLINE_LEAVE"));
        leave.CustomMinimumSize = new Vector2(91, 25);
        leave.Pressed += () => LeaveRequested?.Invoke();
        actions.AddChild(leave);
        AddChild(actions);

        AddChild(UiFactory.CreateLabel(Tr("UI_ONLINE_SHARE_TITLE"), 8));
        if (string.IsNullOrWhiteSpace(state.SelectedVoidlingName))
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_ONLINE_SELECT_VOIDLING"), 7));
            return;
        }

        AddChild(UiFactory.CreateLabel(
            string.Format(Tr("UI_ONLINE_SELECTED_VOIDLING"), state.SelectedVoidlingName),
            7));

        var share = UiFactory.CreateButton(state.SelectedVoidlingShared
            ? Tr("UI_ONLINE_REMOVE_VOIDLING")
            : Tr("UI_ONLINE_SHARE_VOIDLING"));
        share.CustomMinimumSize = new Vector2(190, 25);
        share.Pressed += () =>
        {
            if (state.SelectedVoidlingShared)
                RemoveSelectedRequested?.Invoke();
            else
                ShareSelectedRequested?.Invoke();
        };
        AddChild(share);
    }

    private Button BuildDailyRaceButton()
    {
        var button = UiFactory.CreateButton(Tr("UI_ONLINE_DAILY_RACE"));
        button.CustomMinimumSize = new Vector2(140, 25);
        button.Pressed += () => DailyRaceRequested?.Invoke();
        return button;
    }

    private Button BuildFriendsBoardButton()
    {
        var button = UiFactory.CreateButton(Tr("UI_ONLINE_FRIEND_BOARDS"));
        button.CustomMinimumSize = new Vector2(140, 25);
        button.Pressed += () => FriendsLeaderboardRequested?.Invoke();
        return button;
    }
}
