using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Voidling.Application.Breeding;
using Voidling.Domain.Breeding;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Trading;

/// <summary>
/// Local half of the casual peer-to-peer trade transaction. Network coordination never edits a
/// save directly: it asks this service to build transfer data, persist a prepared journal entry,
/// commit idempotently, or abort. Deliberate save rollback/cheating remains out of scope.
/// </summary>
public sealed class TradeTransferService
{
    public const int MaxAssetsPerSide = 8;
    public const int MaxLineageEntriesPerBundle = 256;

    private readonly GameBalanceRules _rules;
    private readonly LineageArchiveService _lineage = new();

    public TradeTransferService(GameBalanceRules rules)
        => _rules = rules ?? throw new ArgumentNullException(nameof(rules));

    public bool TryBuildTransferBundle(
        GameStateData state,
        IReadOnlyCollection<TradeAssetReference> assets,
        out TradeTransferBundle bundle,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(assets);
        bundle = TradeTransferBundle.Empty;
        error = null;

        if (!ValidateOutgoingReferences(state, assets, ignoredTradeId: null, out error))
            return false;

        var voidlings = new List<VoidlingData>();
        var eggs = new List<EggData>();
        var lineageRoots = new HashSet<string>(StringComparer.Ordinal);

        foreach (var asset in assets)
        {
            switch (asset.Kind)
            {
                case TradeAssetKind.Voidling:
                {
                    var creature = state.Voidlings.First(v =>
                        string.Equals(v.Id, asset.AssetId, StringComparison.Ordinal));
                    voidlings.Add(Clone(creature));
                    lineageRoots.Add(creature.Id);
                    break;
                }
                case TradeAssetKind.Egg:
                {
                    var egg = state.OwnedEggs.First(e =>
                        string.Equals(e.Id, asset.AssetId, StringComparison.Ordinal));
                    eggs.Add(Clone(egg));
                    if (!string.IsNullOrWhiteSpace(egg.ParentAId))
                        lineageRoots.Add(egg.ParentAId);
                    if (!string.IsNullOrWhiteSpace(egg.ParentBId))
                        lineageRoots.Add(egg.ParentBId);
                    break;
                }
                default:
                    error = "Trade contains an unsupported asset kind.";
                    return false;
            }
        }

        var lineage = _lineage.GetAncestryClosure(
            state,
            lineageRoots,
            _rules.Genetics.RelatedAncestorDepth);
        if (lineage.Count > MaxLineageEntriesPerBundle)
        {
            error = "Trade ancestry payload is too large.";
            return false;
        }

        bundle = new TradeTransferBundle(
            voidlings.ToArray(),
            eggs.ToArray(),
            lineage.ToArray());
        return true;
    }

    public TradeLocalOperationResult Prepare(
        GameStateData state,
        string tradeId,
        ulong lobbyId,
        ulong counterpartyPlatformUserId,
        string termsHash,
        IReadOnlyCollection<TradeAssetReference> outgoingAssets,
        TradeTransferBundle incomingBundle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(outgoingAssets);
        ArgumentNullException.ThrowIfNull(incomingBundle);
        EnsureTradeCollections(state);

        if (!IsValidTradeId(tradeId))
            return TradeLocalOperationResult.Failed("Trade ID is invalid.");
        if (lobbyId == 0)
            return TradeLocalOperationResult.Failed("Trade lobby is invalid.");
        if (counterpartyPlatformUserId == 0)
            return TradeLocalOperationResult.Failed("Trade counterparty is invalid.");
        if (string.IsNullOrWhiteSpace(termsHash) || termsHash.Length > 128)
            return TradeLocalOperationResult.Failed("Trade terms hash is invalid.");

        if (state.AppliedTradeIds.Contains(tradeId, StringComparer.Ordinal))
            return TradeLocalOperationResult.AppliedPreviously;

        var existing = state.PendingTradeJournal.FirstOrDefault(entry =>
            string.Equals(entry.TradeId, tradeId, StringComparison.Ordinal));
        if (existing != null)
        {
            return IsEquivalentPreparedEntry(
                existing,
                lobbyId,
                counterpartyPlatformUserId,
                termsHash,
                outgoingAssets,
                incomingBundle)
                ? TradeLocalOperationResult.Succeeded
                : TradeLocalOperationResult.Failed("Trade ID is already prepared with different terms.");
        }

        if (!ValidateOutgoingReferences(state, outgoingAssets, tradeId, out var error))
            return TradeLocalOperationResult.Failed(error!);
        if (!ValidateIncomingBundle(state, incomingBundle, out error))
            return TradeLocalOperationResult.Failed(error!);

        state.PendingTradeJournal.Add(new PendingTradeJournalEntry(
            tradeId,
            lobbyId,
            counterpartyPlatformUserId,
            termsHash,
            outgoingAssets.ToArray(),
            Clone(incomingBundle)));
        return TradeLocalOperationResult.Succeeded;
    }

