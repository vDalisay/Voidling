using System;
using System.Collections.Generic;

namespace Voidling.Domain.Racing;

/// <summary>
/// Explicitly authored Cup economy. Fees are per stable Cup ID so a Cup can be free or charge
/// stakes without forking Cup identity. Refunds are expressed as a fraction per finishing
/// placement, which can express either discussed direction (winner-only refund is a single entry
/// for placement 1; placement-based partial refund adds further entries) without choosing between
/// them. Prize value is deliberately absent: the design direction is item/medal/trophy/unlock
/// rather than currency, and the exact prizes remain a product decision.
/// </summary>
public sealed record CupEconomyRules(
    IReadOnlyDictionary<string, int> EntryFeesByCupId,
    IReadOnlyDictionary<int, double> RefundFractionByPlacement)
{
    /// <summary>No authored stakes: no Cup charges, nothing is refunded. This is the shipped default.</summary>
    public static CupEconomyRules Free { get; } = new(
        new Dictionary<string, int>(StringComparer.Ordinal),
        new Dictionary<int, double>());
}

/// <summary>Pure entry-fee/refund arithmetic. Holds no state and never touches the player's wallet.</summary>
public static class CupEconomy
{
    public static int EntryFee(CupEconomyRules rules, string cupId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return !string.IsNullOrWhiteSpace(cupId) &&
               rules.EntryFeesByCupId.TryGetValue(cupId, out var fee)
            ? Math.Max(0, fee)
            : 0;
    }

    /// <summary>Refund owed for finishing at <paramref name="placement"/> (1 = first). Never exceeds the fee paid.</summary>
    public static int Refund(CupEconomyRules rules, string cupId, int placement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var fee = EntryFee(rules, cupId);
        if (fee <= 0 || placement < 1 || !rules.RefundFractionByPlacement.TryGetValue(placement, out var fraction))
            return 0;

        return (int)Math.Floor(fee * Math.Clamp(fraction, 0.0, 1.0));
    }
}
