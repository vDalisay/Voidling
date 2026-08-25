using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Multiplayer;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Presentation shell for a synchronized multiplayer race. It never constructs RaceSimulation;
/// all result-affecting advancement and Cheer scheduling goes through the Application lockstep bridge.
/// </summary>
public partial class MultiplayerRaceScreen : Control
{
    public event Action? ReturnRequested;

    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;
    private const float TrackLeft = 132.0f;
    private const float TrackWidth = 432.0f;
    private const float LaneStartY = 70.0f;
    private const float LaneSpacing = 57.0f;
    private const int MaxCatchUpStepsPerFrame = 30;

    private sealed class LaneVisual
    {
        public TextureRect Portrait { get; init; } = null!;
        public Label Name { get; init; } = null!;
        public ProgressBar Stamina { get; init; } = null!;
        public Label Status { get; init; } = null!;
    }

    private readonly Dictionary<string, LaneVisual> _lanes = new(StringComparer.Ordinal);
    private MultiplayerRacePresentationBridge? _bridge;
    private string _challengeId = string.Empty;
    private double _tickAccumulator;
    private Label _headline = null!;
    private Label _localStamina = null!;
    private Button _cheer = null!;
    private Button _return = null!;
    private bool _complete;

    public void Configure(MultiplayerRacePresentationBridge bridge, string challengeId)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("MultiplayerRaceScreen must be configured before entering the scene tree.");
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _challengeId = string.IsNullOrWhiteSpace(challengeId)
            ? throw new ArgumentException("Challenge ID is required.", nameof(challengeId))
            : challengeId;
    }

    public override void _Ready()
    {
        if (_bridge == null || !_bridge.TryGetFrame(_challengeId, out var frame))
            throw new InvalidOperationException("Multiplayer race lockstep frame is unavailable at presentation launch.");

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildPresentation(frame);
        RenderFrame(frame);
    }

    public override void _Process(double delta)
    {
        if (_bridge == null || _complete)
            return;

        _tickAccumulator += Math.Max(0.0, delta);
        var availableSteps = (int)Math.Floor(_tickAccumulator / RaceSimulation.FixedStepSeconds);
        var steps = Math.Min(availableSteps, MaxCatchUpStepsPerFrame);
        if (steps > 0)
        {
            var advanced = _bridge.AdvanceFixedSteps(_challengeId, steps);
            if (!advanced.Success)
            {
                _headline.Text = advanced.Error ?? Tr("UI_MP_RACE_SYNC_ERROR");
                _headline.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
                return;
            }
            _tickAccumulator -= steps * RaceSimulation.FixedStepSeconds;
        }

        if (_bridge.TryGetFrame(_challengeId, out var frame))
            RenderFrame(frame);
    }

    private void BuildPresentation(MultiplayerRaceFrameView frame)
    {
        var background = new ColorRect
        {
            Color = Color.FromHtml("#A7D8C7"),
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(background);

        var title = UiFactory.CreateTitle(Tr("UI_MP_RACE_TITLE"));
        title.Position = new Vector2(230, 10);
        title.Size = new Vector2(180, 22);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(title);

        _headline = UiFactory.CreateLabel(Tr("UI_MP_RACE_RUNNING"), 7);
        _headline.Position = new Vector2(150, 36);
        _headline.Size = new Vector2(340, 18);
        _headline.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_headline);

        for (var i = 0; i < frame.Participants.Count; i++)
            BuildLane(frame.Participants[i], i);

        _cheer = UiFactory.CreateButton(Tr("UI_RACE_CHEER"));
        _cheer.Position = new Vector2(18, 322);
        _cheer.Size = new Vector2(92, 26);
        _cheer.Pressed += RequestCheer;
        AddChild(_cheer);

        _localStamina = UiFactory.CreateLabel(string.Empty, 7);
        _localStamina.Position = new Vector2(122, 326);
        _localStamina.Size = new Vector2(235, 18);
        AddChild(_localStamina);

        _return = UiFactory.CreateButton(Tr("UI_RACE_RETURN"));
        _return.Position = new Vector2(445, 322);
        _return.Size = new Vector2(176, 26);
        _return.Visible = false;
        _return.Pressed += () => ReturnRequested?.Invoke();
        AddChild(_return);
    }

    private void BuildLane(MultiplayerRaceParticipantView participant, int index)
    {
        var y = LaneStartY + index * LaneSpacing;
        var lane = new ColorRect
        {
            Color = index % 2 == 0
                ? new Color(0.95f, 0.89f, 0.72f, 0.72f)
                : new Color(0.91f, 0.82f, 0.67f, 0.72f),
            Position = new Vector2(14, y - 7),
            Size = new Vector2(612, 50),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(lane);

        var name = UiFactory.CreateLabel(participant.DisplayName, participant.IsLocal ? 8 : 7);
        name.Position = new Vector2(23, y + 3);
        name.Size = new Vector2(102, 20);
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (participant.IsLocal)
            name.AddThemeColorOverride("font_color", Color.FromHtml("#866324"));
        AddChild(name);

        var track = new ColorRect
        {
            Color = Color.FromHtml("#7E6856"),
            Position = new Vector2(TrackLeft, y + 12),
            Size = new Vector2(TrackWidth, 5),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(track);

        var portrait = UiFactory.CreatePortrait(
            ParseTint(participant.TintHex),
            participant.HasAngelMutation,
            participant.OtherMutationCount,
            new Vector2(34, 34));
        portrait.Position = new Vector2(TrackLeft - 17, y - 4);
        portrait.Size = new Vector2(34, 34);
        AddChild(portrait);

        var stamina = new ProgressBar
        {
            Position = new Vector2(TrackLeft, y + 25),
            Size = new Vector2(180, 10),
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = Math.Max(1.0f, participant.MaxStamina),
            Value = participant.CurrentStamina
        };
        UiFactory.ApplyPixelFont(stamina, 6);
        AddChild(stamina);

        var status = UiFactory.CreateLabel(string.Empty, 6);
        status.Position = new Vector2(TrackLeft + 196, y + 22);
        status.Size = new Vector2(230, 16);
        AddChild(status);

        _lanes[participant.ParticipantId] = new LaneVisual
        {
            Portrait = portrait,
            Name = name,
            Stamina = stamina,
            Status = status
        };
    }

    private void RenderFrame(MultiplayerRaceFrameView frame)
    {
        foreach (var participant in frame.Participants)
        {
            if (!_lanes.TryGetValue(participant.ParticipantId, out var visual))
                continue;

            var targetX = TrackLeft - 17 + participant.Progress * TrackWidth;
            visual.Portrait.Position = new Vector2(targetX, visual.Portrait.Position.Y);
            visual.Stamina.MaxValue = Math.Max(1.0f, participant.MaxStamina);
            visual.Stamina.Value = participant.CurrentStamina;
            visual.Status.Text = participant.Finished
                ? string.Format(Tr("UI_MP_RACE_FINISHED"), participant.Placement ?? frame.Participants.Count)
                : participant.CheerSeconds > 0.0f
                    ? Tr("UI_RACE_CHEERING")
                    : TerrainLabel(participant.Terrain);

            if (!participant.IsLocal)
                continue;

            _localStamina.Text = string.Format(
                Tr("UI_RACE_STAMINA"),
                Mathf.CeilToInt(participant.CurrentStamina),
                Mathf.CeilToInt(participant.MaxStamina));
            _cheer.Disabled = participant.Finished ||
                              participant.CheerSeconds > 0.0f ||
                              participant.CurrentStamina <= 0.0f;
        }

        if (!frame.IsComplete || _complete)
            return;

        _complete = true;
        _cheer.Disabled = true;
        _return.Visible = true;
        var ordered = frame.Participants
            .Where(value => value.Placement.HasValue)
            .OrderBy(value => value.Placement)
            .Select(value => $"#{value.Placement} {value.DisplayName}");
        _headline.Text = string.Join("   ", ordered);
    }

    private void RequestCheer()
    {
        if (_bridge == null || _complete)
            return;

        var result = _bridge.RequestCheer(_challengeId);
        if (!result.Success)
        {
            _headline.Text = result.Error ?? Tr("UI_MP_RACE_CHEER_FAILED");
            _headline.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        }
    }

    private string TerrainLabel(RaceTerrain terrain)
        => terrain switch
        {
            RaceTerrain.Swim or RaceTerrain.FailedGlideSwim => Tr("UI_RACE_SECTION_SWIM"),
            RaceTerrain.Glide => Tr("UI_RACE_SECTION_GLIDE"),
            _ => Tr("UI_RACE_SECTION_RUN")
        };

    private static Color ParseTint(string tintHex)
    {
        try
        {
            return Color.FromHtml(tintHex);
        }
        catch
        {
            return Color.FromHtml("#F6F0C9");
        }
    }
}