    public TradeLocalOperationResult CommitPrepared(GameStateData state, string tradeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureTradeCollections(state);

        if (!IsValidTradeId(tradeId))
            return TradeLocalOperationResult.Failed("Trade ID is invalid.");

        if (state.AppliedTradeIds.Contains(tradeId, StringComparer.Ordinal))
        {
            state.PendingTradeJournal.RemoveAll(entry =>
                string.Equals(entry.TradeId, tradeId, StringComparison.Ordinal));
            return TradeLocalOperationResult.AppliedPreviously;
        }

        var journal = state.PendingTradeJournal.FirstOrDefault(entry =>
            string.Equals(entry.TradeId, tradeId, StringComparison.Ordinal));
        if (journal == null)
            return TradeLocalOperationResult.Failed("Trade was not prepared on this client.");

        if (!ValidateOutgoingReferences(state, journal.OutgoingAssets, tradeId, out var error))
            return TradeLocalOperationResult.Failed(error!);
        if (!ValidateIncomingBundle(state, journal.IncomingBundle, out error))
            return TradeLocalOperationResult.Failed(error!);

        // Every mutation that can fail is validated above. Merge ancestry before inserting assets;
        // it is staged atomically by LineageArchiveService and cannot partially merge on conflict.
        if (!_lineage.TryMerge(state, journal.IncomingBundle.Lineage, out error))
            return TradeLocalOperationResult.Failed(error ?? "Incoming lineage could not be merged.");

        foreach (var asset in journal.OutgoingAssets)
            RemoveOwnedAsset(state, asset);

        foreach (var incoming in journal.IncomingBundle.Voidlings)
        {
            var creature = Clone(incoming);
            NormalizeIncomingCreature(creature);
            creature.WorldX = 0;
            creature.WorldY = 0;
            state.Voidlings.Add(creature);
        }

        foreach (var incoming in journal.IncomingBundle.Eggs)
        {
            var egg = Clone(incoming);
            NormalizeIncomingEgg(egg);
            egg.WorldX = 0;
            egg.WorldY = 0;
            state.OwnedEggs.Add(egg);
        }

        _lineage.EnsureCurrentEntries(state);
        state.AppliedTradeIds.Add(tradeId);
        state.PendingTradeJournal.Remove(journal);
        return TradeLocalOperationResult.Succeeded;
    }

    public TradeLocalOperationResult AbortPrepared(GameStateData state, string tradeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureTradeCollections(state);

        if (!IsValidTradeId(tradeId))
            return TradeLocalOperationResult.Failed("Trade ID is invalid.");

        state.PendingTradeJournal.RemoveAll(entry =>
            string.Equals(entry.TradeId, tradeId, StringComparison.Ordinal));
        return TradeLocalOperationResult.Succeeded;
    }

    public int AbortPreparedForLobby(GameStateData state, ulong lobbyId)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureTradeCollections(state);
        if (lobbyId == 0)
            return 0;

