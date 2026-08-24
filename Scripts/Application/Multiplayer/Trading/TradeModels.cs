using System;
using Voidling.Domain.Breeding;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Trading;

public enum TradeAssetKind
{
    Voidling,
    Egg
}

public sealed record TradeAssetReference(TradeAssetKind Kind, string AssetId);

/// <summary>
/// Full biological item state plus the ancestry closure needed to preserve future relatedness.
/// World placement is not authoritative across players and is reset when an incoming trade commits.
/// </summary>
public sealed record TradeTransferBundle(
    VoidlingData[] Voidlings,
    EggData[] Eggs,
    LineageArchiveEntry[] Lineage)
{
    public static TradeTransferBundle Empty { get; } = new(
        Array.Empty<VoidlingData>(),
        Array.Empty<EggData>(),
        Array.Empty<LineageArchiveEntry>());

    public int AssetCount => (Voidlings?.Length ?? 0) + (Eggs?.Length ?? 0);
}

/// <summary>
/// Persisted after the local client validates a canonical trade prepare. If the game closes before
/// commit, the journal preserves enough information to apply or abort predictably after reload.
/// </summary>
public sealed record PendingTradeJournalEntry(
    string TradeId,
    ulong LobbyId,
    ulong CounterpartyPlatformUserId,
    string TermsHash,
    TradeAssetReference[] OutgoingAssets,
    TradeTransferBundle IncomingBundle);

public sealed record TradeLocalOperationResult(bool Success, bool AlreadyApplied, string? Error)
{
    public static TradeLocalOperationResult Succeeded { get; } = new(true, false, null);
    public static TradeLocalOperationResult AppliedPreviously { get; } = new(true, true, null);

    public static TradeLocalOperationResult Failed(string error)
        => new(false, false, error);
}
