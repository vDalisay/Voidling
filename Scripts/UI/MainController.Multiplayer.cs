using System;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Presentation.UI.Multiplayer;

namespace VoidlingGame;

public partial class MainController
{
    private ConnectedZonePanel? _connectedZonePanel;

    private void ShowConnectedZone()
    {
        var box = OpenModal(Tr("UI_ONLINE_TITLE"), new Vector2(470, 318));
        var panel = new ConnectedZonePanel();
        panel.Configure(BuildConnectedZonePanelState(_connectedZoneBridge.Current));
        panel.CreateRequested += CreateConnectedZone;
        panel.JoinRequested += JoinConnectedZone;
        panel.InviteRequested += _connectedZoneBridge.OpenInviteOverlay;
        panel.LeaveRequested += LeaveConnectedZone;
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
        try
        {
            var result = await _connectedZoneBridge.CreateAsync();
            if (!result.Success)
            {
                ShowToast(string.Format(
                    Tr("UI_ONLINE_CREATE_FAILED"),
                    result.Error ?? "unknown error"));
            }
        }
        catch (Exception exception)
        {
            ShowToast(string.Format(Tr("UI_ONLINE_CREATE_FAILED"), exception.Message));
        }

        RefreshConnectedZonePanel();
    }

    private async void JoinConnectedZone(ulong lobbyId)
    {
        try
        {
            var result = await _connectedZoneBridge.JoinAsync(lobbyId);
            if (!result.Success)
            {
                ShowToast(string.Format(
                    Tr("UI_ONLINE_JOIN_FAILED"),
                    result.Error ?? "unknown error"));
            }
        }
        catch (Exception exception)
        {
            ShowToast(string.Format(Tr("UI_ONLINE_JOIN_FAILED"), exception.Message));
        }

        RefreshConnectedZonePanel();
    }

    private async void LeaveConnectedZone()
    {
        try
        {
            await _connectedZoneBridge.LeaveAsync();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not leave connected Garden cleanly: {exception.Message}");
        }

        RefreshConnectedZonePanel();
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
