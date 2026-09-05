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
    public event Action? StopTrainingRequested;

    private LineEdit _name = null!;
    private Label _stage = null!;
    private Label _care = null!;
    private Label _favorite = null!;
    private Label _training = null!;
    private Button _follow = null!;
    private Button _treat = null!;
    private Button _stop = null!;
    private TextureRect _portrait = null!;
    private CreatureProfileProjection _visualProfile = null!;
    private readonly Dictionary<string, (Label rank, Label level)> _stats = new();
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

        var table = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        table.AddThemeConstantOverride("h_separation", 8);
        table.AddThemeConstantOverride("v_separation", 2);
        foreach (var key in new[] { "UI_PROFILE_TRAINED_STAT", "UI_PROFILE_RANK", "UI_PROFILE_LEVEL" })
            table.AddChild(UiFactory.CreateLabel(Tr(key), 8));
        foreach (var stat in profile.Stats)
        {
            var label = UiFactory.CreateLabel(StatPresentationCatalog.NameFor(stat.StatId), 8);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            table.AddChild(label);
            var rank = UiFactory.CreateLabel(string.Empty, 8);
            var level = UiFactory.CreateLabel(string.Empty, 8);
            table.AddChild(rank);
            table.AddChild(level);
            _stats.Add(stat.StatId, (rank, level));
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
        var training = new HBoxContainer();
        _training = UiFactory.CreateLabel(string.Empty, 8);
        _training.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _training.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _training.TooltipText = Tr("UI_PROFILE_PASSIVE_HINT");
        training.AddChild(_training);
        _stop = MakeButton("StopTraining", "UI_PROFILE_PASSIVE_STOP", () => StopTrainingRequested?.Invoke());
        _stop.CustomMinimumSize = new Vector2(34, 19);
        training.AddChild(_stop);
        box.AddChild(training);
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
        }
        _follow.SetPressedNoSignal(following);
        _stop.Visible = trainingStat.Length > 0;
        _training.Text = trainingStat.Length > 0
            ? string.Format(Tr("UI_PROFILE_PASSIVE_ON"), StatPresentationCatalog.NameFor(trainingStat))
            : Tr("UI_PROFILE_PASSIVE_OFF");
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
