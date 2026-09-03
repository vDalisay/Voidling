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

public sealed record StoreEggPurchaseResult(ShopFailure Failure, EggData? PurchasedEgg)
{
    public bool Succeeded => Failure == ShopFailure.None && PurchasedEgg != null;
}

public readonly record struct EggShellSaleResult(ShopFailure Failure, int CoinsGained)
{
    public bool Succeeded => Failure == ShopFailure.None;
}

/// <summary>Coordinates fixed store transactions and the rotating rare convenience slot.</summary>
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
        if (state.StoreEggs.All(egg => egg.Id != eggId)) return ShopFailure.EggNotFound;
        return state.Coins < _rules.Shop.StoreEggPrice ? ShopFailure.NotEnoughCurrency : ShopFailure.None;
    }

    public EggData CreateStoreInventoryEgg(string eggId, ulong eggSeed) => _storeEggFactory.Create(eggId, eggSeed);

    /// <summary>
    /// Moves one store egg into the player's inventory. The bought slot stays empty until the Shop
    /// is opened again, and the egg does not incubate until the player places it in the Garden.
    /// </summary>
    public StoreEggPurchaseResult BuyStoreEgg(GameStateData state, string eggId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var failure = ValidateStoreEggPurchase(state, eggId);
        if (failure != ShopFailure.None) return new StoreEggPurchaseResult(failure, null);
        var purchasedEgg = state.StoreEggs.First(egg => egg.Id == eggId);
        state.Coins -= _rules.Shop.StoreEggPrice;
        state.StoreEggs.Remove(purchasedEgg);
        purchasedEgg.Source = EggSource.Store;
        purchasedEgg.IncubationSeconds = 0.0f;
        purchasedEgg.State = EggState.Stored;
        state.OwnedEggs.Add(purchasedEgg);
        return new StoreEggPurchaseResult(ShopFailure.None, purchasedEgg);
    }

    /// <summary>Tops the store back up to its slot count. Called when the Shop is opened.</summary>
    public bool RefillStoreEggSlots(GameStateData state, Func<EggData> createEgg)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(createEgg);
        var missing = Math.Max(0, _rules.Shop.StoreEggSlotCount - state.StoreEggs.Count);
        for (var i = 0; i < missing; i++) state.StoreEggs.Add(createEgg());
        return missing > 0;
    }

    /// <summary>Places a stored egg in the Garden, which is what starts its incubation timer.</summary>
    public ShopFailure PlaceStoredEgg(GameStateData state, string eggId, float worldX, float worldY)
    {
        ArgumentNullException.ThrowIfNull(state);
        var egg = state.OwnedEggs.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, eggId, StringComparison.Ordinal) && candidate.State == EggState.Stored);
        if (egg == null) return ShopFailure.EggNotFound;
        egg.WorldX = worldX;
        egg.WorldY = worldY;
        egg.IncubationSeconds = 0.0f;
        egg.State = EggState.Incubating;
        return ShopFailure.None;
    }

    public ShopFailure BuyRareOffer(GameStateData state, string itemId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!string.Equals(itemId, ShopItemIds.FullIncubationSkip, StringComparison.Ordinal) ||
            !string.Equals(state.ShopRareOfferItemId, itemId, StringComparison.Ordinal)) return ShopFailure.RareOfferNotFound;
        var price = Math.Max(0, _rules.Shop.FullIncubationSkipPrice);
        if (state.Coins < price) return ShopFailure.NotEnoughCurrency;
        state.Coins -= price;
        state.UtilityItems.TryGetValue(itemId, out var owned);
        state.UtilityItems[itemId] = owned == int.MaxValue ? int.MaxValue : owned + 1;
        state.ShopRareOfferItemId = string.Empty;
        return ShopFailure.None;
    }

    public ShopFailure UseFullIncubationSkip(GameStateData state, string eggId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.UtilityItems.TryGetValue(ShopItemIds.FullIncubationSkip, out var owned) || owned <= 0) return ShopFailure.UtilityItemNotOwned;
        var egg = state.OwnedEggs.FirstOrDefault(candidate => string.Equals(candidate.Id, eggId, StringComparison.Ordinal));
        if (egg == null || egg.State != EggState.Incubating) return ShopFailure.EggNotIncubating;
        if (egg.IncubationSeconds >= egg.RequiredIncubationSeconds) return ShopFailure.EggAlreadyReady;
        egg.IncubationSeconds = Math.Max(0.0f, egg.RequiredIncubationSeconds);
        state.UtilityItems[ShopItemIds.FullIncubationSkip] = owned - 1;
        return ShopFailure.None;
    }

    public EggShellSaleResult SellEggShell(GameStateData state, string shellId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var shell = state.EggShells.FirstOrDefault(candidate => candidate.Id == shellId);
        if (shell == null) return new EggShellSaleResult(ShopFailure.EggShellNotFound, 0);
        var saleValue = Math.Max(0, _rules.Shop.EggShellSalePrice);
        var availableCoinCapacity = Math.Max(0L, (long)int.MaxValue - state.Coins);
        var awarded = (int)Math.Min(saleValue, availableCoinCapacity);
        state.EggShells.Remove(shell);
        state.Coins += awarded;
        return new EggShellSaleResult(ShopFailure.None, awarded);
    }
}
