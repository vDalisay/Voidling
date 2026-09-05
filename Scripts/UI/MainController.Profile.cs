using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Domain.Shop;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Inventory;
using Voidling.Presentation.UI.Shop;

namespace VoidlingGame;

public partial class MainController : Node
{
    private const float DetailsPanelRestX = 404.0f;
    private const float DetailsPanelHiddenX = 646.0f;
    private const double DetailsPanelEnterSeconds = 0.24;
    private const double DetailsPanelExitSeconds = 0.18;
    private const double ProfileProgressTweenSeconds = 0.34;

    private readonly Dictionary<string, double> _profileDisplayedProgress = new(StringComparer.Ordinal);
    private string _profileProgressCreatureId = string.Empty;

    private void RebuildDetailsPanel()
    {
        var data = _session.FindVoidling(_selectedId);
        var profile = data == null ? null : _session.CreateCreatureProfileProjection(data.Id);
        if (data == null || profile == null)
        {
            _profileProgressCreatureId = string.Empty;
            _profileDisplayedProgress.Clear();
            SlideOutDetailsPanel();
            return;
        }

        var sameCreature = string.Equals(_profileProgressCreatureId, data.Id, StringComparison.Ordinal);
        if (!sameCreature)
        {
            _profileDisplayedProgress.Clear();
            _profileProgressCreatureId = data.Id;
        }

        if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            _detailsPanel.QueueFree();
        _detailsPanel = null;

        _detailsPanel = UiFactory.CreatePanel(new Vector2(226, 318));
        _detailsPanel.Position = new Vector2(sameCreature ? DetailsPanelRestX : DetailsPanelHiddenX, 33);
        _detailsPanel.Size = new Vector2(226, 318);
        _uiRoot.AddChild(_detailsPanel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        _detailsPanel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 4);

        var nameButton = new Button
        {
            Text = data.Name,
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = "Click to rename"
        };
        UiFactory.ApplyPixelFont(nameButton, 14);
        nameButton.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        nameButton.AddThemeColorOverride("font_hover_color", Color.FromHtml("#263B31"));
        nameButton.AddThemeColorOverride("font_pressed_color", Color.FromHtml("#263B31"));
        heading.AddChild(nameButton);
        nameButton.Pressed += () => BeginInlineRename(heading, nameButton, data);

        var follow = UiFactory.CreateButton("◉");
        follow.CustomMinimumSize = new Vector2(28, 23);
        follow.TooltipText = "Follow this Voidling with the camera";
        follow.ToggleMode = true;
        follow.ButtonPressed = _garden.IsFollowing(data.Id);
        follow.Pressed += () =>
        {
            _garden.ToggleFollowVoidling(data.Id);
            follow.ButtonPressed = _garden.IsFollowing(data.Id);
        };
        heading.AddChild(follow);

        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(28, 23);
        close.Pressed += DeselectVoidling;
        heading.AddChild(close);
        box.AddChild(heading);

        var personalityLabel = PersonalityPresentationCatalog.LabelFor(profile.Personality);
        var stage = data.Stage == LifeStage.Adult
            ? $"Adult • {personalityLabel}"
            : $"Child • {Math.Max(0, (int)Math.Ceiling(GameRules.ChildToAdultSeconds - data.AgeSeconds))}s • {personalityLabel}";
        var stageLabel = UiFactory.CreateLabel(stage, 7);
        stageLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        stageLabel.TooltipText = $"Personality: {PersonalityPresentationCatalog.FlavorFor(profile.Personality)}";
        box.AddChild(stageLabel);

        var demeanor = UiFactory.CreateLabel(
            profile.CareDemeanor == Voidling.Application.Roster.CreatureCareDemeanor.Settled
                ? "Seems content and at ease."
                : "Seems restless and could use some attention.",
            6);
        demeanor.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        demeanor.CustomMinimumSize = new Vector2(194, 15);
        box.AddChild(demeanor);

        if (!string.IsNullOrWhiteSpace(profile.DiscoveredFavoriteFoodId))
        {
            var favoriteFood = UiFactory.CreateLabel(
                string.Format(Tr("UI_PROFILE_FAVORITE_FOOD"), StatPresentationCatalog.NameFor(profile.DiscoveredFavoriteFoodId)),
                6);
            favoriteFood.CustomMinimumSize = new Vector2(194, 15);
            box.AddChild(favoriteFood);
        }

        box.AddChild(CreatePassiveTrainingRow(data));

        foreach (var statId in GameRules.StatIds)
            box.AddChild(CreateProfileStatBlock(data, statId, sameCreature));

        var details = UiFactory.CreateButton("Details");
        details.CustomMinimumSize = new Vector2(194, 22);
        UiFactory.ApplyPixelFont(details, 8);
        details.Pressed += ShowDetails;
        box.AddChild(details);

        var parentText = data.ParentAId.Length > 0
            ? $"Parents: {_session.NameFor(data.ParentAId)} + {_session.NameFor(data.ParentBId)}"
            : "Parents: starter/store line";
        var parents = UiFactory.CreateLabel(parentText, 6);
        parents.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        parents.CustomMinimumSize = new Vector2(194, 18);
        box.AddChild(parents);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 5);
        var familyTree = UiFactory.CreateButton("Family tree");
        familyTree.CustomMinimumSize = new Vector2(103, 21);
        UiFactory.ApplyPixelFont(familyTree, 7);
        familyTree.Pressed += ShowFamilyTree;
        actions.AddChild(familyTree);

