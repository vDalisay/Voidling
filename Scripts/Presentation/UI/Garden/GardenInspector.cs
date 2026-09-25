using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Audio;
using Voidling.Application.Roster;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>Compact, persistent selection view. Refreshing simulation values keeps input focus alive.</summary>
public partial class GardenInspector : PanelContainer
{
    public event Action<string>? RenameRequested;
    public event Action? CloseRequested;
    public event Action? TreatRequested;
    public event Action? DetailsRequested;
    public event Action? FamilyRequested;
    public event Action? FollowRequested;

    private LineEdit _name = null!;
    private Label _stage = null!;
    private Label _care = null!;
    private Label _favorite = null!;
    private Button _follow = null!;
    private Button _treat = null!;
    private TextureRect _portrait = null!;
    private CreatureProfileProjection _visualProfile = null!;
    private readonly Dictionary<string, (Label rank, Label level, Label rate, ProgressBar progress, StyleBoxFlat background)> _stats = new();
    private readonly Dictionary<string, int> _levels = new();
    private string _trainingStat = string.Empty;
    public string CreatureId { get; private set; } = string.Empty;

    public void Build(CreatureProfileProjection profile)
    {
        CreatureId = profile.CreatureId;
        Name = "GardenInspector";
        CustomMinimumSize = new Vector2(162, 230);
        var style = UiSkin.Window();
        style.ContentMarginLeft = style.ContentMarginRight = 9;
        style.ContentMarginTop = style.ContentMarginBottom = 9;
        AddThemeStyleboxOverride("panel", style);
        // Pops in beside the Garden rather than blinking into place.
        Callable.From(() => UiMotion.Appear(this, 0.0, UiMotion.Normal, 0.08f)).CallDeferred();

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        AddChild(box);
        var identity = new HBoxContainer();
        identity.AddThemeConstantOverride("separation", 5);
        _visualProfile = profile;
        _portrait = CreatePortrait(profile);
        identity.AddChild(_portrait);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var eyebrow = UiFactory.CreateLabel(Tr("UI_GARDEN_COMPANION"), 7);
        eyebrow.AddThemeColorOverride("font_color", UiSkin.InkSoft);
        heading.AddChild(eyebrow);
        _name = new LineEdit
        {
            Text = profile.Name, MaxLength = 18, ExpandToTextLength = false,
            CustomMinimumSize = new Vector2(60, 20), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled, TooltipText = Tr("UI_GARDEN_RENAME")
        };
        UiFactory.StyleInput(_name);
        UiFactory.ApplyPixelFont(_name, 11);
        _name.TextSubmitted += _ => _name.ReleaseFocus();
        _name.FocusExited += () => RenameRequested?.Invoke(_name.Text);
        heading.AddChild(_name);
        identity.AddChild(heading);
        var close = MakeButton("CloseInspector", "UI_COMMON_CLOSE", () => CloseRequested?.Invoke());
        UiSkin.ApplyIconButton(close, UiSkin.IconGlyph.SmallClose);
        close.TooltipText = Tr("UI_COMMON_CLOSE");
        close.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        identity.AddChild(close);
        box.AddChild(identity);

        _stage = UiFactory.CreateLabel(string.Empty, 8);
        _stage.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        box.AddChild(_stage);
        _care = UiFactory.CreateLabel(string.Empty, 8);
        _care.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_care);
        _favorite = UiFactory.CreateLabel(string.Empty, 8);
        _favorite.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        box.AddChild(_favorite);

