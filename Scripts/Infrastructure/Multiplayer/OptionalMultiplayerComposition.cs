using System;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Infrastructure.Steam;

namespace Voidling.Infrastructure.Multiplayer;

public sealed record OptionalMultiplayerComposition(
    MultiplayerConnectionService Connection,
    ConnectedZoneService ConnectedZone,
    ChallengeCoordinator Challenges,
    ILeaderboardService Leaderboards,
    Node? RuntimeNode,
    bool SteamAvailable,
    string? UnavailableReason);

/// <summary>
/// Selects Steam adapters when available and offline adapters otherwise. Failure to initialize
/// Steam is a capability loss only; it is never a game-startup failure.
/// </summary>
public static class OptionalMultiplayerComposer
{
    public static OptionalMultiplayerComposition Create()
    {
        if (!Engine.HasSingleton("Steam"))
            return CreateOffline("GodotSteam is not installed or not loaded. Single-player remains available.");

        try
        {
            var steamObject = Engine.GetSingleton("Steam");
            if (steamObject == null)
                return CreateOffline("GodotSteam singleton could not be resolved. Single-player remains available.");

            var api = new GodotSteamApi(steamObject);
            var appId = ResolveSteamAppId();
            if (!api.TryInitialize(appId, out var failureReason))
            {
                return CreateOffline(
                    $"Steam initialization failed: {failureReason ?? "unknown error"}. Single-player remains available.");
            }

            var runtime = new GodotSteamRuntime
            {
                Name = nameof(GodotSteamRuntime)
            };
            runtime.Configure(api);

            IPlatformIdentityService identity = new SteamPlatformIdentityService(api);
            ILobbyService lobbies = new SteamLobbyService(api, runtime, identity);
            IMultiplayerTransport transport = new SteamNetworkingMessagesTransport(api, runtime, lobbies);
            ILeaderboardService leaderboards = new SteamLeaderboardService(api, runtime);
            var connection = new MultiplayerConnectionService(identity, lobbies, transport);
            var connectedZone = new ConnectedZoneService(connection);
            var challenges = new ChallengeCoordinator(connection);
            runtime.SetPollAction(connection.Poll);

            if (!connection.IsAvailable)
                return CreateOffline(connection.UnavailableReason ?? "Steam multiplayer is unavailable.");

            return new OptionalMultiplayerComposition(
                connection,
                connectedZone,
                challenges,
                leaderboards,
                runtime,
                true,
                null);
        }
        catch (Exception exception)
        {
            return CreateOffline($"Steam multiplayer setup failed: {exception.Message}. Single-player remains available.");
        }
    }

    private static OptionalMultiplayerComposition CreateOffline(string reason)
    {
        var identity = new OfflinePlatformIdentityService(reason);
        var lobbies = new OfflineLobbyService(reason);
        var transport = new OfflineMultiplayerTransport(reason);
        var leaderboards = new OfflineLeaderboardService(reason);
        var connection = new MultiplayerConnectionService(identity, lobbies, transport);
        var connectedZone = new ConnectedZoneService(connection);
        var challenges = new ChallengeCoordinator(connection);
        return new OptionalMultiplayerComposition(
            connection,
            connectedZone,
            challenges,
            leaderboards,
            null,
            false,
            reason);
    }

    private static ulong ResolveSteamAppId()
    {
        if (ProjectSettings.HasSetting("steam/initialization/app_id"))
        {
            var configured = ProjectSettings.GetSetting("steam/initialization/app_id").AsInt64();
            if (configured > 0)
                return unchecked((ulong)configured);
        }

        foreach (var environmentName in new[] { "SteamAppId", "SteamGameId" })
        {
            var raw = OS.GetEnvironment(environmentName);
            if (ulong.TryParse(raw, out var parsed) && parsed > 0)
                return parsed;
        }

        // GodotSteam can still resolve an app ID from steam_appid.txt. Passing zero causes the
        // dynamic adapter to call steamInitEx() without an explicit app ID.
        return 0;
    }
}