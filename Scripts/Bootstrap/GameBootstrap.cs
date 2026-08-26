using System;
using System.Linq;
using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Creatures;
using Voidling.Application.Multiplayer;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Application.Persistence;
using Voidling.Application.Racing;
using Voidling.Application.Roster;
using Voidling.Application.Settings;
using Voidling.Application.Shop;
using Voidling.Application.Simulation;
using Voidling.Application.Training;
using Voidling.Domain.Rules;
using Voidling.Infrastructure.Audio;
using Voidling.Infrastructure.Multiplayer;
using Voidling.Infrastructure.Persistence;
using Voidling.Infrastructure.Resources;
using Voidling.Presentation.UI.Multiplayer;
using VoidlingGame;

namespace Voidling.Bootstrap;

/// <summary>
/// Application composition root. This is the one place that intentionally knows concrete
/// infrastructure implementations and the Godot-owned lifetime of the game session.
/// </summary>
public partial class GameBootstrap : Node
{
    private const string SavePath = "user://voidling_mvp_save.json";
    private const string BalancePath = "res://Resources/Balance/demo_balance.tres";

    // Kept private: Bootstrap owns lifetime/composition, but it is not a service locator.
    // Future multiplayer presentation should receive these dependencies explicitly when composed.
    private MultiplayerConnectionService? _multiplayerConnection;
    private ConnectedZoneService? _connectedZone;
    private ChallengeCoordinator? _challengeCoordinator;
    private MultiplayerRaceStartCoordinator? _multiplayerRaceStarts;
    private MultiplayerRaceLockstepCoordinator? _multiplayerRaceLockstep;
    private MultiplayerRaceResultCoordinator? _multiplayerRaceResults;
    private TradeNetworkCoordinator? _tradeCoordinator;
    private LeaderboardProjectionService? _leaderboardProjection;
    private DailyFriendRaceCoordinator? _dailyFriendRace;

    public override void _Ready()
    {
        var rules = LoadBalanceRules();

        // Transitional presentation code still reads the GameRules facade. Configure it with
        // the exact same immutable rules used below so there is only one effective ruleset.
        GameRules.Configure(rules);

        ComposeOptionalMultiplayer(rules);

        var stateRepository = new GodotJsonGameStateRepository(SavePath);
        var session = new GameSession
        {
            Name = nameof(GameSession)
        };

        session.Configure(
            stateRepository,
            new GodotAudioSettingsAdapter(),
            new GameStateMigrationService(rules),
            new AdvanceSimulationUseCase(rules),
            new TrainingUseCase(rules),
            new BreedVoidlingsUseCase(rules),
            new ShopUseCase(rules),
            new SettingsUseCase(),
            new VoidlingRosterUseCase(),
            new RaceResultUseCase(rules),
            new LineageTreeProjectionService(rules));
        session.ConfigureRacing(new RaceEntryFactory(rules));
        session.ConfigureReadModels(new VoidlingProfileProjectionService(rules));

        AddChild(session);
        ComposeTrading(rules, stateRepository, session);
        ComposeMultiplayerRaceResults(rules, stateRepository, session);
        ComposeDailyFriendRace(rules, stateRepository, session);

        // Steam leaderboards are only a social projection of already-persisted local progress.
        // Retrying the total on every Steam-capable startup makes transient upload failures harmless
        // without adding another persisted "last uploaded" field or making Steam part of save loading.
        if (session.State.MultiplayerWins > 0)
            ProjectMultiplayerWins(session.State.MultiplayerWins, "startup retry");

        ComposeMultiplayerProbeIfRequested(session);
    }

