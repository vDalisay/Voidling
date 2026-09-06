using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Roster;
using Voidling.Presentation.UI.Common;
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
    private string _trainingStat = string.Empty;
    public string CreatureId { get; private set; } = string.Empty;

    public void Build(CreatureProfileProjection profile)
    {
        CreatureId = profile.CreatureId;
        Name = "GardenInspector";
        CustomMinimumSize = new Vector2(162, 230);
        var chrome = UiFactory.CreatePanel(Vector2.Zero);
        var style = (StyleBoxTexture)chrome.GetThemeStylebox("panel").Duplicate();
        chrome.Free();
        style.ContentMarginLeft = style.ContentMarginRight = 9;
        style.ContentMarginTop = style.ContentMarginBottom = 9;
        AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        AddChild(box);
        var identity = new HBoxContainer();
        identity.AddThemeConstantOverride("separation", 5);
        _visualProfile = profile;
        _portrait = CreatePortrait(profile);
        identity.AddChild(_portrait);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(UiFactory.CreateLabel(Tr("UI_GARDEN_COMPANION"), 8));
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
        close.Text = "×";
        close.TooltipText = Tr("UI_COMMON_CLOSE");
        close.CustomMinimumSize = new Vector2(18, 18);
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
            label.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(stat.StatId));
            label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
            label.AddThemeConstantOverride("outline_size", 1);
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
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
        {
            var buttonStyle = (StyleBoxTexture)_treat.GetThemeStylebox(state).Duplicate();
            buttonStyle.ModulateColor = Color.FromHtml("#587E60");
            _treat.AddThemeStyleboxOverride(state, buttonStyle);
        }
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            _treat.AddThemeColorOverride(state, Color.FromHtml("#FFF7DE"));
        actions.AddChild(_treat);
        actions.AddChild(MakeButton("Details", "UI_GARDEN_DETAILS", () => DetailsRequested?.Invoke()));
        actions.AddChild(MakeButton("Family", "UI_GARDEN_FAMILY", () => FamilyRequested?.Invoke()));
        _follow = MakeButton("Follow", "UI_PROFILE_FOLLOW", () => FollowRequested?.Invoke());
        _follow.TooltipText = Tr("UI_GARDEN_FOLLOW");
        _follow.ToggleMode = true;
        actions.AddChild(_follow);
        box.AddChild(actions);
    }

    public override void _Process(double delta)
    {
        var pulse = (Mathf.Sin((float)Time.GetTicksMsec() / 240.0f) + 1.0f) * 0.5f;
        foreach (var (statId, view) in _stats)
        {
            var active = string.Equals(statId, _trainingStat, StringComparison.Ordinal);
            view.background.SetBorderWidthAll(active ? 1 : 0);
            var border = StatPresentationCatalog.ColorFor(statId);
            border.A = 0.45f + pulse * 0.45f;
            view.background.BorderColor = border;
        }
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
            PersonalityPresentationCatalog.LabelFor(profile.Personality));
        _stage.TooltipText = PersonalityPresentationCatalog.FlavorFor(profile.Personality);
        _care.Text = Tr(profile.CareDemeanor == CreatureCareDemeanor.Settled ? "UI_PROFILE_CONTENT" : "UI_PROFILE_RESTLESS");
        _favorite.Visible = !string.IsNullOrEmpty(profile.DiscoveredFavoriteFoodId);
        _favorite.Text = _favorite.Visible ? string.Format(Tr("UI_PROFILE_FAVORITE_FOOD"),
            StatPresentationCatalog.NameFor(profile.DiscoveredFavoriteFoodId!)) : string.Empty;
        _favorite.TooltipText = _favorite.Text;
        foreach (var stat in profile.Stats)
        {
            _stats[stat.StatId].rank.Text = stat.InheritedRank;
            _stats[stat.StatId].level.Text = string.Format(Tr("UI_PROFILE_LEVEL_VALUE"), stat.TrainingLevel);
            _stats[stat.StatId].progress.Value = stat.TrainingProgress;
            _stats[stat.StatId].rate.Visible = stat.TrainingPointsPerSecond > 0;
            _stats[stat.StatId].rate.Text = stat.TrainingPointsPerSecond > 0
                ? string.Format(Tr("UI_PROFILE_EXP_PER_SECOND"), stat.TrainingPointsPerSecond)
                : string.Empty;
        }
        _follow.SetPressedNoSignal(following);
        _trainingStat = trainingStat;
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
        var background = new StyleBoxFlat { BgColor = Color.FromHtml("#C5B798") };
        var fill = new StyleBoxFlat { BgColor = StatPresentationCatalog.ColorFor(statId) };
        background.SetCornerRadiusAll(1);
        fill.SetCornerRadiusAll(1);
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
