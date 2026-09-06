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
/// The live race HUD keeps the world dominant: standings stay on the right, while the actionable
/// stamina/Cheer unit and course progress occupy opposite bottom corners.
///
/// Presentation only. Every value shown is read from the simulation or lockstep snapshot the rest of
/// <see cref="RaceScreen"/> already owns; nothing here can change a race outcome.
/// </summary>
public partial class RaceScreen
{
    /// <summary>Canvas layer the in-race HUD lives on, below results and pause.</summary>
    internal const int HudCanvasLayer = 20;

    /// <summary>
    /// The HUD groups, including the pause reminder. Named so the
    /// CI probe can require each one on screen and clear of the others.
    /// </summary>
    internal static readonly string[] HudGroupNames =
    {
        "RaceHudMenuHint", "RaceHudStandings", "RaceHudCourse", "RaceHudPlayer"
    };

    private static readonly Color StaminaColor = Color.FromHtml("#7FB56B");
    private static readonly Color StandingHighlight = Color.FromHtml("#8FBF7C");

    private Button _cheerButton = null!;
    private ProgressBar _staminaBar = null!;
    private Label _staminaLabel = null!;
    private Control _staminaTicks = null!;
    private double _staminaTickMaximum = -1.0;
    private Label _faultLabel = null!;
    private ColorRect _faultPlaque = null!;
    private RaceMiniMap _miniMap = null!;

    private Label _progressLabel = null!;
    private readonly List<StandingRow> _standingRows = new();

    /// <summary>One opponent-standings line. Positional: row 0 always shows whoever is leading.</summary>
    private sealed record StandingRow(PanelContainer Panel, Label Place, Label Name, Label Status);

    private void CreateHud()
    {
        var canvas = new CanvasLayer { Layer = HudCanvasLayer, Name = "RaceHud" };
        AddChild(canvas);

        canvas.AddChild(BuildMenuHint());
        canvas.AddChild(BuildStandings());
        canvas.AddChild(BuildCoursePanel());
        canvas.AddChild(BuildPlayerPanel());
        BuildFaultStrip(canvas);
    }

    // ---- Groups -------------------------------------------------------------------------------

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
        var panel = ButtonChromePanel("RaceHudCourse", new Vector2(468, 288), new Vector2(164, 64));
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
        var panel = ButtonChromePanel("RaceHudPlayer", new Vector2(8, 306), new Vector2(228, 46));
        var row = new HBoxContainer();
        panel.AddChild(row);

        _cheerButton = UiFactory.CreateButton(string.Empty);
        _cheerButton.Name = "CheerButton";
        _cheerButton.Icon = UiFactory.CreateGardenIcon(14, 0);
        _cheerButton.ExpandIcon = true;
        _cheerButton.CustomMinimumSize = new Vector2(34, 34);
        _cheerButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _cheerButton.TooltipText = Tr("UI_RACE_CHEER");
        StyleCheerIcon(_cheerButton);
        _cheerButton.Pressed += CheerPlayer;
        var cheerCost = UiFactory.CreateLabel(Mathf.CeilToInt(_entry!.Rules.CheerCost).ToString(CultureInfo.CurrentCulture), 7);
        cheerCost.Name = "CheerCost";
        cheerCost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        cheerCost.HorizontalAlignment = HorizontalAlignment.Center;
        cheerCost.VerticalAlignment = VerticalAlignment.Center;
        cheerCost.MouseFilter = Control.MouseFilterEnum.Ignore;
        cheerCost.AddThemeColorOverride("font_color", Color.FromHtml("#5B431F"));
        cheerCost.AddThemeConstantOverride("outline_size", 2);
        cheerCost.AddThemeColorOverride("font_outline_color", Color.FromHtml("#FFF0B0"));
        _cheerButton.AddChild(cheerCost);
        row.AddChild(_cheerButton);

        _staminaBar = new ProgressBar
        {
            Name = "StaminaBar",
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 18),
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

