using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Application.Racing;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Multiplayer;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Godot presentation shell for the race. All result-affecting state lives in RaceSimulation;
/// this node only maps immutable race-entry data and simulation snapshots to visuals/input.
/// </summary>
public partial class RaceScreen : Node2D
{
    public event Action<int>? RaceCompleted;
    public event Action? ReturnRequested;

    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;
    private const float TrackY = 184.0f;
    private const float TrackTop = 126.0f;
    private const float TrackBottom = 244.0f;
    private const float FlightAltitude = 38.0f;
    private const float JumpDurationSeconds = 0.58f;
    private const int MaxCatchUpStepsPerFrame = 30;

    private static readonly RaceCourse Course = RaceCourse.Demo;
    private static readonly Texture2D WaterTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Tilesets/Water.png");
    private static readonly Texture2D FenceTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Tilesets/Fences.png");

    private static readonly Color RunColor = Color.FromHtml("#78C96A");
    private static readonly Color SwimColor = Color.FromHtml("#F2D45C");
    private static readonly Color FlyColor = Color.FromHtml("#B47AE5");
    private static readonly Color StaminaColor = Color.FromHtml("#F7F3E7");

    private readonly float[] _racerOffsets = { -16.0f, -5.0f, 6.0f, 17.0f };
    private readonly Dictionary<string, RacerVisual> _visuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _cheerParticleAccumulators = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _previousObstacleIndices = new(StringComparer.Ordinal);

    private RaceEntry? _entry;
    private RaceSimulation? _simulation;
    private MultiplayerRacePresentationBridge? _multiplayerBridge;
    private MultiplayerRaceFrameView? _multiplayerFrame;
    private string _multiplayerChallengeId = string.Empty;
    private double _multiplayerTickAccumulator;
    private bool _autoFinish;
    private bool _running;
    private bool _resultsShown;
    private bool _completionReported;
    private string _playerId = "";
    private RacerVisual? _playerVisual;
    private Random _vfxRandom = new(1);
    private Camera2D _camera = null!;
    private Polygon2D _playerMarker = null!;
    private Button _cheerButton = null!;
    private ProgressBar _staminaBar = null!;
    private Label _staminaLabel = null!;
    private Label _sectionLabel = null!;
    private RaceMiniMap _miniMap = null!;

    private sealed class RacerVisual
    {
        public RaceEntrant Entrant { get; init; } = null!;
        public VoidlingVisualAppearance Appearance { get; init; }
        public AnimatedSprite2D Sprite { get; init; } = null!;
        public Polygon2D Shadow { get; init; } = null!;
        public float BaseY { get; init; }
        public float JumpSeconds { get; set; }
        public string VisualMode { get; set; } = "run";
    }

    public void Configure(RaceEntry entry, bool autoFinish)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("RaceScreen must be configured before it enters the scene tree.");
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        if (entry.Entrants.Count == 0)
            throw new ArgumentException("Race entry requires at least one entrant.", nameof(entry));

