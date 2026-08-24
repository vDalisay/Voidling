using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Persistence;
using Voidling.Application.Racing;
using Voidling.Application.Roster;
using Voidling.Application.Settings;
using Voidling.Application.Shop;
using Voidling.Application.Simulation;
using Voidling.Application.Training;
using Voidling.Domain.Rules;
using Voidling.Infrastructure.Audio;
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

    public override void _Ready()
    {
        var rules = LoadBalanceRules();

        // Transitional presentation code still reads the GameRules facade. Configure it with
        // the exact same immutable rules used below so there is only one effective ruleset.
        GameRules.Configure(rules);

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

        AddChild(session);
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