    private void ComposeOptionalMultiplayer(GameBalanceRules rules)
    {
        var multiplayer = OptionalMultiplayerComposer.Create();
        _multiplayerConnection = multiplayer.Connection;
        _connectedZone = multiplayer.ConnectedZone;
        _challengeCoordinator = multiplayer.Challenges;
        _leaderboardProjection = new LeaderboardProjectionService(multiplayer.Leaderboards);

        var connectedZoneBridge = new ConnectedZonePresentationBridge
        {
            Name = nameof(ConnectedZonePresentationBridge)
        };
        connectedZoneBridge.Configure(multiplayer.ConnectedZoneFacade);
        AddChild(connectedZoneBridge);

        var friendsLeaderboardBridge = new FriendsLeaderboardPresentationBridge
        {
            Name = nameof(FriendsLeaderboardPresentationBridge)
        };
        friendsLeaderboardBridge.Configure(_leaderboardProjection);
        AddChild(friendsLeaderboardBridge);

        var challengeBridge = new ChallengePresentationBridge
        {
            Name = nameof(ChallengePresentationBridge)
        };
        challengeBridge.Configure(new ChallengeFacade(multiplayer.Connection, multiplayer.Challenges));
        AddChild(challengeBridge);

        multiplayer.ConnectedZoneTransient.ProtocolRejected += reason =>
            GD.PushWarning($"Rejected connected-Garden transient packet: {reason}");
        _challengeCoordinator.ProtocolRejected += reason =>
            GD.PushWarning($"Rejected multiplayer challenge packet: {reason}");

        _multiplayerRaceStarts = new MultiplayerRaceStartCoordinator(
            _multiplayerConnection,
            _challengeCoordinator,
            rules);
        _multiplayerRaceStarts.ProtocolRejected += reason =>
            GD.PushWarning($"Rejected multiplayer race start packet: {reason}");
        _multiplayerRaceStarts.RacePreparationFailed += (challengeId, reason) =>
            GD.PushWarning($"Multiplayer race {challengeId} preparation failed: {reason}");

        _multiplayerRaceLockstep = new MultiplayerRaceLockstepCoordinator(
            _multiplayerConnection,
            _challengeCoordinator);
        _multiplayerRaceLockstep.ProtocolRejected += reason =>
            GD.PushWarning($"Rejected multiplayer race lockstep packet: {reason}");
        _multiplayerRaceLockstep.SyncIssue += (challengeId, reason) =>
            GD.PushWarning($"Multiplayer race {challengeId} sync issue: {reason}");
        _multiplayerRaceLockstep.DesyncDetected += desync =>
            GD.PushWarning(
                $"Multiplayer race {desync.ChallengeId} desync at tick {desync.Tick} with peer " +
                $"{desync.PeerId.Value}: host={desync.HostChecksum}, peer={desync.PeerChecksum}");
        _multiplayerRaceStarts.RaceReadyToLaunch += race =>
        {
            if (!_multiplayerRaceLockstep.AttachRace(race, out var error))
            {
                GD.PushWarning(
                    $"Multiplayer race {race.Start.ChallengeId} could not attach lockstep: " +
                    (error ?? "unknown error"));
            }
        };

        if (multiplayer.RuntimeNode != null)
        {
            AddChild(multiplayer.RuntimeNode);
            if (multiplayer.SteamAvailable)
            {
                GD.Print($"Steam multiplayer available for {_multiplayerConnection.LocalUser?.DisplayName ?? "local user"}.");
            }
            else if (multiplayer.RuntimeNode is LanMultiplayerRuntime)
            {
                GD.Print("Development LAN multiplayer transport enabled. Steam is intentionally bypassed for this launch.");
            }
            return;
        }

        // This is informational, never fatal. Single-player must remain fully functional offline.
        GD.Print(multiplayer.UnavailableReason ?? "Steam multiplayer unavailable; continuing in single-player mode.");
    }

    private void ComposeTrading(
        GameBalanceRules rules,
        GodotJsonGameStateRepository stateRepository,
        GameSession session)
    {
        if (_multiplayerConnection == null)
            return;

        var transfers = new TradeTransferService(rules);
        RecoverInterruptedTradePrepares(transfers, stateRepository, session);

        _tradeCoordinator = new TradeNetworkCoordinator(
            _multiplayerConnection,
            transfers,
            stateRepository,
            () => session.State);
        _tradeCoordinator.LocalStateChanged += session.NotifyExternallyPersistedStateChanged;
        _tradeCoordinator.ProtocolRejected += reason =>
            GD.PushWarning($"Rejected multiplayer trade packet: {reason}");

        // The negotiation coordinator is deliberately separate from the durable coordinator. It
        // synchronizes invite/selection/confirmation only, then hands the two agreed Voidlings to
        // the existing journaled two-phase transfer implementation.
        var negotiationCoordinator = new TradeNegotiationCoordinator(
            _multiplayerConnection,
            _tradeCoordinator);
        negotiationCoordinator.ProtocolRejected += reason =>
            GD.PushWarning($"Rejected multiplayer trade negotiation packet: {reason}");

        var durableFacade = new TradeFacade(
            _multiplayerConnection,
            _tradeCoordinator,
            () => session.State);
        var negotiationFacade = new TradeNegotiationFacade(
            _multiplayerConnection,
            negotiationCoordinator,
            () => session.State);
        var tradeBridge = new TradePresentationBridge
        {
            Name = nameof(TradePresentationBridge)
        };
        tradeBridge.Configure(durableFacade, negotiationFacade);
        AddChild(tradeBridge);
    }