        _entry = entry;
        _autoFinish = autoFinish;
        _playerId = entry.Entrants[0].Participant.CreatureId;
        _vfxRandom = new Random(unchecked((int)(entry.SimulationSeed ^ 0x51A7E5UL)));
    }

    public void ConfigureMultiplayer(
        ResolvedMultiplayerRace race,
        MultiplayerRacePresentationBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(bridge);
        if (!bridge.TryGetFrame(race.Start.ChallengeId, out var frame))
            throw new InvalidOperationException("Multiplayer race lockstep frame is unavailable at presentation launch.");

        var local = frame.Participants.SingleOrDefault(participant => participant.IsLocal)
            ?? throw new InvalidOperationException("Multiplayer race has no local participant.");
        Configure(race.Entry, autoFinish: false);
        _multiplayerBridge = bridge;
        _multiplayerChallengeId = race.Start.ChallengeId;
        _multiplayerFrame = frame;
        _playerId = local.ParticipantId;
    }

    public override void _Ready()
    {
        if (_entry == null)
            throw new InvalidOperationException("RaceScreen must be configured before AddChild.");

        if (_multiplayerBridge == null)
        {
            _simulation = new RaceSimulation(
                Course,
                _entry.Rules,
                _entry.Entrants.Select(entrant => entrant.Participant).ToArray(),
                _entry.SimulationSeed);
        }

        BuildCoursePresentation();
        CreateEntrantVisuals(_entry.Entrants);
        CreateCamera();
        CreatePlayerMarker();
        CreateHud();

        _running = false;
        QueueRedraw();
        if (_multiplayerFrame != null)
            RenderMultiplayerFrame(_multiplayerFrame, 0.0f);
        else
            SyncVisuals(0.0f);
        UpdatePlayerTracking();
        UpdateHud();
        PlayRaceIntro();
    }

    public override void _Process(double delta)
    {
        if (!_running || _playerVisual == null)
            return;

        if (_multiplayerBridge != null)
        {
            ProcessMultiplayer(delta);
            return;
        }
        if (_simulation == null)
            return;

        ApplySimulationEvents(_simulation.Advance(delta));
        SyncVisuals((float)delta);
        HandleCheerVfx((float)delta);
        UpdatePlayerTracking();
        UpdateHud();
        HandleAutoFinish();

        if (_simulation.IsComplete && !_resultsShown)
        {
            _running = false;
            SyncVisuals(0.0f);
            ShowResults();
        }
    }

    private void ProcessMultiplayer(double delta)
    {
        var bridge = _multiplayerBridge!;
        _multiplayerTickAccumulator += Math.Max(0.0, delta);
        var availableSteps = (int)Math.Floor(_multiplayerTickAccumulator / RaceSimulation.FixedStepSeconds);
        var steps = Math.Min(availableSteps, MaxCatchUpStepsPerFrame);
        if (steps > 0)
        {
            var advanced = bridge.AdvanceFixedSteps(_multiplayerChallengeId, steps);
            if (!advanced.Success)
            {
                _running = false;
                SetSectionLabel(Tr("UI_MP_RACE_SYNC_ERROR"), Color.FromHtml("#9C514B"));
                return;
            }
            _multiplayerTickAccumulator -= steps * RaceSimulation.FixedStepSeconds;
        }

        if (!bridge.TryGetFrame(_multiplayerChallengeId, out var frame))
            return;

        _multiplayerFrame = frame;
        RenderMultiplayerFrame(frame, (float)delta);
        HandleCheerVfx((float)delta);
        UpdatePlayerTracking();
        UpdateHud();

        if (frame.IsComplete && !_resultsShown)
        {
            _running = false;
            RenderMultiplayerFrame(frame, 0.0f);
            ShowResults(frame.Participants
                .Where(participant => participant.Placement.HasValue)
                .OrderBy(participant => participant.Placement)
                .Select(participant => participant.ParticipantId)
                .ToArray());
        }
    }

    public override void _Draw()
    {
        var worldLeft = -ScreenWidth;
        var worldWidth = Course.EndX + ScreenWidth * 2.0f;

        DrawRect(new Rect2(worldLeft, 0, worldWidth, ScreenHeight), Color.FromHtml("#A7D8C7"));
        DrawRect(new Rect2(worldLeft, 102, worldWidth, 166), Color.FromHtml("#8EBE85"));
        DrawRect(new Rect2(worldLeft, TrackTop, worldWidth, TrackBottom - TrackTop), Color.FromHtml("#D9774E"));
        DrawLine(new Vector2(worldLeft, TrackTop), new Vector2(worldLeft + worldWidth, TrackTop), Color.FromHtml("#E9B777"), 4.0f);
        DrawLine(new Vector2(worldLeft, TrackBottom), new Vector2(worldLeft + worldWidth, TrackBottom), Color.FromHtml("#9C6049"), 4.0f);

        for (var x = worldLeft + 40.0f; x < worldLeft + worldWidth; x += 80.0f)
            DrawLine(new Vector2(x, TrackY), new Vector2(x + 28, TrackY), new Color(0.95f, 0.72f, 0.52f, 0.30f), 2.0f);

        DrawRect(new Rect2(Course.EndX - 10, TrackTop - 8, 20, TrackBottom - TrackTop + 16), Color.FromHtml("#F5F0DE"));
        const float square = 10.0f;
        for (var row = 0; row < 13; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                if ((row + col) % 2 == 0)
                    DrawRect(new Rect2(Course.EndX - 10 + col * square, TrackTop - 6 + row * square, square, square), Color.FromHtml("#39423D"));
            }
        }
        DrawLine(new Vector2(Course.EndX - 14, TrackTop - 10), new Vector2(Course.EndX - 14, TrackBottom + 10), Color.FromHtml("#68584B"), 4.0f);
        DrawLine(new Vector2(Course.EndX + 14, TrackTop - 10), new Vector2(Course.EndX + 14, TrackBottom + 10), Color.FromHtml("#68584B"), 4.0f);
    }

    private void BuildCoursePresentation()
    {
        var swim = Course.Segments.Single(segment => segment.Kind == RaceSegmentKind.Swim);
        var glide = Course.GlideSegment;

        AddWaterSection(swim.StartX, swim.EndX);
        AddWaterSection(glide.StartX, glide.EndX);
        AddFlightRamp();

        foreach (var obstacleX in Course.Obstacles)
            AddHurdle(obstacleX + 18.0f);

        AddWorldLabel("SWIM", (swim.StartX + swim.EndX) * 0.5f, 108, SwimColor);
        AddWorldLabel("GLIDE / SWIM", (glide.StartX + glide.EndX) * 0.5f, 108, FlyColor);
    }

    private void CreateEntrantVisuals(IReadOnlyList<RaceEntrant> entrants)
    {
        for (var i = 0; i < entrants.Count; i++)
        {
            var entrant = entrants[i];
            var appearance = AppearanceFor(entrant.Participant);
            var visualTypeId = appearance.VisualTypeId;
            var baseY = TrackY + _racerOffsets[Math.Min(i, _racerOffsets.Length - 1)];
            var raceScale = VoidlingVisualFactory.RaceScaleFor(visualTypeId);

            var shadow = new Polygon2D
            {
                Polygon = VoidlingVisualFactory.BuildShadowPolygon(raceScale, 18, visualTypeId),
                Color = new Color(0.15f, 0.18f, 0.16f, 0.34f),
                Position = new Vector2(
                    Course.StartX,
                    baseY + VoidlingVisualFactory.ShadowCenterYOffsetFor(visualTypeId)),
                ZIndex = 7 + i
            };
            AddChild(shadow);

            var sprite = new AnimatedSprite2D
            {
                Position = new Vector2(
                    Course.StartX,
                    baseY + VoidlingVisualFactory.RaceSpriteCenterYOffset(visualTypeId)),
                Scale = Vector2.One * raceScale,
                ZIndex = 10 + i
            };
            VoidlingVisualFactory.ApplyAppearance(sprite, appearance, race: true);
            AddChild(sprite);
            sprite.Play("run");

            var mutationAdornment = new MutationAdornment2D();
            mutationAdornment.Setup(
                entrant.HasAngelMutation,
                entrant.OtherMutationCount,
                sprite,
                visualTypeId);
            AddChild(mutationAdornment);

            var visual = new RacerVisual
            {
                Entrant = entrant,
                Appearance = appearance,
                Sprite = sprite,
                Shadow = shadow,
                BaseY = baseY
            };
            _visuals.Add(entrant.Participant.CreatureId, visual);

            if (entrant.Participant.CreatureId == _playerId)
                _playerVisual = visual;
        }
    }

    private void ApplySimulationEvents(IReadOnlyList<RaceSimulationEvent> events)
    {
        foreach (var simulationEvent in events)
        {
            switch (simulationEvent)
            {
                case RaceObstacleResolvedEvent obstacle:
                    if (_visuals.TryGetValue(obstacle.ParticipantId, out var visual))
                        visual.JumpSeconds = JumpDurationSeconds;
                    break;
                case RaceParticipantFinishedEvent finished:
                    if (_visuals.TryGetValue(finished.ParticipantId, out var finisher))
                        finisher.Sprite.Stop();
                    break;
            }
        }
    }

    private void HandleAutoFinish()
    {
        if (!_autoFinish || _simulation == null || _simulation.IsComplete)
            return;

        var playerState = _simulation.GetState(_playerId);
        if (playerState.Finished)
        {
            ApplySimulationEvents(_simulation.FastForwardToFinish());
            SyncVisuals(0.0f);
            return;
        }

        var allCpuFinished = _entry!.Entrants
            .Skip(1)
            .All(entrant => _simulation.GetState(entrant.Participant.CreatureId).Finished);
        if (!allCpuFinished)
            return;

        var forcedFinish = _simulation.CompleteParticipantAsLast(_playerId);
        if (forcedFinish != null)
            ApplySimulationEvents(new RaceSimulationEvent[] { forcedFinish });
        SyncVisuals(0.0f);
    }

    private void SyncVisuals(float delta)
    {
        if (_simulation == null)
            return;

        foreach (var pair in _visuals)
        {
            var state = _simulation.GetState(pair.Key);
            UpdateRacerVisual(pair.Value, state, delta);
        }
    }

    private void RenderMultiplayerFrame(MultiplayerRaceFrameView frame, float delta)
    {
        var entrants = _entry!.Entrants.ToDictionary(
            entrant => entrant.Participant.CreatureId,
            StringComparer.Ordinal);
        foreach (var participant in frame.Participants)
        {
            if (!_visuals.TryGetValue(participant.ParticipantId, out var visual) ||
                !entrants.TryGetValue(participant.ParticipantId, out var entrant))
            {
                continue;
            }

            var previousObstacleIndex = _previousObstacleIndices.GetValueOrDefault(
                participant.ParticipantId,
                participant.NextObstacleIndex);
            if (participant.NextObstacleIndex > previousObstacleIndex && !participant.Finished)
                visual.JumpSeconds = JumpDurationSeconds;
            _previousObstacleIndices[participant.ParticipantId] = participant.NextObstacleIndex;

            UpdateRacerVisual(visual, new RaceParticipantStateSnapshot(
                entrant.Participant,
                participant.X,
                participant.MaxStamina,
                participant.CurrentStamina,
                participant.DelaySeconds,
                participant.CheerSeconds,
                participant.Terrain == RaceTerrain.Glide,
                participant.Terrain == RaceTerrain.FailedGlideSwim,
                participant.GlideEndX,
                participant.NextObstacleIndex,
                participant.Finished,
                participant.Terrain), delta);
        }
    }

    private void UpdateRacerVisual(RacerVisual visual, RaceParticipantStateSnapshot state, float delta)
    {
        if (visual.JumpSeconds > 0.0f)
            visual.JumpSeconds = Math.Max(0.0f, visual.JumpSeconds - delta);

        if (state.Finished)
            visual.Sprite.Stop();
        else if (state.Terrain is RaceTerrain.Swim or RaceTerrain.FailedGlideSwim)
            SetVisualMode(visual, "swim");
        else if (state.Terrain == RaceTerrain.Glide)
            SetVisualMode(visual, "glide");
        else
            SetVisualMode(visual, "run");

        var yOffset = 0.0f;
        var shadowScale = Vector2.One;
        var shadowAlpha = 0.34f;

        if (visual.JumpSeconds > 0.0f)
        {
            var normalized = 1.0f - visual.JumpSeconds / JumpDurationSeconds;
            yOffset = -Mathf.Sin(normalized * Mathf.Pi) * 17.0f;
            shadowScale = new Vector2(0.75f, 0.75f);
        }
        else if (state.Terrain is RaceTerrain.Swim or RaceTerrain.FailedGlideSwim)
        {
            yOffset = 7.0f + Mathf.Sin((float)Time.GetTicksMsec() / 150.0f + visual.BaseY) * 2.0f;
            shadowScale = new Vector2(0.45f, 0.45f);
            shadowAlpha = 0.15f;
            visual.Sprite.Rotation = 0.0f;
        }
        else if (InLaunchRamp(state.X))
        {
            var progress = Mathf.Clamp(
                (state.X - Course.GlideLaunchStartX) / (Course.GlideSegment.StartX - Course.GlideLaunchStartX),
                0.0f,
                1.0f);
            progress = progress * progress * (3.0f - 2.0f * progress);
            yOffset = -FlightAltitude * progress;
            visual.Sprite.Rotation = -0.09f * progress;
            shadowScale = Vector2.One.Lerp(new Vector2(0.52f, 0.52f), progress);
            shadowAlpha = Mathf.Lerp(0.34f, 0.14f, progress);
        }
        else if (state.Terrain == RaceTerrain.Glide)
        {
            var glideSpan = Math.Max(1.0f, state.GlideEndX - Course.GlideSegment.StartX);
            var glideProgress = Mathf.Clamp((state.X - Course.GlideSegment.StartX) / glideSpan, 0.0f, 1.0f);
            var easedDescent = Mathf.Pow(glideProgress, 1.35f);
            var fallsIntoWater = state.GlideEndX < Course.GlideSegment.EndX - 1.0f;
            var destinationY = fallsIntoWater ? 7.0f : 0.0f;
            yOffset = Mathf.Lerp(-FlightAltitude, destinationY, easedDescent) +
                      Mathf.Sin((float)Time.GetTicksMsec() / 210.0f + visual.BaseY) * 1.2f * (1.0f - glideProgress);
            visual.Sprite.Rotation = Mathf.Lerp(-0.08f, 0.08f, glideProgress);
            var destinationScale = fallsIntoWater ? 0.45f : 0.82f;
            shadowScale = new Vector2(
                Mathf.Lerp(0.48f, destinationScale, glideProgress),
                Mathf.Lerp(0.48f, destinationScale, glideProgress));
            shadowAlpha = Mathf.Lerp(0.11f, fallsIntoWater ? 0.15f : 0.26f, glideProgress);
        }
        else
        {
            visual.Sprite.Rotation = 0.0f;
        }

        var visualTypeId = visual.Appearance.VisualTypeId;
        visual.Sprite.Position = new Vector2(
            state.X,
            visual.BaseY + VoidlingVisualFactory.RaceSpriteCenterYOffset(visualTypeId) + yOffset);
        visual.Shadow.Position = new Vector2(
            state.X,
            visual.BaseY + VoidlingVisualFactory.ShadowCenterYOffsetFor(visualTypeId));
        visual.Shadow.Scale = shadowScale;
        var shadowColor = visual.Shadow.Color;
        shadowColor.A = shadowAlpha;
        visual.Shadow.Color = shadowColor;
    }

    private void CreateCamera()
    {
        _camera = new Camera2D
        {
            Enabled = true,
            Position = new Vector2(Course.StartX, ScreenHeight * 0.5f),
            Zoom = Vector2.One,
            PositionSmoothingEnabled = false
        };
        AddChild(_camera);
    }

    private void CreatePlayerMarker()
    {
        _playerMarker = new Polygon2D
        {
            Polygon = new Vector2[] { new(-7, -9), new(7, -9), new(0, 0) },
            Color = Color.FromHtml("#FFF26E"),
            ZIndex = 40
        };
        AddChild(_playerMarker);
    }

    private void CreateHud()
    {
        var canvas = new CanvasLayer { Layer = 20 };
        AddChild(canvas);

        var title = UiFactory.CreateTitle("SPROUT RUN");
        title.Position = new Vector2(255, 8);
        title.Size = new Vector2(180, 24);
        canvas.AddChild(title);

        _sectionLabel = UiFactory.CreateLabel("RUN", 8);
        _sectionLabel.Position = new Vector2(292, 34);
        _sectionLabel.Size = new Vector2(120, 18);
        _sectionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        canvas.AddChild(_sectionLabel);

        var cheerPanel = UiFactory.CreatePanel(new Vector2(228, 70));
        cheerPanel.Position = new Vector2(14, 276);
        cheerPanel.Size = new Vector2(228, 70);
        canvas.AddChild(cheerPanel);

        var cheerBox = new VBoxContainer();
        cheerBox.AddThemeConstantOverride("separation", 3);
        cheerPanel.AddChild(cheerBox);

        var cheerRow = new HBoxContainer();
        cheerRow.AddThemeConstantOverride("separation", 6);
        _cheerButton = UiFactory.CreateButton("CHEER!");
        _cheerButton.CustomMinimumSize = new Vector2(88, 27);
        _cheerButton.Pressed += CheerPlayer;
        cheerRow.AddChild(_cheerButton);
        _staminaLabel = UiFactory.CreateLabel("STAMINA", 7);
        _staminaLabel.VerticalAlignment = VerticalAlignment.Center;
        cheerRow.AddChild(_staminaLabel);
        cheerBox.AddChild(cheerRow);

        _staminaBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(196, 16)
        };
        UiFactory.ApplyPixelFont(_staminaBar, 7);
        var background = new StyleBoxFlat { BgColor = Color.FromHtml("#66594C") };
        background.CornerRadiusTopLeft = background.CornerRadiusTopRight = 2;
        background.CornerRadiusBottomLeft = background.CornerRadiusBottomRight = 2;
        var fill = new StyleBoxFlat { BgColor = StaminaColor };
        fill.CornerRadiusTopLeft = fill.CornerRadiusTopRight = 2;
        fill.CornerRadiusBottomLeft = fill.CornerRadiusBottomRight = 2;
        _staminaBar.AddThemeStyleboxOverride("background", background);
        _staminaBar.AddThemeStyleboxOverride("fill", fill);
        cheerBox.AddChild(_staminaBar);

        var mapPanel = UiFactory.CreatePanel(new Vector2(205, 64));
        mapPanel.Position = new Vector2(421, 282);
        mapPanel.Size = new Vector2(205, 64);
        canvas.AddChild(mapPanel);

        _miniMap = new RaceMiniMap { CustomMinimumSize = new Vector2(181, 40) };
        mapPanel.AddChild(_miniMap);
    }

    private void CheerPlayer()
    {
        if (!_running)
            return;

        if (_multiplayerBridge != null)
        {
            var result = _multiplayerBridge.RequestCheer(_multiplayerChallengeId);
            if (!result.Success)
            {
                SetSectionLabel(result.Error ?? Tr("UI_MP_RACE_CHEER_FAILED"), Color.FromHtml("#9C514B"));
                return;
            }
        }
        else if (_simulation == null || !_simulation.TryCheer(_playerId))
        {
            return;
        }

        if (TryGetPlayerState(out var playerState) && _playerVisual != null)
        {
            _cheerParticleAccumulators[_playerId] = 0.0f;
            SpawnCheerSpeedParticle(playerState, _playerVisual);
        }
        UpdateHud();
    }

    private void HandleCheerVfx(float delta)
    {
        if (_entry == null)
            return;

        foreach (var entrant in _entry.Entrants)
        {
            var id = entrant.Participant.CreatureId;
            var state = GetParticipantState(entrant);
            if (state.CheerSeconds <= 0.0f || state.Finished || !_visuals.TryGetValue(id, out var visual))
            {
                _cheerParticleAccumulators[id] = 0.0f;
                continue;
            }

            var accumulator = _cheerParticleAccumulators.GetValueOrDefault(id) + delta;
            while (accumulator >= 0.075f)
            {
                accumulator -= 0.075f;
                SpawnCheerSpeedParticle(state, visual);
            }
            _cheerParticleAccumulators[id] = accumulator;
        }
    }

    private void SpawnCheerSpeedParticle(
        RaceParticipantStateSnapshot state,
        RacerVisual visual)
    {
        var yJitter = (float)(_vfxRandom.NextDouble() * 18.0 - 9.0);
        var length = 9.0f + (float)_vfxRandom.NextDouble() * 8.0f;
        var streak = new Line2D
        {
            Width = 1.7f,
            DefaultColor = Color.FromHtml("#FFF0A1"),
            ZIndex = 8,
            Points = new[]
            {
                new Vector2(state.X - 13.0f - length, visual.Sprite.Position.Y + 8.0f + yJitter),
                new Vector2(state.X - 13.0f, visual.Sprite.Position.Y + 8.0f + yJitter)
            }
        };
        AddChild(streak);

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(streak, "position:x", -20.0f, 0.28)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(streak, "modulate:a", 0.0f, 0.28);
        tween.Finished += streak.QueueFree;
    }

    private void UpdateHud()
    {
        if (_entry == null || _miniMap == null || !TryGetPlayerState(out var player))
            return;

        _staminaBar.MaxValue = player.MaxStamina;
        _staminaBar.Value = player.CurrentStamina;
        _staminaLabel.Text = $"STAMINA {Mathf.CeilToInt(player.CurrentStamina)} / {Mathf.CeilToInt(player.MaxStamina)}";
        _cheerButton.Disabled = !_running || player.Finished || player.CheerSeconds > 0.0f || player.CurrentStamina < _entry.Rules.CheerCost;
        _cheerButton.Text = player.CheerSeconds > 0.0f ? "CHEERING!" : "CHEER!";

        if (player.Terrain == RaceTerrain.Swim)
        {
            SetSectionLabel("SWIM", SwimColor);
        }
        else if (player.Terrain == RaceTerrain.FailedGlideSwim)
        {
            SetSectionLabel("SWIM", SwimColor);
        }
        else if (player.Terrain == RaceTerrain.Glide)
        {
            SetSectionLabel("GLIDE", FlyColor);
        }
        else if (InLaunchRamp(player.X))
        {
            SetSectionLabel("TAKEOFF", FlyColor);
        }
        else
        {
            SetSectionLabel("RUN", RunColor);
        }

        var points = _entry.Entrants.Select(entrant =>
        {
            var state = GetParticipantState(entrant);
            return new RaceMiniMapPoint
            {
                Id = entrant.Participant.CreatureId,
                Color = ParseTint(entrant.Participant.TintHex),
                Progress = Mathf.Clamp((state.X - Course.StartX) / (Course.EndX - Course.StartX), 0.0f, 1.0f),
                IsPlayer = entrant.Participant.CreatureId == _playerId
            };
        }).ToList();
        _miniMap.SetPoints(points);
    }

    private RaceParticipantStateSnapshot GetParticipantState(RaceEntrant entrant)
    {
        if (_simulation != null)
            return _simulation.GetState(entrant.Participant.CreatureId);

        var participant = _multiplayerFrame!.Participants.Single(value =>
            string.Equals(value.ParticipantId, entrant.Participant.CreatureId, StringComparison.Ordinal));
        return new RaceParticipantStateSnapshot(
            entrant.Participant,
            participant.X,
            participant.MaxStamina,
            participant.CurrentStamina,
            participant.DelaySeconds,
            participant.CheerSeconds,
            participant.Terrain == RaceTerrain.Glide,
            participant.Terrain == RaceTerrain.FailedGlideSwim,
            participant.GlideEndX,
            participant.NextObstacleIndex,
            participant.Finished,
            participant.Terrain);
    }

    private bool TryGetPlayerState(out RaceParticipantStateSnapshot state)
    {
        state = default;
        if (_entry == null)
            return false;
        var entrant = _entry.Entrants.FirstOrDefault(value =>
            string.Equals(value.Participant.CreatureId, _playerId, StringComparison.Ordinal));
        if (entrant == null || (_simulation == null && _multiplayerFrame == null))
            return false;
        state = GetParticipantState(entrant);
        return true;
    }

    private void SetSectionLabel(string text, Color color)
    {
        _sectionLabel.Text = text;
        _sectionLabel.AddThemeColorOverride("font_color", color);
    }

    private void UpdatePlayerTracking()
    {
        if (_playerVisual == null || !TryGetPlayerState(out var player))
            return;

        _camera.Zoom = Vector2.One;
        _camera.Position = new Vector2(player.X, ScreenHeight * 0.5f);
        _playerMarker.Position = new Vector2(player.X, _playerVisual.Sprite.Position.Y - 21.0f);
    }

    private void ShowResults(IReadOnlyList<string>? multiplayerFinishOrder = null)
    {
        if (_entry == null || (_simulation == null && multiplayerFinishOrder == null))
            return;

        _resultsShown = true;
        var finishOrder = multiplayerFinishOrder ?? _simulation!.FinishOrder;
        var selectedPlace = finishOrder.ToList().IndexOf(_playerId) + 1;
        if (selectedPlace <= 0)
            selectedPlace = _entry.Entrants.Count;

        if (!_completionReported)
        {
            _completionReported = true;
            RaceCompleted?.Invoke(selectedPlace);
        }

        var byId = _entry.Entrants.ToDictionary(entrant => entrant.Participant.CreatureId, StringComparer.Ordinal);
        var finishers = finishOrder.Select(id => byId[id]).ToList();

        var canvas = new CanvasLayer { Layer = 50 };
        AddChild(canvas);
        var shade = new ColorRect
        {
            Color = new Color(0.12f, 0.18f, 0.16f, 0.55f),
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        canvas.AddChild(shade);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(center);

        var panel = UiFactory.CreatePanel(new Vector2(520, 292));
        center.AddChild(panel);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        var resultTitle = UiFactory.CreateTitle($"RACE RESULTS — YOU #{selectedPlace}");
        resultTitle.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(resultTitle);

        var stage = new Control { CustomMinimumSize = new Vector2(480, 192) };
        box.AddChild(stage);

        if (finishers.Count >= 2)
            AddPodiumSlot(stage, finishers[1], 2, new Vector2(55, 105), new Vector2(100, 67));
        if (finishers.Count >= 1)
            AddPodiumSlot(stage, finishers[0], 1, new Vector2(181, 72), new Vector2(112, 100));
        if (finishers.Count >= 3)
            AddPodiumSlot(stage, finishers[2], 3, new Vector2(319, 120), new Vector2(92, 52));
        if (finishers.Count >= 4)
            AddFourthPlacePuddle(stage, finishers[3], new Vector2(428, 112));

        var button = UiFactory.CreateButton("Return to Garden");
        button.CustomMinimumSize = new Vector2(160, 25);
        button.Pressed += () => ReturnRequested?.Invoke();
        box.AddChild(button);
    }

    private static void AddPodiumSlot(Control stage, RaceEntrant entrant, int place, Vector2 blockPosition, Vector2 blockSize)
    {
        var block = new PanelContainer { Position = blockPosition, Size = blockSize };
        var style = new StyleBoxFlat
        {
            BgColor = place == 1 ? Color.FromHtml("#EBCB63") : place == 2 ? Color.FromHtml("#CDD0C8") : Color.FromHtml("#C28B5D"),
            BorderColor = Color.FromHtml("#7E6856")
        };
        style.SetBorderWidthAll(2);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 3;
        block.AddThemeStyleboxOverride("panel", style);
        stage.AddChild(block);

        var placeLabel = UiFactory.CreateLabel(place.ToString(), 14);
        placeLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        placeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        placeLabel.VerticalAlignment = VerticalAlignment.Center;
        block.AddChild(placeLabel);

        var portraitX = blockPosition.X + (blockSize.X - 48.0f) * 0.5f;
        var portraitY = blockPosition.Y - 49.0f;
        var portrait = CreateEntrantPortrait(entrant, new Vector2(48, 48));
        portrait.Position = new Vector2(portraitX, portraitY);
        portrait.Size = new Vector2(48, 48);
        stage.AddChild(portrait);

        var name = UiFactory.CreateLabel(entrant.Participant.DisplayName, 6);
        name.Position = new Vector2(blockPosition.X + (blockSize.X - 100.0f) * 0.5f, portraitY - 15.0f);
        name.Size = new Vector2(100, 14);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        stage.AddChild(name);
    }

    private static void AddFourthPlacePuddle(Control stage, RaceEntrant entrant, Vector2 position)
    {
        var puddle = new PanelContainer
        {
            Position = new Vector2(position.X - 8, position.Y + 41),
            Size = new Vector2(62, 14)
        };
        var puddleStyle = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#7FC5C8"),
            BorderColor = Color.FromHtml("#5A9DA6")
        };
        puddleStyle.SetBorderWidthAll(1);
        puddleStyle.CornerRadiusTopLeft = puddleStyle.CornerRadiusTopRight = 8;
        puddleStyle.CornerRadiusBottomLeft = puddleStyle.CornerRadiusBottomRight = 8;
        puddle.AddThemeStyleboxOverride("panel", puddleStyle);
        stage.AddChild(puddle);

        var portrait = CreateEntrantPortrait(entrant, new Vector2(48, 48));
        portrait.Position = position;
        portrait.Size = new Vector2(48, 48);
        stage.AddChild(portrait);

        var name = UiFactory.CreateLabel($"4th • {entrant.Participant.DisplayName}", 6);
        name.Position = new Vector2(position.X - 18, position.Y - 17);
        name.Size = new Vector2(84, 14);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        stage.AddChild(name);
    }

    private static TextureRect CreateEntrantPortrait(RaceEntrant entrant, Vector2 size)
        => UiFactory.CreatePortrait(
            AppearanceFor(entrant.Participant),
            entrant.HasAngelMutation,
            entrant.OtherMutationCount,
            size);

    private void AddWaterSection(float startX, float endX)
    {
        var tile = new AtlasTexture
        {
            Atlas = WaterTexture,
            Region = new Rect2(0, 0, 16, 16)
        };

        for (var x = startX + 8.0f; x < endX; x += 16.0f)
        {
            for (var y = TrackTop + 8.0f; y < TrackBottom; y += 16.0f)
            {
                AddChild(new Sprite2D
                {
                    Texture = tile,
                    Position = new Vector2(x, y),
                    ZIndex = 2
                });
            }
        }
    }

    private void AddFlightRamp()
    {
        const float laneBandHeight = 28.0f;
        var width = Course.GlideSegment.StartX - Course.GlideLaunchStartX;
        var centerX = (Course.GlideLaunchStartX + Course.GlideSegment.StartX) * 0.5f;

        for (var y = TrackTop; y < TrackBottom; y += laneBandHeight)
        {
            var bandHeight = Math.Min(laneBandHeight, TrackBottom - y);
            var ramp = new Polygon2D
            {
                Polygon = new[]
                {
                    new Vector2(-width * 0.5f, bandHeight * 0.45f),
                    new Vector2(width * 0.5f, -bandHeight * 0.45f),
                    new Vector2(width * 0.5f, bandHeight * 0.5f),
                    new Vector2(-width * 0.5f, bandHeight * 0.5f)
                },
                Color = Color.FromHtml("#D99B63"),
                Position = new Vector2(centerX, y + bandHeight * 0.5f),
                ZIndex = 4
            };
            AddChild(ramp);

            var edge = new Line2D
            {
                Width = 2.0f,
                DefaultColor = Color.FromHtml("#8D654F"),
                Points = new[]
                {
                    new Vector2(-width * 0.5f, bandHeight * 0.45f),
                    new Vector2(width * 0.5f, -bandHeight * 0.45f)
                },
                ZIndex = 5
            };
            ramp.AddChild(edge);
        }
    }

    private void AddHurdle(float x)
    {
        var tile = new AtlasTexture
        {
            Atlas = FenceTexture,
            Region = new Rect2(0, 0, 16, 16)
        };

        for (var y = TrackTop + 9.0f; y < TrackBottom; y += 18.0f)
        {
            AddChild(new Sprite2D
            {
                Texture = tile,
                Position = new Vector2(x, y),
                Scale = new Vector2(1.15f, 1.15f),
                ZIndex = 6
            });
        }
    }

    private void AddWorldLabel(string text, float x, float y, Color color)
    {
        var label = UiFactory.CreateLabel(text, 8);
        label.Position = new Vector2(x - 45, y);
        label.Size = new Vector2(90, 16);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        label.AddThemeConstantOverride("outline_size", 1);
        label.ZIndex = 4;
        AddChild(label);
    }

    private static void SetVisualMode(RacerVisual visual, string mode)
    {
        if (visual.VisualMode == mode)
            return;

        visual.VisualMode = mode;
        visual.Sprite.Rotation = 0.0f;
        switch (mode)
        {
            case "swim":
                visual.Sprite.Play("swim");
                visual.Sprite.SpeedScale = 1.0f;
                break;
            case "glide":
                visual.Sprite.Play("run");
                visual.Sprite.SpeedScale = 0.38f;
                break;
            default:
                visual.Sprite.Play("run");
                visual.Sprite.SpeedScale = 1.0f;
                break;
        }
    }

    private static VoidlingVisualAppearance AppearanceFor(RaceParticipantSnapshot participant)
        => new(
            participant.VisualTypeId,
            participant.PaletteHue,
            participant.LayerIds,
            participant.TintHex);

    private static bool InLaunchRamp(float x)
        => x >= Course.GlideLaunchStartX && x < Course.GlideSegment.StartX;

    private static Color ParseTint(string html)
        => string.IsNullOrWhiteSpace(html) ? Colors.White : Color.FromHtml(html);
}
