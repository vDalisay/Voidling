using Godot;
using Voidling.Application.Collection;
using Voidling.Application.Shop;
using Voidling.Domain.Creatures;
using Voidling.Domain.Rules;
using Voidling.Domain.Shop;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class GameSession
{
    public void BuyStoreEgg(string eggId)
    {
        var result = _shop!.BuyStoreEgg(State, eggId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForShop(result.Failure));
            return;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        SaveAndNotify("Bought a mystery egg.");
        RaiseGardenEvent("A mystery egg was added to your inventory.");
    }

    /// <summary>Refills the empty store slots. The Shop screen calls this as it opens.</summary>
    public void RefillStoreEggs()
    {
        if (!_shop!.RefillStoreEggSlots(State, CreateStoreEgg))
            return;

        Save();
        StateChanged?.Invoke();
    }

    public bool PlaceStoredEgg(string eggId, Vector2 worldPosition)
    {
        var failure = _shop!.PlaceStoredEgg(State, eggId, worldPosition.X, worldPosition.Y);
        if (failure != ShopFailure.None)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForShop(failure));
            return false;
        }

        SaveAndNotify("The egg is nestled in. Incubation started.");
        RaiseGardenEvent("An egg was placed in the garden.");
        return true;
    }

    /// <summary>Puts a carried egg down somewhere else in the Garden.</summary>
    public bool MoveEgg(string eggId, Vector2 worldPosition)
    {
        var failure = _shop!.MovePlacedEgg(State, eggId, worldPosition.X, worldPosition.Y);
        if (failure != ShopFailure.None)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForShop(failure));
            return false;
        }

        Save();
        StateChanged?.Invoke();
        return true;
    }

    public bool BuyRareShopOffer(string itemId)
    {
        var failure = _shop!.BuyRareOffer(State, itemId);
        if (failure != ShopFailure.None)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForShop(failure));
            return false;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        var message = itemId == ShopItemIds.FullIncubationSkip
            ? "Bought a full incubation skip."
            : "Bought a rare Shop item.";
        SaveAndNotify(message);
        return true;
    }

    /// <summary>
    /// The environment a special variant's egg is waiting for (for the Swamp guy: an unused Swamp),
    /// or "" when the egg is ordinary or already sits where it can incubate.
    /// </summary>
    public string EnvironmentEggIsWaitingFor(EggData egg)
        => SpecialVariantCatalog.Find(egg.SpecialVariantId) is { } variant &&
           !SpecialVariantTracker.CanIncubate(State, egg, GameRules.GardenModuleRules.Hex)
            ? variant.Environment
            : string.Empty;

    /// <summary>Buys a departed special variant's respawn egg; it goes to the inventory to be placed.</summary>
    public bool BuySpecialVariantEgg(string variantId)
    {
        var previousSeedCounter = State.SeedCounter;
        var result = _shop!.BuySpecialVariantEgg(State, variantId, NewId(), NextSeed());
        if (!result.Succeeded)
        {
            // A refused purchase must not consume an authoritative seed that never produced an egg.
            State.SeedCounter = previousSeedCounter;
            ToastRequested?.Invoke(PlayerActionFailureText.ForShop(result.Failure));
            return false;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        var variant = SpecialVariantCatalog.Find(variantId);
        var message = string.Format(Tr("LOG_SPECIAL_EGG_BOUGHT"),
            VoidlingFormPresentationCatalog.NameFor(variant?.VisualTypeId), BiomePresentationCatalog.NameFor(variant?.Environment));
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    public bool UseFullIncubationSkip(string eggId)
    {
        var failure = _shop!.UseFullIncubationSkip(State, eggId);
        if (failure != ShopFailure.None)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForShop(failure));
            return false;
        }

        const string message = "Used an incubation skip. The egg is ready to hatch.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    public bool SellEggShell(string shellId)
    {
        var result = _shop!.SellEggShell(State, shellId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForShop(result.Failure));
            return false;
        }

        SaveAndNotify($"Sold an eggshell for {result.CoinsGained} sprouts.");
        return true;
    }
}
