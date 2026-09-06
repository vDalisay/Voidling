using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Voidling.Application.Racing;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Racing;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

/// <summary>
/// The live race HUD, composed as the "creature first" reference asks: the world stays dominant and
/// the chrome sits in four named groups around it — the course banner top left, the opponent
/// standings on the right, course progress bottom left, and the player's own portrait, place,
/// stamina and Cheer together at bottom centre.
///
/// Presentation only. Every value shown is read from the simulation or lockstep snapshot the rest of
/// <see cref="RaceScreen"/> already owns; nothing here can change a race outcome.
/// </summary>
public partial class RaceScreen
{
    /// <summary>Canvas layer the in-race HUD lives on, below results and pause.</summary>
    internal const int HudCanvasLayer = 20;

    /// <summary>
    /// The four groups the "creature first" layout is made of, plus the pause reminder. Named so the
    /// CI probe can require each one on screen and clear of the others.
    /// </summary>
    internal static readonly string[] HudGroupNames =
    {
        "RaceHudBanner", "RaceHudMenuHint", "RaceHudStandings", "RaceHudCourse", "RaceHudPlayer"
    };

    /// <summary>Frames in the premium stamina dial sheet, empty through full.</summary>
    private const int StaminaDialFrames = 37;

    private static readonly Texture2D StaminaDialSheet = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Other UI sprites/Stamina circle with black outline sprite sheet .png");

    private static readonly Color StaminaColor = Color.FromHtml("#7FB56B");
    private static readonly Color StandingHighlight = Color.FromHtml("#8FBF7C");

    private Button _cheerButton = null!;
    private ProgressBar _staminaBar = null!;
    private Label _staminaLabel = null!;
    private TextureRect _staminaDial = null!;
    private AtlasTexture _staminaDialTexture = null!;
    private Label _faultLabel = null!;
    private ColorRect _faultPlaque = null!;
    private RaceMiniMap _miniMap = null!;

    private Label _sectionLabel = null!;
    private Label _progressLabel = null!;
    private Label _playerNameLabel = null!;
    private Label _playerPlaceLabel = null!;
    private readonly List<StandingRow> _standingRows = new();

    /// <summary>One opponent-standings line. Positional: row 0 always shows whoever is leading.</summary>
    private sealed record StandingRow(PanelContainer Panel, Label Place, Label Name, Label Status);

    private void CreateHud()
    {
        var canvas = new CanvasLayer { Layer = HudCanvasLayer, Name = "RaceHud" };
        AddChild(canvas);

        canvas.AddChild(BuildBanner());
        canvas.AddChild(BuildMenuHint());
        canvas.AddChild(BuildStandings());
        canvas.AddChild(BuildCoursePanel());
        canvas.AddChild(BuildPlayerPanel());
        BuildFaultStrip(canvas);
    }

    // ---- Groups -------------------------------------------------------------------------------

    private Control BuildBanner()
    {
        var panel = HudPanel("RaceHudBanner", new Vector2(8, 6), new Vector2(176, 50));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);
        panel.AddChild(box);

        var eyebrow = UiFactory.CreateLabel(
            Tr(_multiplayerBridge != null ? "UI_RACE_HUD_ONLINE" : "UI_RACE_HUD_LOCAL"),
            6);
        eyebrow.AddThemeColorOverride("font_color", Color.FromHtml("#7A8A6F"));
        box.AddChild(eyebrow);

        var (nameKey, _) = RaceCoursePresentationCatalog.KeysFor(_entry!.CourseDefinition.Id);
        var title = UiFactory.CreateLabel(Tr(nameKey), 12);
        title.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        UiFactory.SetLabelBold(title, true);
        box.AddChild(title);

