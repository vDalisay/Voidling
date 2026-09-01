using Voidling.Application.Shop;
using Voidling.Domain.Rules;
using Voidling.Domain.Shop;

namespace VoidlingGame;

public partial class GameSession
{
    public void BuyStoreEgg(string eggId)
    {
        var failure = _shop!.ValidateStoreEggPurchase(State, eggId);
        if (failure == ShopFailure.NotEnoughCurrency)
        {
            ToastRequested?.Invoke("Not enough sprouts.");
            return;
        }
        if (failure != ShopFailure.None)
            return;

        // Only allocate the replacement's persistent seed after the transaction is known to
        // be valid. Failed clicks must not shift later deterministic random streams.
        var nestPosition = NextNestPosition();
        var replacementSeed = NextSeed();
        var replacementId = NewId();
        var result = _shop.BuyStoreEgg(
            State,
            eggId,
            replacementId,
            replacementSeed,
            nestPosition.X,
            nestPosition.Y);

        if (!result.Succeeded)
            return;

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        SaveAndNotify("Bought a mystery egg.");
        RaiseGardenEvent("A mystery egg was placed in the garden.");
    }

    public bool BuyRareShopOffer(string itemId)
    {
        var failure = _shop!.BuyRareOffer(State, itemId);
        if (failure == ShopFailure.NotEnoughCurrency)
        {
            ToastRequested?.Invoke("Not enough sprouts.");
            return false;
        }
        if (failure != ShopFailure.None)
        {
            ToastRequested?.Invoke("That rare Shop offer is no longer available.");
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
            ToastRequested?.Invoke(failure switch
            {
                ShopFailure.UtilityItemNotOwned => "No incubation skips are available.",
                ShopFailure.EggAlreadyReady => "That egg is already ready to hatch.",
                ShopFailure.EggNotIncubating => "Choose an egg that is still incubating.",
                _ => "The incubation skip could not be used."
            });
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
            return false;

        SaveAndNotify($"Sold an eggshell for {result.CoinsGained} sprouts.");
        return true;
    }
}
