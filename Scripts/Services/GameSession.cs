using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace VoidlingGame;

public partial class GameSession : Node
{
    public static GameSession Instance { get; private set; } = null!;

    public event Action? StateChanged;
    public event Action<string>? ToastRequested;

    public GameStateData State { get; private set; } = new();

    private const string SavePath = "user://voidling_mvp_save.json";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private double _simulationAccumulator;

    public override void _Ready()
    {
        Instance = this;
        LoadOrCreate();
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
        State.OwnedEggs.Add(egg);

        // The replacement is generated now, when it enters store inventory.
        State.StoreEggs.Add(CreateStoreEgg());
        SaveAndNotify("Bought a mystery egg. Its genetics were already fixed in the shop.");
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

        var related = GeneticsService.AreRelated(a, b, State.Voidlings);
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

    public bool TryBreed(string parentAId, string parentBId)
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
        var related = GeneticsService.AreRelated(a, b, State.Voidlings);
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
            RareTraits = rareTraits
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

    public string NameFor(string id)
        => FindVoidling(id)?.Name ?? "Unknown";

    public ulong CreateRaceSeed()
    {
        var seed = NextSeed();
        Save();
        return seed;
    }

    public void ResetDemo()
    {
        State = CreateFreshState();
        SaveAndNotify("Demo save reset.");
    }

    private void HatchEgg(EggData egg)
    {
        var suffix = State.Voidlings.Count + 1;
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
            RareTraits = egg.RareTraits
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
            if (Godot.FileAccess.FileExists(SavePath))
            {
                using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
                var json = file.GetAsText();
                var loaded = JsonSerializer.Deserialize<GameStateData>(json, _jsonOptions);
                if (loaded != null)
                {
                    State = loaded;
                    NormalizeState();
                    return;
                }
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
        foreach (var statId in GameRules.StatIds)
        {
            if (!State.TrainingItems.ContainsKey(statId))
                State.TrainingItems[statId] = 0;

            foreach (var creature in State.Voidlings)
            {
                if (!creature.TrainingPoints.ContainsKey(statId))
                    creature.TrainingPoints[statId] = 0;
            }
        }

        while (State.StoreEggs.Count < 3)
            State.StoreEggs.Add(CreateStoreEgg());
    }

    private GameStateData CreateFreshState()
    {
        var state = new GameStateData
        {
            Coins = 120,
            SeedCounter = DateTime.UtcNow.Ticks
        };

        foreach (var statId in GameRules.StatIds)
            state.TrainingItems[statId] = 1;

        State = state;

        var first = CreateStarter("Pip", "#E7A6B6");
        var second = CreateStarter("Mallow", "#A9D5C0");
        State.Voidlings.Add(first);
        State.Voidlings.Add(second);

        for (var i = 0; i < 3; i++)
            State.StoreEggs.Add(CreateStoreEgg());

        return State;
    }

    private VoidlingData CreateStarter(string name, string tint)
    {
        var seed = NextSeed();
        var id = NewId();
        var genome = GeneticsService.CreateRandomGenome(seed);

        // Keep the requested visible starter colors while retaining their own hidden color genes.
        return new VoidlingData
        {
            Id = id,
            Name = name,
            Genome = genome,
            Stage = LifeStage.Adult,
            AgeSeconds = GameRules.ChildToAdultSeconds,
            TintHex = tint,
            RareTraits = GeneticsService.RollFounderTraits(seed, id),
            TrainingPoints = GameRules.StatIds.ToDictionary(stat => stat, _ => 0)
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
            using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(JsonSerializer.Serialize(State, _jsonOptions));
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not save MVP state: {exception.Message}");
        }
    }
}
