using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void ShowShop()
    {
        var box = OpenModal("SPROUT SHOP", new Vector2(438, 302));
        box.AddChild(UiFactory.CreateLabel("Training treats", 10));

        foreach (var statId in GameRules.StatIds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var owned = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var count) ? count : 0;
            var label = UiFactory.CreateLabel($"{GameRules.StatDisplayNames[statId]} treat  • owned {owned}", 8);
            label.CustomMinimumSize = new Vector2(255, 22);
            label.AddThemeColorOverride("font_color", GameRules.StatColor(statId));
            row.AddChild(label);
            var buy = UiFactory.CreateButton($"{GameRules.TrainingItemPrice} sprouts");
            buy.CustomMinimumSize = new Vector2(112, 22);
            UiFactory.ApplyPixelFont(buy, 7);
            var capturedStat = statId;
            buy.Pressed += () => { GameSession.Instance.BuyTrainingItem(capturedStat); ShowShop(); };
            row.AddChild(buy);
            box.AddChild(row);
        }

        box.AddChild(UiFactory.CreateLabel("Mystery eggs", 10));
        for (var i = 0; i < GameSession.Instance.State.StoreEggs.Count; i++)
        {
            var egg = GameSession.Instance.State.StoreEggs[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ColorRect { Color = GameRules.TintColor(egg.TintHex), CustomMinimumSize = new Vector2(20, 20) });
            var label = UiFactory.CreateLabel($"Egg {i + 1}", 8);
            label.CustomMinimumSize = new Vector2(225, 22);
            row.AddChild(label);
            var buy = UiFactory.CreateButton($"{GameRules.StoreEggPrice} sprouts");
            buy.CustomMinimumSize = new Vector2(112, 22);
            UiFactory.ApplyPixelFont(buy, 7);
            var eggId = egg.Id;
            buy.Pressed += () => { GameSession.Instance.BuyStoreEgg(eggId); ShowShop(); };
            row.AddChild(buy);
            box.AddChild(row);
        }
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