        var table = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        table.AddThemeConstantOverride("separation", 2);
        table.AddChild(CreateStatRow(
            UiFactory.CreateLabel(Tr("UI_PROFILE_TRAINED_STAT"), 7),
            UiFactory.CreateLabel(Tr("UI_PROFILE_RANK"), 7),
            UiFactory.CreateLabel(Tr("UI_PROFILE_LEVEL"), 7)));
        foreach (var stat in profile.Stats)
        {
            var label = UiFactory.CreateLabel(StatPresentationCatalog.NameFor(stat.StatId), 8);
            // The paper-ink shade of the stat colour, as in the treat chooser: the bright bar colours
            // (yellow Swim above all) are unreadable as text on the tan panel.
            label.AddThemeColorOverride("font_color", PaperCard.Ink(StatPresentationCatalog.ColorFor(stat.StatId)));
            var statName = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            statName.AddThemeConstantOverride("separation", 3);
            statName.AddChild(label);
            var rate = UiFactory.CreateLabel(string.Empty, 6);
            rate.Name = "Rate_" + stat.StatId;
            rate.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            statName.AddChild(rate);
            var rank = UiFactory.CreateLabel(string.Empty, 8);
            var level = UiFactory.CreateLabel(string.Empty, 8);
            var block = new VBoxContainer();
            block.AddThemeConstantOverride("separation", 1);
            block.AddChild(CreateStatRow(statName, rank, level));
            var (progress, background) = CreateProgressBar(stat.StatId);
            block.AddChild(progress);
            table.AddChild(block);
            _stats.Add(stat.StatId, (rank, level, rate, progress, background));
        }
        box.AddChild(table);
        var actions = new GridContainer { Columns = 2 };
        actions.AddThemeConstantOverride("h_separation", 4);
        actions.AddThemeConstantOverride("v_separation", 3);
        _treat = MakeButton("GiveTreat", "UI_PROFILE_GIVE_TREAT", () => TreatRequested?.Invoke());
        UiFactory.ApplyPrimaryStyle(_treat);
        actions.AddChild(_treat);
        actions.AddChild(MakeButton("Details", "UI_GARDEN_DETAILS", () => DetailsRequested?.Invoke()));
        actions.AddChild(MakeButton("Family", "UI_GARDEN_FAMILY", () => FamilyRequested?.Invoke()));
        _follow = MakeButton("Follow", "UI_PROFILE_FOLLOW", () => FollowRequested?.Invoke());
        _follow.TooltipText = Tr("UI_GARDEN_FOLLOW");
        _follow.ToggleMode = true;
        actions.AddChild(_follow);
        box.AddChild(actions);
    }

    /// <summary>
    /// The stat being trained on its land wears a pulsing border. A looping tween on that one
    /// bar's border replaces a per-frame update of every bar.
    /// </summary>
    private void PulseTrainingStat(string statId)
    {
        foreach (var (id, view) in _stats)
        {
            if (id == statId) continue;
            UiMotion.Kill(view.progress, "pulse");
            view.background.SetBorderWidthAll(0);
        }
        if (!_stats.TryGetValue(statId, out var active)) return;
        active.background.SetBorderWidthAll(1);
        var color = StatPresentationCatalog.ColorFor(statId);
        active.background.BorderColor = color;
        var pulse = UiMotion.Loop(active.progress, "pulse");
        if (pulse == null) return;
        var dim = color;
        dim.A = 0.45f;
        pulse.TweenProperty(active.background, "border_color", dim, 0.38).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(active.background, "border_color", color, 0.38).SetTrans(Tween.TransitionType.Sine);
    }

    public void FocusCare() => _treat.GrabFocus();

    public void Render(CreatureProfileProjection profile, string trainingStat, bool following)
    {
        if (profile.VisualTypeId != _visualProfile.VisualTypeId || profile.PaletteHue != _visualProfile.PaletteHue ||
            profile.TintHex != _visualProfile.TintHex || profile.HasAngelMutation != _visualProfile.HasAngelMutation ||
            profile.OtherMutationCount != _visualProfile.OtherMutationCount || !profile.LayerIds.SequenceEqual(_visualProfile.LayerIds))
        {
            var parent = _portrait.GetParent();
            parent.RemoveChild(_portrait);
            _portrait.QueueFree();
            _portrait = CreatePortrait(profile);
            parent.AddChild(_portrait);
            parent.MoveChild(_portrait, 0);
            _visualProfile = profile;
        }
        if (!_name.HasFocus()) _name.Text = profile.Name;
        _stage.Text = string.Format(Tr(profile.IsAdult ? "UI_PROFILE_ADULT_PERSONALITY" : "UI_PROFILE_CHILD_PERSONALITY"),
            PersonalityPresentationCatalog.LabelFor(profile.Personality),
            VoidlingFormPresentationCatalog.NameFor(profile.VisualTypeId));
        _stage.TooltipText = PersonalityPresentationCatalog.FlavorFor(profile.Personality);
        _care.Text = Tr(profile.CareDemeanor == CreatureCareDemeanor.Settled ? "UI_PROFILE_CONTENT" : "UI_PROFILE_RESTLESS");
        _favorite.Visible = !string.IsNullOrEmpty(profile.DiscoveredFavoriteFoodId);
        _favorite.Text = _favorite.Visible ? string.Format(Tr("UI_PROFILE_FAVORITE_FOOD"),
            StatPresentationCatalog.NameFor(profile.DiscoveredFavoriteFoodId!)) : string.Empty;
        _favorite.TooltipText = _favorite.Text;
        foreach (var stat in profile.Stats)
        {
            var view = _stats[stat.StatId];
            view.rank.Text = stat.InheritedRank;
            view.level.Text = string.Format(Tr("UI_PROFILE_LEVEL_VALUE"), stat.TrainingLevel);
            var levelledUp = _levels.TryGetValue(stat.StatId, out var previousLevel) && stat.TrainingLevel > previousLevel;
            _levels[stat.StatId] = stat.TrainingLevel;
            SetProgress(view.progress, stat.TrainingProgress, levelledUp);
            if (levelledUp)
            {
                // A level earned while watching: the level pops, the bar flashes and sparks fly.
                UiMotion.Pop(view.level, 0.3f, UiMotion.Slow);
                UiMotion.Flash(view.progress, new Color(1.6f, 1.6f, 1.4f), UiMotion.Slow);
                PixelBurst.Spawn(this, view.level.GetGlobalRect().GetCenter(), PixelBurst.Palette.Leafy, 12);
                UiSounds.Play(this, UiCue.Confirm);
            }
            _stats[stat.StatId].rate.Visible = stat.TrainingPointsPerSecond > 0;
            _stats[stat.StatId].rate.Text = stat.TrainingPointsPerSecond > 0
                ? string.Format(Tr("UI_PROFILE_EXP_PER_SECOND"), stat.TrainingPointsPerSecond)
                : string.Empty;
        }
        _follow.SetPressedNoSignal(following);
        if (!string.Equals(_trainingStat, trainingStat, StringComparison.Ordinal) || !_pulseStarted)
        {
            _pulseStarted = true;
            _trainingStat = trainingStat;
            PulseTrainingStat(trainingStat);
        }
    }

    private bool _pulseStarted;

    /// <summary>Progress fills smoothly as it grows; a new level (the bar wrapping) starts clean.</summary>
    private static void SetProgress(ProgressBar bar, double value, bool wrapped)
    {
        if (wrapped || value < bar.Value || !bar.IsVisibleInTree())
        {
            UiMotion.Kill(bar, "fill");
            bar.Value = value;
            return;
        }
        if (Math.Abs(value - bar.Value) < 0.0005) return;
        var fill = UiMotion.Start(bar, "fill");
        if (fill == null) { bar.Value = value; return; }
        fill.TweenProperty(bar, "value", value, 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    private static HBoxContainer CreateStatRow(Control name, Control rank, Control level)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rank.CustomMinimumSize = new Vector2(24, 0);
        level.CustomMinimumSize = new Vector2(29, 0);
        row.AddChild(name);
        row.AddChild(rank);
        row.AddChild(level);
        return row;
    }

    private static (ProgressBar bar, StyleBoxFlat background) CreateProgressBar(string statId)
    {
        var bar = new ProgressBar
        {
            Name = "Progress_" + statId,
            MinValue = 0,
            MaxValue = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 4)
        };
        // Square pixel bars: a dark trough and a fill with a lit top row, never anti-aliased.
        var background = UiSkin.BarTrack();
        var fill = new StyleBoxFlat
        {
            BgColor = StatPresentationCatalog.ColorFor(statId), AntiAliasing = false,
            BorderColor = StatPresentationCatalog.ColorFor(statId).Lightened(0.35f), BorderWidthTop = 1
        };
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);
        return (bar, background);
    }

    private static TextureRect CreatePortrait(CreatureProfileProjection profile)
        => UiFactory.CreatePortrait(new VoidlingVisualAppearance(
            profile.VisualTypeId, profile.PaletteHue, profile.LayerIds, profile.TintHex),
            profile.HasAngelMutation, profile.OtherMutationCount, new Vector2(27, 35));

    private static Button MakeButton(string name, string key, Action action)
    {
        var button = UiFactory.CreateButton(TranslationServer.Translate(key));
        button.Name = name;
        button.CustomMinimumSize = new Vector2(68, 20);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        UiFactory.ApplyPixelFont(button, 8);
        button.Pressed += action;
        return button;
    }
}