        return state.PendingTradeJournal.RemoveAll(entry => entry.LobbyId == lobbyId);
    }

    public bool IsAssetLocked(GameStateData state, TradeAssetReference asset)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(asset);
        EnsureTradeCollections(state);
        return state.PendingTradeJournal.Any(entry =>
            entry.OutgoingAssets.Any(reference => reference == asset));
    }

    private bool ValidateIncomingBundle(
        GameStateData state,
        TradeTransferBundle bundle,
        out string? error)
    {
        error = null;
        var voidlings = bundle.Voidlings ?? Array.Empty<VoidlingData>();
        var eggs = bundle.Eggs ?? Array.Empty<EggData>();
        var lineage = bundle.Lineage ?? Array.Empty<LineageArchiveEntry>();

        if (voidlings.Length + eggs.Length > MaxAssetsPerSide)
        {
            error = $"A trade can contain at most {MaxAssetsPerSide} assets per side.";
            return false;
        }
        if (lineage.Length > MaxLineageEntriesPerBundle)
        {
            error = "Incoming trade ancestry payload is too large.";
            return false;
        }

        // Egg IDs become creature IDs when they hatch, so every incoming asset shares one stable
        // identity namespace regardless of asset kind. This prevents a trade from creating a save
        // that can later contain two different objects with the same durable ID.
        var incomingAssetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var creature in voidlings)
        {
            if (!IsValidIncomingCreature(creature) ||
                !incomingAssetIds.Add(creature.Id))
            {
                error = "Incoming trade contains an invalid or duplicate Voidling identity.";
                return false;
            }

            if (state.Voidlings.Any(v => v.Id == creature.Id) ||
                state.DepartedVoidlings.Any(v => v.Id == creature.Id) ||
                state.OwnedEggs.Any(e => e.Id == creature.Id) ||
                state.StoreEggs.Any(e => e.Id == creature.Id))
            {
                error = $"Asset identity '{creature.Id}' already exists in this save.";
                return false;
            }
        }

        foreach (var egg in eggs)
        {
            if (!IsValidIncomingEgg(egg) ||
                !incomingAssetIds.Add(egg.Id))
            {
                error = "Incoming trade contains an invalid or duplicate egg identity.";
                return false;
            }

            if (state.OwnedEggs.Any(e => e.Id == egg.Id) ||
                state.StoreEggs.Any(e => e.Id == egg.Id) ||
                state.Voidlings.Any(v => v.Id == egg.Id) ||
                state.DepartedVoidlings.Any(v => v.Id == egg.Id) ||
                state.LineageArchive.Any(entry => entry.CreatureId == egg.Id))
            {
                error = $"Asset identity '{egg.Id}' already exists in this save.";
                return false;
            }
        }

        var lineageById = lineage
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.CreatureId))
            .GroupBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var entry in lineageById.Values)
        {
            if (state.OwnedEggs.Any(egg => egg.Id == entry.CreatureId) ||
                state.StoreEggs.Any(egg => egg.Id == entry.CreatureId))
            {
                error = $"Incoming lineage identity '{entry.CreatureId}' conflicts with an existing egg.";
                return false;
            }
        }

        foreach (var creature in voidlings)
        {
            if (!lineageById.TryGetValue(creature.Id, out var ownLineage) ||
                !ownLineage.HasSameLineageIdentity(LineageArchiveEntry.FromVoidling(creature)))
            {
                error = $"Incoming Voidling '{creature.Id}' is missing matching lineage identity.";
                return false;
            }
        }

        foreach (var egg in eggs)
        {
            if (lineageById.ContainsKey(egg.Id))
            {
                error = $"Incoming egg identity '{egg.Id}' conflicts with a creature lineage identity.";
                return false;
            }
            if (!HasLineageRoot(lineageById, egg.ParentAId) ||
                !HasLineageRoot(lineageById, egg.ParentBId))
            {
                error = $"Incoming egg '{egg.Id}' is missing parent lineage information.";
                return false;
            }
        }

        return _lineage.CanMerge(state, lineage, out error);
    }

    private bool ValidateOutgoingReferences(
        GameStateData state,
        IReadOnlyCollection<TradeAssetReference> assets,
        string? ignoredTradeId,
        out string? error)
    {
        EnsureTradeCollections(state);
        error = null;

        if (assets.Count > MaxAssetsPerSide)
        {
            error = $"A trade can contain at most {MaxAssetsPerSide} assets per side.";
            return false;
        }

        var unique = new HashSet<TradeAssetReference>();
        foreach (var asset in assets)
        {
            if (asset == null ||
                string.IsNullOrWhiteSpace(asset.AssetId) ||
                asset.AssetId.Length > 128 ||
                !unique.Add(asset))
            {
                error = "Trade contains an invalid or duplicate asset reference.";
                return false;
            }

            var owned = asset.Kind switch
            {
                TradeAssetKind.Voidling => state.Voidlings.Any(v => v.Id == asset.AssetId),
                TradeAssetKind.Egg => state.OwnedEggs.Any(e => e.Id == asset.AssetId),
                _ => false
            };
            if (!owned)
            {
                error = $"Trade asset '{asset.AssetId}' is not locally owned.";
                return false;
            }

            var lockedElsewhere = state.PendingTradeJournal.Any(entry =>
                !string.Equals(entry.TradeId, ignoredTradeId, StringComparison.Ordinal) &&
                entry.OutgoingAssets.Any(reference => reference == asset));
            if (lockedElsewhere)
            {
                error = $"Trade asset '{asset.AssetId}' is already locked by another prepared trade.";
                return false;
            }
        }

        return true;
    }

    private void NormalizeIncomingCreature(VoidlingData creature)
    {
        creature.TrainingPoints ??= new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var statId in _rules.Genetics.StatIds)
        {
            if (!creature.TrainingPoints.ContainsKey(statId))
                creature.TrainingPoints[statId] = 0;
        }
        creature.RareTraits ??= new List<RareTraitData>();
    }

    private static void NormalizeIncomingEgg(EggData egg)
        => egg.RareTraits ??= new List<RareTraitData>();

    private static bool IsValidIncomingCreature(VoidlingData? creature)
        => creature != null &&
           !string.IsNullOrWhiteSpace(creature.Id) &&
           creature.Id.Length <= 128 &&
           creature.Genome != null &&
           creature.FamilyGeneration >= 0 &&
           Enum.IsDefined(creature.Stage);

    private static bool IsValidIncomingEgg(EggData? egg)
        => egg != null &&
           !string.IsNullOrWhiteSpace(egg.Id) &&
           egg.Id.Length <= 128 &&
           egg.Genome != null &&
           egg.FamilyGeneration >= 0 &&
           egg.RequiredIncubationSeconds >= 0 &&
           egg.IncubationSeconds >= 0 &&
           Enum.IsDefined(egg.Source) &&
           Enum.IsDefined(egg.State);

    private static bool HasLineageRoot(
        IReadOnlyDictionary<string, LineageArchiveEntry> lineageById,
        string parentId)
        => string.IsNullOrWhiteSpace(parentId) || lineageById.ContainsKey(parentId);

    private static void RemoveOwnedAsset(GameStateData state, TradeAssetReference asset)
    {
        switch (asset.Kind)
        {
            case TradeAssetKind.Voidling:
                state.Voidlings.RemoveAll(v => string.Equals(v.Id, asset.AssetId, StringComparison.Ordinal));
                break;
            case TradeAssetKind.Egg:
                state.OwnedEggs.RemoveAll(e => string.Equals(e.Id, asset.AssetId, StringComparison.Ordinal));
                break;
        }
    }

    private static bool IsEquivalentPreparedEntry(
        PendingTradeJournalEntry existing,
        ulong lobbyId,
        ulong counterpartyPlatformUserId,
        string termsHash,
        IReadOnlyCollection<TradeAssetReference> outgoingAssets,
        TradeTransferBundle incomingBundle)
    {
        if (existing.LobbyId != lobbyId ||
            existing.CounterpartyPlatformUserId != counterpartyPlatformUserId ||
            !string.Equals(existing.TermsHash, termsHash, StringComparison.Ordinal) ||
            !existing.OutgoingAssets.SequenceEqual(outgoingAssets))
        {
            return false;
        }

        return string.Equals(
            JsonSerializer.Serialize(existing.IncomingBundle),
            JsonSerializer.Serialize(incomingBundle),
            StringComparison.Ordinal);
    }

    private static void EnsureTradeCollections(GameStateData state)
    {
        state.PendingTradeJournal ??= new List<PendingTradeJournalEntry>();
        state.AppliedTradeIds ??= new List<string>();
    }

    private static bool IsValidTradeId(string tradeId)
        => !string.IsNullOrWhiteSpace(tradeId) && Guid.TryParse(tradeId, out _);

    private static T Clone<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        return JsonSerializer.Deserialize<T>(bytes)
               ?? throw new InvalidOperationException($"Could not clone trade payload type {typeof(T).Name}.");
    }
}
