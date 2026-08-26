using System;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Trading;

public static class TradeProtocol
{
    public const string OfferCommandType = "trade.offer.command";
    public const string OfferedType = "trade.offered";
    public const string AcceptCommandType = "trade.accept.command";
    public const string DeclineCommandType = "trade.decline.command";
    public const string PrepareRequestType = "trade.prepare.request";
    public const string BundlePreparedType = "trade.bundle.prepared";
    public const string PersistRequestType = "trade.persist.request";
    public const string ReadyType = "trade.ready";
    public const string CommitType = "trade.commit";
    public const string CommittedType = "trade.committed";
    public const string AbortType = "trade.abort";

    private sealed record OfferCommandPayload(
        string TradeId,
        ulong LobbyId,
        PlatformUserId CounterpartyId,
        TradeAssetReference[] InitiatorAssets);

    private sealed record OfferedPayload(TradeOfferNotice Offer);
    private sealed record AcceptCommandPayload(string TradeId, TradeAssetReference[] CounterpartyAssets);
    private sealed record DeclineCommandPayload(string TradeId);
    private sealed record PrepareRequestPayload(TradeTerms Terms, string TermsHash);
    private sealed record BundlePreparedPayload(string TradeId, string TermsHash, TradeTransferBundle Bundle);
    private sealed record PersistRequestPayload(TradeTerms Terms, string TermsHash, TradeTransferBundle IncomingBundle);
    private sealed record ReadyPayload(string TradeId, string TermsHash, bool Success, string? Error);
    private sealed record CommitPayload(string TradeId, string TermsHash);
    private sealed record CommittedPayload(string TradeId, string TermsHash, bool Success, string? Error);
    private sealed record AbortPayload(string TradeId, string? Reason);

    public static byte[] EncodeOfferCommand(
        PlatformUser sender,
        string tradeId,
        ulong lobbyId,
        PlatformUserId counterpartyId,
        TradeAssetReference[] initiatorAssets)
        => MultiplayerProtocol.EncodeMessage(
            OfferCommandType,
            sender,
            new OfferCommandPayload(tradeId, lobbyId, counterpartyId, initiatorAssets));

