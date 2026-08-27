using System;
using System.Linq;
using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Persistence;
using Voidling.Application.Ports;
using Voidling.Application.Racing;
using Voidling.Application.Roster;
using Voidling.Application.Settings;
using Voidling.Application.Shop;
using Voidling.Application.Simulation;
using Voidling.Application.Training;

namespace VoidlingGame;

/// <summary>
/// Transitional Godot lifetime facade. Existing presentation code still calls this API,
/// while infrastructure and deterministic rules are progressively moved behind explicit
/// collaborators. New features should prefer focused Application services over adding more
/// responsibilities here.
/// </summary>
public partial class GameSession : Node
{
    public event Action? StateChanged;
    public event Action<string>? ToastRequested;
    public event Action<string>? GardenEventRaised;

    public GameStateData State { get; private set; } = new();

    private double _simulationAccumulator;
    private IGameStateRepository? _stateRepository;
    private IAudioSettingsAdapter? _audioSettings;
    private GameStateMigrationService? _migrations;
    private AdvanceSimulationUseCase? _simulation;
    private TrainingUseCase? _training;
    private BreedVoidlingsUseCase? _breeding;
    private ShopUseCase? _shop;
    private SettingsUseCase? _settings;
    private VoidlingRosterUseCase? _roster;
    private RaceResultUseCase? _raceResults;
    private LineageTreeProjectionService? _lineageTreeProjection;

