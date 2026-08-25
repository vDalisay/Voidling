using System;
using Godot;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Steam;

/// <summary>
/// Narrow dynamic wrapper over the optional GodotSteam singleton. Keeping calls here means the
/// rest of the project compiles and runs without a GodotSteam C# binding or a running Steam client.
/// </summary>
internal sealed class GodotSteamApi
{
    private const int FriendsOnlyLobbyType = 1; // Steam ELobbyType::k_ELobbyTypeFriendsOnly.
    private const int NetworkingSendUnreliable = 0;
    private const int NetworkingSendReliable = 8;
    private const int LeaderboardSortAscending = 1;
    private const int LeaderboardSortDescending = 2;
    private const int LeaderboardDisplayNumeric = 1;
    private const int LeaderboardDisplaySeconds = 2;
    private const int LeaderboardDisplayMilliseconds = 3;
    private const int LeaderboardDataRequestFriends = 2;

    private readonly GodotObject _steam;

    public GodotSteamApi(GodotObject steam)
        => _steam = steam ?? throw new ArgumentNullException(nameof(steam));

    public GodotObject SteamObject => _steam;

    public bool TryInitialize(ulong appId, out string? failureReason)
    {
        failureReason = null;

        try
        {
            if (!_steam.HasMethod("steamInitEx"))
            {
                failureReason = "GodotSteam is present but steamInitEx is unavailable.";
                return false;
            }

            var result = appId > 0
                ? _steam.Call("steamInitEx", unchecked((long)appId))
                : _steam.Call("steamInitEx");

            if (result.VariantType != Variant.Type.Dictionary)
            {
                failureReason = "GodotSteam returned an unexpected initialization result.";
                return false;
            }

            var dictionary = result.AsGodotDictionary();
            var status = dictionary.ContainsKey("status")
                ? dictionary["status"].AsInt64()
                : 1L;
            var verbal = dictionary.ContainsKey("verbal")
                ? dictionary["verbal"].AsString()
                : "Unknown Steam initialization failure.";

            if (status != 0)
            {
                failureReason = verbal;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            failureReason = exception.Message;
            return false;
        }
    }

    public void RunCallbacks()
    {
        if (_steam.HasMethod("run_callbacks"))
            _steam.Call("run_callbacks");
        else if (_steam.HasMethod("runCallbacks"))
            _steam.Call("runCallbacks");
    }

    public ulong GetSteamId()
        => unchecked((ulong)_steam.Call("getSteamID").AsInt64());

    public string GetPersonaName()
        => _steam.Call("getPersonaName").AsString();

    public string GetFriendPersonaName(ulong steamId)
    {
        if (!_steam.HasMethod("getFriendPersonaName"))
            return steamId.ToString();

        var name = _steam.Call("getFriendPersonaName", unchecked((long)steamId)).AsString();
        return string.IsNullOrWhiteSpace(name) ? steamId.ToString() : name;
    }

    public void CreateFriendsLobby(int maxMembers)
        => _steam.Call("createLobby", FriendsOnlyLobbyType, maxMembers);

    public void JoinLobby(ulong lobbyId)
        => _steam.Call("joinLobby", unchecked((long)lobbyId));

    public void LeaveLobby(ulong lobbyId)
        => _steam.Call("leaveLobby", unchecked((long)lobbyId));

    public ulong GetLobbyOwner(ulong lobbyId)
        => unchecked((ulong)_steam.Call("getLobbyOwner", unchecked((long)lobbyId)).AsInt64());

    public int GetNumLobbyMembers(ulong lobbyId)
        => (int)_steam.Call("getNumLobbyMembers", unchecked((long)lobbyId)).AsInt64();

    public ulong GetLobbyMemberByIndex(ulong lobbyId, int index)
        => unchecked((ulong)_steam.Call(
            "getLobbyMemberByIndex",
            unchecked((long)lobbyId),
            index).AsInt64());

    public void OpenInviteOverlay(ulong lobbyId)
        => _steam.Call("activateGameOverlayInviteDialog", unchecked((long)lobbyId));

    public bool AcceptSessionWithUser(ulong steamId)
        => _steam.Call("acceptSessionWithUser", unchecked((long)steamId)).AsBool();

    public bool CloseSessionWithUser(ulong steamId)
        => _steam.Call("closeSessionWithUser", unchecked((long)steamId)).AsBool();

    public bool SendMessageToUser(
        ulong steamId,
        byte[] payload,
        bool reliable,
        int channel)
    {
        var flags = reliable ? NetworkingSendReliable : NetworkingSendUnreliable;
        var result = _steam.Call(
            "sendMessageToUser",
            unchecked((long)steamId),
            payload,
            flags,
            channel);

        // Steam EResult::k_EResultOK is 1.
        return result.AsInt64() == 1;
    }

    public Variant ReceiveMessagesOnChannel(int channel, int maxMessages)
        => _steam.Call("receiveMessagesOnChannel", channel, maxMessages);

    public bool SupportsLeaderboards
        => _steam.HasMethod("findOrCreateLeaderboard") &&
           _steam.HasMethod("uploadLeaderboardScore") &&
           _steam.HasMethod("downloadLeaderboardEntries");

    public void FindOrCreateLeaderboard(LeaderboardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _steam.Call(
            "findOrCreateLeaderboard",
            definition.Name,
            definition.SortDirection == LeaderboardSortDirection.Ascending
                ? LeaderboardSortAscending
                : LeaderboardSortDescending,
            definition.DisplayFormat switch
            {
                LeaderboardDisplayFormat.Numeric => LeaderboardDisplayNumeric,
                LeaderboardDisplayFormat.Seconds => LeaderboardDisplaySeconds,
                LeaderboardDisplayFormat.Milliseconds => LeaderboardDisplayMilliseconds,
                _ => LeaderboardDisplayNumeric
            });
    }

    public void UploadLeaderboardScore(
        ulong leaderboardHandle,
        int score,
        bool keepBest,
        int[] details)
        => _steam.Call(
            "uploadLeaderboardScore",
            score,
            keepBest,
            details ?? Array.Empty<int>(),
            unchecked((long)leaderboardHandle));

    public void DownloadFriendLeaderboardEntries(ulong leaderboardHandle)
        => _steam.Call(
            "downloadLeaderboardEntries",
            0,
            0,
            LeaderboardDataRequestFriends,
            unchecked((long)leaderboardHandle));
}