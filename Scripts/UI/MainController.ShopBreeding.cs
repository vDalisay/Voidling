using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Audio;
using Voidling.Domain.Creatures;
using Voidling.Domain.Garden;
using Voidling.Application.Breeding;
using Voidling.Application.Collection;
using Voidling.Domain.Shop;
using Voidling.Presentation.UI.Breeding;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
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

        var biomeTiles = BiomeCatalog.Biomes
            .Select(biome => new ShopBiomeTileViewState(
                BiomeId: biome.Id,
                DisplayName: string.Format(Tr("UI_SHOP_BIOME_TILE"), BiomePresentationCatalog.NameFor(biome.Id)),
                TrainsText: string.Format(Tr("UI_SHOP_BIOME_TRAINS"), StatPresentationCatalog.NameFor(biome.StatId)),
                Tint: BiomePresentationCatalog.ColorFor(biome.Id),
                Owned: state.BiomeTiles
                    .Where(stack => string.Equals(stack.BiomeId, biome.Id, StringComparison.Ordinal) && stack.Stars == 1)
                    .Sum(stack => stack.Count),
                Price: GameRules.GardenModuleRules.BiomeTilePrice))
            .ToArray();

        // A departed special variant's respawn egg is sold only while it is gone.
        var specialEggs = SpecialVariantCatalog.All
            .Where(variant => SpecialVariantTracker.RespawnAvailable(state, variant))
            .Select(variant => new ShopSpecialEggViewState(
                variant.Id,
                string.Format(Tr("UI_SHOP_SPECIAL_EGG"), VoidlingFormPresentationCatalog.NameFor(variant.VisualTypeId)),
                string.Format(Tr("UI_SHOP_SPECIAL_EGG_HINT"), BiomePresentationCatalog.NameFor(variant.Environment)),
                VoidlingFormPresentationCatalog.SpecialEggTint(variant.Id),
                GameRules.SpecialVariantEggPrice))
            .ToArray();

        var box = OpenRailModal(Tr("UI_SHOP_TITLE"), new Vector2(520, 344), icon: ScreenIcons.Shop);
        box.AddThemeConstantOverride("separation", 4);
        _modalHost.ShowWallet(state.Coins);

        var screen = new ShopScreen();
        screen.Configure(new ShopScreenState(state.Coins, trainingItems, eggs, rareOffer, landPieces)
        {
            BiomeTiles = biomeTiles,
            SpecialEggs = specialEggs
        }, _shopCategory, _shopSelection);
        screen.SelectionChanged += (category, selection) =>
        {
            _shopCategory = category;
            _shopSelection = selection;
        };
        screen.InventoryRequested += ShowInventory;
        screen.TrainingItemPurchaseRequested += statId =>
        {
            var origin = screen.PurchaseOrigin;
            var coinsBefore = state.Coins;
            _session.BuyTrainingItem(statId);
            RenderShop();
            if (state.Coins != coinsBefore) Celebrate(origin);
        };
        screen.EggPurchaseRequested += eggId =>
        {
            var tint = GameRules.TintColor(
                state.StoreEggs.FirstOrDefault(egg => egg.Id == eggId)?.TintHex ?? "#F6F0C9");
            var coinsBefore = state.Coins;
            var origin = screen.PurchaseOrigin;
            _session.BuyStoreEgg(eggId);
            if (state.Coins != coinsBefore)
            {
                Celebrate(origin, cue: UiCue.Celebrate);
                PurchaseCelebration.ShowEgg(
                    _uiRoot,
                    new Vector2(ScreenWidth, ScreenHeight),
                    tint,
                    Tr("UI_SHOP_EGG_BOUGHT"));
            }

            RenderShop();
        };
        screen.BiomeTilePurchaseRequested += biomeId =>
        {
            var origin = screen.PurchaseOrigin;
            var coinsBefore = state.Coins;
            _session.BuyBiomeTile(biomeId);
            RenderShop();
            if (state.Coins != coinsBefore) Celebrate(origin);
        };
        screen.SpecialEggPurchaseRequested += variantId =>
        {
            var origin = screen.PurchaseOrigin;
            var coinsBefore = state.Coins;
            _session.BuySpecialVariantEgg(variantId);
            RenderShop();
            if (state.Coins != coinsBefore) Celebrate(origin);
        };
        screen.RareOfferPurchaseRequested += itemId =>
        {
            var origin = screen.PurchaseOrigin;
            var coinsBefore = state.Coins;
            _session.BuyRareShopOffer(itemId);
            RenderShop();
            if (state.Coins != coinsBefore) Celebrate(origin);
        };
        screen.LandPurchaseRequested += shapeId =>
        {
            var origin = screen.PurchaseOrigin;
            var moduleId = _session.BuyLandShape(shapeId);
            if (moduleId != null) Celebrate(origin);
            if (moduleId == null)
            {
                RenderShop();
                return;
            }

            CloseModal(false);
            BeginShopLandPlacement(moduleId, shapeId);
        };
        box.AddChild(screen);
        Callable.From(screen.FocusSelection).CallDeferred();
    }

    private void BeginShopLandPlacement(string moduleId, string shapeId)
    {
        _garden.CancelLandPlacement();
        _shopLandPurchaseId = moduleId;
        _cancelShopLandPurchase = false;
        _garden.BeginLandPlacement(moduleId, shapeId);
    }

    private async void FinishShopLandPlacement()
    {
        var moduleId = _shopLandPurchaseId;
        var cancelPurchase = _cancelShopLandPurchase;
        var placed = _session.State.GardenModules.Find(module => module.Id == moduleId)?.Placed == true;
        _shopLandPurchaseId = string.Empty;
        _cancelShopLandPurchase = false;
        _landPurchaseActions.Visible = false;

        if (cancelPurchase)
            _session.CancelLandPurchase(moduleId);
        else
            _session.CommitLandPurchase(moduleId);

        if (placed)
        {
            await ToSignal(
                GetTree().CreateTimer(GardenController.LandPlacementAnimationSeconds + 0.5),
                SceneTreeTimer.SignalName.Timeout);
            if (!IsInsideTree())
                return;
        }
        RenderShop();
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

        var box = OpenModal(Tr("UI_BREED_TITLE"), new Vector2(520, 282), ScreenIcons.Breeding);
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

            Celebrate(ModalPoint("BreedAction"), PixelBurst.Palette.Rosy, UiCue.Celebrate);
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