    public static bool TryDecodeOfferCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string tradeId,
        out ulong lobbyId,
        out PlatformUserId counterpartyId,
        out TradeAssetReference[] initiatorAssets)
    {
        tradeId = string.Empty;
        lobbyId = 0;
        counterpartyId = default;
        initiatorAssets = Array.Empty<TradeAssetReference>();
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                OfferCommandType,
                out messageId,
                out OfferCommandPayload? payload) ||
            payload == null ||
            !IsValidTradeId(payload.TradeId) ||
            payload.LobbyId == 0 ||
            payload.CounterpartyId.Value == 0 ||
            !TradeValidation.IsValidAssetReferences(payload.InitiatorAssets, out _))
        {
            return false;
        }

        tradeId = payload.TradeId;
        lobbyId = payload.LobbyId;
        counterpartyId = payload.CounterpartyId;
        initiatorAssets = payload.InitiatorAssets;
        return true;
    }

    public static byte[] EncodeOffered(PlatformUser sender, TradeOfferNotice offer)
        => MultiplayerProtocol.EncodeMessage(OfferedType, sender, new OfferedPayload(offer));

    public static bool TryDecodeOffered(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out TradeOfferNotice offer)
    {
        offer = default!;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                OfferedType,
                out _,
                out OfferedPayload? payload) ||
            payload?.Offer == null ||
            !IsValidTradeId(payload.Offer.TradeId) ||
            payload.Offer.InitiatorId.Value == 0 ||
            payload.Offer.CounterpartyId.Value == 0 ||
            !TradeValidation.IsValidAssetReferences(payload.Offer.InitiatorAssets, out _))
        {
            return false;
        }

        offer = payload.Offer;
        return true;
    }

    public static byte[] EncodeAcceptCommand(
        PlatformUser sender,
        string tradeId,
        TradeAssetReference[] counterpartyAssets)
        => MultiplayerProtocol.EncodeMessage(
            AcceptCommandType,
            sender,
            new AcceptCommandPayload(tradeId, counterpartyAssets));

    public static bool TryDecodeAcceptCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string tradeId,
        out TradeAssetReference[] counterpartyAssets)
    {
        tradeId = string.Empty;
        counterpartyAssets = Array.Empty<TradeAssetReference>();
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                AcceptCommandType,
                out messageId,
                out AcceptCommandPayload? payload) ||
            payload == null ||
            !IsValidTradeId(payload.TradeId) ||
            !TradeValidation.IsValidAssetReferences(payload.CounterpartyAssets, out _))
        {
            return false;
        }

        tradeId = payload.TradeId;
        counterpartyAssets = payload.CounterpartyAssets;
        return true;
    }

    public static byte[] EncodeDeclineCommand(PlatformUser sender, string tradeId)
        => MultiplayerProtocol.EncodeMessage(DeclineCommandType, sender, new DeclineCommandPayload(tradeId));

    public static bool TryDecodeDeclineCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string tradeId)
    {
        tradeId = string.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                DeclineCommandType,
                out messageId,
                out DeclineCommandPayload? payload) ||
            payload == null ||
            !IsValidTradeId(payload.TradeId))
        {
            return false;
        }

        tradeId = payload.TradeId;
        return true;
    }

    public static byte[] EncodePrepareRequest(
        PlatformUser sender,
        TradeTerms terms,
        string termsHash)
        => MultiplayerProtocol.EncodeMessage(
            PrepareRequestType,
            sender,
            new PrepareRequestPayload(terms, termsHash));

    public static bool TryDecodePrepareRequest(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out TradeTerms terms,
        out string termsHash)
    {
        terms = default!;
        termsHash = string.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                PrepareRequestType,
                out _,
                out PrepareRequestPayload? payload) ||
            payload?.Terms == null ||
            !IsValidTerms(payload.Terms, payload.TermsHash))
        {
            return false;
        }

        terms = payload.Terms;
        termsHash = payload.TermsHash;
        return true;
    }

    public static byte[] EncodeBundlePrepared(
        PlatformUser sender,
        string tradeId,
        string termsHash,
        TradeTransferBundle bundle)
        => MultiplayerProtocol.EncodeMessage(
            BundlePreparedType,
            sender,
            new BundlePreparedPayload(tradeId, termsHash, bundle));

    public static bool TryDecodeBundlePrepared(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string tradeId,
        out string termsHash,
        out TradeTransferBundle bundle)
    {
        tradeId = string.Empty;
        termsHash = string.Empty;
        bundle = TradeTransferBundle.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                BundlePreparedType,
                out messageId,
                out BundlePreparedPayload? payload) ||
            payload?.Bundle == null ||
            !IsValidTradeId(payload.TradeId) ||
            !IsValidHash(payload.TermsHash))
        {
            return false;
        }

        tradeId = payload.TradeId;
        termsHash = payload.TermsHash;
        bundle = payload.Bundle;
        return true;
    }

    public static byte[] EncodePersistRequest(
        PlatformUser sender,
        TradeTerms terms,
        string termsHash,
        TradeTransferBundle incomingBundle)
        => MultiplayerProtocol.EncodeMessage(
            PersistRequestType,
            sender,
            new PersistRequestPayload(terms, termsHash, incomingBundle));

    public static bool TryDecodePersistRequest(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out TradeTerms terms,
        out string termsHash,
        out TradeTransferBundle incomingBundle)
    {
        terms = default!;
        termsHash = string.Empty;
        incomingBundle = TradeTransferBundle.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                PersistRequestType,
                out _,
                out PersistRequestPayload? payload) ||
            payload?.Terms == null ||
            payload.IncomingBundle == null ||
            !IsValidTerms(payload.Terms, payload.TermsHash))
        {
            return false;
        }

        terms = payload.Terms;
        termsHash = payload.TermsHash;
        incomingBundle = payload.IncomingBundle;
        return true;
    }

    public static byte[] EncodeReady(
        PlatformUser sender,
        string tradeId,
        string termsHash,
        bool success,
        string? error)
        => MultiplayerProtocol.EncodeMessage(
            ReadyType,
            sender,
            new ReadyPayload(tradeId, termsHash, success, TrimError(error)));

    public static bool TryDecodeReady(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string tradeId,
        out string termsHash,
        out bool success,
        out string? error)
    {
        tradeId = string.Empty;
        termsHash = string.Empty;
        success = false;
        error = null;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                ReadyType,
                out messageId,
                out ReadyPayload? payload) ||
            payload == null ||
            !IsValidTradeId(payload.TradeId) ||
            !IsValidHash(payload.TermsHash))
        {
            return false;
        }

        tradeId = payload.TradeId;
        termsHash = payload.TermsHash;
        success = payload.Success;
        error = TrimError(payload.Error);
        return true;
    }

    public static byte[] EncodeCommit(PlatformUser sender, string tradeId, string termsHash)
        => MultiplayerProtocol.EncodeMessage(CommitType, sender, new CommitPayload(tradeId, termsHash));

    public static bool TryDecodeCommit(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out string tradeId,
        out string termsHash)
    {
        tradeId = string.Empty;
        termsHash = string.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                CommitType,
                out _,
                out CommitPayload? payload) ||
            payload == null ||
            !IsValidTradeId(payload.TradeId) ||
            !IsValidHash(payload.TermsHash))
        {
            return false;
        }

        tradeId = payload.TradeId;
        termsHash = payload.TermsHash;
        return true;
    }

    public static byte[] EncodeCommitted(
        PlatformUser sender,
        string tradeId,
        string termsHash,
        bool success,
        string? error)
        => MultiplayerProtocol.EncodeMessage(
            CommittedType,
            sender,
            new CommittedPayload(tradeId, termsHash, success, TrimError(error)));

    public static bool TryDecodeCommitted(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string tradeId,
        out string termsHash,
        out bool success,
        out string? error)
    {
        tradeId = string.Empty;
        termsHash = string.Empty;
        success = false;
        error = null;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                CommittedType,
                out messageId,
                out CommittedPayload? payload) ||
            payload == null ||
            !IsValidTradeId(payload.TradeId) ||
            !IsValidHash(payload.TermsHash))
        {
            return false;
        }

        tradeId = payload.TradeId;
        termsHash = payload.TermsHash;
        success = payload.Success;
        error = TrimError(payload.Error);
        return true;
    }

    public static byte[] EncodeAbort(PlatformUser sender, string tradeId, string? reason)
        => MultiplayerProtocol.EncodeMessage(AbortType, sender, new AbortPayload(tradeId, TrimError(reason)));

    public static bool TryDecodeAbort(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out string tradeId,
        out string? reason)
    {
        tradeId = string.Empty;
        reason = null;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                AbortType,
                out _,
                out AbortPayload? payload) ||
            payload == null ||
            !IsValidTradeId(payload.TradeId))
        {
            return false;
        }

        tradeId = payload.TradeId;
        reason = TrimError(payload.Reason);
        return true;
    }

    private static bool IsValidTerms(TradeTerms terms, string termsHash)
        => IsValidTradeId(terms.TradeId) &&
           terms.LobbyId != 0 &&
           terms.InitiatorId.Value != 0 &&
           terms.CounterpartyId.Value != 0 &&
           terms.InitiatorId != terms.CounterpartyId &&
           TradeValidation.IsValidAssetReferences(terms.InitiatorAssets, out _) &&
           TradeValidation.IsValidAssetReferences(terms.CounterpartyAssets, out _) &&
           IsValidHash(termsHash) &&
           string.Equals(TradeTermsHasher.Compute(terms), termsHash, StringComparison.Ordinal);

    private static bool IsValidTradeId(string tradeId)
        => !string.IsNullOrWhiteSpace(tradeId) && Guid.TryParse(tradeId, out _);

    private static bool IsValidHash(string hash)
        => !string.IsNullOrWhiteSpace(hash) && hash.Length == 64;

    private static string? TrimError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;
        var trimmed = error.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }
}
