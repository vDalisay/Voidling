using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Creatures;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Inventory;

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
        var profile = _session.CreateVoidlingProfileProjection(_selectedId);
        if (data == null || profile == null)
        {
            _profileProgressCreatureId = string.Empty;
            _profileDisplayedProgress.Clear();
            SlideOutDetailsPanel();
            return;
        }

        var sameCreature = string.Equals(_profileProgressCreatureId, profile.CreatureId, StringComparison.Ordinal);
        if (!sameCreature)
        {
            _profileDisplayedProgress.Clear();
            _profileProgressCreatureId = profile.CreatureId;
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
            Text = profile.DisplayName,
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
        follow.ButtonPressed = _garden.IsFollowing(profile.CreatureId);
        follow.Pressed += () =>
        {
            _garden.ToggleFollowVoidling(profile.CreatureId);
            follow.ButtonPressed = _garden.IsFollowing(profile.CreatureId);
        };
        heading.AddChild(follow);

        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(28, 23);
        close.Pressed += DeselectVoidling;
        heading.AddChild(close);
        box.AddChild(heading);

        var stage = profile.IsAdult
            ? "Adult"
            : $"Child • {Math.Max(0, (int)Math.Ceiling(GameRules.ChildToAdultSeconds - data.AgeSeconds))}s to adult";
        box.AddChild(UiFactory.CreateLabel(stage, 7));
        box.AddChild(CreatePassiveTrainingRow(data));

        foreach (var stat in profile.Stats)
            box.AddChild(CreateProfileStatBlock(stat, sameCreature));

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
        goodbye.Pressed += () => ShowGoodbyeFirst(profile.CreatureId);
        actions.AddChild(goodbye);
        box.AddChild(actions);

        if (!sameCreature)
            SlideInDetailsPanel(_detailsPanel);
    }

    private Control CreatePassiveTrainingRow(VoidlingData data)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(194, 22) };
        row.AddThemeConstantOverride("separation", 4);

        var label = UiFactory.CreateLabel("Passive", 7);
        label.CustomMinimumSize = new Vector2(52, 20);
        label.TooltipText = "Slow open-game training. Active treats remain faster.";
        row.AddChild(label);

        var selector = new OptionButton
        {
            CustomMinimumSize = new Vector2(138, 20),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Choose one stat to train slowly while the game remains open."
        };
        UiFactory.ApplyPixelFont(selector, 7);
        selector.AddItem("Off");
        var selected = 0;
        for (var i = 0; i < GameRules.StatIds.Length; i++)
        {
            var statId = GameRules.StatIds[i];
            selector.AddItem(StatPresentationCatalog.NameFor(statId));
            if (string.Equals(data.PassiveTrainingStatId, statId, StringComparison.Ordinal))
                selected = i + 1;
        }
        selector.Select(selected);
        selector.ItemSelected += index =>
        {
            var selectedIndex = (int)index;
            var statId = selectedIndex <= 0 ? string.Empty : GameRules.StatIds[selectedIndex - 1];
            if (_session.SetPassiveTraining(data.Id, statId))
                RebuildDetailsPanel();
        };
        row.AddChild(selector);
        return row;
    }

    private void SlideInDetailsPanel(PanelContainer panel)
    {
        panel.Modulate = new Color(panel.Modulate.R, panel.Modulate.G, panel.Modulate.B, 0.94f);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(panel, "position:x", DetailsPanelRestX, DetailsPanelEnterSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel, "modulate:a", 1.0f, DetailsPanelEnterSeconds * 0.65)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
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
        tween.TweenProperty(panel, "position:x", DetailsPanelHiddenX, DetailsPanelExitSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        tween.TweenProperty(panel, "modulate:a", 0.92f, DetailsPanelExitSeconds);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(panel))
                panel.QueueFree();
        };
    }

    private void BeginInlineRename(HBoxContainer heading, Button nameButton, VoidlingData data)
    {
        if (!GodotObject.IsInstanceValid(nameButton) || !nameButton.Visible)
            return;

        nameButton.Visible = false;
        var edit = new LineEdit
        {
            Text = data.Name,
            MaxLength = 18,
            CustomMinimumSize = new Vector2(118, 23),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SelectAllOnFocus = true
        };
        UiFactory.ApplyPixelFont(edit, 10);
        heading.AddChild(edit);
        heading.MoveChild(edit, 0);

        var committed = false;
        void CommitRename()
        {
            if (committed || !GodotObject.IsInstanceValid(edit))
                return;
            committed = true;

            if (!_session.RenameVoidling(data.Id, edit.Text))
            {
                edit.QueueFree();
                if (GodotObject.IsInstanceValid(nameButton))
                    nameButton.Visible = true;
            }
        }

        edit.TextSubmitted += _ => CommitRename();
        edit.FocusExited += CommitRename;
        edit.GrabFocus();
        edit.SelectAll();
    }

    private Control CreateProfileStatBlock(VoidlingStatProfileProjection stat, bool animateProgress)
    {
        var statId = stat.StatId;
        var container = new VBoxContainer { CustomMinimumSize = new Vector2(194, 28) };
        container.AddThemeConstantOverride("separation", 1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var effective = (int)Math.Round(stat.EffectiveValue);
        var level = stat.TrainingLevel;
        var count = _session.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;
        var color = StatPresentationCatalog.ColorFor(statId);

        var label = UiFactory.CreateLabel(
            $"{StatPresentationCatalog.NameFor(statId).ToUpperInvariant(),-7} {StatPresentationCatalog.RankFor(stat.ExpressedPotentialRank)}  LV{level:00}  {effective:00}", 7);
        label.CustomMinimumSize = new Vector2(142, 17);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        label.AddThemeConstantOverride("outline_size", statId == "stamina" ? 2 : 1);
        label.TooltipText = $"DNA {StatPresentationCatalog.RankFor(stat.DnaProfile1Rank)}/{StatPresentationCatalog.RankFor(stat.DnaProfile2Rank)} • training {stat.TrainingPoints}";
        row.AddChild(label);

        var use = UiFactory.CreateButton($"+1 ({count})");
        use.CustomMinimumSize = new Vector2(48, 17);
        UiFactory.ApplyPixelFont(use, 6);
        use.Disabled = count <= 0;
        var capturedStat = statId;
        use.Pressed += () => _session.UseTrainingItem(_selectedId, capturedStat);
        row.AddChild(use);
        container.AddChild(row);

        var bar = CreateStatProgressBar(statId, stat.TrainingLevelProgress, new Vector2(142, 6), animateProgress);
        container.AddChild(bar);
        return container;
    }

    private ProgressBar CreateStatProgressBar(string statId, double target, Vector2 size, bool animateProgress)
    {
        var start = animateProgress && _profileDisplayedProgress.TryGetValue(statId, out var previous)
            ? previous
            : target;
        _profileDisplayedProgress[statId] = target;

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = start,
            ShowPercentage = false,
            CustomMinimumSize = size
        };
        var background = new StyleBoxFlat { BgColor = Color.FromHtml("#6D6658") };
        var fill = new StyleBoxFlat { BgColor = StatPresentationCatalog.ColorFor(statId) };
        background.CornerRadiusTopLeft = background.CornerRadiusTopRight = 1;
        background.CornerRadiusBottomLeft = background.CornerRadiusBottomRight = 1;
        fill.CornerRadiusTopLeft = fill.CornerRadiusTopRight = 1;
        fill.CornerRadiusBottomLeft = fill.CornerRadiusBottomRight = 1;
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);

        if (animateProgress && Math.Abs(target - start) > 0.0001)
        {
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(bar) || !bar.IsInsideTree())
                    return;
                var tween = bar.CreateTween();
                tween.TweenProperty(bar, "value", target, ProfileProgressTweenSeconds)
                    .SetTrans(Tween.TransitionType.Cubic)
                    .SetEase(Tween.EaseType.Out);
            }).CallDeferred();
        }

        return bar;
    }

    private void ShowInventory()
    {
        var items = GameRules.StatIds
            .Select((statId, index) => new InventoryItemViewState(
                string.Format(Tr("UI_INVENTORY_TREAT"), StatPresentationCatalog.NameFor(statId)),
                _session.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0,
                18 + index))
            .ToList();
        items.Add(new InventoryItemViewState(
            Tr("UI_INVENTORY_EGGS"),
            _session.State.OwnedEggs.Count,
            -1,
            UsesEggIcon: true));

        var failedEggs = _session.State.OwnedEggs
            .Where(egg => egg.State == EggState.Failed)
            .Select((egg, index) => new FailedEggViewState(
                egg.Id,
                string.Format(Tr("UI_INVENTORY_FAILED_EGG"), index + 1)))
            .ToList();

        var box = OpenModal(Tr("UI_INVENTORY_TITLE"), new Vector2(380, 292));
        var screen = new InventoryScreen();
        screen.Configure(new InventoryScreenState(items, failedEggs));
        screen.DiscardFailedEggRequested += eggId =>
        {
            _session.DiscardFailedEgg(eggId);
            CallDeferred(nameof(ShowInventory));
        };
        box.AddChild(screen);
    }
}