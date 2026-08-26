using System;
using System.Linq;
using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Creatures;
using Voidling.Presentation.UI.Breeding;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Shop;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void ShowShop()
    {
        var state = _session.State;
        var trainingItems = GameRules.StatIds
            .Select(statId => new ShopTrainingItemViewState(
                StatId: statId,
                DisplayName: StatPresentationCatalog.NameFor(statId),
                IdentityColor: StatPresentationCatalog.ColorFor(statId),
                Owned: state.TrainingItems.TryGetValue(statId, out var count) ? count : 0,
                Price: GameRules.TrainingItemPrice))
            .ToArray();

        var eggs = state.StoreEggs
            .Select((egg, index) => new ShopEggViewState(
                EggId: egg.Id,
                TintColor: GameRules.TintColor(egg.TintHex),
                Number: index + 1,
                Price: GameRules.StoreEggPrice))
            .ToArray();

        var box = OpenModal(Tr("UI_SHOP_TITLE"), new Vector2(558, 320));
        box.AddThemeConstantOverride("separation", 4);

        var screen = new ShopScreen();
        screen.Configure(new ShopScreenState(state.Coins, trainingItems, eggs));
        screen.TrainingItemPurchaseRequested += statId =>
        {
            _session.BuyTrainingItem(statId);
            ShowShop();
        };
        screen.EggPurchaseRequested += eggId =>
        {
            _session.BuyStoreEgg(eggId);
            ShowShop();
        };
        box.AddChild(screen);
    }

    private void ShowBreeding()
    {
        var adults = _session.CreateActiveVoidlingProfileProjections()
            .Where(profile => profile.IsAdult)
            .ToArray();

        var parentViews = adults.Select(CreateBreedingParentView).ToArray();
        var initialPreview = parentViews.Length >= 2
            ? CreateBreedingPreviewView(_session.GetBreedingPreviewData(parentViews[0].Id, parentViews[1].Id))
            : new BreedingPreviewViewState(Tr("UI_BREED_NEED_TWO_ADULTS"), false);

        var box = OpenModal(Tr("UI_BREED_TITLE"), new Vector2(440, 270));
        var screen = new BreedingScreen();
        screen.Configure(new BreedingScreenState(parentViews, initialPreview));
        screen.PairChanged += (parentAId, parentBId) =>
        {
            var preview = _session.GetBreedingPreviewData(parentAId, parentBId);
            screen.SetPreview(CreateBreedingPreviewView(preview));
        };
        screen.BreedRequested += (parentAId, parentBId) =>
        {
            var preview = _session.GetBreedingPreviewData(parentAId, parentBId);
            if (!preview.CanBreed)
            {
                screen.SetPreview(CreateBreedingPreviewView(preview));
                return;
            }

            var parentA = _session.FindVoidling(parentAId);
            var parentB = _session.FindVoidling(parentBId);
            if (parentA == null || parentB == null)
            {
                screen.SetPreview(new BreedingPreviewViewState(Tr("UI_BREED_CHOOSE_TWO"), false));
                return;
            }

            CloseModal();
            _garden.PlayBreedingAnimation(
                parentA.Id,
                parentB.Id,
                eggPosition => _session.TryBreed(parentA.Id, parentB.Id, eggPosition));
        };
        box.AddChild(screen);
    }

    private static BreedingParentViewState CreateBreedingParentView(VoidlingProfileProjection profile)
        => new(
            Id: profile.CreatureId,
            Name: profile.DisplayName,
            TintColor: ParseProfileTint(profile.TintHex),
            HasAngelMutation: profile.HasAngelMutation,
            OtherMutationCount: profile.OtherMutationCount);

    private BreedingPreviewViewState CreateBreedingPreviewView(BreedingPreview preview)
    {
        string text;
        if (!preview.CanBreed)
        {
            text = preview.Failure switch
            {
                BreedingFailure.SameParent => Tr("UI_BREED_DIFFERENT_PARENTS"),
                BreedingFailure.ParentNotAdult => Tr("UI_BREED_ADULTS_ONLY"),
                BreedingFailure.ParentOnCooldown => Tr("UI_BREED_COOLDOWN"),
                _ => Tr("UI_BREED_CHOOSE_TWO")
            };
        }
        else if (preview.Related)
        {
            text = string.Format(
                Tr("UI_BREED_RELATED"),
                preview.ChildBurden,
                preview.HatchFailurePercent);
        }
        else if (preview.IsCleanOutcross)
        {
            text = string.Format(Tr("UI_BREED_CLEAN_OUTCROSS"), preview.ChildBurden);
        }
        else if (preview.ChildBurden > 0)
        {
            text = string.Format(Tr("UI_BREED_UNRELATED_BURDEN"), preview.ChildBurden);
        }
        else
        {
            text = Tr("UI_BREED_UNRELATED_CLEAN");
        }

        return new BreedingPreviewViewState(text, preview.CanBreed);
    }
}