        _staminaTicks = new Control { Name = "StaminaTicks", MouseFilter = Control.MouseFilterEnum.Ignore };
        _staminaTicks.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _staminaBar.AddChild(_staminaTicks);
        _staminaLabel = UiFactory.CreateLabel(string.Empty, 7);
        _staminaLabel.Name = "StaminaValue";
        _staminaLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _staminaLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _staminaLabel.VerticalAlignment = VerticalAlignment.Center;
        _staminaLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _staminaLabel.AddThemeColorOverride("font_color", Color.FromHtml("#34452F"));
        _staminaLabel.AddThemeConstantOverride("outline_size", 2);
        _staminaLabel.AddThemeColorOverride("font_outline_color", Color.FromHtml("#EEF4DF"));
        _staminaBar.AddChild(_staminaLabel);
        row.AddChild(_staminaBar);
        return panel;
    }

    private static void StyleCheerIcon(Button button)
    {
        static StyleBoxFlat State(Color color) => new() { BgColor = color, CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };

        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", State(new Color(1.0f, 0.91f, 0.48f, 0.35f)));
        button.AddThemeStyleboxOverride("pressed", State(new Color(0.65f, 0.43f, 0.16f, 0.35f)));
        button.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());
        var focus = State(new Color(1.0f, 0.95f, 0.68f, 0.22f));
        focus.DrawCenter = false;
        focus.BorderColor = Color.FromHtml("#8A682F");
        focus.SetBorderWidthAll(2);
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeColorOverride("icon_disabled_color", new Color(0.55f, 0.55f, 0.48f, 0.65f));
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

    /// <summary>Uses the same premium normal-state texture as Return to Garden.</summary>
    private static PanelContainer ButtonChromePanel(string name, Vector2 position, Vector2 size)
    {
        var source = UiFactory.CreateButton(string.Empty);
        var panel = new PanelContainer { Name = name, Position = position, Size = size, CustomMinimumSize = size };
        panel.AddThemeStyleboxOverride("panel", (StyleBox)source.GetThemeStylebox("normal").Duplicate());
        source.Free();
        return panel;
    }

    // ---- Live values --------------------------------------------------------------------------

    private void UpdateHud()
    {
        if (_entry == null || _miniMap == null || !TryGetPlayerState(out var player))
            return;

        var standings = ComputeStandings();
        UpdatePlayerGroup(player);
        UpdateStandings(standings);
        UpdateCourseGroup(player);
    }

    private void UpdatePlayerGroup(RaceParticipantStateSnapshot player)
    {
        _staminaBar.MaxValue = player.MaxStamina;
        _staminaBar.Value = player.CurrentStamina;
        RebuildStaminaTicks(player.MaxStamina);
        _staminaLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            Tr("UI_RACE_STAMINA"),
            Mathf.CeilToInt(player.CurrentStamina),
            Mathf.CeilToInt(player.MaxStamina));

        _cheerButton.Disabled = !_running ||
                                player.Finished ||
                                player.CheerSeconds > 0.0f ||
                                player.CurrentStamina < _entry!.Rules.CheerCost;
        _cheerButton.TooltipText = Tr(player.CheerSeconds > 0.0f ? "UI_RACE_CHEERING" : "UI_RACE_CHEER");
    }

    private void RebuildStaminaTicks(double maximum)
    {
        if (Mathf.IsEqualApprox((float)_staminaTickMaximum, (float)maximum))
            return;

        foreach (var child in _staminaTicks.GetChildren())
            child.QueueFree();
        _staminaTickMaximum = maximum;
        if (maximum <= 0.0)
            return;

        for (var stamina = 50; stamina < maximum; stamina += 50)
        {
            var tick = new ColorRect
            {
                Name = $"StaminaTick{stamina}",
                Color = new Color(0.96f, 0.92f, 0.70f, 0.9f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var anchor = (float)(stamina / maximum);
            tick.SetAnchor(Side.Left, anchor);
            tick.SetAnchor(Side.Right, anchor);
            tick.SetAnchor(Side.Bottom, 1.0f);
            tick.OffsetLeft = -1.0f;
            tick.OffsetRight = 1.0f;
            _staminaTicks.AddChild(tick);
        }
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
        var section = Tr(RaceCoursePresentationCatalog.SectionKeyFor(player.Terrain));
        _progressLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            Tr("UI_RACE_HUD_PROGRESS"),
            Mathf.RoundToInt(progress * 100.0f),
            section);

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
