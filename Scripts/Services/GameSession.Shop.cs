using Godot;
using Voidling.Application.Shop;
using Voidling.Domain.Rules;
using Voidling.Domain.Shop;

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
