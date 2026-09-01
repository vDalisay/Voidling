using System;
using Voidling.Domain.Shared;

namespace Voidling.Domain.Shop;

public static class ShopItemIds
{
    public const string FullIncubationSkip = "shop.full-incubation-skip";
}

/// <summary>
/// Pure deterministic rare-offer resolver. Shop outcome chance is allowed to vary, while reward
/// amounts and the open-game rotation cadence remain separately authorable balance values.
/// </summary>
public static class RareShopOfferResolver
{
    public static string Resolve(ulong seed, double appearanceChance)
    {
        var chance = double.IsFinite(appearanceChance) ? Math.Clamp(appearanceChance, 0.0, 1.0) : 0.0;
        if (chance <= 0.0)
            return string.Empty;
        if (chance >= 1.0)
            return ShopItemIds.FullIncubationSkip;

        return StableRandom.Create(seed, "shop:rare-offer").NextDouble() < chance
            ? ShopItemIds.FullIncubationSkip
            : string.Empty;
    }
}