        var goodbye = UiFactory.CreateButton("Goodbye");
        goodbye.CustomMinimumSize = new Vector2(86, 21);
        UiFactory.ApplyPixelFont(goodbye, 7);
        goodbye.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        goodbye.Pressed += () => ShowGoodbyeFirst(data.Id);
        actions.AddChild(goodbye);
        box.AddChild(actions);

        if (!sameCreature)
            SlideInDetailsPanel(_detailsPanel);
    }

    /// <summary>
    /// Passive training is assigned by dropping this Voidling onto a Garden land tile, so the
    /// panel only reports what it is training and offers a way out of it.
    /// </summary>
    private Control CreatePassiveTrainingRow(VoidlingData data)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(194, 22) };
        row.AddThemeConstantOverride("separation", 4);

        var training = data.PassiveTrainingStatId.Length > 0;
        var label = UiFactory.CreateLabel(
            training
                ? string.Format(Tr("UI_PROFILE_PASSIVE_ON"), StatPresentationCatalog.NameFor(data.PassiveTrainingStatId))
                : Tr("UI_PROFILE_PASSIVE_OFF"),
            7);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.TooltipText = Tr("UI_PROFILE_PASSIVE_HINT");
        if (training)
            label.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(data.PassiveTrainingStatId));
        row.AddChild(label);

        if (!training)
            return row;

        var stop = UiFactory.CreateButton(Tr("UI_PROFILE_PASSIVE_STOP"));
        stop.CustomMinimumSize = new Vector2(52, 20);
        UiFactory.ApplyPixelFont(stop, 6);
        stop.Pressed += () =>
        {
            if (_session.StopPassiveTraining(data.Id))
                RebuildDetailsPanel();
        };
        row.AddChild(stop);
        return row;
    }

    private void SlideInDetailsPanel(PanelContainer panel)
    {
        panel.Modulate = new Color(panel.Modulate.R, panel.Modulate.G, panel.Modulate.B, 0.94f);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(panel, "position:x", DetailsPanelRestX, DetailsPanelEnterSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel, "modulate:a", 1.0f, DetailsPanelEnterSeconds * 0.65).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    private void SlideOutDetailsPanel()
    {
        if (_detailsPanel == null || !GodotObject.IsInstanceValid(_detailsPanel))
        {
            _detailsPanel = null;
            return;
        }
        var panel = _detailsPanel;
        _detailsPanel = null;
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(panel, "position:x", DetailsPanelHiddenX, DetailsPanelExitSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(panel, "modulate:a", 0.92f, DetailsPanelExitSeconds);
        tween.Finished += () => { if (GodotObject.IsInstanceValid(panel)) panel.QueueFree(); };
    }

    private void BeginInlineRename(HBoxContainer heading, Button nameButton, VoidlingData data)
    {
        if (!GodotObject.IsInstanceValid(nameButton) || !nameButton.Visible)
            return;
        nameButton.Visible = false;
        var edit = new LineEdit { Text = data.Name, MaxLength = 18, CustomMinimumSize = new Vector2(118, 23), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SelectAllOnFocus = true };
        UiFactory.ApplyPixelFont(edit, 10);
        heading.AddChild(edit);
        heading.MoveChild(edit, 0);
        var committed = false;
        void CommitRename()
        {
            if (committed || !GodotObject.IsInstanceValid(edit)) return;
            committed = true;
            if (!_session.RenameVoidling(data.Id, edit.Text))
            {
                edit.QueueFree();
                if (GodotObject.IsInstanceValid(nameButton)) nameButton.Visible = true;
            }
        }
        edit.TextSubmitted += _ => CommitRename();
        edit.FocusExited += CommitRename;
        edit.GrabFocus();
        edit.SelectAll();
    }

    private Control CreateProfileStatBlock(VoidlingData data, string statId, bool animateProgress)
    {
        var container = new VBoxContainer { CustomMinimumSize = new Vector2(194, 28) };
        container.AddThemeConstantOverride("separation", 1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var gene = GameRules.GetGene(data, statId);
        var effective = (int)Math.Round(GameRules.EffectiveStat(data, statId));
        var level = GameRules.StatLevel(data, statId);
        var count = _session.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;
        var color = StatPresentationCatalog.ColorFor(statId);
        var statName = StatPresentationCatalog.NameFor(statId);
        var label = UiFactory.CreateLabel($"{statName.ToUpperInvariant(),-7} {GameRules.GradeName(gene.ExpressedValue)}  LV{level:00}  {effective:00}", 7);
        label.CustomMinimumSize = new Vector2(142, 17);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        label.AddThemeConstantOverride("outline_size", statId == "stamina" ? 2 : 1);
        label.TooltipText = $"DNA {GameRules.GradeName(gene.AlleleA)}/{GameRules.GradeName(gene.AlleleB)} • training {GameRules.GetTrainingPoints(data, statId)}";
        row.AddChild(label);
        var use = UiFactory.CreateButton(TrainingItemEffectPresentation.BaseEffectText);
        use.CustomMinimumSize = new Vector2(48, 17);
        UiFactory.ApplyPixelFont(use, 6);
        use.Disabled = count <= 0;
        use.TooltipText = TrainingItemEffectPresentation.ProfileTooltip(statName, count);
        var capturedStat = statId;
        use.Pressed += () => _session.UseTrainingItem(_selectedId, capturedStat);
        row.AddChild(use);
        container.AddChild(row);
        container.AddChild(CreateStatProgressBar(data, statId, new Vector2(142, 6), animateProgress));
        return container;
    }

    private ProgressBar CreateStatProgressBar(VoidlingData data, string statId, Vector2 size, bool animateProgress)
    {
        var target = GameRules.StatLevelProgress(data, statId);
        var start = animateProgress && _profileDisplayedProgress.TryGetValue(statId, out var previous) ? previous : target;
        _profileDisplayedProgress[statId] = target;
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = start, ShowPercentage = false, CustomMinimumSize = size };
        var background = new StyleBoxFlat { BgColor = Color.FromHtml("#6D6658") };
        var fill = new StyleBoxFlat { BgColor = StatPresentationCatalog.ColorFor(statId) };
        background.CornerRadiusTopLeft = background.CornerRadiusTopRight = background.CornerRadiusBottomLeft = background.CornerRadiusBottomRight = 1;
        fill.CornerRadiusTopLeft = fill.CornerRadiusTopRight = fill.CornerRadiusBottomLeft = fill.CornerRadiusBottomRight = 1;
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);
        if (animateProgress && Math.Abs(target - start) > 0.0001)
        {
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(bar) || !bar.IsInsideTree()) return;
                bar.CreateTween().TweenProperty(bar, "value", target, ProfileProgressTweenSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            }).CallDeferred();
        }
        return bar;
    }

    private void ShowInventory()
    {
        var state = _session.State;
        var items = GameRules.StatIds
            .Select((statId, index) => new InventoryItemViewState(
                string.Format(Tr("UI_INVENTORY_TREAT"), StatPresentationCatalog.NameFor(statId)),
                state.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0,
                18 + index))
            .ToList();
        items.Add(new InventoryItemViewState(Tr("UI_INVENTORY_EGGS"), state.OwnedEggs.Count, -1, UsesEggIcon: true));

        var storedEggs = state.OwnedEggs
            .Where(egg => egg.State == EggState.Stored)
            .Select((egg, index) => new StoredEggViewState(
                egg.Id,
                string.Format(Tr("UI_INVENTORY_STORED_EGG"), index + 1),
                GameRules.TintColor(egg.TintHex)))
            .ToList();

        var storedLand = state.GardenModules
            .Where(module => !module.Placed)
            .OrderBy(module => module.StatId, StringComparer.Ordinal)
            .ThenBy(module => module.ShapeId, StringComparer.Ordinal)
            .ThenBy(module => module.Id, StringComparer.Ordinal)
            .Select(module => new StoredLandViewState(
                module.Id,
                LandShapePresentation.DescribeStoredPiece(module.ShapeId, module.StatId, module.Level),
                module.ShapeId,
                LandShapePresentation.TintFor(module.StatId)))
            .ToList();

        var failedEggs = state.OwnedEggs
            .Where(egg => egg.State == EggState.Failed)
            .Select((egg, index) => new FailedEggViewState(egg.Id, string.Format(Tr("UI_INVENTORY_FAILED_EGG"), index + 1)))
            .ToList();
        var eggShells = state.EggShells
            .Select((shell, index) => new EggShellViewState(shell.Id, $"Eggshell {index + 1}", GameRules.EggShellSalePrice))
            .ToList();
        var incubationSkipCount = state.UtilityItems.TryGetValue(ShopItemIds.FullIncubationSkip, out var ownedSkips) ? Math.Max(0, ownedSkips) : 0;
        var incubatingEggs = state.OwnedEggs
            .Where(egg => egg.State == EggState.Incubating && egg.IncubationSeconds < egg.RequiredIncubationSeconds)
            .Select((egg, index) => new IncubatingEggViewState(egg.Id, $"Egg {index + 1}", Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds))))
            .ToList();

        var box = OpenModal(Tr("UI_INVENTORY_TITLE"), new Vector2(380, 292));
        var screen = new InventoryScreen();
        screen.Configure(new InventoryScreenState(items, failedEggs, eggShells, incubationSkipCount, incubatingEggs, storedEggs, storedLand));
        screen.PlaceStoredEggRequested += egg =>
        {
            CloseModal();
            _garden.BeginEggPlacement(egg.EggId, egg.TintColor);
        };
        screen.PlaceStoredLandRequested += land =>
        {
            CloseModal();
            _garden.BeginLandPlacement(land.ModuleId, land.ShapeId);
        };
        screen.DiscardFailedEggRequested += eggId => { _session.DiscardFailedEgg(eggId); CallDeferred(nameof(ShowInventory)); };
        screen.SellEggShellRequested += shellId => { if (_session.SellEggShell(shellId)) CallDeferred(nameof(ShowInventory)); };
        screen.UseIncubationSkipRequested += eggId => { if (_session.UseFullIncubationSkip(eggId)) CallDeferred(nameof(ShowInventory)); };
        box.AddChild(screen);
    }
}
