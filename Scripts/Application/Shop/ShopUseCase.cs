using System;
using System.Linq;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Shop;

public enum ShopFailure
{
    None,
    EggNotFound,
    EggShellNotFound,
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
/// Coordinates store transactions while preserving the product rule that a store egg is a
/// specific pre-rolled object. Purchase moves that same EggData into ownership and creates the
/// next pre-rolled store listing from an explicit ID/seed. Shell sale consumes one persisted
/// hatch output and awards the authorable fixed base value.
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

        // Move the existing rolled egg. Do not recreate or mutate its genetics here.
        purchasedEgg.Source = EggSource.Store;
        purchasedEgg.IncubationSeconds = 0.0f;
        purchasedEgg.WorldX = worldX;
        purchasedEgg.WorldY = worldY;
        state.OwnedEggs.Add(purchasedEgg);

        var replacement = _storeEggFactory.Create(replacementEggId, replacementEggSeed);
        state.StoreEggs.Add(replacement);

        return new StoreEggPurchaseResult(ShopFailure.None, purchasedEgg, replacement);
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