    public void Configure(
        IGameStateRepository stateRepository,
        IAudioSettingsAdapter audioSettings,
        GameStateMigrationService migrations,
        AdvanceSimulationUseCase simulation,
        TrainingUseCase training,
        BreedVoidlingsUseCase breeding,
        ShopUseCase shop,
        SettingsUseCase settings,
        VoidlingRosterUseCase roster,
        RaceResultUseCase raceResults,
        LineageTreeProjectionService lineageTreeProjection)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("GameSession must be configured before entering the scene tree.");

        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _audioSettings = audioSettings ?? throw new ArgumentNullException(nameof(audioSettings));
        _migrations = migrations ?? throw new ArgumentNullException(nameof(migrations));
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _training = training ?? throw new ArgumentNullException(nameof(training));
        _breeding = breeding ?? throw new ArgumentNullException(nameof(breeding));
        _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _raceResults = raceResults ?? throw new ArgumentNullException(nameof(raceResults));
        _lineageTreeProjection = lineageTreeProjection ?? throw new ArgumentNullException(nameof(lineageTreeProjection));
    }

    public override void _Ready()
    {
        if (_stateRepository == null || _audioSettings == null || _migrations == null ||
            _simulation == null || _training == null || _breeding == null || _shop == null ||
            _settings == null || _roster == null || _raceResults == null || _lineageTreeProjection == null)
        {
            throw new InvalidOperationException("GameSession must be created by the composition root.");
        }

        LoadOrCreate();
        ApplyAudioSettings();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _simulationAccumulator += delta;
        if (_simulationAccumulator < 0.5)
            return;

        var step = (float)_simulationAccumulator;
        _simulationAccumulator = 0.0;
        var result = _simulation!.Advance(State, step);

        foreach (var simulationEvent in result.Events)
        {
            switch (simulationEvent)
            {
                case CreatureBecameAdultEvent adult:
                {
                    var message = $"{adult.Name} grew into an adult.";
                    ToastRequested?.Invoke(message);
                    RaiseGardenEvent(message);
                    break;
                }
                case CreatureReincarnatedEvent reincarnated:
                {
                    var message = $"{reincarnated.Name} reincarnated and began a new life.";
                    ToastRequested?.Invoke(message);
                    RaiseGardenEvent(message);
                    break;
                }
                case CreatureDiedEvent died:
                {
                    var message = $"{died.Name} reached the end of their life.";
                    ToastRequested?.Invoke(message);
                    RaiseGardenEvent(message);
                    break;
                }
                case CreatureHatchedEvent hatched:
                {
                    var message = $"An egg hatched and {hatched.Name} was born!";
                    ToastRequested?.Invoke(message);
                    RaiseGardenEvent(message);
                    break;
                }
                case EggFailedEvent:
                {
                    const string message = "An egg failed to hatch.";
                    ToastRequested?.Invoke(message);
                    RaiseGardenEvent(message);
                    break;
                }
            }
        }

        if (!result.Changed)
            return;

        Save();
        StateChanged?.Invoke();
    }

    public void ResetDemo()
    {
        State = CreateFreshState();
        ApplyAudioSettings();
        SaveAndNotify("Demo save reset.");
        RaiseGardenEvent("The garden was reset.");
    }

    /// <summary>
    /// Notifies existing presentation that an external Application service already mutated and
    /// persisted this exact State instance. This intentionally does not save again.
    /// </summary>
    public void NotifyExternallyPersistedStateChanged()
        => StateChanged?.Invoke();

    private void LoadOrCreate()
    {
        try
        {
            var loaded = _stateRepository!.Load();
            if (loaded != null)
            {
                State = loaded;
                NormalizeState();
                return;
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not load MVP save: {exception.Message}");
        }

        State = CreateFreshState();
        Save();
    }

    private void NormalizeState()
    {
        _migrations!.Normalize(State);

        for (var i = 0; i < State.Voidlings.Count; i++)
        {
            var creature = State.Voidlings[i];
            if (Math.Abs(creature.WorldX) < 0.01f && Math.Abs(creature.WorldY) < 0.01f)
            {
                var p = StarterSpawnPosition(i);
                creature.WorldX = p.X;
                creature.WorldY = p.Y;
            }
        }

        for (var i = 0; i < State.OwnedEggs.Count; i++)
        {
            var egg = State.OwnedEggs[i];
            if (Math.Abs(egg.WorldX) < 0.01f && Math.Abs(egg.WorldY) < 0.01f)
            {
                var p = NestPosition(i);
                egg.WorldX = p.X;
                egg.WorldY = p.Y;
            }
        }

        while (State.StoreEggs.Count < 3)
            State.StoreEggs.Add(CreateStoreEgg());

        EnsureAngelMutation();
        Save();
    }

    private GameStateData CreateFreshState()
    {
        var state = new GameStateData
        {
            SaveVersion = GameStateMigrationService.CurrentSaveVersion,
            Coins = 120,
            SeedCounter = DateTime.UtcNow.Ticks,
            MasterVolume = 1.0f,
            AutoFinishRaces = true
        };

        foreach (var statId in GameRules.StatIds)
            state.TrainingItems[statId] = 1;

        State = state;
        State.Voidlings.Add(CreateStarter("Pip", "#E7A6B6", StarterSpawnPosition(0)));
        State.Voidlings.Add(CreateStarter("Mallow", "#A9D5C0", StarterSpawnPosition(1)));
        EnsureAngelMutation();

        for (var i = 0; i < 3; i++)
            State.StoreEggs.Add(CreateStoreEgg());

        return State;
    }

    private void EnsureAngelMutation()
    {
        if (State.Voidlings.Concat(State.DepartedVoidlings)
            .Any(v => GameRules.HasMutation(v, GameRules.AngelMutationId)))
            return;
        if (State.Voidlings.Count == 0)
            return;

        var rng = GeneticsService.CreateRandom(unchecked((ulong)State.SeedCounter), "demo:angel-mutation");
        var chosen = State.Voidlings[rng.Next(State.Voidlings.Count)];
        chosen.RareTraits.Add(new RareTraitData
        {
            TraitId = GameRules.AngelMutationId,
            FounderCreatureId = chosen.Id,
            GenerationFromFounder = 0,
            CanTransmit = true
        });
    }

    private VoidlingData CreateStarter(string name, string tint, Vector2 position)
    {
        var seed = NextSeed();
        var id = NewId();
        var genome = GeneticsService.CreateRandomGenome(seed);

        return new VoidlingData
        {
            Id = id,
            Name = name,
            Genome = genome,
            Stage = LifeStage.Adult,
            AgeSeconds = GameRules.ChildToAdultSeconds,
            TintHex = tint,
            RareTraits = GeneticsService.RollFounderTraits(seed, id),
            TrainingPoints = GameRules.StatIds.ToDictionary(stat => stat, _ => 0),
            WorldX = position.X,
            WorldY = position.Y
        };
    }

    private EggData CreateStoreEgg()
    {
        var seed = NextSeed();
        var id = NewId();
        return _shop!.CreateStoreInventoryEgg(id, seed);
    }

    private void ApplyAudioSettings()
        => _audioSettings!.ApplyMasterVolume(State.MasterVolume);

    private Vector2 NextNestPosition() => NestPosition(State.OwnedEggs.Count);

    private static Vector2 NestPosition(int index)
    {
        var column = index % 5;
        var row = index / 5;
        return new Vector2(315 + column * 26, 275 + row * 24);
    }

    private static Vector2 StarterSpawnPosition(int index)
    {
        var positions = new[]
        {
            new Vector2(300, 185),
            new Vector2(420, 210),
            new Vector2(250, 250),
            new Vector2(485, 160),
            new Vector2(360, 290),
            new Vector2(530, 250)
        };
        return positions[index % positions.Length];
    }

    private ulong NextSeed()
    {
        State.SeedCounter++;
        return unchecked((ulong)State.SeedCounter);
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private void SaveAndNotify(string toast)
    {
        Save();
        StateChanged?.Invoke();
        ToastRequested?.Invoke(toast);
    }

    private void RaiseGardenEvent(string message)
        => GardenEventRaised?.Invoke(message);

    private void Save()
    {
        try
        {
            _stateRepository!.Save(State);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not save MVP state: {exception.Message}");
        }
    }
}
