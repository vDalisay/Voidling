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
using Voidling.Domain.Rules;
using Voidling.Domain.Shop;

namespace VoidlingGame;

public enum GameSessionStartupNotice
{
    None,
    SaveRecoveredFromBackup,
    SaveLoadFailed,
    SaveUnavailable
}

public partial class GameSession : Node
{
    public event Action? StateChanged;
    public event Action<string>? ToastRequested;
    public event Action<string>? GardenEventRaised;
    public event Action<string, bool>? LifecycleCocoonRequested;
    public event Action<bool>? SaveFeedbackRequested;

    public GameStateData State { get; private set; } = new();
    public GameSessionStartupNotice StartupNotice { get; private set; }

    private double _simulationAccumulator;
    private IGameStateRepository? _stateRepository;
    private IGameStateRecoveryInfo? _stateRecoveryInfo;
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
    private bool _saveFailureLatched;

    public void Configure(IGameStateRepository stateRepository, IAudioSettingsAdapter audioSettings,
        GameStateMigrationService migrations, AdvanceSimulationUseCase simulation, TrainingUseCase training,
        BreedVoidlingsUseCase breeding, ShopUseCase shop, SettingsUseCase settings, VoidlingRosterUseCase roster,
        RaceResultUseCase raceResults, LineageTreeProjectionService lineageTreeProjection)
    {
        if (IsInsideTree()) throw new InvalidOperationException("GameSession must be configured before entering the scene tree.");
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _stateRecoveryInfo = stateRepository as IGameStateRecoveryInfo;
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
        if (_stateRepository == null || _audioSettings == null || _migrations == null || _simulation == null ||
            _training == null || _breeding == null || _shop == null || _settings == null || _roster == null ||
            _raceResults == null || _lineageTreeProjection == null)
            throw new InvalidOperationException("GameSession must be created by the composition root.");
        LoadOrCreate();
        ApplyAudioSettings();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _simulationAccumulator += delta;
        if (_simulationAccumulator < 0.5) return;
        var step = (float)_simulationAccumulator;
        _simulationAccumulator = 0.0;
        var result = _simulation!.Advance(State, step);

        foreach (var simulationEvent in result.Events)
        {
            switch (simulationEvent)
            {
                case CreatureBecameAdultEvent adult:
                    Announce($"{adult.Name} grew into an adult.", true); break;
                case CreatureEnteredCocoonEvent cocoon:
                    RaiseGardenEvent(cocoon.WillReincarnate ? $"{cocoon.Name} entered a bright cocoon." : $"{cocoon.Name} entered a fading cocoon.");
                    LifecycleCocoonRequested?.Invoke(cocoon.CreatureId, cocoon.WillReincarnate); break;
                case CreatureReincarnatedEvent reincarnated:
                    Announce($"{reincarnated.Name} reincarnated and began a new life.", true); break;
                case CreatureDiedEvent died:
                    Announce($"{died.Name} reached the end of their life.", true); break;
                case CreatureCareRiskEvent risk:
                    RaiseGardenEvent($"{risk.Name} seems unsettled and needs more care before the end of this life."); break;
                case CreaturePassiveTrainingCappedEvent capped:
                    RaiseGardenEvent($"{capped.Name} finished passive {DisplayStatId(capped.StatId)} training at their current DNA cap."); break;
                case CreatureHatchedEvent hatched:
                    RecordDailyMissionEvent(DailyMissionEventKind.HatchEgg);
                    Announce($"An egg hatched and {hatched.Name} was born!", true); break;
                case EggFailedEvent:
                    Announce("An egg failed to hatch.", true); break;
                case EggWaitingForGardenSpaceEvent:
                    Announce("The Garden is full. Say goodbye to a Voidling before this egg can hatch.", true); break;
            }
        }
        if (!result.Changed) return;
        Save();
        StateChanged?.Invoke();
    }

    private void Announce(string message, bool toast)
    {
        if (toast) ToastRequested?.Invoke(message);
        RaiseGardenEvent(message);
    }

    public bool ShouldStartTutorial() => !State.TutorialCompleted;
    public void CompleteTutorial()
    {
        if (State.TutorialCompleted) return;
        State.TutorialCompleted = true;
        Save();
        StateChanged?.Invoke();
    }

    public void ResetDemo()
    {
        var tutorialCompleted = State.TutorialCompleted;
        State = CreateFreshState();
        State.TutorialCompleted = tutorialCompleted;
        ApplyAudioSettings();
        SaveAndNotify("Demo save reset.");
        RaiseGardenEvent("The garden was reset.");
    }

    public void NotifyExternallyPersistedStateChanged()
    {
        StateChanged?.Invoke();
        SaveFeedbackRequested?.Invoke(true);
    }

    private void LoadOrCreate()
    {
        StartupNotice = GameSessionStartupNotice.None;
        var loadFailed = false;
        try
        {
            var loaded = _stateRepository!.Load();
            if (loaded != null)
            {
                if (_stateRecoveryInfo?.LastLoadRecoveryStatus == GameStateRecoveryStatus.RecoveredFromBackup)
                    StartupNotice = GameSessionStartupNotice.SaveRecoveredFromBackup;
                State = loaded;
                NormalizeState();
                return;
            }
        }
        catch (Exception exception)
        {
            loadFailed = true;
            GD.PushWarning($"Could not load MVP save: {exception.Message}");
        }
        State = CreateFreshState();
        if (!Save()) StartupNotice = GameSessionStartupNotice.SaveUnavailable;
        else if (loadFailed) StartupNotice = GameSessionStartupNotice.SaveLoadFailed;
    }

