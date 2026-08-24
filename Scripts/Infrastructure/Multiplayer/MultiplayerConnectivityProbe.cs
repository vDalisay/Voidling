using System;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Infrastructure.Multiplayer;

/// <summary>
/// Command-line-only integration probe for testing real Steam accounts before multiplayer UI exists.
/// It is never created during ordinary launches or CI unless an explicit probe flag is used.
/// </summary>
public partial class MultiplayerConnectivityProbe : Node
{
    private MultiplayerConnectionService? _connection;
    private ConnectedZoneService? _connectedZone;
    private GameSession? _session;
    private string[] _args = Array.Empty<string>();

    public void Configure(
        MultiplayerConnectionService connection,
        ConnectedZoneService connectedZone,
        GameSession session,
        string[] args)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("Connectivity probe must be configured before entering the scene tree.");

        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connectedZone = connectedZone ?? throw new ArgumentNullException(nameof(connectedZone));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _args = args ?? Array.Empty<string>();
    }

    public override async void _Ready()
    {
        if (_connection == null || _connectedZone == null || _session == null)
            throw new InvalidOperationException("Connectivity probe must be configured by GameBootstrap.");

        _connection.PeerHelloReceived += user =>
            GD.Print($"[multiplayer-probe] hello from {user.DisplayName} ({user.Id.Value})");
        _connection.LobbyChanged += lobby =>
            GD.Print($"[multiplayer-probe] lobby {lobby.LobbyId}, owner {lobby.OwnerId.Value}, members {lobby.Members.Count}");
        _connection.JoinRequested += request => _ = JoinFromInviteAsync(request);
        _connectedZone.StateChanged += LogConnectedZoneState;
        _connectedZone.ProtocolRejected += reason =>
            GD.Print($"[multiplayer-probe] rejected zone packet: {reason}");

        if (!_connection.IsAvailable)
        {
            GD.Print($"[multiplayer-probe] unavailable: {_connection.UnavailableReason}");
            return;
        }

        var joinId = ReadJoinLobbyId(_args);
        if (joinId > 0)
        {
            var joined = await _connection.JoinConnectedZoneAsync(joinId);
            LogResult("join", joined);
            if (joined.Success)
            {
                _connection.SendHelloToLobbyMembers();
                TryPublishFirstVoidling();
            }
            return;
        }

        if (!HasFlag(_args, "--voidling-mp-host"))
            return;

        var created = await _connection.CreateConnectedZoneAsync();
        LogResult("host", created);
        if (!created.Success)
            return;

        GD.Print($"[multiplayer-probe] share lobby id {created.Lobby!.LobbyId} with the second account or use Steam invite.");
        TryPublishFirstVoidling();
        if (HasFlag(_args, "--voidling-mp-invite"))
            _connection.OpenInviteOverlay();
    }

    private async Task JoinFromInviteAsync(LobbyJoinRequest request)
    {
        if (_connection == null || _connection.CurrentLobby?.LobbyId == request.LobbyId)
            return;

        var joined = await _connection.JoinConnectedZoneAsync(request.LobbyId);
        LogResult("invite join", joined);
        if (joined.Success)
        {
            _connection.SendHelloToLobbyMembers();
            TryPublishFirstVoidling();
        }
    }

    private void TryPublishFirstVoidling()
    {
        if (_connectedZone == null ||
            _session == null ||
            !HasFlag(_args, "--voidling-mp-publish-first") ||
            _session.State.Voidlings.Count == 0)
        {
            return;
        }

        var creature = _session.State.Voidlings[0];
        var result = _connectedZone.PublishOwnedVoidling(
            _session.State,
            creature.Id,
            zoneX: 0,
            zoneY: 0);

        GD.Print(result.Success
            ? $"[multiplayer-probe] published {creature.Name} ({creature.Id}) into the connected zone"
            : $"[multiplayer-probe] publish failed: {result.Error}");
    }

    private static void LogConnectedZoneState(ConnectedZoneSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            GD.Print("[multiplayer-probe] connected zone cleared");
            return;
        }

        GD.Print(
            $"[multiplayer-probe] zone lobby {snapshot.LobbyId}, host {snapshot.HostId.Value}, " +
            $"epoch {snapshot.AuthorityEpoch}, revision {snapshot.Revision}, shared Voidlings {snapshot.Voidlings.Length}");

        foreach (var voidling in snapshot.Voidlings)
        {
            GD.Print(
                $"[multiplayer-probe] shared {voidling.DisplayName} ({voidling.CreatureId}) " +
                $"owner {voidling.OwnerId.Value} at {voidling.ZoneX},{voidling.ZoneY}");
        }
    }

    private static void LogResult(string operation, LobbyOperationResult result)
    {
        if (result.Success)
            GD.Print($"[multiplayer-probe] {operation} succeeded for lobby {result.Lobby!.LobbyId}");
        else
            GD.Print($"[multiplayer-probe] {operation} failed: {result.Error}");
    }

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static ulong ReadJoinLobbyId(string[] args)
    {
        const string prefix = "--voidling-mp-join=";
        foreach (var arg in args)
        {
            if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            return ulong.TryParse(arg[prefix.Length..], out var lobbyId) ? lobbyId : 0;
        }

        return 0;
    }
}
