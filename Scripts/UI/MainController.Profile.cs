using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void RebuildDetailsPanel()
    {
        if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            _detailsPanel.QueueFree();
        _detailsPanel = null;

        var data = GameSession.Instance.FindVoidling(_selectedId);
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
        var title = UiFactory.CreateTitle(data.Name);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(title);

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
            ? $"Parents: {GameSession.Instance.NameFor(data.ParentAId)} + {GameSession.Instance.NameFor(data.ParentBId)}"
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

    private Control CreateProfileStatBlock(VoidlingData data, string statId)
    {
        var container = new VBoxContainer { CustomMinimumSize = new Vector2(194, 28) };
        container.AddThemeConstantOverride("separation", 1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var gene = GameRules.GetGene(data, statId);
        var effective = (int)Math.Round(GameRules.EffectiveStat(data, statId));
        var level = GameRules.StatLevel(data, statId);
        var count = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;
        var color = GameRules.StatColor(statId);

        var label = UiFactory.CreateLabel(
            $"{GameRules.StatDisplayNames[statId].ToUpperInvariant(),-7} {GameRules.GradeName(gene.ExpressedValue)}  LV{level:00}  {effective:00}", 7);
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
        use.Pressed += () => GameSession.Instance.UseTrainingItem(_selectedId, capturedStat);
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
        var fill = new StyleBoxFlat { BgColor = GameRules.StatColor(statId) };
        background.CornerRadiusTopLeft = background.CornerRadiusTopRight = 1;
        background.CornerRadiusBottomLeft = background.CornerRadiusBottomRight = 1;
        fill.CornerRadiusTopLeft = fill.CornerRadiusTopRight = 1;
        fill.CornerRadiusBottomLeft = fill.CornerRadiusBottomRight = 1;
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);
        return bar;
    }

    private void RebuildEggsPanel()
    {
        if (_eggsPanel != null && GodotObject.IsInstanceValid(_eggsPanel))
            _eggsPanel.QueueFree();

        _eggsPanel = UiFactory.CreatePanel(new Vector2(386, 54));
        _eggsPanel.Position = new Vector2(10, 294);
        _eggsPanel.Size = new Vector2(386, 54);
        _uiRoot.AddChild(_eggsPanel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _eggsPanel.AddChild(row);

        var eggs = GameSession.Instance.State.OwnedEggs;
        var text = new VBoxContainer { CustomMinimumSize = new Vector2(292, 32) };
        text.AddChild(UiFactory.CreateLabel($"Eggs on island: {eggs.Count}", 8));
        if (eggs.Count == 0)
            text.AddChild(UiFactory.CreateLabel("Buy or breed an egg to place it in the garden.", 6));
        else
        {
            var summaries = eggs.Take(5).Select(egg => egg.State == EggState.Failed
                ? "FAILED"
                : $"{Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds))}s");
            text.AddChild(UiFactory.CreateLabel(string.Join("  •  ", summaries), 6));
        }
        row.AddChild(text);

        var failed = eggs.FirstOrDefault(e => e.State == EggState.Failed);
        if (failed != null)
        {
            var discard = UiFactory.CreateButton("Discard");
            discard.CustomMinimumSize = new Vector2(66, 22);
            UiFactory.ApplyPixelFont(discard, 7);
            discard.Pressed += () => GameSession.Instance.DiscardFailedEgg(failed.Id);
            row.AddChild(discard);
        }
    }

    private void ShowInventory()
    {
        var box = OpenModal("INVENTORY", new Vector2(380, 292));
        box.AddChild(UiFactory.CreateLabel("Items you currently own", 9));
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(340, 198),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddChild(scroll);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(list);

        for (var i = 0; i < GameRules.StatIds.Length; i++)
        {
            var statId = GameRules.StatIds[i];
            var count = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;
            list.AddChild(CreateInventoryRow(UiFactory.CreateIcon(18 + i), $"{GameRules.StatDisplayNames[statId]} Treat", count));
        }

        var eggAtlas = new AtlasTexture
        {
            Atlas = EggTexture,
            Region = new Rect2(0, 0, EggTexture.GetWidth(), EggTexture.GetHeight())
        };
        list.AddChild(CreateInventoryRow(eggAtlas, "Eggs on Island", GameSession.Instance.State.OwnedEggs.Count));
    }

    private static Control CreateInventoryRow(Texture2D iconTexture, string itemName, int count)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(328, 32) };
        var style = new StyleBoxFlat { BgColor = Color.FromHtml("#F0D9A8"), BorderColor = Color.FromHtml("#C59670") };
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = style.ContentMarginRight = 7;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);
        row.AddChild(new TextureRect
        {
            Texture = iconTexture,
            CustomMinimumSize = new Vector2(22, 22),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        var name = UiFactory.CreateLabel(itemName, 8);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(name);
        var amount = UiFactory.CreateLabel($"x{count}", 9);
        amount.CustomMinimumSize = new Vector2(42, 20);
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        amount.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(amount);
        return panel;
    }
}