    private void NormalizeState()
    {
        _migrations!.Normalize(State);
        for (var i = 0; i < State.Voidlings.Count; i++)
        {
            var creature = State.Voidlings[i];
            if (Math.Abs(creature.WorldX) < 0.01f && Math.Abs(creature.WorldY) < 0.01f)
            { var p = StarterSpawnPosition(i); creature.WorldX = p.X; creature.WorldY = p.Y; }
        }
        for (var i = 0; i < State.OwnedEggs.Count; i++)
        {
            var egg = State.OwnedEggs[i];
            // Stored eggs are meant to have no Garden position until the player places them.
            if (egg.State != EggState.Stored && Math.Abs(egg.WorldX) < 0.01f && Math.Abs(egg.WorldY) < 0.01f)
            { var p = NestPosition(i); egg.WorldX = p.X; egg.WorldY = p.Y; }
        }
        var slots = GameRules.StoreEggSlotCount;
        if (State.StoreEggs.Count > slots) State.StoreEggs.RemoveRange(slots, State.StoreEggs.Count - slots);
        while (State.StoreEggs.Count < slots) State.StoreEggs.Add(CreateStoreEgg());
        EnsureAngelMutation();
        if (!Save()) StartupNotice = GameSessionStartupNotice.SaveUnavailable;
    }

    private GameStateData CreateFreshState()
    {
        var state = new GameStateData
        {
            SaveVersion = GameStateMigrationService.CurrentSaveVersion, Coins = 120, SeedCounter = DateTime.UtcNow.Ticks,
            MasterVolume = 1.0f, SoundEffectVolume = 1.0f, UiSoundVolume = 1.0f,
            AutoFinishRaces = true, TutorialCompleted = false
        };
        foreach (var statId in GameRules.StatIds) state.TrainingItems[statId] = 1;
        State = state;
        State.Voidlings.Add(CreateStarter("Pip", "#E7A6B6", StarterSpawnPosition(0)));
        State.Voidlings.Add(CreateStarter("Mallow", "#A9D5C0", StarterSpawnPosition(1)));
        EnsureAngelMutation();
        for (var i = 0; i < GameRules.StoreEggSlotCount; i++) State.StoreEggs.Add(CreateStoreEgg());
        State.ShopRareOfferItemId = RareShopOfferResolver.Resolve(unchecked((ulong)State.SeedCounter), GameRules.RareOfferAppearanceChance);
        return State;
    }

    private void EnsureAngelMutation()
    {
        if (State.Voidlings.Concat(State.DepartedVoidlings).Any(v => GameRules.HasMutation(v, GameRules.AngelMutationId)) || State.Voidlings.Count == 0) return;
        var rng = GeneticsService.CreateRandom(unchecked((ulong)State.SeedCounter), "demo:angel-mutation");
        var chosen = State.Voidlings[rng.Next(State.Voidlings.Count)];
        chosen.RareTraits.Add(new RareTraitData { TraitId = GameRules.AngelMutationId, FounderCreatureId = chosen.Id, GenerationFromFounder = 0, CanTransmit = true });
    }

    private VoidlingData CreateStarter(string name, string tint, Vector2 position)
    {
        var seed = NextSeed(); var id = NewId(); var genome = GeneticsService.CreateRandomGenome(seed);
        return new VoidlingData
        {
            Id = id, Name = name, Genome = genome, Stage = LifeStage.Adult, AgeSeconds = GameRules.ChildToAdultSeconds,
            TintHex = tint, RareTraits = GeneticsService.RollFounderTraits(seed, id),
            TrainingPoints = GameRules.StatIds.ToDictionary(stat => stat, _ => 0), WorldX = position.X, WorldY = position.Y
        };
    }

    private static Vector2 NestPosition(int index) => new(315 + (index % 5) * 26, 275 + (index / 5) * 24);
    private EggData CreateStoreEgg() { var seed = NextSeed(); return _shop!.CreateStoreInventoryEgg(NewId(), seed); }
    private void ApplyAudioSettings()
    { _audioSettings!.ApplyMasterVolume(State.MasterVolume); _audioSettings.ApplySoundEffectVolume(State.SoundEffectVolume); _audioSettings.ApplyUiSoundVolume(State.UiSoundVolume); }
    private static Vector2 StarterSpawnPosition(int index)
    {
        var p = new[] { new Vector2(300,185), new Vector2(420,210), new Vector2(250,250), new Vector2(485,160), new Vector2(360,290), new Vector2(530,250) };
        return p[index % p.Length];
    }
    private ulong NextSeed() { State.SeedCounter++; return unchecked((ulong)State.SeedCounter); }
    private static string NewId() => Guid.NewGuid().ToString("N");
    private void SaveAndNotify(string toast) { Save(showFeedback:true); StateChanged?.Invoke(); ToastRequested?.Invoke(toast); }
    private void RaiseGardenEvent(string message) => GardenEventRaised?.Invoke(message);
    private bool Save(bool showFeedback = false)
    {
        try
        {
            _stateRepository!.Save(State); _saveFailureLatched = false;
            if (showFeedback) SaveFeedbackRequested?.Invoke(true);
            return true;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not save MVP state: {exception.Message}");
            if (showFeedback || !_saveFailureLatched) SaveFeedbackRequested?.Invoke(false);
            _saveFailureLatched = true; return false;
        }
    }
}
