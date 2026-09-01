using System;
using System.Linq;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using Voidling.Domain.Shop;
using VoidlingGame;

namespace Voidling.Application.Shop;

public enum ShopFailure
{
    None,
    EggNotFound,
    EggShellNotFound,
    RareOfferNotFound,
    UtilityItemNotOwned,
    EggNotIncubating,
    EggAlreadyReady,
    NotEnoughCurrency
}

public sealed record StoreEggPurchaseResult(ShopFailure Failure, EggData? PurchasedEgg, EggData? ReplacementEgg)
{
    public bool Succeeded => Failure == ShopFailure.None && PurchasedEgg != null && ReplacementEgg != null;
}

public readonly record struct EggShellSaleResult(ShopFailure Failure, int CoinsGained)
{
    public bool Succeeded => Failure == ShopFailure.None;
}

/// <summary>
/// Coordinates fixed store transactions and the rotating rare convenience slot. Store eggs remain
/// pre-rolled objects; utility inventory is persistent and using an incubation skip only advances
/// the selected egg to its authored incubation boundary. Hatching itself remains simulation-owned.
/// </summary>
public sealed class ShopUseCase
{
    private readonly GameBalanceRules _rules;
    private readonly StoreEggFactory _storeEggFactory;

    public ShopUseCase(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _storeEggFactory = new StoreEggFactory(rules);
    }

    public ShopFailure ValidateStoreEggPurchase(GameStateData state, string eggId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.StoreEggs.All(egg => egg.Id != eggId))
            return ShopFailure.EggNotFound;
        return state.Coins < _rules.Shop.StoreEggPrice
            ? ShopFailure.NotEnoughCurrency
            : ShopFailure.None;
    }

    public EggData CreateStoreInventoryEgg(string eggId, ulong eggSeed)
        => _storeEggFactory.Create(eggId, eggSeed);

    public StoreEggPurchaseResult BuyStoreEgg(
        GameStateData state,
        string eggId,
        string replacementEggId,
        ulong replacementEggSeed,
        float worldX,
        float worldY)
    {
        ArgumentNullException.ThrowIfNull(state);
        var failure = ValidateStoreEggPurchase(state, eggId);
        if (failure != ShopFailure.None)
            return new StoreEggPurchaseResult(failure, null, null);

        var purchasedEgg = state.StoreEggs.First(egg => egg.Id == eggId);
        state.Coins -= _rules.Shop.StoreEggPrice;
        state.StoreEggs.Remove(purchasedEgg);
        purchasedEgg.Source = EggSource.Store;
        purchasedEgg.IncubationSeconds = 0.0f;
        purchasedEgg.WorldX = worldX;
        purchasedEgg.WorldY = worldY;
        state.OwnedEggs.Add(purchasedEgg);

        var replacement = _storeEggFactory.Create(replacementEggId, replacementEggSeed);
        state.StoreEggs.Add(replacement);
        return new StoreEggPurchaseResult(ShopFailure.None, purchasedEgg, replacement);
    }

    public ShopFailure BuyRareOffer(GameStateData state, string itemId)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.UtilityItems ??= new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);

        if (!string.Equals(itemId, ShopItemIds.FullIncubationSkip, StringComparison.Ordinal) ||
            !string.Equals(state.ShopRareOfferItemId, itemId, StringComparison.Ordinal))
        {
            return ShopFailure.RareOfferNotFound;
        }

        var price = Math.Max(0, _rules.Shop.FullIncubationSkipPrice);
        if (state.Coins < price)
            return ShopFailure.NotEnoughCurrency;

        state.Coins -= price;
        state.UtilityItems.TryGetValue(itemId, out var owned);
        state.UtilityItems[itemId] = owned == int.MaxValue ? int.MaxValue : owned + 1;
        state.ShopRareOfferItemId = string.Empty;
        return ShopFailure.None;
    }

    public ShopFailure UseFullIncubationSkip(GameStateData state, string eggId)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.UtilityItems ??= new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
        if (!state.UtilityItems.TryGetValue(ShopItemIds.FullIncubationSkip, out var owned) || owned <= 0)
            return ShopFailure.UtilityItemNotOwned;

        var egg = state.OwnedEggs.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, eggId, StringComparison.Ordinal));
        if (egg == null || egg.State != EggState.Incubating)
            return ShopFailure.EggNotIncubating;
        if (egg.IncubationSeconds >= egg.RequiredIncubationSeconds)
            return ShopFailure.EggAlreadyReady;

        egg.IncubationSeconds = Math.Max(0.0f, egg.RequiredIncubationSeconds);
        state.UtilityItems[ShopItemIds.FullIncubationSkip] = owned - 1;
        return ShopFailure.None;
    }

    public EggShellSaleResult SellEggShell(GameStateData state, string shellId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var shell = state.EggShells.FirstOrDefault(candidate => candidate.Id == shellId);
        if (shell == null)
            return new EggShellSaleResult(ShopFailure.EggShellNotFound, 0);

        var saleValue = Math.Max(0, _rules.Shop.EggShellSalePrice);
        var availableCoinCapacity = Math.Max(0L, (long)int.MaxValue - state.Coins);
        var awarded = (int)Math.Min(saleValue, availableCoinCapacity);

        state.EggShells.Remove(shell);
        state.Coins += awarded;
        return new EggShellSaleResult(ShopFailure.None, awarded);
    }
}
