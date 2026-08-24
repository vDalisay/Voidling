using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Trading;

public sealed record TradeTerms(
    string TradeId,
    ulong LobbyId,
    PlatformUserId InitiatorId,
    PlatformUserId CounterpartyId,
    TradeAssetReference[] InitiatorAssets,
    TradeAssetReference[] CounterpartyAssets);

public sealed record TradeOfferNotice(
    string TradeId,
    PlatformUserId InitiatorId,
    PlatformUserId CounterpartyId,
    TradeAssetReference[] InitiatorAssets);

public enum TradeSessionStatus
{
    Offered,
    PreparingBundles,
    PersistingPrepare,
    ReadyToCommit,
    Committing,
    Completed,
    Declined,
    Aborted,
    Failed
}

public sealed record TradeStatusUpdate(
    string TradeId,
    TradeSessionStatus Status,
    string? Message);

public sealed record TradeNetworkOperationResult(bool Success, string? TradeId, string? Error)
{
    public static TradeNetworkOperationResult Succeeded(string tradeId)
        => new(true, tradeId, null);

    public static TradeNetworkOperationResult Failed(string error)
        => new(false, null, error);
}

public static class TradeTermsHasher
{
    public static string Compute(TradeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var canonical = new StringBuilder();
        canonical.Append("v1|");
        canonical.Append(terms.TradeId).Append('|');
        canonical.Append(terms.LobbyId).Append('|');
        canonical.Append(terms.InitiatorId.Value).Append('|');
        canonical.Append(terms.CounterpartyId.Value).Append('|');
        AppendAssets(canonical, terms.InitiatorAssets);
        canonical.Append('|');
        AppendAssets(canonical, terms.CounterpartyAssets);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendAssets(StringBuilder builder, IEnumerable<TradeAssetReference>? assets)
    {
        var ordered = (assets ?? Array.Empty<TradeAssetReference>())
            .OrderBy(asset => (int)asset.Kind)
            .ThenBy(asset => asset.AssetId, StringComparer.Ordinal);

        var first = true;
        foreach (var asset in ordered)
        {
            if (!first)
                builder.Append(',');
            first = false;
            builder.Append((int)asset.Kind).Append(':').Append(asset.AssetId);
        }
    }
}

public static class TradeValidation
{
    public static bool IsValidAssetReferences(
        IEnumerable<TradeAssetReference>? assets,
        out string? error)
    {
        error = null;
        if (assets == null)
        {
            error = "Trade assets are missing.";
            return false;
        }

        var array = assets.ToArray();
        if (array.Length > TradeTransferService.MaxAssetsPerSide)
        {
            error = $"A trade can contain at most {TradeTransferService.MaxAssetsPerSide} assets per side.";
            return false;
        }

        var unique = new HashSet<TradeAssetReference>();
        foreach (var asset in array)
        {
            if (asset == null ||
                string.IsNullOrWhiteSpace(asset.AssetId) ||
                asset.AssetId.Length > 128 ||
                !Enum.IsDefined(asset.Kind) ||
                !unique.Add(asset))
            {
                error = "Trade contains an invalid or duplicate asset reference.";
                return false;
            }
        }

        return true;
    }

    public static bool IsParticipant(TradeTerms terms, PlatformUserId userId)
        => terms.InitiatorId == userId || terms.CounterpartyId == userId;

    public static TradeAssetReference[] AssetsFor(TradeTerms terms, PlatformUserId userId)
    {
        if (terms.InitiatorId == userId)
            return terms.InitiatorAssets ?? Array.Empty<TradeAssetReference>();
        if (terms.CounterpartyId == userId)
            return terms.CounterpartyAssets ?? Array.Empty<TradeAssetReference>();
        return Array.Empty<TradeAssetReference>();
    }

    public static PlatformUserId CounterpartyFor(TradeTerms terms, PlatformUserId userId)
    {
        if (terms.InitiatorId == userId)
            return terms.CounterpartyId;
        if (terms.CounterpartyId == userId)
            return terms.InitiatorId;
        return default;
    }
}