    private void ComposeMultiplayerRaceResults(
        GameBalanceRules rules,
        GodotJsonGameStateRepository stateRepository,
        GameSession session)
    {
        if (_multiplayerConnection == null ||
            _challengeCoordinator == null ||
            _multiplayerRaceStarts == null ||
            _multiplayerRaceLockstep == null)
        {
            return;
        }

        var raceFacade = new MultiplayerRaceFacade(
            _multiplayerConnection,
            _challengeCoordinator,
            _multiplayerRaceStarts,
            _multiplayerRaceLockstep,
            () => session.State);
        var raceBridge = new MultiplayerRacePresentationBridge
        {
            Name = nameof(MultiplayerRacePresentationBridge)
        };
        raceBridge.Configure(raceFacade);
        AddChild(raceBridge);

        var rewards = new MultiplayerRaceResultUseCase(rules);
        _multiplayerRaceResults = new MultiplayerRaceResultCoordinator(
            _multiplayerConnection,
            _challengeCoordinator,
            _multiplayerRaceLockstep);
        _multiplayerRaceResults.ProtocolRejected += reason =>
            GD.PushWarning($"Rejected multiplayer race result packet: {reason}");
        _multiplayerRaceResults.ResultHandshakeIssue += (challengeId, reason) =>
            GD.PushWarning($"Multiplayer race {challengeId} result handshake issue: {reason}");
        _multiplayerRaceResults.ChecksumMismatch += mismatch =>
            GD.PushWarning(
                $"Multiplayer race {mismatch.ChallengeId} final checksum mismatch: " +
                $"host tick/checksum={mismatch.HostTick}/{mismatch.HostChecksum}, " +
                $"local={mismatch.LocalTick}/{mismatch.LocalChecksum}");

        // This handler is registered after the lockstep attachment handler above, so the exact race
        // session always exists before result coordination attaches to the same resolved start data.
        _multiplayerRaceStarts.RaceReadyToLaunch += race =>
        {
            if (!_multiplayerRaceResults.AttachRace(race, out var error))
            {
                GD.PushWarning(
                    $"Multiplayer race {race.Start.ChallengeId} could not attach result coordination: " +
                    (error ?? "unknown error"));
            }
        };

        _multiplayerRaceResults.ValidatedResultReady += result =>
        {
            var local = _multiplayerConnection.LocalUser;
            if (local == null)
            {
                GD.PushWarning($"Could not apply multiplayer race {result.ChallengeId}: local identity is unavailable.");
                return;
            }

            // Keep a small rollback snapshot. If persistence fails after the use case mutates the
            // in-memory aggregate, restore it so a later duplicate result can retry instead of being
            // suppressed by an applied-ID that never reached disk.
            var previousCoins = session.State.Coins;
            var previousWins = session.State.MultiplayerWins;
            var previousAppliedRaceIds = session.State.AppliedMultiplayerRaceIds.ToList();
            var applied = rewards.Apply(session.State, local.Id, result);
            if (!applied.Success)
            {
                GD.PushWarning(
                    $"Could not apply multiplayer race {result.ChallengeId}: " +
                    (applied.Error ?? "unknown result validation failure"));
                return;
            }
            if (applied.AlreadyApplied)
                return;

            try
            {
                stateRepository.Save(session.State);
                session.NotifyExternallyPersistedStateChanged();
                GD.Print(
                    $"Applied multiplayer race {result.ChallengeId}: place {applied.Place}, " +
                    $"reward {applied.CoinReward}, multiplayer wins {applied.MultiplayerWins}.");

                // Projection happens strictly after the local save. If Steam is unavailable or the
                // callback fails, the persisted total remains intact and will be retried next startup/win.
                ProjectMultiplayerWins(applied.MultiplayerWins, $"race {result.ChallengeId}");
            }
            catch (Exception exception)
            {
                session.State.Coins = previousCoins;
                session.State.MultiplayerWins = previousWins;
                session.State.AppliedMultiplayerRaceIds = previousAppliedRaceIds;
                GD.PushWarning(
                    $"Could not persist multiplayer race {result.ChallengeId}; local reward was rolled back: " +
                    exception.Message);
            }
        };
    }

