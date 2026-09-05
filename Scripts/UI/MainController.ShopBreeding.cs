using System;
using System.Linq;
using Godot;
using Voidling.Domain.Garden;
using Voidling.Application.Breeding;
using Voidling.Domain.Shop;
using Voidling.Presentation.UI.Breeding;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Shop;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class MainController : Node
{
    /// <summary>Opens the Shop. Restocking happens here, so a bought slot stays empty for the visit.</summary>
    private void ShowShop()
    {
        _session.RefillStoreEggs();
        RenderShop();
    }

    /// <summary>Redraws the Shop in place after a transaction, without restocking the stall.</summary>
    private void RenderShop()
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

        ShopRareOfferViewState? rareOffer = string.Equals(
            state.ShopRareOfferItemId,
            ShopItemIds.FullIncubationSkip,
            StringComparison.Ordinal)
            ? new ShopRareOfferViewState(
                ShopItemIds.FullIncubationSkip,
                "RARE: INCUBATION SKIP",
                "Completes one owned egg's incubation timer. Hatching still follows normal Garden rules.",
                GameRules.FullIncubationSkipPrice)
            : null;

        var landPieces = GardenTileShape.Catalog
            .Select(shape => new ShopLandPieceViewState(
                ShapeId: shape.Id,
                DisplayName: LandShapePresentation.NameFor(shape.Id),
                Cells: shape.Cells,
                Stored: state.GardenModules.Count(module =>
                    !module.Placed && string.Equals(module.ShapeId, shape.Id, StringComparison.Ordinal)),
                Price: GameRules.GardenModuleRules.EmptyHexCost * shape.HexCount))
            .ToArray();

        var rotationRemaining = (int)Math.Ceiling(Math.Max(
            0.0,
            GameRules.ShopEggRotationIntervalSeconds - state.ShopEggRotationElapsedSeconds));

        var box = OpenModal(Tr("UI_SHOP_TITLE"), new Vector2(558, 344));
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(CreateDailyLoginPanel());

        // Three shelves no longer fit the stall at once, so the stock scrolls.
        var stall = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddChild(stall);

        var screen = new ShopScreen();
        screen.Configure(new ShopScreenState(state.Coins, trainingItems, eggs, rotationRemaining, rareOffer, landPieces));
        screen.TrainingItemPurchaseRequested += statId =>
        {
            _session.BuyTrainingItem(statId);
            RenderShop();
        };
        screen.EggPurchaseRequested += eggId =>
        {
            var tint = GameRules.TintColor(
                state.StoreEggs.FirstOrDefault(egg => egg.Id == eggId)?.TintHex ?? "#F6F0C9");
            var coinsBefore = state.Coins;
            _session.BuyStoreEgg(eggId);
            if (state.Coins != coinsBefore)
            {
                PurchaseCelebration.ShowEgg(
                    _uiRoot,
                    new Vector2(ScreenWidth, ScreenHeight),
                    tint,
                    Tr("UI_SHOP_EGG_BOUGHT"));
            }

            RenderShop();
        };
        screen.RareOfferPurchaseRequested += itemId =>
        {
            _session.BuyRareShopOffer(itemId);
            RenderShop();
        };
        screen.LandPurchaseRequested += shapeId =>
        {
            _session.BuyLandShape(shapeId);
            RenderShop();
        };
        stall.AddChild(screen);
    }

    private void ShowBreeding()
    {
        var adults = _session.State.Voidlings
            .Where(v => v.Stage == LifeStage.Adult)
            .ToArray();

        var parentViews = adults.Select(CreateBreedingParentView).ToArray();
        var initialPreview = parentViews.Length >= 2
            ? CreateBreedingPreviewView(_session.GetBreedingPairInfo(parentViews[0].Id, parentViews[1].Id))
            : new BreedingPreviewViewState(Tr("UI_BREED_NEED_TWO_ADULTS"), false);

        var box = OpenModal(Tr("UI_BREED_TITLE"), new Vector2(440, 270));
        var screen = new BreedingScreen();
        screen.Configure(new BreedingScreenState(parentViews, initialPreview));
        screen.PairChanged += (parentAId, parentBId) =>
        {
            var preview = _session.GetBreedingPairInfo(parentAId, parentBId);
            screen.SetPreview(CreateBreedingPreviewView(preview));
        };
        screen.BreedRequested += (parentAId, parentBId) =>
        {
            var preview = _session.GetBreedingPairInfo(parentAId, parentBId);
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

    private static BreedingParentViewState CreateBreedingParentView(VoidlingData data)
    {
        var hasAngel = GameRules.HasMutation(data, GameRules.AngelMutationId);
        var otherMutations = data.RareTraits?.Count(trait =>
            !string.Equals(trait.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase)) ?? 0;

        return new BreedingParentViewState(
            Id: data.Id,
            Name: data.Name,
            Appearance: VoidlingVisualAppearance.From(data.Appearance, data.TintHex),
            HasAngelMutation: hasAngel,
            OtherMutationCount: otherMutations);
    }

    private BreedingPreviewViewState CreateBreedingPreviewView(BreedingPairInfoProjection preview)
    {
        string text;
        if (!preview.CanBreed)
        {
            text = preview.Failure switch
            {
                BreedingFailure.SameParent => Tr("UI_BREED_DIFFERENT_PARENTS"),
                BreedingFailure.GardenFull => Tr("UI_BREED_GARDEN_FULL"),
                BreedingFailure.ParentNotAdult => Tr("UI_BREED_ADULTS_ONLY"),
                BreedingFailure.ParentOnCooldown => Tr("UI_BREED_COOLDOWN"),
                _ => Tr("UI_BREED_CHOOSE_TWO")
            };
        }
        else if (preview.Related)
        {
            text = $"Related pairing • lineage risk: {LineageRiskDisplayName(preview.LineageRisk)} • {preview.HatchFailurePercent}% hatch-failure risk.";
        }
        else if (preview.IsCleanOutcross)
        {
            text = $"Clean outcross • lineage risk improves to {LineageRiskDisplayName(preview.LineageRisk)} • {preview.HatchFailurePercent}% hatch-failure risk.";
        }
        else if (preview.ChildBurden > 0)
        {
            text = $"Unrelated pairing • lineage risk remains {LineageRiskDisplayName(preview.LineageRisk)} • {preview.HatchFailurePercent}% hatch-failure risk.";
        }
        else
        {
            text = Tr("UI_BREED_UNRELATED_CLEAN");
        }

        return new BreedingPreviewViewState(text, preview.CanBreed);
    }

    private static string LineageRiskDisplayName(LineageRiskBand risk)
        => risk switch
        {
            LineageRiskBand.None => "None",
            LineageRiskBand.Low => "Low",
            LineageRiskBand.Moderate => "Moderate",
            LineageRiskBand.High => "High",
            _ => "Critical"
        };
}
