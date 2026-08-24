using System;
using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Multiplayer;
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

    public override void _Ready()
    {
        var rules = LoadBalanceRules();

        // Transitional presentation code still reads the GameRules facade. Configure it with
        // the exact same immutable rules used below so there is only one effective ruleset.
        GameRules.Configure(rules);

        ComposeOptionalMultiplayer();

        var session = new GameSession
        {
            Name = nameof(GameSession)
        };

        session.Configure(
            new GodotJsonGameStateRepository(SavePath),
            new GodotAudioSettingsAdapter(),
            new GameStateMigrationService(rules),
            new AdvanceSimulationUseCase(rules),
            new TrainingUseCase(rules),
            new BreedVoidlingsUseCase(rules),
            new ShopUseCase(rules),
            new SettingsUseCase(),
            new VoidlingRosterUseCase(),
            new RaceResultUseCase(rules));
        session.ConfigureRacing(new RaceEntryFactory(rules));

        AddChild(session);
        ComposeMultiplayerProbeIfRequested(session);
    }

    private void ComposeOptionalMultiplayer()
    {
        var multiplayer = OptionalMultiplayerComposer.Create();
        _multiplayerConnection = multiplayer.Connection;
        _connectedZone = multiplayer.ConnectedZone;

        if (multiplayer.RuntimeNode != null)
        {
            AddChild(multiplayer.RuntimeNode);
            GD.Print($"Steam multiplayer available for {_multiplayerConnection.LocalUser?.DisplayName ?? "local user"}.");
            return;
        }

        // This is informational, never fatal. Single-player must remain fully functional offline.
        GD.Print(multiplayer.UnavailableReason ?? "Steam multiplayer unavailable; continuing in single-player mode.");
    }

    private void ComposeMultiplayerProbeIfRequested(GameSession session)
    {
        if (_multiplayerConnection == null || _connectedZone == null)
            return;

        var args = OS.GetCmdlineArgs();
        var requested = false;
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--voidling-mp-", StringComparison.OrdinalIgnoreCase))
                continue;

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
