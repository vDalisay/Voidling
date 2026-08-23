using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Ports;
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
    public static GameSession Instance { get; private set; } = null!;

    public event Action? StateChanged;
    public event Action<string>? ToastRequested;

    public GameStateData State { get; private set; } = new();

    private double _simulationAccumulator;
    private IGameStateRepository? _stateRepository;
    private IAudioSettingsAdapter? _audioSettings;
    private TrainingUseCase? _training;
    private BreedVoidlingsUseCase? _breeding;

    public void Configure(
        IGameStateRepository stateRepository,
        IAudioSettingsAdapter audioSettings,
        TrainingUseCase training,
        BreedVoidlingsUseCase breeding)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("GameSession must be configured before entering the scene tree.");

        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _audioSettings = audioSettings ?? throw new ArgumentNullException(nameof(audioSettings));
        _training = training ?? throw new ArgumentNullException(nameof(training));
        _breeding = breeding ?? throw new ArgumentNullException(nameof(breeding));
    }

    public override void _Ready()
    {
        if (_stateRepository == null || _audioSettings == null || _training == null || _breeding == null)
            throw new InvalidOperationException("GameSession must be created by the composition root.");

        Instance = this;
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
        var changed = false;

        foreach (var creature in State.Voidlings)
        {
            if (creature.BreedCooldownSeconds > 0.0f)
            {
                creature.BreedCooldownSeconds = Math.Max(0.0f, creature.BreedCooldownSeconds - step);
                changed = true;
            }

            if (creature.Stage == LifeStage.Child)
            {
                creature.AgeSeconds += step;
                if (creature.AgeSeconds >= GameRules.ChildToAdultSeconds)
                {
                    creature.Stage = LifeStage.Adult;
                    ToastRequested?.Invoke($"{creature.Name} grew into an adult.");
                }
                changed = true;
            }
        }

        var hatchQueue = new List<EggData>();
        foreach (var egg in State.OwnedEggs)
        {
            if (egg.State != EggState.Incubating)
                continue;

            egg.IncubationSeconds += step;
            changed = true;
            if (egg.IncubationSeconds >= egg.RequiredIncubationSeconds)
                hatchQueue.Add(egg);
        }

        foreach (var egg in hatchQueue)
        {
            if (!egg.IsViable)
            {
                egg.State = EggState.Failed;
                egg.FailureResolved = true;
                ToastRequested?.Invoke("An egg failed to hatch.");
                continue;
            }

            HatchEgg(egg);
        }

        if (changed || hatchQueue.Count > 0)
        {
            Save();
            StateChanged?.Invoke();
        }
    }

    public void ResetDemo()
    {
        State = CreateFreshState();
        ApplyAudioSettings();
        SaveAndNotify("Demo save reset.");
    }

    private void HatchEgg(EggData egg)
    {
        var suffix = State.Voidlings.Count + State.DepartedVoidlings.Count + 1;
        var creature = new VoidlingData
        {
            Id = egg.Id,
            Name = $"Voidling {suffix}",
            Genome = egg.Genome,
            Stage = LifeStage.Child,
            ParentAId = egg.ParentAId,
            ParentBId = egg.ParentBId,
            FamilyGeneration = egg.FamilyGeneration,
            InbreedingBurdenLevel = egg.InbreedingBurdenLevel,
            InbreedingHistoryFlag = egg.InbreedingHistoryFlag,
            TintHex = egg.TintHex,
            RareTraits = egg.RareTraits,
            WorldX = egg.WorldX,
            WorldY = egg.WorldY
        };

        foreach (var statId in GameRules.StatIds)
            creature.TrainingPoints[statId] = 0;

        State.Voidlings.Add(creature);
        State.OwnedEggs.Remove(egg);
        ToastRequested?.Invoke($"{creature.Name} hatched.");
    }

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
        var previousVersion = State.SaveVersion;
        State.DepartedVoidlings ??= new List<VoidlingData>();

        if (previousVersion < 4)
        {
            State.MasterVolume = 1.0f;
            State.AutoFinishRaces = true;
        }
        State.SaveVersion = 4;

        foreach (var statId in GameRules.StatIds)
        {
            if (!State.TrainingItems.ContainsKey(statId))
                State.TrainingItems[statId] = 0;

            foreach (var creature in State.Voidlings.Concat(State.DepartedVoidlings))
            {
                if (!creature.TrainingPoints.ContainsKey(statId))
                    creature.TrainingPoints[statId] = 0;
                creature.RareTraits ??= new List<RareTraitData>();
            }
        }

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
            egg.RareTraits ??= new List<RareTraitData>();
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
            SaveVersion = 4,
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
        var genome = GeneticsService.CreateRandomGenome(seed);

        return new EggData
        {
            Id = id,
            Source = EggSource.Store,
            Seed = seed,
            Genome = genome,
            RequiredIncubationSeconds = GameRules.EggIncubationSeconds,
            TintHex = GeneticsService.ResolveTint(genome),
            RareTraits = GeneticsService.RollFounderTraits(seed, id),
            IsViable = true,
            FailureResolved = true
        };
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
