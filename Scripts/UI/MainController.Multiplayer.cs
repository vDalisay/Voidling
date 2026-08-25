using System;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Presentation.UI.Multiplayer;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class MainController
{
    private ConnectedZonePanel? _connectedZonePanel;
    private ConnectedZoneGardenSync? _connectedZoneGardenSync;

    private void ComposeConnectedZoneGardenPresentation()
    {
        if (_connectedZoneGardenSync != null && GodotObject.IsInstanceValid(_connectedZoneGardenSync))
            return;

        var sync = new ConnectedZoneGardenSync
        {
            Name = nameof(ConnectedZoneGardenSync),
            ZIndex = 1
        };
        sync.Configure(_connectedZoneBridge, _garden);
        _connectedZoneGardenSync = sync;
        _garden.AddChild(sync);
    }

    private void ShowConnectedZone()
    {
        var box = OpenModal(Tr("UI_ONLINE_TITLE"), new Vector2(470, 318));
        var panel = new ConnectedZonePanel();
        panel.Configure(BuildConnectedZonePanelState(_connectedZoneBridge.Current));
        panel.CreateRequested += CreateConnectedZone;
        panel.JoinRequested += JoinConnectedZone;
        panel.InviteRequested += _connectedZoneBridge.OpenInviteOverlay;
        panel.LeaveRequested += LeaveConnectedZone;
        panel.FriendsLeaderboardRequested += ShowFriendsLeaderboards;
        panel.DailyRaceRequested += ShowDailyRace;
        panel.ChallengesRequested += ShowChallenges;
        panel.ShareSelectedRequested += ShareSelectedVoidling;
        panel.RemoveSelectedRequested += RemoveSelectedSharedVoidling;
        _connectedZonePanel = panel;
        box.AddChild(panel);
    }

    private ConnectedZonePanelState BuildConnectedZonePanelState(ConnectedZoneViewState state)
    {
        var selected = _session.FindVoidling(_selectedId);
        var selectedShared = selected != null &&
            state.LocalUser != null &&
            state.Voidlings.Any(shared =>
                shared.OwnerId == state.LocalUser.Id &&
                string.Equals(shared.CreatureId, selected.Id, StringComparison.Ordinal));

        var members = state.Members
            .Select(member => new ConnectedZonePanelMemberState(
                member.DisplayName,
                member.IsHost,
                member.IsLocal))
            .ToArray();

        return new ConnectedZonePanelState(
            state.Availability.IsAvailable,
            state.Availability.Reason ?? string.Empty,
            state.IsConnected,
            state.LobbyId?.ToString() ?? string.Empty,
            members,
            selected?.Name ?? string.Empty,
            selectedShared);
    }

    private void OnConnectedZoneStateChanged(ConnectedZoneViewState state)
    {
        if (_connectedZonePanel == null || !GodotObject.IsInstanceValid(_connectedZonePanel))
            return;

        _connectedZonePanel.Render(BuildConnectedZonePanelState(state));
    }

    private void RefreshConnectedZonePanel()
    {
        if (_connectedZonePanel == null || !GodotObject.IsInstanceValid(_connectedZonePanel))
            return;

        _connectedZonePanel.Render(BuildConnectedZonePanelState(_connectedZoneBridge.Current));
    }

    private async void CreateConnectedZone()
    {
        string? failure = null;
        try
        {
            var result = await _connectedZoneBridge.CreateAsync();
            if (!result.Success)
                failure = result.Error ?? "unknown error";
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }

        var capturedFailure = failure;
        Callable.From(() =>
        {
            if (!string.IsNullOrWhiteSpace(capturedFailure))
                ShowToast(string.Format(Tr("UI_ONLINE_CREATE_FAILED"), capturedFailure));
            RefreshConnectedZonePanel();
        }).CallDeferred();
    }

    private async void JoinConnectedZone(ulong lobbyId)
    {
        string? failure = null;
        try
        {
            var result = await _connectedZoneBridge.JoinAsync(lobbyId);
            if (!result.Success)
                failure = result.Error ?? "unknown error";
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }

        var capturedFailure = failure;
        Callable.From(() =>
        {
            if (!string.IsNullOrWhiteSpace(capturedFailure))
                ShowToast(string.Format(Tr("UI_ONLINE_JOIN_FAILED"), capturedFailure));
            RefreshConnectedZonePanel();
        }).CallDeferred();
    }

    private async void LeaveConnectedZone()
    {
        string? failure = null;
        try
        {
            await _connectedZoneBridge.LeaveAsync();
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }

        var capturedFailure = failure;
        Callable.From(() =>
        {
            if (!string.IsNullOrWhiteSpace(capturedFailure))
                GD.PushWarning($"Could not leave connected Garden cleanly: {capturedFailure}");
            RefreshConnectedZonePanel();
        }).CallDeferred();
    }

    private void ShareSelectedVoidling()
    {
        var selected = _session.FindVoidling(_selectedId);
        if (selected == null)
        {
            RefreshConnectedZonePanel();
            return;
        }

        var position = new Vector2(selected.WorldX, selected.WorldY);
        if (_garden.TryGetActorPosition(selected.Id, out var livePosition))
            position = livePosition;

        var result = _connectedZoneBridge.PublishVoidling(
            _session.State,
            selected.Id,
            position.X,
            position.Y);
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_ONLINE_SHARE_FAILED"),
                result.Error ?? "unknown error"));
        }

        RefreshConnectedZonePanel();
    }

    private void RemoveSelectedSharedVoidling()
    {
        if (string.IsNullOrWhiteSpace(_selectedId))
            return;

        var result = _connectedZoneBridge.RemoveVoidling(_selectedId);
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_ONLINE_REMOVE_FAILED"),
                result.Error ?? "unknown error"));
        }

        RefreshConnectedZonePanel();
    }
}
