using System.Linq;
using Godot;
using Voidling.Presentation.UI.Shop;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void ShowShop()
    {
        var state = GameSession.Instance.State;
        var trainingItems = GameRules.StatIds
            .Select(statId => new ShopTrainingItemViewState(
                StatId: statId,
                DisplayName: GameRules.StatDisplayNames[statId],
                IdentityColor: GameRules.StatColor(statId),
                Owned: state.TrainingItems.TryGetValue(statId, out var count) ? count : 0,
                Price: GameRules.TrainingItemPrice))
            .ToArray();

        var eggs = state.StoreEggs
            .Select((egg, index) => new ShopEggViewState(
                EggId: egg.Id,
                TintHex: egg.TintHex,
                Number: index + 1,
                Price: GameRules.StoreEggPrice))
            .ToArray();

        var box = OpenModal(Tr("UI_SHOP_TITLE"), new Vector2(558, 320));
        box.AddThemeConstantOverride("separation", 4);

        var screen = new ShopScreen();
        screen.Configure(new ShopScreenState(state.Coins, trainingItems, eggs));
        screen.TrainingItemPurchaseRequested += statId =>
        {
            GameSession.Instance.BuyTrainingItem(statId);
            ShowShop();
        };
        screen.EggPurchaseRequested += eggId =>
        {
            GameSession.Instance.BuyStoreEgg(eggId);
            ShowShop();
        };
        box.AddChild(screen);
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
