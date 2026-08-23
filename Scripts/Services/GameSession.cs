using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Ports;
using Voidling.Infrastructure.Audio;
using Voidling.Infrastructure.Persistence;

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

    private const string SavePath = "user://voidling_mvp_save.json";
    private double _simulationAccumulator;
    private IGameStateRepository? _stateRepository;
    private IAudioSettingsAdapter? _audioSettings;

    /// <summary>
    /// Allows the future composition root and tests to inject platform adapters. The current
    /// autoload path has safe Godot defaults so this refactor does not change scene setup.
    /// </summary>
    public void Configure(IGameStateRepository stateRepository, IAudioSettingsAdapter audioSettings)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("GameSession must be configured before entering the scene tree.");

        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _audioSettings = audioSettings ?? throw new ArgumentNullException(nameof(audioSettings));
    }

    public override void _Ready()
    {
        Instance = this;
        _stateRepository ??= new GodotJsonGameStateRepository(SavePath);
        _audioSettings ??= new GodotAudioSettingsAdapter();
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

    public VoidlingData? FindVoidling(string id)
        => State.Voidlings.FirstOrDefault(v => v.Id == id);

    public VoidlingData? FindLineageVoidling(string id)
        => State.Voidlings.FirstOrDefault(v => v.Id == id)
           ?? State.DepartedVoidlings.FirstOrDefault(v => v.Id == id);

    public IReadOnlyList<VoidlingData> GetLineageVoidlings()
        => State.Voidlings.Concat(State.DepartedVoidlings).ToList();

    public bool IsDeparted(string id)
        => State.DepartedVoidlings.Any(v => v.Id == id);

    public void BuyTrainingItem(string statId)
    {
        if (!GameRules.StatIds.Contains(statId))
            return;

        if (State.Coins < GameRules.TrainingItemPrice)
        {
            ToastRequested?.Invoke("Not enough sprouts.");
            return;
        }

        State.Coins -= GameRules.TrainingItemPrice;
        State.TrainingItems.TryGetValue(statId, out var count);
        State.TrainingItems[statId] = count + 1;
        SaveAndNotify($"Bought a {GameRules.StatDisplayNames[statId]} treat.");
    }

    public void UseTrainingItem(string creatureId, string statId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return;

        State.TrainingItems.TryGetValue(statId, out var count);
        if (count <= 0)
        {
            ToastRequested?.Invoke($"Buy a {GameRules.StatDisplayNames[statId]} treat first.");
            return;
        }

        State.TrainingItems[statId] = count - 1;
        var rng = GeneticsService.CreateRandom(NextSeed(), $"training:{creatureId}:{statId}");
        var gain = rng.Next(5, 10);
        creature.TrainingPoints.TryGetValue(statId, out var current);
        creature.TrainingPoints[statId] = Math.Min(120, current + gain);
        SaveAndNotify($"{creature.Name} gained +{gain} {GameRules.StatDisplayNames[statId]} training.");
    }

    public void BuyStoreEgg(string eggId)
    {
        var egg = State.StoreEggs.FirstOrDefault(e => e.Id == eggId);
        if (egg == null)
            return;

        if (State.Coins < GameRules.StoreEggPrice)
        {
            ToastRequested?.Invoke("Not enough sprouts.");
            return;
        }

        State.Coins -= GameRules.StoreEggPrice;
        State.StoreEggs.Remove(egg);
        egg.Source = EggSource.Store;
        egg.IncubationSeconds = 0.0f;

        var nestPosition = NextNestPosition();
        egg.WorldX = nestPosition.X;
        egg.WorldY = nestPosition.Y;
        State.OwnedEggs.Add(egg);
        State.StoreEggs.Add(CreateStoreEgg());
        SaveAndNotify("Bought a mystery egg.");
    }

    public string GetBreedingPreview(string parentAId, string parentBId)
    {
        var a = FindVoidling(parentAId);
        var b = FindVoidling(parentBId);

        if (a == null || b == null)
            return "Choose two adults.";
        if (a.Id == b.Id)
            return "Choose two different Voidlings.";
        if (a.Stage != LifeStage.Adult || b.Stage != LifeStage.Adult)
            return "Both parents must be adults.";
        if (a.BreedCooldownSeconds > 0.0f || b.BreedCooldownSeconds > 0.0f)
            return "One parent is still on breeding cooldown.";

        var lineage = GetLineageVoidlings();
        var related = GeneticsService.AreRelated(a, b, lineage);
        var burden = GeneticsService.ComputeChildBurden(a, b, related);
        var failure = GameRules.HatchFailurePercent(burden);

        if (related)
            return $"Related pairing • inbreeding level {burden} • {failure}% hatch-failure risk.";

        if (burden < Math.Max(a.InbreedingBurdenLevel, b.InbreedingBurdenLevel))
            return $"Clean outcross • inherited burden falls to level {burden}.";

        return burden > 0
            ? $"Unrelated pairing • inherited burden remains level {burden}."
            : "Unrelated pairing • no inbreeding penalty.";
    }

    public bool TryBreed(string parentAId, string parentBId, Vector2 eggWorldPosition)
    {
        var a = FindVoidling(parentAId);
        var b = FindVoidling(parentBId);

        if (a == null || b == null || a.Id == b.Id)
            return false;

        if (a.Stage != LifeStage.Adult || b.Stage != LifeStage.Adult)
        {
            ToastRequested?.Invoke("Both parents must be adults.");
            return false;
        }

        if (a.BreedCooldownSeconds > 0.0f || b.BreedCooldownSeconds > 0.0f)
        {
            ToastRequested?.Invoke("A parent is still on breeding cooldown.");
            return false;
        }

        var seed = NextSeed();
        var eggId = NewId();
        var related = GeneticsService.AreRelated(a, b, GetLineageVoidlings());
        var burden = GeneticsService.ComputeChildBurden(a, b, related);
        var genome = GeneticsService.CreateChildGenome(a, b, seed);
        var rareTraits = GeneticsService.InheritRareTraits(a, b, seed);
        var viable = GeneticsService.RollViability(seed, burden);

        var egg = new EggData
        {
            Id = eggId,
            Source = EggSource.Bred,
            Seed = seed,
            Genome = genome,
            ParentAId = a.Id,
            ParentBId = b.Id,
            FamilyGeneration = Math.Max(a.FamilyGeneration, b.FamilyGeneration) + 1,
            InbreedingBurdenLevel = burden,
            InbreedingHistoryFlag = related || a.InbreedingHistoryFlag || b.InbreedingHistoryFlag,
            IsViable = viable,
            FailureResolved = true,
            RequiredIncubationSeconds = GameRules.EggIncubationSeconds,
            TintHex = GeneticsService.ResolveTint(genome),
            RareTraits = rareTraits,
            WorldX = eggWorldPosition.X,
            WorldY = eggWorldPosition.Y
        };

        State.OwnedEggs.Add(egg);
        a.BreedCooldownSeconds = GameRules.BreedCooldownSeconds;
        b.BreedCooldownSeconds = GameRules.BreedCooldownSeconds;

        var warning = related
            ? $" Egg carries level {burden} inbreeding risk ({GameRules.HatchFailurePercent(burden)}%)."
            : "";
        SaveAndNotify($"Breeding produced an egg.{warning}");
        return true;
    }

    public void DiscardFailedEgg(string eggId)
    {
        var egg = State.OwnedEggs.FirstOrDefault(e => e.Id == eggId && e.State == EggState.Failed);
        if (egg == null)
            return;

        State.OwnedEggs.Remove(egg);
        SaveAndNotify("Removed the failed egg.");
    }

    public bool SayGoodbye(string creatureId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return false;

        State.Voidlings.Remove(creature);
        State.DepartedVoidlings.Add(creature);
        SaveAndNotify($"{creature.Name} left the farm forever. Their family record remains.");
        return true;
    }

    public void AddRaceReward(int place)
    {
        var reward = place switch
        {
            1 => 30,
            2 => 20,
            3 => 10,
            _ => 5
        };

        State.Coins += reward;
        SaveAndNotify($"Race reward: +{reward} sprouts.");
    }

    public void SetMasterVolume(float value)
    {
        State.MasterVolume = Mathf.Clamp(value, 0.0f, 1.0f);
        ApplyAudioSettings();
        Save();
    }

    public void SetAutoFinishRaces(bool enabled)
    {
        State.AutoFinishRaces = enabled;
        Save();
    }

    public string NameFor(string id)
        => FindLineageVoidling(id)?.Name ?? "Unknown";

    public ulong CreateRaceSeed()
    {
        var seed = NextSeed();
        Save();
        return seed;
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
