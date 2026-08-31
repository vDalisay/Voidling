using Voidling.Application.Shop;
using Voidling.Domain.Rules;

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

    public bool SellEggShell(string shellId)
    {
        var result = _shop!.SellEggShell(State, shellId);
        if (!result.Succeeded)
            return false;

        SaveAndNotify($"Sold an eggshell for {result.CoinsGained} sprouts.");
        return true;
    }
}
