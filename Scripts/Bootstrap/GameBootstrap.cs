using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Training;
using Voidling.Domain.Rules;
using Voidling.Infrastructure.Audio;
using Voidling.Infrastructure.Persistence;
using VoidlingGame;

namespace Voidling.Bootstrap;

/// <summary>
/// Application composition root. This is the one place that intentionally knows concrete
/// infrastructure implementations and the Godot-owned lifetime of the game session.
/// </summary>
public partial class GameBootstrap : Node
{
    private const string SavePath = "user://voidling_mvp_save.json";

    public override void _Ready()
    {
        var rules = GameBalanceRules.DemoDefaults;
        var session = new GameSession
        {
            Name = nameof(GameSession)
        };

        session.Configure(
            new GodotJsonGameStateRepository(SavePath),
            new GodotAudioSettingsAdapter(),
            new TrainingUseCase(rules),
            new BreedVoidlingsUseCase(rules));

        AddChild(session);
    }
}