    private void ComposeDailyFriendRace(
        GameBalanceRules rules,
        GodotJsonGameStateRepository stateRepository,
        GameSession session)
    {
        if (_leaderboardProjection == null)
            return;

        _dailyFriendRace = new DailyFriendRaceCoordinator(
            new DailyFriendRaceService(new RaceEntryFactory(rules)),
            stateRepository,
            _leaderboardProjection);

        var dailyRaceFacade = new DailyFriendRaceFacade(
            _dailyFriendRace,
            () => session.State,
            session.NotifyExternallyPersistedStateChanged);
        var dailyRaceBridge = new DailyFriendRacePresentationBridge
        {
            Name = nameof(DailyFriendRacePresentationBridge)
        };
        dailyRaceBridge.Configure(dailyRaceFacade);
        AddChild(dailyRaceBridge);

        // Only retry today's completed board on startup. This keeps Steam work bounded and avoids
        // replaying a history of dynamic leaderboard operations. Local completion remains valid even
        // if this projection never succeeds.
        var today = DateTimeOffset.UtcNow;
        var todayKey = DailyFriendRaceService.GetDailyKey(today);
        var completedToday = session.State.DailyRaceAttempts.FirstOrDefault(attempt =>
            string.Equals(attempt.DailyKey, todayKey, StringComparison.Ordinal) &&
            attempt.State == DailyRaceAttemptState.Completed);
        if (completedToday != null)
            ProjectDailyRaceAttempt(completedToday, "startup retry");
    }

    private async void ProjectMultiplayerWins(int totalWins, string reason)
    {
        var projection = _leaderboardProjection;
        if (projection == null || !projection.Availability.IsAvailable || totalWins < 0)
            return;

        try
        {
            var result = await projection.UploadMultiplayerWinsAsync(totalWins);
            if (!result.Success)
            {
                GD.PushWarning(
                    $"Steam multiplayer-win leaderboard projection failed during {reason}: " +
                    (result.Error ?? "unknown Steam leaderboard error") +
                    ". Local progress is already saved and will be retried later.");
            }
        }
        catch (Exception exception)
        {
            // The adapter is designed to return failures, but this final boundary protects the
            // single-player/session lifetime from any unexpected Steam callback/interop exception.
            GD.PushWarning(
                $"Steam multiplayer-win leaderboard projection threw during {reason}: {exception.Message}. " +
                "Local progress is already saved and will be retried later.");
        }
    }

    private async void ProjectDailyRaceAttempt(DailyRaceAttemptData attempt, string reason)
    {
        var daily = _dailyFriendRace;
        if (daily == null || !daily.LeaderboardAvailability.IsAvailable)
            return;

        try
        {
            var result = await daily.ProjectCompletedAttemptAsync(attempt);
            if (!result.Success)
            {
                GD.PushWarning(
                    $"Steam daily-race leaderboard projection failed during {reason}: " +
                    (result.Error ?? "unknown Steam leaderboard error") +
                    ". The local daily result remains saved.");
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning(
                $"Steam daily-race leaderboard projection threw during {reason}: {exception.Message}. " +
                "The local daily result remains saved.");
        }
    }

    private static void RecoverInterruptedTradePrepares(
        TradeTransferService transfers,
        GodotJsonGameStateRepository stateRepository,
        GameSession session)
    {
        var interrupted = session.State.PendingTradeJournal
            .Select(entry => entry.TradeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (interrupted.Length == 0)
            return;

        foreach (var tradeId in interrupted)
            transfers.AbortPrepared(session.State, tradeId);

        try
        {
            stateRepository.Save(session.State);
            session.NotifyExternallyPersistedStateChanged();
            GD.Print($"Recovered {interrupted.Length} interrupted pre-commit multiplayer trade(s) by aborting them locally.");
        }
        catch (Exception exception)
        {
            // Recovery is an unlock operation only. A persistence problem must not prevent the
            // offline/singleplayer game from starting; the same idempotent recovery will retry next launch.
            GD.PushWarning($"Could not persist interrupted trade recovery: {exception.Message}");
        }
    }

    private void ComposeMultiplayerProbeIfRequested(GameSession session)
    {
        if (_multiplayerConnection == null || _connectedZone == null)
            return;

        var args = OS.GetCmdlineUserArgs();
        var requested = false;
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--voidling-mp-", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(arg, "--voidling-lan-smoke", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            requested = true;
            break;
        }

        if (!requested)
            return;

        var probe = new MultiplayerConnectivityProbe
        {
            Name = nameof(MultiplayerConnectivityProbe)
        };
        probe.Configure(_multiplayerConnection, _connectedZone, session, args);
        AddChild(probe);
    }

    private static GameBalanceRules LoadBalanceRules()
    {
        var authored = ResourceLoader.Load<GameBalanceResource>(BalancePath);
        if (authored != null)
            return authored.ToDomainRules();

        GD.PushWarning($"Could not load balance resource at {BalancePath}; using code defaults.");
        return GameBalanceRules.DemoDefaults;
    }
}
