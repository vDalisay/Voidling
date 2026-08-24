using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Inventory;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void RebuildDetailsPanel()
    {
        if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            _detailsPanel.QueueFree();
        _detailsPanel = null;

        var data = _session.FindVoidling(_selectedId);
        if (data == null)
            return;

        _detailsPanel = UiFactory.CreatePanel(new Vector2(226, 294));
        _detailsPanel.Position = new Vector2(404, 57);
        _detailsPanel.Size = new Vector2(226, 294);
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

        var stage = data.Stage == LifeStage.Adult
            ? "Adult"
            : $"Child • {Math.Max(0, (int)Math.Ceiling(GameRules.ChildToAdultSeconds - data.AgeSeconds))}s to adult";
        box.AddChild(UiFactory.CreateLabel(stage, 7));

        foreach (var statId in GameRules.StatIds)
            box.AddChild(CreateProfileStatBlock(data, statId));

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

    private Control CreateProfileStatBlock(VoidlingData data, string statId)
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

        var label = UiFactory.CreateLabel(
            $"{StatPresentationCatalog.NameFor(statId).ToUpperInvariant(),-7} {GameRules.GradeName(gene.ExpressedValue)}  LV{level:00}  {effective:00}", 7);
        label.CustomMinimumSize = new Vector2(142, 17);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        label.AddThemeConstantOverride("outline_size", statId == "stamina" ? 2 : 1);
        label.TooltipText = $"DNA {GameRules.GradeName(gene.AlleleA)}/{GameRules.GradeName(gene.AlleleB)} • training {GameRules.GetTrainingPoints(data, statId)}";
        row.AddChild(label);

        var use = UiFactory.CreateButton($"+1 ({count})");
        use.CustomMinimumSize = new Vector2(48, 17);
        UiFactory.ApplyPixelFont(use, 6);
        use.Disabled = count <= 0;
        var capturedStat = statId;
        use.Pressed += () => _session.UseTrainingItem(_selectedId, capturedStat);
        row.AddChild(use);
        container.AddChild(row);

        var bar = CreateStatProgressBar(data, statId, new Vector2(142, 6));
        container.AddChild(bar);
        return container;
    }

    private static ProgressBar CreateStatProgressBar(VoidlingData data, string statId, Vector2 size)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = GameRules.StatLevelProgress(data, statId),
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
            // Reopen on the next idle frame: the current signal emitter belongs to the modal
            // subtree and ModalHost intentionally defers freeing that subtree until dispatch ends.
            CallDeferred(nameof(ShowInventory));
        };
        box.AddChild(screen);
    }
}