        _sectionLabel = UiFactory.CreateLabel(string.Empty, 7);
        _sectionLabel.Name = "CourseSection";
        _sectionLabel.AddThemeColorOverride("font_color", Color.FromHtml("#6F7F66"));
        box.AddChild(_sectionLabel);
        return panel;
    }

    /// <summary>
    /// A reminder of the pause key, not a control: the race is already left through Escape, and a
    /// clickable button here would sit under the pointer during the skippable opening flyover.
    /// </summary>
    private Control BuildMenuHint()
    {
        var panel = HudPanel("RaceHudMenuHint", new Vector2(548, 6), new Vector2(84, 22));
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var label = UiFactory.CreateLabel(Tr("UI_RACE_HUD_MENU"), 7);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(label);
        return panel;
    }

    private Control BuildStandings()
    {
        var count = _entry!.Entrants.Count;
        var panel = HudPanel("RaceHudStandings", new Vector2(486, 74), new Vector2(146, 30 + count * 17));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        panel.AddChild(box);

        var heading = UiFactory.CreateLabel(Tr("UI_RACE_HUD_STANDINGS"), 8);
        heading.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        UiFactory.SetLabelBold(heading, true);
        box.AddChild(heading);

        for (var index = 0; index < count; index++)
        {
            var row = new PanelContainer { Name = $"Standing{index}", CustomMinimumSize = new Vector2(0, 15) };
            row.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 5);
            row.AddChild(line);

            var place = UiFactory.CreateLabel(string.Empty, 8);
            place.Name = "Place";
            place.CustomMinimumSize = new Vector2(10, 0);
            line.AddChild(place);

            var name = UiFactory.CreateLabel(string.Empty, 8);
            name.Name = "Racer";
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.ClipText = true;
            line.AddChild(name);

            var status = UiFactory.CreateLabel(string.Empty, 7);
            status.Name = "Status";
            status.HorizontalAlignment = HorizontalAlignment.Right;
            status.CustomMinimumSize = new Vector2(34, 0);
            line.AddChild(status);

            box.AddChild(row);
            _standingRows.Add(new StandingRow(row, place, name, status));
        }

        return panel;
    }

    private Control BuildCoursePanel()
    {
        var panel = HudPanel("RaceHudCourse", new Vector2(8, 288), new Vector2(164, 64));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        panel.AddChild(box);

        var header = new HBoxContainer();
        var heading = UiFactory.CreateLabel(Tr("UI_RACE_HUD_COURSE"), 8);
        heading.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        UiFactory.SetLabelBold(heading, true);
        heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(heading);
        _progressLabel = UiFactory.CreateLabel(string.Empty, 7);
        _progressLabel.Name = "CourseProgress";
        _progressLabel.HorizontalAlignment = HorizontalAlignment.Right;
        header.AddChild(_progressLabel);
        box.AddChild(header);

        _miniMap = new RaceMiniMap { Name = "CourseStrip", CustomMinimumSize = new Vector2(0, 22) };
        _miniMap.SetCourse(
            Course.StartX,
            Course.EndX,
            Course.Segments
                .Select(segment => new CourseMinimapSegment(segment.Kind.ToString(), segment.StartX, segment.EndX))
                .ToList());
        box.AddChild(_miniMap);

        var posts = new HBoxContainer();
        var start = UiFactory.CreateLabel(Tr("UI_RACE_HUD_START"), 6);
        start.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        posts.AddChild(start);
        var finish = UiFactory.CreateLabel(Tr("UI_RACE_HUD_FINISH"), 6);
        finish.HorizontalAlignment = HorizontalAlignment.Right;
        posts.AddChild(finish);
        box.AddChild(posts);
        return panel;
    }

    private Control BuildPlayerPanel()
    {
        var panel = HudPanel("RaceHudPlayer", new Vector2(180, 282), new Vector2(280, 70));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);

        var entrant = _entry!.Entrants.FirstOrDefault(value =>
            string.Equals(value.Participant.CreatureId, _playerId, StringComparison.Ordinal))
            ?? _entry.Entrants[0];
        var portrait = CreateEntrantPortrait(entrant, new Vector2(44, 44));
        portrait.Name = "PlayerPortrait";
        portrait.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(portrait);

        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        row.AddChild(column);

        var identity = new HBoxContainer();
        identity.AddThemeConstantOverride("separation", 4);
        _playerNameLabel = UiFactory.CreateLabel(entrant.Participant.DisplayName, 10);
        _playerNameLabel.Name = "PlayerName";
        _playerNameLabel.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        _playerNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _playerNameLabel.ClipText = true;
        UiFactory.SetLabelBold(_playerNameLabel, true);
        identity.AddChild(_playerNameLabel);
        _playerPlaceLabel = UiFactory.CreateLabel(string.Empty, 8);
        _playerPlaceLabel.Name = "PlayerPlace";
        _playerPlaceLabel.HorizontalAlignment = HorizontalAlignment.Right;
        identity.AddChild(_playerPlaceLabel);
        column.AddChild(identity);

        var staminaRow = new HBoxContainer();
        staminaRow.AddThemeConstantOverride("separation", 4);
        _staminaDialTexture = new AtlasTexture { Atlas = StaminaDialSheet, Region = new Rect2(0, 0, 16, 16) };
        _staminaDial = new TextureRect
        {
            Name = "StaminaDial",
            Texture = _staminaDialTexture,
            CustomMinimumSize = new Vector2(20, 20),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        staminaRow.AddChild(_staminaDial);

        _staminaBar = new ProgressBar
        {
            Name = "StaminaBar",
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 12),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        UiFactory.ApplyPixelFont(_staminaBar, 7);
        var background = new StyleBoxFlat { BgColor = Color.FromHtml("#B6AE96") };
        background.SetCornerRadiusAll(2);
        var fill = new StyleBoxFlat { BgColor = StaminaColor };
        fill.SetCornerRadiusAll(2);
        _staminaBar.AddThemeStyleboxOverride("background", background);
        _staminaBar.AddThemeStyleboxOverride("fill", fill);
        staminaRow.AddChild(_staminaBar);
        column.AddChild(staminaRow);

        _staminaLabel = UiFactory.CreateLabel(string.Empty, 7);
        _staminaLabel.AddThemeColorOverride("font_color", Color.FromHtml("#5C6B54"));
        column.AddChild(_staminaLabel);

        _cheerButton = UiFactory.CreateButton(Tr("UI_RACE_CHEER"));
        _cheerButton.Name = "CheerButton";
        _cheerButton.CustomMinimumSize = new Vector2(80, 44);
        _cheerButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        UiFactory.ApplyPrimaryStyle(_cheerButton);
        _cheerButton.Pressed += CheerPlayer;
        row.AddChild(_cheerButton);
        return panel;
    }

    // The strip only appears to report a fault the player has to know about, such as multiplayer
    // losing sync. The track itself carries no signposting.
    private void BuildFaultStrip(CanvasLayer canvas)
    {
        _faultPlaque = new ColorRect
        {
            Name = "RaceHudFaultPlaque",
            Color = new Color(0.16f, 0.20f, 0.17f, 0.72f),
            Position = new Vector2(212, 62),
            Size = new Vector2(216, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        canvas.AddChild(_faultPlaque);

        _faultLabel = UiFactory.CreateLabel(string.Empty, 8);
        _faultLabel.Name = "RaceHudFault";
        _faultLabel.Position = new Vector2(212, 61);
        _faultLabel.Size = new Vector2(216, 18);
        _faultLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _faultLabel.Visible = false;
        canvas.AddChild(_faultLabel);
    }

    /// <summary>
    /// A premium panel placed by hand on the HUD layer. The tighter content margins are what let a
    /// three-line group fit the small logical viewport without the world losing screen space.
    /// </summary>
    private static PanelContainer HudPanel(string name, Vector2 position, Vector2 size)
    {
        var panel = UiFactory.CreatePanel(size);
        panel.Name = name;
        if (panel.GetThemeStylebox("panel") is StyleBoxTexture style)
        {
            style.ContentMarginLeft = 7;
            style.ContentMarginRight = 7;
            style.ContentMarginTop = 5;
            style.ContentMarginBottom = 5;
        }
        panel.Position = position;
        panel.Size = size;
        return panel;
    }

    // ---- Live values --------------------------------------------------------------------------

    private void UpdateHud()
    {
        if (_entry == null || _miniMap == null || !TryGetPlayerState(out var player))
            return;

        var standings = ComputeStandings();
        UpdatePlayerGroup(player, standings);
        UpdateStandings(standings);
        UpdateCourseGroup(player);
    }

    private void UpdatePlayerGroup(
        RaceParticipantStateSnapshot player,
        IReadOnlyList<(RaceEntrant Entrant, RaceParticipantStateSnapshot State)> standings)
    {
        _staminaBar.MaxValue = player.MaxStamina;
        _staminaBar.Value = player.CurrentStamina;
        var fraction = player.MaxStamina <= 0.0f
            ? 0.0f
            : Mathf.Clamp(player.CurrentStamina / player.MaxStamina, 0.0f, 1.0f);
        _staminaDialTexture.Region = new Rect2(
            Mathf.RoundToInt(fraction * (StaminaDialFrames - 1)) * 16,
            0,
            16,
            16);
        _staminaLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            Tr("UI_RACE_STAMINA"),
            Mathf.CeilToInt(player.CurrentStamina),
            Mathf.CeilToInt(player.MaxStamina));

        var place = 1 + standings.ToList().FindIndex(value =>
            string.Equals(value.Entrant.Participant.CreatureId, _playerId, StringComparison.Ordinal));
        if (place <= 0)
            place = standings.Count;
        _playerPlaceLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            Tr("UI_RACE_HUD_PLACE"),
            Ordinal(place),
            standings.Count);

        _cheerButton.Disabled = !_running ||
                                player.Finished ||
                                player.CheerSeconds > 0.0f ||
                                player.CurrentStamina < _entry!.Rules.CheerCost;
        _cheerButton.Text = Tr(player.CheerSeconds > 0.0f ? "UI_RACE_CHEERING" : "UI_RACE_CHEER");
    }

    private void UpdateStandings(
        IReadOnlyList<(RaceEntrant Entrant, RaceParticipantStateSnapshot State)> standings)
    {
        for (var index = 0; index < _standingRows.Count && index < standings.Count; index++)
        {
            var row = _standingRows[index];
            var entrant = standings[index].Entrant;
            var isPlayer = string.Equals(entrant.Participant.CreatureId, _playerId, StringComparison.Ordinal);

            row.Place.Text = (index + 1).ToString(CultureInfo.CurrentCulture);
            row.Name.Text = entrant.Participant.DisplayName;
            row.Status.Text = isPlayer
                ? Tr("UI_RACE_HUD_YOU")
                : index == 0
                    ? Tr("UI_RACE_HUD_LEAD")
                    : Ordinal(index + 1);

            if (isPlayer)
            {
                var highlight = new StyleBoxFlat { BgColor = StandingHighlight };
                highlight.SetCornerRadiusAll(3);
                highlight.SetContentMarginAll(1);
                row.Panel.AddThemeStyleboxOverride("panel", highlight);
                row.Name.AddThemeColorOverride("font_color", Color.FromHtml("#25341F"));
                row.Status.AddThemeColorOverride("font_color", Color.FromHtml("#25341F"));
                row.Place.AddThemeColorOverride("font_color", Color.FromHtml("#25341F"));
            }
            else
            {
                row.Panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
                row.Name.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
                row.Status.AddThemeColorOverride("font_color", Color.FromHtml("#6F7F66"));
                row.Place.AddThemeColorOverride("font_color", Color.FromHtml("#6F7F66"));
            }
        }
    }

    private void UpdateCourseGroup(RaceParticipantStateSnapshot player)
    {
        var progress = Mathf.Clamp((player.X - Course.StartX) / (Course.EndX - Course.StartX), 0.0f, 1.0f);
        _sectionLabel.Text = Tr(RaceCoursePresentationCatalog.SectionKeyFor(player.Terrain));
        _progressLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            Tr("UI_RACE_HUD_PROGRESS"),
            Mathf.RoundToInt(progress * 100.0f),
            _sectionLabel.Text);

        _miniMap.SetPoints(_entry!.Entrants.Select(entrant =>
        {
            var state = GetParticipantState(entrant);
            return new RaceMiniMapPoint
            {
                Id = entrant.Participant.CreatureId,
                Color = ParseTint(entrant.Participant.TintHex),
                Progress = Mathf.Clamp((state.X - Course.StartX) / (Course.EndX - Course.StartX), 0.0f, 1.0f),
                IsPlayer = entrant.Participant.CreatureId == _playerId
            };
        }).ToList());
    }

    /// <summary>
    /// Who is where right now. Finishers keep the order they actually finished in; everyone still
    /// racing is ranked by how far along the course they are. Read-only: the authoritative placement
    /// stays with the simulation and the lockstep frame.
    /// </summary>
    private IReadOnlyList<(RaceEntrant Entrant, RaceParticipantStateSnapshot State)> ComputeStandings()
    {
        var finishRank = new Dictionary<string, int>(StringComparer.Ordinal);
        if (_simulation != null)
        {
            var order = _simulation.FinishOrder;
            for (var index = 0; index < order.Count; index++)
                finishRank[order[index]] = index;
        }
        else if (_multiplayerFrame != null)
        {
            foreach (var participant in _multiplayerFrame.Participants.Where(value => value.Placement.HasValue))
                finishRank[participant.ParticipantId] = participant.Placement!.Value;
        }

        return _entry!.Entrants
            .Select(entrant => (Entrant: entrant, State: GetParticipantState(entrant)))
            .OrderBy(value => finishRank.TryGetValue(value.Entrant.Participant.CreatureId, out var rank)
                ? rank
                : int.MaxValue)
            .ThenByDescending(value => value.State.X)
            .ToList();
    }

    private string Ordinal(int place) => place switch
    {
        1 => Tr("UI_RACE_ORDINAL_1"),
        2 => Tr("UI_RACE_ORDINAL_2"),
        3 => Tr("UI_RACE_ORDINAL_3"),
        4 => Tr("UI_RACE_ORDINAL_4"),
        _ => string.Format(CultureInfo.CurrentCulture, Tr("UI_RACE_ORDINAL_N"), place)
    };

    private void ShowFault(string text)
    {
        _faultLabel.Text = text;
        _faultLabel.AddThemeColorOverride("font_color", Color.FromHtml("#F2B6AF"));
        _faultLabel.Visible = true;
        _faultPlaque.Visible = true;
    }
}
