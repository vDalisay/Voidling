using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void ShowShop()
    {
        var box = OpenModal("SPROUT MARKET", new Vector2(558, 320));
        box.AddThemeConstantOverride("separation", 4);

        var summary = new HBoxContainer();
        summary.AddThemeConstantOverride("separation", 8);
        var welcome = UiFactory.CreateLabel("Pick something from the stall", 8);
        welcome.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        summary.AddChild(welcome);

        var wallet = UiFactory.CreatePanel(new Vector2(128, 24));
        var walletLabel = UiFactory.CreateLabel($"SPROUTS  {GameSession.Instance.State.Coins}", 8);
        walletLabel.HorizontalAlignment = HorizontalAlignment.Center;
        walletLabel.VerticalAlignment = VerticalAlignment.Center;
        wallet.AddChild(walletLabel);
        summary.AddChild(wallet);
        box.AddChild(summary);

        var awning = new HBoxContainer { CustomMinimumSize = new Vector2(518, 9) };
        awning.AddThemeConstantOverride("separation", 0);
        for (var i = 0; i < 14; i++)
        {
            awning.AddChild(new ColorRect
            {
                Color = Color.FromHtml(i % 2 == 0 ? "#E8C977" : "#749B75"),
                CustomMinimumSize = new Vector2(24, 9),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }
        box.AddChild(awning);

        box.AddChild(CreateShopSectionLabel("TRAINING TREATS", "One treat is consumed when you train a stat."));

        var treatShelf = CreateStallShelf();
        var treatGrid = new GridContainer { Columns = 5 };
        treatGrid.AddThemeConstantOverride("h_separation", 5);
        treatGrid.AddThemeConstantOverride("v_separation", 3);
        treatShelf.GetNode<VBoxContainer>("ShelfBox").AddChild(treatGrid);

        foreach (var statId in GameRules.StatIds)
        {
            var owned = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var count) ? count : 0;
            var capturedStat = statId;
            treatGrid.AddChild(CreateTreatProduct(statId, owned, () =>
            {
                GameSession.Instance.BuyTrainingItem(capturedStat);
                ShowShop();
            }));
        }
        box.AddChild(treatShelf);

        box.AddChild(CreateShopSectionLabel("MYSTERY EGGS", "Each egg already carries its own hidden DNA."));

        var eggShelf = CreateStallShelf();
        var eggGrid = new GridContainer { Columns = 3 };
        eggGrid.AddThemeConstantOverride("h_separation", 7);
        eggGrid.AddThemeConstantOverride("v_separation", 3);
        eggShelf.GetNode<VBoxContainer>("ShelfBox").AddChild(eggGrid);

        for (var i = 0; i < GameSession.Instance.State.StoreEggs.Count; i++)
        {
            var egg = GameSession.Instance.State.StoreEggs[i];
            var eggId = egg.Id;
            eggGrid.AddChild(CreateEggProduct(egg, i + 1, () =>
            {
                GameSession.Instance.BuyStoreEgg(eggId);
                ShowShop();
            }));
        }
        box.AddChild(eggShelf);
    }

    private static Control CreateShopSectionLabel(string title, string subtitle)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var heading = UiFactory.CreateLabel(title, 9);
        heading.CustomMinimumSize = new Vector2(125, 14);
        heading.AddThemeColorOverride("font_color", Color.FromHtml("#6B4B34"));
        row.AddChild(heading);
        var note = UiFactory.CreateLabel(subtitle, 6);
        note.VerticalAlignment = VerticalAlignment.Center;
        note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        note.AddThemeColorOverride("font_color", Color.FromHtml("#786C5B"));
        row.AddChild(note);
        return row;
    }

    private static PanelContainer CreateStallShelf()
    {
        var shelf = new PanelContainer { CustomMinimumSize = new Vector2(518, 78) };
        var wood = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#B98255"),
            BorderColor = Color.FromHtml("#76513C"),
            ContentMarginLeft = 7,
            ContentMarginRight = 7,
            ContentMarginTop = 5,
            ContentMarginBottom = 7
        };
        wood.SetBorderWidthAll(2);
        wood.CornerRadiusTopLeft = wood.CornerRadiusTopRight = 3;
        wood.CornerRadiusBottomLeft = wood.CornerRadiusBottomRight = 3;
        shelf.AddThemeStyleboxOverride("panel", wood);

        var shelfBox = new VBoxContainer { Name = "ShelfBox" };
        shelfBox.AddThemeConstantOverride("separation", 2);
        shelf.AddChild(shelfBox);

        var lip = new ColorRect
        {
            Color = Color.FromHtml("#6E4936"),
            CustomMinimumSize = new Vector2(1, 5),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        shelfBox.AddChild(lip);
        shelfBox.MoveChild(lip, 0);
        return shelf;
    }

    private static Control CreateTreatProduct(string statId, int owned, Action buyAction)
    {
        var card = CreateMarketCard(new Vector2(94, 70));
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 1);
        card.AddChild(column);

        var display = new CenterContainer { CustomMinimumSize = new Vector2(82, 24) };
        var packet = new PanelContainer { CustomMinimumSize = new Vector2(29, 21) };
        var packetStyle = new StyleBoxFlat
        {
            BgColor = GameRules.StatColor(statId),
            BorderColor = Color.FromHtml("#66594C")
        };
        packetStyle.SetBorderWidthAll(1);
        packetStyle.CornerRadiusTopLeft = packetStyle.CornerRadiusTopRight = 2;
        packetStyle.CornerRadiusBottomLeft = packetStyle.CornerRadiusBottomRight = 2;
        packet.AddThemeStyleboxOverride("panel", packetStyle);
        var initial = UiFactory.CreateLabel(GameRules.StatDisplayNames[statId][0].ToString().ToUpperInvariant(), 9);
        initial.HorizontalAlignment = HorizontalAlignment.Center;
        initial.VerticalAlignment = VerticalAlignment.Center;
        packet.AddChild(initial);
        display.AddChild(packet);
        column.AddChild(display);

        var name = UiFactory.CreateLabel(GameRules.StatDisplayNames[statId], 7);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AddThemeColorOverride("font_color", GameRules.StatColor(statId).Darkened(0.35f));
        column.AddChild(name);

        var stock = UiFactory.CreateLabel($"OWNED {owned}", 5);
        stock.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(stock);

        var buy = UiFactory.CreateButton($"BUY {GameRules.TrainingItemPrice}");
        buy.CustomMinimumSize = new Vector2(78, 19);
        UiFactory.ApplyPixelFont(buy, 6);
        buy.Pressed += buyAction;
        column.AddChild(buy);
        return card;
    }

    private static Control CreateEggProduct(EggData egg, int number, Action buyAction)
    {
        var card = CreateMarketCard(new Vector2(160, 70));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 5);
        card.AddChild(row);

        var eggVisual = new TextureRect
        {
            Texture = EggTexture,
            SelfModulate = GameRules.TintColor(egg.TintHex),
            CustomMinimumSize = new Vector2(34, 34),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(eggVisual);

        var info = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        info.AddThemeConstantOverride("separation", 1);
        var name = UiFactory.CreateLabel($"MYSTERY EGG {number}", 6);
        info.AddChild(name);
        var hint = UiFactory.CreateLabel("Hidden DNA", 5);
        hint.AddThemeColorOverride("font_color", Color.FromHtml("#786C5B"));
        info.AddChild(hint);
        var buy = UiFactory.CreateButton($"BUY {GameRules.StoreEggPrice}");
        buy.CustomMinimumSize = new Vector2(91, 19);
        UiFactory.ApplyPixelFont(buy, 6);
        buy.Pressed += buyAction;
        info.AddChild(buy);
        row.AddChild(info);
        return card;
    }

    private static PanelContainer CreateMarketCard(Vector2 minimumSize)
    {
        var card = new PanelContainer { CustomMinimumSize = minimumSize };
        var style = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#F4E5BD"),
            BorderColor = Color.FromHtml("#8A6248"),
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        style.SetBorderWidthAll(1);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 2;
        style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 2;
        card.AddThemeStyleboxOverride("panel", style);
        return card;
    }

    private void ShowBreeding()
    {
        var adults = GameSession.Instance.State.Voidlings.Where(v => v.Stage == LifeStage.Adult).ToList();
        var box = OpenModal("BREEDING NEST", new Vector2(440, 270));
        if (adults.Count < 2)
        {
            box.AddChild(UiFactory.CreateLabel("You need two adult Voidlings.", 10));
            return;
        }

        var parentA = new OptionButton();
        var parentB = new OptionButton();
        StyleOption(parentA);
        StyleOption(parentB);
        foreach (var adult in adults)
        {
            parentA.AddItem(adult.Name);
            parentB.AddItem(adult.Name);
        }
        parentA.Selected = 0;
        parentB.Selected = 1;

        var selectors = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        selectors.AddThemeConstantOverride("separation", 12);
        var left = CreateBreedingSelector(adults[0], parentA);
        var right = CreateBreedingSelector(adults[1], parentB);
        selectors.AddChild(left.Container);
        selectors.AddChild(UiFactory.CreateLabel("+", 14));
        selectors.AddChild(right.Container);
        box.AddChild(selectors);

        var preview = UiFactory.CreateLabel("", 7);
        preview.CustomMinimumSize = new Vector2(390, 36);
        preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(preview);

        void UpdatePreview()
        {
            UiFactory.SetPortraitData(left.Portrait, adults[parentA.Selected]);
            UiFactory.SetPortraitData(right.Portrait, adults[parentB.Selected]);
            preview.Text = GameSession.Instance.GetBreedingPreview(adults[parentA.Selected].Id, adults[parentB.Selected].Id);
        }

        parentA.ItemSelected += _ => UpdatePreview();
        parentB.ItemSelected += _ => UpdatePreview();
        UpdatePreview();

        var breed = UiFactory.CreateButton("Breed");
        breed.CustomMinimumSize = new Vector2(120, 26);
        breed.Pressed += () =>
        {
            var a = adults[parentA.Selected];
            var b = adults[parentB.Selected];
            if (a.Id == b.Id || a.Stage != LifeStage.Adult || b.Stage != LifeStage.Adult ||
                a.BreedCooldownSeconds > 0.0f || b.BreedCooldownSeconds > 0.0f)
            {
                preview.Text = GameSession.Instance.GetBreedingPreview(a.Id, b.Id);
                return;
            }

            CloseModal();
            _garden.PlayBreedingAnimation(a.Id, b.Id, eggPosition => GameSession.Instance.TryBreed(a.Id, b.Id, eggPosition));
        };
        box.AddChild(breed);
    }

    private static (VBoxContainer Container, TextureRect Portrait) CreateBreedingSelector(VoidlingData data, OptionButton option)
    {
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 3);
        var portrait = UiFactory.CreatePortrait(data, new Vector2(70, 70));
        column.AddChild(portrait);
        column.AddChild(option);
        return (column, portrait);
    }
}
