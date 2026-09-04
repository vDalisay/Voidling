using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Application.Racing;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Common;
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

    /// <summary>True once the results overlay has been built. Read by the CI completion probe.</summary>
    internal bool ResultsShown => _resultsShown;

    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;
    private const float TrackY = 184.0f;
    private const float TrackTop = 126.0f;
    private const float TrackBottom = 244.0f;
    private const float FlightAltitude = 38.0f;
    private const float JumpMinPeak = 15.0f;
    private const float JumpMaxPeak = 31.0f;
    private const float JumpMinDurationSeconds = 0.50f;
    private const float JumpMaxDurationSeconds = 0.74f;

    /// <summary>A refused hurdle: a real attempt, but a short scuffed hop that does not clear it.</summary>
    private const float FailedJumpPeak = 7.0f;
    private const float FailedJumpDurationSeconds = 0.30f;

    /// <summary>How long a racer spends backing up to line the hurdle up again.</summary>
    private const float RetreatSeconds = 0.42f;

    /// <summary>
    /// Forward travel between dust clouds. Distance, not time: the simulation is fixed-step, so a
    /// racer's X only changes on some frames, and a time-based trail had its accumulator reset by
    /// every frame in between and so never fired outside the slow walk back from a refused hurdle.
    /// </summary>
    private const float DustSpacingPixels = 7.0f;

    /// <summary>Racers coast a random distance past the line rather than all stopping on it.</summary>
    private const float FinishOverrunMin = 22.0f;
    private const float FinishOverrunMax = 66.0f;
    private const float FinishCoastSeconds = 0.85f;

    /// <summary>
    /// Camera zoom range. One is the framing the race has always used and stays the furthest out,
    /// so zooming can only ever bring the player closer and never reveals more than the track art
    /// is drawn for.
    /// </summary>
    private const float MinZoom = 1.0f;
    private const float MaxZoom = 3.2f;
    private const float ZoomStepFactor = 1.18f;
    private const int MaxCatchUpStepsPerFrame = 30;

    /// <summary>Canvas layer the results overlay lives on. The CI completion probe looks for it.</summary>
    internal const int ResultsCanvasLayer = 50;

    private RaceCourse Course => _entry?.CourseDefinition.Course ?? RaceCourse.Demo;

    private static RaceTrackLayout Layout => new(TrackTop, TrackBottom, ScreenWidth, ScreenHeight, RaceTrackArt.ClimbHeight);

    /// <summary>
    /// The river's own colour, drawn over a swimmer's submerged body. Matching the water exactly is
    /// what keeps the overlay from reading as a tinted box floating on the surface.
    /// </summary>
    private static readonly Color SubmergedWater = new(0.608f, 0.831f, 0.765f, 0.56f);
    private static readonly Color WakeRipple = new(1.0f, 1.0f, 1.0f, 0.42f);

    private static readonly Color StaminaColor = Color.FromHtml("#F7F3E7");

    // Lanes spread across the full dirt band. The old 33px cluster stacked four racers on top of
    // each other on the start line and left most of the track empty.
    private readonly float[] _racerOffsets = { -30.0f, -10.0f, 10.0f, 30.0f };
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
    private bool _pausedRunning;
    private bool _resultsShown;
    private bool _completionReported;
    private string _playerId = "";
    private string? _firstFinisherId;
    private RacerVisual? _playerVisual;
    private Random _vfxRandom = new(1);
    private float _waterPhase;
    private float _zoom = MinZoom;
    private float _zoomTarget = MinZoom;
    private Camera2D _camera = null!;
    private Polygon2D _playerMarker = null!;
    private Button _cheerButton = null!;
    private ProgressBar _staminaBar = null!;
    private Label _staminaLabel = null!;
    private Label _faultLabel = null!;
    private ColorRect _faultPlaque = null!;
    private RaceMiniMap _miniMap = null!;

    private sealed class RacerVisual
    {
        public RaceEntrant Entrant { get; init; } = null!;
        public VoidlingVisualAppearance Appearance { get; init; }
        public AnimatedSprite2D Sprite { get; init; } = null!;
        public Polygon2D Shadow { get; init; } = null!;
        public float BaseY { get; init; }
        public float JumpSeconds { get; set; }
        public float JumpDuration { get; set; } = JumpMaxDurationSeconds;
        public float JumpPeak { get; set; } = JumpMaxPeak;

        /// <summary>Counts down through the scuffed hop and the walk back after a refused hurdle.</summary>
        public float RecoverySeconds { get; set; }
        public float RetreatFromX { get; set; }

        /// <summary>Water drawn over the submerged part of the body while swimming.</summary>
        public Polygon2D Submersion { get; init; } = null!;
        public float DustDistance { get; set; }

        /// <summary>How far past the line this racer coasts, and how far through that coast it is.</summary>
        public bool Finished { get; set; }
        public float FinishOverrun { get; set; }
        public float FinishSeconds { get; set; }
        public bool Celebrates { get; set; }
        public float WindSeconds { get; set; }
        public float LastX { get; set; }
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

        // Tiled DrawTextureRect needs repeat sampling on this canvas item.
        TextureRepeat = TextureRepeatEnum.Enabled;

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
        // The stream keeps flowing, and the camera keeps answering the wheel, on the start line,
        // during the opening flyover and on the results screen.
        _waterPhase = (_waterPhase + (float)delta * 2.2f) % 1.0f;
        UpdateZoom((float)delta);
        QueueRedraw();

        // Keep the framing on the player between the countdown and the podium too, so a zoom taken
        // on the start line or after the finish is centred on the same Voidling. The flyover owns
        // the camera while it is running.
        if (!_running && !_flyoverRunning)
            UpdatePlayerTracking();

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
                ShowFault(Tr("UI_MP_RACE_SYNC_ERROR"));
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
        => RaceTrackArt.Paint(this, Course, Layout, _waterPhase);

    /// <summary>
    /// What a segment kind looks like on the track. Ground needs no extra geometry because the track
    /// itself is the ground surface; every other kind must name the geometry it adds.
    /// </summary>
    internal readonly record struct SegmentVisual(bool Water, bool Climb, bool Ramp);

    /// <summary>
    /// The single declaration of race terrain geometry. BuildCoursePresentation renders from it and
    /// RacePresentationSmokeProbe asserts every authored segment kind resolves here, so a kind can
    /// never silently render as bare ground the way Climb did.
    /// </summary>
    internal static SegmentVisual VisualFor(RaceSegmentKind kind) => kind switch
    {
        RaceSegmentKind.Ground => new SegmentVisual(false, false, false),
        RaceSegmentKind.Swim => new SegmentVisual(true, false, false),
        RaceSegmentKind.Climb => new SegmentVisual(false, true, false),
        RaceSegmentKind.Glide => new SegmentVisual(true, false, true),
        _ => throw new InvalidOperationException($"Race segment kind '{kind}' has no track presentation.")
    };

    private void BuildCoursePresentation()
    {
        // Terrain is painted in _Draw by RaceTrackArt. The HUD names the section the player is in,
        // so the track itself carries no signposting.
        foreach (var obstacleX in Course.Obstacles)
            AddHurdle(obstacleX + 18.0f);
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
                    baseY + VoidlingVisualFactory.ShadowCenterYOffset(raceScale, visualTypeId)),
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

            // Drawn over the sprite, inside it, so only what is above the waterline stays visible.
            // Frame-local units, so it follows the sprite's own scale and any art revision with it.
            var submersion = new Polygon2D
            {
                Polygon = BuildSubmersionPolygon(),
                Color = SubmergedWater,
                ZIndex = 5,
                Visible = false
            };
            submersion.AddChild(new Line2D
            {
                Points = BuildRipplePolygon(),
                Closed = true,
                Width = 1.4f,
                DefaultColor = WakeRipple
            });
            sprite.AddChild(submersion);

            var visual = new RacerVisual
            {
                Entrant = entrant,
                Appearance = appearance,
                Sprite = sprite,
                Shadow = shadow,
                BaseY = baseY,
                Submersion = submersion,
                LastX = Course.StartX
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
                        StartJump(visual, obstacle);
                    break;
                case RaceParticipantFinishedEvent finished:
                    // The coast past the line and any celebration are driven per frame, so the
                    // sprite is not stopped here.
                    _ = finished;
                    break;
            }
        }
    }

    /// <summary>
    /// Starts the hurdle animation for one resolved obstacle.
    ///
    /// A cleared hurdle varies in height and hang time so four racers do not jump in lockstep; the
    /// variation is a hash of who jumped and which hurdle, never the VFX random stream, so a replay
    /// of the same race jumps the same way. A refused hurdle is a short scuffed hop followed by the
    /// racer backing up to line the jump up again, which is the rollback the simulation already
    /// applied being shown rather than teleported through.
    /// </summary>
    private static void StartJump(RacerVisual visual, RaceObstacleResolvedEvent obstacle)
    {
        if (!obstacle.Avoided)
        {
            visual.JumpPeak = FailedJumpPeak;
            visual.JumpDuration = FailedJumpDurationSeconds;
            visual.JumpSeconds = FailedJumpDurationSeconds;
            visual.RetreatFromX = visual.Sprite.Position.X;
            visual.RecoverySeconds = FailedJumpDurationSeconds + RetreatSeconds;
            return;
        }

        var variation = JumpVariation(obstacle.ParticipantId, obstacle.ObstacleIndex);
        visual.JumpPeak = Mathf.Lerp(JumpMinPeak, JumpMaxPeak, variation);
        visual.JumpDuration = Mathf.Lerp(JumpMinDurationSeconds, JumpMaxDurationSeconds, variation);
        visual.JumpSeconds = visual.JumpDuration;
        visual.RecoverySeconds = 0.0f;
    }

    private static float JumpVariation(string participantId, int obstacleIndex)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in participantId)
                hash = (hash ^ character) * 16777619u;
            hash = (hash ^ (uint)obstacleIndex) * 16777619u;
            return hash % 1000u / 1000.0f;
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
            {
                StartJump(visual, new RaceObstacleResolvedEvent(
                    participant.ParticipantId,
                    previousObstacleIndex,
                    Avoided: true));
            }
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
        if (visual.RecoverySeconds > 0.0f)
            visual.RecoverySeconds = Math.Max(0.0f, visual.RecoverySeconds - delta);

        var finishOffset = UpdateFinish(visual, state, delta);
        if (state.Finished && !visual.Celebrates && visual.FinishSeconds >= FinishCoastSeconds)
            visual.Sprite.Stop();
        else if (state.Terrain is RaceTerrain.Swim or RaceTerrain.FailedGlideSwim)
            SetVisualMode(visual, "swim");
        else if (state.Terrain == RaceTerrain.Glide)
            SetVisualMode(visual, "glide");
        else
            SetVisualMode(visual, "run");

        var yOffset = 0.0f;
        var swimming = state.Terrain is RaceTerrain.Swim or RaceTerrain.FailedGlideSwim;

        if (visual.Celebrates && state.Finished && visual.FinishSeconds >= FinishCoastSeconds)
        {
            // Hopping on the spot, over and over, for as long as the podium takes to appear.
            var hop = Mathf.Abs(Mathf.Sin((visual.FinishSeconds - FinishCoastSeconds) * 6.4f));
            yOffset = -hop * 13.0f;
        }
        else if (visual.JumpSeconds > 0.0f)
        {
            var normalized = 1.0f - visual.JumpSeconds / visual.JumpDuration;
            yOffset = -Mathf.Sin(normalized * Mathf.Pi) * visual.JumpPeak;
        }
        else if (swimming)
        {
            yOffset = 7.0f + Mathf.Sin((float)Time.GetTicksMsec() / 150.0f + visual.BaseY) * 2.0f;
            visual.Sprite.Rotation = 0.0f;
        }
        else if (InLaunchRamp(state.X))
        {
            // The ramp is a surface the racer runs up, not altitude: the lift below carries it, and
            // only the lean forward is animation.
            var progress = Mathf.Clamp(
                (state.X - Course.GlideLaunchStartX) / (Course.GlideSegment.StartX - Course.GlideLaunchStartX),
                0.0f,
                1.0f);
            visual.Sprite.Rotation = -0.09f * progress;
        }
        else if (state.Terrain == RaceTerrain.Glide)
        {
            var glideSpan = Math.Max(1.0f, state.GlideEndX - Course.GlideSegment.StartX);
            var glideProgress = Mathf.Clamp((state.X - Course.GlideSegment.StartX) / glideSpan, 0.0f, 1.0f);
            var easedDescent = Mathf.Pow(glideProgress, 1.35f);
            var fallsIntoWater = state.GlideEndX < Course.GlideSegment.EndX - 1.0f;
            var destinationY = fallsIntoWater ? 7.0f : 0.0f;
            // The glide starts at exactly the height the ramp lip left the racer at, so a take-off
            // from a clifftop launches from the clifftop rather than snapping back to ground level.
            var seconds = (float)Time.GetTicksMsec() / 1000.0f;
            var gust = Mathf.Sin(seconds * 5.1f + visual.BaseY) * 0.6f +
                       Mathf.Sin(seconds * 11.3f + visual.BaseY * 0.5f) * 0.3f;
            yOffset = Mathf.Lerp(-RaceTrackArt.LaunchHeight(Course), destinationY, easedDescent) +
                      gust * 2.2f * (1.0f - glideProgress * 0.6f);

            // Nose held up into the headwind, buffeted by the gusts, easing level as the lift runs
            // out. The old linear tilt read as a falling brick rather than a glide.
            visual.Sprite.Rotation = Mathf.Lerp(-0.20f, 0.06f, easedDescent) + gust * 0.045f;
            SpawnHeadwind(visual, glideProgress, delta);
        }
        else
        {
            visual.Sprite.Rotation = 0.0f;
        }

        // One altitude rule for every airborne branch: the shadow shrinks and fades on the way up
        // and grows back on the way down, so jump, ramp and glide cannot drift apart.
        var altitude = Mathf.Clamp(
            -yOffset / Math.Max(FlightAltitude, RaceTrackArt.LaunchHeight(Course)),
            0.0f,
            1.0f);
        var shadowScale = Vector2.One * Mathf.Lerp(1.0f, 0.42f, altitude);
        var shadowAlpha = Mathf.Lerp(0.34f, 0.10f, altitude);
        if (swimming)
        {
            shadowScale = new Vector2(0.45f, 0.45f);
            shadowAlpha = 0.15f;
        }

        // The clifftop and the launch ramp raise the ground itself, so sprite and shadow move
        // together and the racer stays planted on the surface RaceTrackArt draws.
        var groundLift = SurfaceLiftAt(state.X);

        var visualTypeId = visual.Appearance.VisualTypeId;
        var drawX = RetreatX(visual, state.X) + finishOffset;
        UpdateSubmersion(visual, swimming);
        HandleRunningDust(visual, state, drawX, swimming);

        visual.Sprite.Position = new Vector2(
            drawX,
            visual.BaseY + VoidlingVisualFactory.RaceSpriteCenterYOffset(visualTypeId) + yOffset - groundLift);
        visual.Shadow.Position = new Vector2(
            drawX,
            visual.BaseY + VoidlingVisualFactory.ShadowCenterYOffset(
                VoidlingVisualFactory.RaceScaleFor(visualTypeId),
                visualTypeId) - groundLift);
        visual.Shadow.Scale = shadowScale;
        var shadowColor = visual.Shadow.Color;
        shadowColor.A = shadowAlpha;
        visual.Shadow.Color = shadowColor;
    }

    /// <summary>
    /// Carries a racer through the finish: it coasts a random distance past the line instead of
    /// stopping dead on it, and the first one over sometimes celebrates by hopping on the spot.
    ///
    /// The distance is a hash of the racer, not the VFX random stream, so a replay of the same race
    /// puts everyone in the same place. Driven from the snapshot rather than the finish event, so
    /// multiplayer frames get the same behaviour without one.
    /// </summary>
    private float UpdateFinish(RacerVisual visual, RaceParticipantStateSnapshot state, float delta)
    {
        if (!state.Finished)
        {
            visual.Finished = false;
            visual.FinishSeconds = 0.0f;
            return 0.0f;
        }

        if (!visual.Finished)
        {
            visual.Finished = true;
            var id = visual.Entrant.Participant.CreatureId;
            var variation = JumpVariation(id, 977);
            visual.FinishOverrun = Mathf.Lerp(FinishOverrunMin, FinishOverrunMax, variation);

            if (_firstFinisherId == null)
            {
                _firstFinisherId = id;
                visual.Celebrates = JumpVariation(id, 4211) < 0.6f;
            }
        }

        visual.FinishSeconds += Math.Max(0.0f, delta);
        var coast = Mathf.Clamp(visual.FinishSeconds / FinishCoastSeconds, 0.0f, 1.0f);
        // Decelerating, so they roll to a stop rather than sliding at full speed and freezing.
        return visual.FinishOverrun * (1.0f - (1.0f - coast) * (1.0f - coast));
    }

    /// <summary>
    /// Where a racer is drawn while recovering from a refused hurdle. The simulation snaps X back
    /// the instant the jump fails; this walks that rollback out so the racer visibly scuffs the
    /// hurdle, then backs up to take another run at it, facing the way it is moving.
    /// </summary>
    private static float RetreatX(RacerVisual visual, float stateX)
    {
        var scale = VoidlingVisualFactory.RaceScaleFor(visual.Appearance.VisualTypeId);
        if (visual.RecoverySeconds <= 0.0f)
        {
            visual.Sprite.Scale = new Vector2(scale, scale);
            return stateX;
        }

        // Still scuffing the hurdle: hold the take-off point rather than snapping backwards mid-hop.
        if (visual.RecoverySeconds > RetreatSeconds)
        {
            visual.Sprite.Scale = new Vector2(scale, scale);
            return visual.RetreatFromX;
        }

        visual.Sprite.Scale = new Vector2(-scale, scale);
        var walked = 1.0f - visual.RecoverySeconds / RetreatSeconds;
        return Mathf.Lerp(visual.RetreatFromX, stateX, walked * walked * (3.0f - 2.0f * walked));
    }

    /// <summary>
    /// Sinks a swimmer up to the neck. The overlay is a child of the sprite in frame-local units,
    /// so only the head stays clear of the water whatever the creature's scale or art revision.
    /// </summary>
    private static void UpdateSubmersion(RacerVisual visual, bool swimming)
    {
        visual.Submersion.Visible = swimming;
        if (!swimming)
            return;

        // Waterline just under the head, bobbing with the swell.
        var waterline = -4.0f + Mathf.Sin((float)Time.GetTicksMsec() / 190.0f + visual.BaseY) * 1.2f;
        visual.Submersion.Position = new Vector2(0.0f, waterline);
    }

    private void HandleRunningDust(RacerVisual visual, RaceParticipantStateSnapshot state, float drawX, bool swimming)
    {
        var moved = drawX - visual.LastX;
        visual.LastX = drawX;

        var grounded = !swimming &&
                       state.Terrain != RaceTerrain.Glide &&
                       visual.JumpSeconds <= 0.0f &&
                       !state.Finished;
        if (!grounded)
        {
            visual.DustDistance = 0.0f;
            return;
        }

        visual.DustDistance += Math.Abs(moved);
        if (visual.DustDistance < DustSpacingPixels)
            return;

        visual.DustDistance = 0.0f;
        SpawnDust(visual, drawX, 0.8f);
    }

    // Chunky pixel-art dust, in the Pokeathlon idiom: solid overlapping blobs with a dark rim that
    // hold their opacity while they grow, rather than soft translucent smudges that vanish on a
    // pastel track.
    private static readonly Color DustRim = new(0.60f, 0.56f, 0.47f, 1.0f);
    private static readonly Color[] DustFills =
    {
        new(0.96f, 0.94f, 0.88f, 1.0f),
        new(0.87f, 0.83f, 0.74f, 1.0f),
        new(0.78f, 0.72f, 0.61f, 1.0f)
    };

    /// <summary>
    /// A cloud of dust kicked out behind the feet. <paramref name="force"/> scales the whole thing,
    /// so one routine covers the running trail and the burst a cheer digs out of the track.
    /// </summary>
    private void SpawnDust(RacerVisual visual, float x, float force)
    {
        var blobs = Math.Clamp(3 + (int)(force * 2.0f), 3, 9);
        var cloud = new Node2D
        {
            Position = new Vector2(
                x - (8.0f + (float)_vfxRandom.NextDouble() * 6.0f) * force,
                visual.Shadow.Position.Y - 3.0f - (float)_vfxRandom.NextDouble() * 4.0f),
            Scale = Vector2.One * 0.75f,
            ZIndex = 9
        };
        AddChild(cloud);

        for (var i = 0; i < blobs; i++)
        {
            var radius = (3.8f + (float)_vfxRandom.NextDouble() * 3.4f) * force;
            var offset = new Vector2(
                (float)(_vfxRandom.NextDouble() * 2.0 - 1.0) * 9.0f * force,
                (float)(_vfxRandom.NextDouble() * 2.0 - 1.0) * 5.0f * force);

            // Rim first, fill on top: two circles are cheaper than outlining a polygon and give the
            // hard pixel-art edge the soft ellipses were missing.
            cloud.AddChild(new Polygon2D
            {
                Polygon = BuildCircle(radius + 1.6f),
                Color = DustRim,
                Position = offset
            });
            cloud.AddChild(new Polygon2D
            {
                Polygon = BuildCircle(radius),
                Color = DustFills[(i + _vfxRandom.Next(DustFills.Length)) % DustFills.Length],
                Position = offset
            });
        }

        var drift = new Vector2(
            -(10.0f + (float)_vfxRandom.NextDouble() * 12.0f) * force,
            -(3.0f + (float)_vfxRandom.NextDouble() * 6.0f) * force);
        var life = 0.30 + _vfxRandom.NextDouble() * 0.18 * force;

        var grow = CreateTween().SetParallel(true);
        grow.TweenProperty(cloud, "position", cloud.Position + drift, life)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        grow.TweenProperty(cloud, "scale", Vector2.One * (1.25f + 0.35f * force), life)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        // Held solid for most of its life, then out quickly, so it never reads as a smudge.
        var fade = CreateTween();
        fade.TweenInterval(life * 0.62);
        fade.TweenProperty(cloud, "modulate:a", 0.0f, life * 0.38);
        fade.Finished += cloud.QueueFree;
    }

    /// <summary>
    /// The burst a cheered racer tears out of the track: a wide cloud, a speed wedge driving out of
    /// it and stones thrown up behind.
    /// </summary>
    private void SpawnDustBurst(RacerVisual visual, float x)
    {
        for (var i = 0; i < 4; i++)
            SpawnDust(visual, x - i * 6.0f, 1.4f + (float)_vfxRandom.NextDouble() * 0.6f);

        SpawnSpeedWedge(visual, x);

        for (var i = 0; i < 8; i++)
            SpawnKickedStone(visual, x);
    }

    private void SpawnSpeedWedge(RacerVisual visual, float x)
    {
        var wedge = new Polygon2D
        {
            Polygon = new[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(-26.0f, -9.0f),
                new Vector2(-17.0f, -1.0f),
                new Vector2(-29.0f, 4.0f),
                new Vector2(-16.0f, 4.0f),
                new Vector2(-24.0f, 11.0f)
            },
            Color = new Color(1.0f, 0.86f, 0.31f, 1.0f),
            Position = new Vector2(x - 8.0f, visual.Shadow.Position.Y - 4.0f),
            ZIndex = 12,
            Scale = new Vector2(0.5f, 0.5f)
        };
        AddChild(wedge);

        var punch = CreateTween().SetParallel(true);
        punch.TweenProperty(wedge, "scale", new Vector2(1.7f, 1.35f), 0.16)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        punch.TweenProperty(wedge, "position:x", wedge.Position.X - 16.0f, 0.30)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        var fade = CreateTween();
        fade.TweenInterval(0.14);
        fade.TweenProperty(wedge, "modulate:a", 0.0f, 0.18);
        fade.Finished += wedge.QueueFree;
    }

    private void SpawnKickedStone(RacerVisual visual, float x)
    {
        var size = 2.4f + (float)_vfxRandom.NextDouble() * 2.6f;
        var stone = new Polygon2D
        {
            Polygon = BuildCircle(size),
            Color = _vfxRandom.Next(2) == 0
                ? new Color(0.72f, 0.75f, 0.76f, 1.0f)
                : new Color(0.85f, 0.74f, 0.54f, 1.0f),
            Position = new Vector2(x - 6.0f, visual.Shadow.Position.Y - 2.0f),
            ZIndex = 13
        };
        stone.AddChild(new Polygon2D
        {
            Polygon = BuildCircle(size * 0.45f),
            Color = new Color(1.0f, 1.0f, 1.0f, 0.55f),
            Position = new Vector2(-size * 0.25f, -size * 0.2f)
        });
        AddChild(stone);

        var target = stone.Position + new Vector2(
            -(16.0f + (float)_vfxRandom.NextDouble() * 40.0f),
            -(30.0f + (float)_vfxRandom.NextDouble() * 48.0f));
        var life = 0.40 + _vfxRandom.NextDouble() * 0.30;

        var toss = CreateTween().SetParallel(true);
        toss.TweenProperty(stone, "position", target, life)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        toss.TweenProperty(stone, "rotation", (float)(_vfxRandom.NextDouble() * 6.0 - 3.0), life);

        var fade = CreateTween();
        fade.TweenInterval(life * 0.6);
        fade.TweenProperty(stone, "modulate:a", 0.0f, life * 0.4);
        fade.Finished += stone.QueueFree;
    }

    /// <summary>Air torn past a glider, so a glide reads as pushing into a headwind.</summary>
    private void SpawnHeadwind(RacerVisual visual, float glideProgress, float delta)
    {
        if (delta <= 0.0f)
            return;

        // Its own accumulator: the running-dust timer is reset every airborne frame, so sharing it
        // meant a glide produced barely a streak.
        visual.WindSeconds += delta;
        if (visual.WindSeconds < 0.05f)
            return;

        visual.WindSeconds = 0.0f;
        var y = visual.Sprite.Position.Y + (float)(_vfxRandom.NextDouble() * 22.0 - 9.0);
        var length = 12.0f + (float)_vfxRandom.NextDouble() * 16.0f;
        var streak = new Line2D
        {
            Width = 1.2f,
            DefaultColor = new Color(1.0f, 1.0f, 1.0f, 0.62f * (1.0f - glideProgress * 0.4f)),
            ZIndex = 9,
            Points = new[]
            {
                new Vector2(visual.Sprite.Position.X - 10.0f - length, y),
                new Vector2(visual.Sprite.Position.X - 10.0f, y)
            }
        };
        AddChild(streak);

        var blow = CreateTween().SetParallel(true);
        blow.TweenProperty(streak, "position:x", -34.0f, 0.30)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        blow.TweenProperty(streak, "modulate:a", 0.0f, 0.30);
        blow.Finished += streak.QueueFree;
    }

    /// <summary>
    /// The water a swimmer sits in: a soft waterline curved over the shoulders, straight sides down.
    /// A plain rectangle read as a pane of glass laid over the creature.
    /// </summary>
    private static Vector2[] BuildSubmersionPolygon()
    {
        var points = new List<Vector2>();
        const float halfWidth = 13.0f;
        for (var i = 0; i <= 12; i++)
        {
            var t = i / 12.0f;
            var x = Mathf.Lerp(-halfWidth, halfWidth, t);
            points.Add(new Vector2(x, -Mathf.Sin(t * Mathf.Pi) * 3.4f));
        }
        points.Add(new Vector2(halfWidth, 30.0f));
        points.Add(new Vector2(-halfWidth, 30.0f));
        return points.ToArray();
    }

    /// <summary>The wake ring where the body breaks the surface.</summary>
    private static Vector2[] BuildRipplePolygon()
    {
        var points = new Vector2[16];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = Mathf.Tau * i / points.Length;
            points[i] = new Vector2(Mathf.Cos(angle) * 12.0f, Mathf.Sin(angle) * 2.4f);
        }
        return points;
    }

    private static Vector2[] BuildCircle(float radius)
    {
        var points = new Vector2[10];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = Mathf.Tau * i / points.Length;
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.82f);
        }
        return points;
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

        // The track no longer names its own sections. This strip only appears to report a fault the
        // player has to know about, such as multiplayer losing sync.
        _faultPlaque = new ColorRect
        {
            Color = new Color(0.16f, 0.20f, 0.17f, 0.72f),
            Position = new Vector2(212, 35),
            Size = new Vector2(216, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        canvas.AddChild(_faultPlaque);

        _faultLabel = UiFactory.CreateLabel(string.Empty, 8);
        _faultLabel.Position = new Vector2(212, 34);
        _faultLabel.Size = new Vector2(216, 18);
        _faultLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _faultLabel.Visible = false;
        canvas.AddChild(_faultLabel);

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
                ShowFault(result.Error ?? Tr("UI_MP_RACE_CHEER_FAILED"));
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
            SpawnDustBurst(_playerVisual, _playerVisual.Sprite.Position.X);
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

    private void ShowFault(string text)
    {
        _faultLabel.Text = text;
        _faultLabel.AddThemeColorOverride("font_color", Color.FromHtml("#F2B6AF"));
        _faultLabel.Visible = true;
        _faultPlaque.Visible = true;
    }

    /// <summary>Wheel zoom. Steps multiply, so each notch feels the same at any distance.</summary>
    internal void HandleZoomInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { Pressed: true } wheel)
            return;

        var factor = wheel.ButtonIndex switch
        {
            MouseButton.WheelUp => ZoomStepFactor,
            MouseButton.WheelDown => 1.0f / ZoomStepFactor,
            _ => 1.0f
        };
        if (Mathf.IsEqualApprox(factor, 1.0f))
            return;

        _zoomTarget = Mathf.Clamp(_zoomTarget * factor, MinZoom, MaxZoom);
    }

    /// <summary>Eases towards the requested zoom at a rate that does not depend on the frame rate.</summary>
    private void UpdateZoom(float delta)
    {
        _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1.0f - Mathf.Pow(0.002f, delta));
        _camera.Zoom = new Vector2(_zoom, _zoom);
    }

    /// <summary>
    /// Keeps the camera on the player's own Voidling.
    ///
    /// Vertically it centres on the creature but never past the edge of the wide framing, so at the
    /// furthest zoom the view is byte-for-byte the one the race has always had, and every step in
    /// follows the Voidling up the cliff, off the ramp and down through a glide instead of leaving
    /// it to drift off the top of a zoomed-in screen.
    /// </summary>
    private void UpdatePlayerTracking()
    {
        if (_playerVisual == null || !TryGetPlayerState(out var player))
            return;

        // The sprite's origin is its frame centre; the creature sits a little below that.
        var focusY = _playerVisual.Sprite.Position.Y + 9.0f;
        var halfHeight = ScreenHeight * 0.5f / Math.Max(0.001f, _zoom);
        var cameraY = halfHeight >= ScreenHeight * 0.5f
            ? ScreenHeight * 0.5f
            : Mathf.Clamp(focusY, halfHeight, ScreenHeight - halfHeight);

        _camera.Position = new Vector2(player.X, cameraY);
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

        var byId = _entry.Entrants.ToDictionary(entrant => entrant.Participant.CreatureId, StringComparer.Ordinal);
        var finishers = finishOrder.Select(id => byId[id]).ToList();

        var canvas = new CanvasLayer { Layer = ResultsCanvasLayer };
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

        var button = UiFactory.CreateButton(Tr("UI_RACE_RETURN"));
        button.CustomMinimumSize = new Vector2(160, 25);
        button.Pressed += () => ReturnRequested?.Invoke();
        box.AddChild(button);

        // The owner is told last, and its faults are contained here. Rewards, saving and leaderboard
        // projection all run through this callback; if any of them throw, the player must still be
        // looking at a results screen with a working way back instead of a frozen track.
        if (_completionReported)
            return;

        _completionReported = true;
        try
        {
            RaceCompleted?.Invoke(selectedPlace);
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"Race completion handling failed after the results screen was shown: {exception}");
        }
    }

    private static readonly Vector2 PodiumPortraitSize = new(48, 48);

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

        StandPortraitOn(stage, entrant, blockPosition, blockSize, blockPosition.Y);
    }

    private static void AddFourthPlacePuddle(Control stage, RaceEntrant entrant, Vector2 position)
    {
        var puddleSize = new Vector2(62, 14);
        var puddlePosition = new Vector2(position.X - 8, position.Y + 41);
        var puddle = new PanelContainer { Position = puddlePosition, Size = puddleSize };
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

        // Fourth place stands in the puddle rather than on it.
        StandPortraitOn(stage, entrant, puddlePosition, puddleSize, puddlePosition.Y + 6.0f, $"4th • {entrant.Participant.DisplayName}");
    }

    /// <summary>
    /// Places an entrant's portrait with its feet on <paramref name="groundY"/> and its name above
    /// it, both centred on the award surface. The foot offset comes from the shared portrait pivot,
    /// so new creature art cannot leave the podium standing in mid-air or sunk into the blocks.
    /// </summary>
    private static void StandPortraitOn(
        Control stage,
        RaceEntrant entrant,
        Vector2 surfacePosition,
        Vector2 surfaceSize,
        float groundY,
        string? nameText = null)
    {
        var portrait = CreateEntrantPortrait(entrant, PodiumPortraitSize);
        portrait.Size = PodiumPortraitSize;
        portrait.Position = new Vector2(
            surfacePosition.X + (surfaceSize.X - PodiumPortraitSize.X) * 0.5f,
            groundY - VoidlingVisualFactory.PortraitGroundYOffset(
                PodiumPortraitSize,
                entrant.Participant.VisualTypeId));
        stage.AddChild(portrait);

        var name = UiFactory.CreateLabel(nameText ?? entrant.Participant.DisplayName, 6);
        name.Size = new Vector2(100, 14);
        name.Position = new Vector2(
            surfacePosition.X + (surfaceSize.X - name.Size.X) * 0.5f,
            portrait.Position.Y - 12.0f);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        stage.AddChild(name);
    }

    private static TextureRect CreateEntrantPortrait(RaceEntrant entrant, Vector2 size)
        => UiFactory.CreatePortrait(
            AppearanceFor(entrant.Participant),
            entrant.HasAngelMutation,
            entrant.OtherMutationCount,
            size);

    private void AddHurdle(float x)
    {
        // Hurdles stand on the running surface, which is a clifftop wherever a climb has raised it.
        var lift = SurfaceLiftAt(x);
        for (var y = TrackTop + 9.0f; y < TrackBottom; y += 18.0f)
        {
            AddChild(new Sprite2D
            {
                Texture = RaceTrackArt.FencePost,
                Position = new Vector2(x, y - lift),
                Scale = new Vector2(1.15f, 1.15f),
                ZIndex = 6
            });
        }
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

    /// <summary>How far the running surface sits above the base track band at <paramref name="x"/>.</summary>
    private float SurfaceLiftAt(float x) => RaceTrackArt.SurfaceLift(Course, x);

    private bool InLaunchRamp(float x)
        => Course.HasGlideSegment &&
           x >= Course.GlideLaunchStartX &&
           x < Course.GlideSegment.StartX;

    private static Color ParseTint(string html)
        => string.IsNullOrWhiteSpace(html) ? Colors.White : Color.FromHtml(html);
}
