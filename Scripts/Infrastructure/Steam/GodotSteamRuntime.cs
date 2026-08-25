using System;
using Godot;
using GodotArray = Godot.Collections.Array;

namespace Voidling.Infrastructure.Steam;

/// <summary>
/// Godot-owned callback pump for the optional Steam integration. It contains no gameplay state.
/// If Steam is unavailable this node is never created; single-player continues normally.
/// </summary>
public partial class GodotSteamRuntime : Node
{
    private GodotSteamApi? _api;
    private Action? _pollAction;

    internal event Action<long, long>? LobbyCreated;
    internal event Action<long, long, bool, long>? LobbyJoined;
    internal event Action<long, long>? JoinRequested;
    internal event Action? LobbyMembershipChanged;
    internal event Action<long>? NetworkingMessagesSessionRequested;
    internal event Action<long, bool>? LeaderboardFound;
    internal event Action<bool, long, int, bool, int, int>? LeaderboardScoreUploaded;
    internal event Action<string, long, GodotArray>? LeaderboardScoresDownloaded;

    internal void Configure(GodotSteamApi api, Action? pollAction = null)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("GodotSteamRuntime must be configured before entering the scene tree.");

        _api = api ?? throw new ArgumentNullException(nameof(api));
        _pollAction = pollAction;
    }

    internal void SetPollAction(Action pollAction)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("GodotSteamRuntime polling must be configured before entering the scene tree.");

        _pollAction = pollAction ?? throw new ArgumentNullException(nameof(pollAction));
    }

    public override void _Ready()
    {
        if (_api == null)
            throw new InvalidOperationException("GodotSteamRuntime must be configured by GameBootstrap.");

        ConnectIfPresent("lobby_created", Callable.From<Variant, Variant>(OnLobbyCreated));
        ConnectIfPresent(
            "lobby_joined",
            Callable.From<Variant, Variant, Variant, Variant>(OnLobbyJoined));
        ConnectIfPresent("join_requested", Callable.From<Variant, Variant>(OnJoinRequested));
        ConnectIfPresent(
            "lobby_chat_update",
            Callable.From<Variant, Variant, Variant, Variant>(OnLobbyChatUpdate));
        ConnectIfPresent(
            "network_messages_session_request",
            Callable.From<Variant>(OnNetworkingMessagesSessionRequest));
        ConnectIfPresent(
            "leaderboard_find_result",
            Callable.From<Variant, Variant>(OnLeaderboardFindResult));
        ConnectIfPresent(
            "leaderboard_score_uploaded",
            Callable.From<Variant, Variant, Variant, Variant, Variant, Variant>(OnLeaderboardScoreUploaded));
        ConnectIfPresent(
            "leaderboard_scores_downloaded",
            Callable.From<Variant, Variant, Variant>(OnLeaderboardScoresDownloaded));

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _api?.RunCallbacks();
        _pollAction?.Invoke();
    }

    private void ConnectIfPresent(string signalName, Callable callable)
    {
        var steam = _api!.SteamObject;
        if (!steam.HasSignal(signalName))
            return;

        var signal = new StringName(signalName);
        if (!steam.IsConnected(signal, callable))
            steam.Connect(signal, callable);
    }

    private void OnLobbyCreated(Variant result, Variant lobbyId)
        => LobbyCreated?.Invoke(result.AsInt64(), lobbyId.AsInt64());

    private void OnLobbyJoined(Variant lobbyId, Variant permissions, Variant locked, Variant response)
        => LobbyJoined?.Invoke(
            lobbyId.AsInt64(),
            permissions.AsInt64(),
            locked.AsBool(),
            response.AsInt64());

    private void OnJoinRequested(Variant lobbyId, Variant friendId)
        => JoinRequested?.Invoke(lobbyId.AsInt64(), friendId.AsInt64());

    private void OnLobbyChatUpdate(
        Variant lobbyId,
        Variant changedUser,
        Variant makingChangeUser,
        Variant memberStateChange)
        => LobbyMembershipChanged?.Invoke();

    private void OnNetworkingMessagesSessionRequest(Variant remoteSteamId)
        => NetworkingMessagesSessionRequested?.Invoke(remoteSteamId.AsInt64());

    private void OnLeaderboardFindResult(Variant leaderboardHandle, Variant found)
        => LeaderboardFound?.Invoke(leaderboardHandle.AsInt64(), found.AsBool() || found.AsInt64() != 0);

    private void OnLeaderboardScoreUploaded(
        Variant success,
        Variant leaderboardHandle,
        Variant score,
        Variant scoreChanged,
        Variant globalRankNew,
        Variant globalRankPrevious)
        => LeaderboardScoreUploaded?.Invoke(
            success.AsBool() || success.AsInt64() != 0,
            leaderboardHandle.AsInt64(),
            (int)score.AsInt64(),
            scoreChanged.AsBool() || scoreChanged.AsInt64() != 0,
            (int)globalRankNew.AsInt64(),
            (int)globalRankPrevious.AsInt64());

    private void OnLeaderboardScoresDownloaded(
        Variant message,
        Variant leaderboardHandle,
        Variant entries)
    {
        var array = entries.VariantType == Variant.Type.Array
            ? entries.AsGodotArray()
            : new GodotArray();
        LeaderboardScoresDownloaded?.Invoke(
            message.AsString(),
            leaderboardHandle.AsInt64(),
            array);
    }
}
