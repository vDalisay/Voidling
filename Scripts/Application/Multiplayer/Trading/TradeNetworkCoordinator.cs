using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Trading;

/// <summary>
/// Coordinates casual two-player trades inside one connected Garden lobby. The Steam lobby owner
/// orders protocol phases, while TradeTransferService remains the only code allowed to prepare or
/// mutate local ownership. A prepared acknowledgement is sent only after the local journal is saved.
/// </summary>
public sealed class TradeNetworkCoordinator
{
    private const int RecentMessageLimit = 512;

    private sealed class HostedTrade
    {
        public required TradeOfferNotice Offer { get; init; }
        public TradeTerms? Terms { get; set; }
        public string? TermsHash { get; set; }
        public TradeTransferBundle? InitiatorBundle { get; set; }
        public TradeTransferBundle? CounterpartyBundle { get; set; }
        public HashSet<PlatformUserId> ReadyPeers { get; } = new();
        public HashSet<PlatformUserId> CommittedPeers { get; } = new();
        public TradeSessionStatus Status { get; set; } = TradeSessionStatus.Offered;
    }

    private readonly MultiplayerConnectionService _connection;
    private readonly TradeTransferService _transfers;
    private readonly IGameStateRepository _repository;
    private readonly Func<GameStateData> _stateAccessor;
    private readonly Dictionary<string, HostedTrade> _hostedTrades = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TradeOfferNotice> _receivedOffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (TradeTerms Terms, string Hash)> _localTerms = new(StringComparer.Ordinal);
    private readonly Queue<Guid> _recentMessageOrder = new();
    private readonly HashSet<Guid> _recentMessageIds = new();
    private ulong _activeLobbyId;
    private PlatformUserId _observedHostId;

    public TradeNetworkCoordinator(
        MultiplayerConnectionService connection,
        TradeTransferService transfers,
        IGameStateRepository repository,
        Func<GameStateData> stateAccessor)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _stateAccessor = stateAccessor ?? throw new ArgumentNullException(nameof(stateAccessor));

        _connection.PacketReceived += HandlePacket;
        _connection.LobbyChanged += HandleLobbyChanged;
        _connection.LobbyLeft += HandleLobbyLeft;

        if (_connection.CurrentLobby != null)
        {
            _activeLobbyId = _connection.CurrentLobby.LobbyId;
            _observedHostId = _connection.CurrentLobby.OwnerId;
        }
    }

    public event Action<TradeOfferNotice>? TradeOfferReceived;
    public event Action<TradeStatusUpdate>? TradeStatusChanged;
    public event Action<TradeTerms>? LocalTradeCommitted;
    public event Action? LocalStateChanged;
    public event Action<string>? ProtocolRejected;

    public TradeNetworkOperationResult OfferTrade(
        PlatformUserId counterpartyId,
        IReadOnlyCollection<TradeAssetReference> initiatorAssets)
    {
        var context = ValidateLocalTradeContext(counterpartyId);
        if (context.Error != null)
            return TradeNetworkOperationResult.Failed(context.Error);

        if (!TradeValidation.IsValidAssetReferences(initiatorAssets, out var error))
            return TradeNetworkOperationResult.Failed(error!);
        if (!_transfers.TryBuildTransferBundle(
                _stateAccessor(),
                initiatorAssets,
                out _,
                out error))
        {
            return TradeNetworkOperationResult.Failed(error!);
        }

        var tradeId = Guid.NewGuid().ToString("N");
        if (_connection.IsLocalHost)
        {
            HandleHostOffer(
                context.Local!.Id,
                tradeId,
                context.Lobby!.LobbyId,
                counterpartyId,
                initiatorAssets.ToArray());
        }
        else
        {
            var payload = TradeProtocol.EncodeOfferCommand(
                context.Local!,
                tradeId,
                context.Lobby!.LobbyId,
                counterpartyId,
                initiatorAssets.ToArray());
            if (!_connection.TrySend(
                    context.Lobby.OwnerId,
                    NetworkChannel.Trade,
                    payload,
                    DeliveryMode.Reliable))
            {
                return TradeNetworkOperationResult.Failed("Could not send the trade offer to the lobby host.");
            }
        }

        return TradeNetworkOperationResult.Succeeded(tradeId);
    }

    public TradeNetworkOperationResult AcceptTrade(
        string tradeId,
        IReadOnlyCollection<TradeAssetReference> counterpartyAssets)
    {
        if (!_receivedOffers.TryGetValue(tradeId, out var offer))
            return TradeNetworkOperationResult.Failed("Trade offer is not available on this client.");

        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null || offer.CounterpartyId != local.Id)
            return TradeNetworkOperationResult.Failed("Trade offer is no longer valid in the current lobby.");

        if (!TradeValidation.IsValidAssetReferences(counterpartyAssets, out var error))
            return TradeNetworkOperationResult.Failed(error!);
        if (!_transfers.TryBuildTransferBundle(
                _stateAccessor(),
                counterpartyAssets,
                out _,
                out error))
        {
            return TradeNetworkOperationResult.Failed(error!);
        }

        if (offer.InitiatorAssets.Length == 0 && counterpartyAssets.Count == 0)
            return TradeNetworkOperationResult.Failed("A trade must transfer at least one Voidling or egg.");

        if (_connection.IsLocalHost)
        {
            HandleHostAccept(local.Id, tradeId, counterpartyAssets.ToArray());
        }
        else
        {
            var payload = TradeProtocol.EncodeAcceptCommand(local, tradeId, counterpartyAssets.ToArray());
            if (!_connection.TrySend(
                    lobby.OwnerId,
                    NetworkChannel.Trade,
                    payload,
                    DeliveryMode.Reliable))
            {
                return TradeNetworkOperationResult.Failed("Could not send trade acceptance to the lobby host.");
            }
        }

        return TradeNetworkOperationResult.Succeeded(tradeId);
    }

    public TradeNetworkOperationResult DeclineTrade(string tradeId)
    {
        if (!_receivedOffers.TryGetValue(tradeId, out var offer))
            return TradeNetworkOperationResult.Failed("Trade offer is not available on this client.");

        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null || offer.CounterpartyId != local.Id)
            return TradeNetworkOperationResult.Failed("Trade offer is no longer valid in the current lobby.");

        if (_connection.IsLocalHost)
        {
            HandleHostDecline(local.Id, tradeId);
        }
        else
        {
            var payload = TradeProtocol.EncodeDeclineCommand(local, tradeId);
            if (!_connection.TrySend(lobby.OwnerId, NetworkChannel.Trade, payload, DeliveryMode.Reliable))
                return TradeNetworkOperationResult.Failed("Could not decline the trade through the lobby host.");
        }

        return TradeNetworkOperationResult.Succeeded(tradeId);
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Trade || _connection.CurrentLobby == null)
            return;

        if (TradeProtocol.TryDecodeOfferCommand(
                packet.Payload.Span,
                packet.Sender,
                out var offerMessageId,
                out var tradeId,
                out var lobbyId,
                out var counterpartyId,
                out var initiatorAssets))
        {
            if (_connection.IsLocalHost && RememberMessage(offerMessageId))
                HandleHostOffer(packet.Sender, tradeId, lobbyId, counterpartyId, initiatorAssets);
            return;
        }

        if (TradeProtocol.TryDecodeOffered(packet.Payload.Span, packet.Sender, out var offer))
        {
            if (IsPacketFromCurrentHost(packet.Sender))
                HandleOfferedFromHost(offer);
            return;
        }

        if (TradeProtocol.TryDecodeAcceptCommand(
                packet.Payload.Span,
                packet.Sender,
                out var acceptMessageId,
                out tradeId,
                out var counterpartyAssets))
        {
            if (_connection.IsLocalHost && RememberMessage(acceptMessageId))
                HandleHostAccept(packet.Sender, tradeId, counterpartyAssets);
            return;
        }

        if (TradeProtocol.TryDecodeDeclineCommand(
                packet.Payload.Span,
                packet.Sender,
                out var declineMessageId,
                out tradeId))
        {
            if (_connection.IsLocalHost && RememberMessage(declineMessageId))
                HandleHostDecline(packet.Sender, tradeId);
            return;
        }

        if (TradeProtocol.TryDecodePrepareRequest(
                packet.Payload.Span,
                packet.Sender,
                out var terms,
                out var termsHash))
        {
            if (IsPacketFromCurrentHost(packet.Sender))
                HandlePrepareRequestFromHost(terms, termsHash);
            return;
        }

        if (TradeProtocol.TryDecodeBundlePrepared(
                packet.Payload.Span,
                packet.Sender,
                out var bundleMessageId,
                out tradeId,
                out termsHash,
                out var bundle))
        {
            if (_connection.IsLocalHost && RememberMessage(bundleMessageId))
                HandleHostBundlePrepared(packet.Sender, tradeId, termsHash, bundle);
            return;
        }

        if (TradeProtocol.TryDecodePersistRequest(
                packet.Payload.Span,
                packet.Sender,
                out terms,
                out termsHash,
                out var incomingBundle))
        {
            if (IsPacketFromCurrentHost(packet.Sender))
                HandlePersistRequestFromHost(terms, termsHash, incomingBundle);
            return;
        }

        if (TradeProtocol.TryDecodeReady(
                packet.Payload.Span,
                packet.Sender,
                out var readyMessageId,
                out tradeId,
                out termsHash,
                out var readySuccess,
                out var readyError))
        {
            if (_connection.IsLocalHost && RememberMessage(readyMessageId))
                HandleHostReady(packet.Sender, tradeId, termsHash, readySuccess, readyError);
            return;
        }

        if (TradeProtocol.TryDecodeCommit(
                packet.Payload.Span,
                packet.Sender,
                out tradeId,
                out termsHash))
        {
            if (IsPacketFromCurrentHost(packet.Sender))
                HandleCommitFromHost(tradeId, termsHash);
            return;
        }

        if (TradeProtocol.TryDecodeCommitted(
                packet.Payload.Span,
                packet.Sender,
                out var committedMessageId,
                out tradeId,
                out termsHash,
                out var committedSuccess,
                out var committedError))
        {
            if (_connection.IsLocalHost && RememberMessage(committedMessageId))
                HandleHostCommitted(packet.Sender, tradeId, termsHash, committedSuccess, committedError);
            return;
        }

        if (TradeProtocol.TryDecodeAbort(
                packet.Payload.Span,
                packet.Sender,
                out tradeId,
                out var reason))
        {
            if (IsPacketFromCurrentHost(packet.Sender))
                HandleAbortFromHost(tradeId, reason);
            return;
        }

        Reject("Trade packet was malformed or used an unsupported message type.");
    }

    private void HandleHostOffer(
        PlatformUserId initiatorId,
        string tradeId,
        ulong lobbyId,
        PlatformUserId counterpartyId,
        TradeAssetReference[] initiatorAssets)
    {
        var lobby = _connection.CurrentLobby;
        string? validationError = null;
        if (!_connection.IsLocalHost ||
            lobby == null ||
            lobby.LobbyId != lobbyId ||
            initiatorId == counterpartyId ||
            !_connection.IsLobbyMember(initiatorId) ||
            !_connection.IsLobbyMember(counterpartyId) ||
            !TradeValidation.IsValidAssetReferences(initiatorAssets, out validationError))
        {
            SendAbortToPeer(initiatorId, tradeId, validationError ?? "Trade offer failed lobby validation.");
            return;
        }

        if (_hostedTrades.ContainsKey(tradeId) ||
            _hostedTrades.Values.Any(trade =>
                IsActive(trade.Status) &&
                (IsParticipant(trade, initiatorId) || IsParticipant(trade, counterpartyId))))
        {
            SendAbortToPeer(initiatorId, tradeId, "One of the players is already in another trade.");
            return;
        }

        var offer = new TradeOfferNotice(tradeId, initiatorId, counterpartyId, initiatorAssets);
        _hostedTrades.Add(tradeId, new HostedTrade { Offer = offer });
        DispatchOffered(offer);
    }

    private void HandleHostAccept(
        PlatformUserId sender,
        string tradeId,
        TradeAssetReference[] counterpartyAssets)
    {
        string? validationError = null;
        if (!_connection.IsLocalHost ||
            !_hostedTrades.TryGetValue(tradeId, out var hosted) ||
            hosted.Status != TradeSessionStatus.Offered ||
            sender != hosted.Offer.CounterpartyId ||
            !TradeValidation.IsValidAssetReferences(counterpartyAssets, out validationError))
        {
            if (_hostedTrades.TryGetValue(tradeId, out var invalidHosted))
                AbortHostTrade(invalidHosted, validationError ?? "Trade acceptance failed validation.");
            return;
        }

        if (hosted.Offer.InitiatorAssets.Length == 0 && counterpartyAssets.Length == 0)
        {
            AbortHostTrade(hosted, "A trade must transfer at least one Voidling or egg.");
            return;
        }

        var lobby = _connection.CurrentLobby!;
        var terms = new TradeTerms(
            tradeId,
            lobby.LobbyId,
            hosted.Offer.InitiatorId,
            hosted.Offer.CounterpartyId,
            hosted.Offer.InitiatorAssets,
            counterpartyAssets);
        var hash = TradeTermsHasher.Compute(terms);
        hosted.Terms = terms;
        hosted.TermsHash = hash;
        hosted.Status = TradeSessionStatus.PreparingBundles;
        RaiseStatusForLocalParticipant(hosted, TradeSessionStatus.PreparingBundles, null);
        DispatchPrepareRequest(hosted);
    }

    private void HandleHostDecline(PlatformUserId sender, string tradeId)
    {
        if (!_connection.IsLocalHost ||
            !_hostedTrades.TryGetValue(tradeId, out var hosted) ||
            sender != hosted.Offer.CounterpartyId)
        {
            return;
        }

        hosted.Status = TradeSessionStatus.Declined;
        AbortHostTrade(hosted, "Trade was declined.", TradeSessionStatus.Declined);
    }

    private void HandleHostBundlePrepared(
        PlatformUserId sender,
        string tradeId,
        string termsHash,
        TradeTransferBundle bundle)
    {
        if (!_connection.IsLocalHost ||
            !_hostedTrades.TryGetValue(tradeId, out var hosted) ||
            hosted.Terms == null ||
            hosted.TermsHash == null ||
            hosted.Status != TradeSessionStatus.PreparingBundles ||
            !string.Equals(hosted.TermsHash, termsHash, StringComparison.Ordinal) ||
            !TradeValidation.IsParticipant(hosted.Terms, sender))
        {
            return;
        }

        var expected = TradeValidation.AssetsFor(hosted.Terms, sender);
        if (!BundleMatchesReferences(bundle, expected))
        {
            AbortHostTrade(hosted, "A prepared trade bundle did not match the canonical trade terms.");
            return;
        }

        if (sender == hosted.Terms.InitiatorId)
            hosted.InitiatorBundle = bundle;
        else
            hosted.CounterpartyBundle = bundle;

        if (hosted.InitiatorBundle == null || hosted.CounterpartyBundle == null)
            return;

        hosted.Status = TradeSessionStatus.PersistingPrepare;
        RaiseStatusForLocalParticipant(hosted, TradeSessionStatus.PersistingPrepare, null);
        DispatchPersistRequests(hosted);
    }

    private void HandleHostReady(
        PlatformUserId sender,
        string tradeId,
        string termsHash,
        bool success,
        string? error)
    {
        if (!_connection.IsLocalHost ||
            !_hostedTrades.TryGetValue(tradeId, out var hosted) ||
            hosted.Terms == null ||
            hosted.TermsHash == null ||
            (hosted.Status != TradeSessionStatus.PersistingPrepare &&
             !(hosted.Status == TradeSessionStatus.PreparingBundles && !success)) ||
            !string.Equals(hosted.TermsHash, termsHash, StringComparison.Ordinal) ||
            !TradeValidation.IsParticipant(hosted.Terms, sender))
        {
            return;
        }

        if (!success)
        {
            AbortHostTrade(hosted, error ?? "A player could not prepare the trade.");
            return;
        }

        hosted.ReadyPeers.Add(sender);
        if (!BothParticipantsPresent(hosted.Terms, hosted.ReadyPeers))
            return;

        hosted.Status = TradeSessionStatus.ReadyToCommit;
        RaiseStatusForLocalParticipant(hosted, TradeSessionStatus.ReadyToCommit, null);
        hosted.Status = TradeSessionStatus.Committing;
        DispatchCommit(hosted);
    }

    private void HandleHostCommitted(
        PlatformUserId sender,
        string tradeId,
        string termsHash,
        bool success,
        string? error)
    {
        if (!_connection.IsLocalHost ||
            !_hostedTrades.TryGetValue(tradeId, out var hosted) ||
            hosted.Terms == null ||
            hosted.TermsHash == null ||
            hosted.Status != TradeSessionStatus.Committing ||
            !string.Equals(hosted.TermsHash, termsHash, StringComparison.Ordinal) ||
            !TradeValidation.IsParticipant(hosted.Terms, sender))
        {
            return;
        }

        if (!success)
        {
            hosted.Status = TradeSessionStatus.Failed;
            RaiseStatusForLocalParticipant(
                hosted,
                TradeSessionStatus.Failed,
                error ?? "A peer could not persist the committed trade. Manual recovery may be required.");
            SendAbortToParticipants(
                hosted,
                error ?? "Trade commit did not complete on both peers. Local results may differ.");
            _hostedTrades.Remove(tradeId);
            return;
        }

        hosted.CommittedPeers.Add(sender);
        if (!BothParticipantsPresent(hosted.Terms, hosted.CommittedPeers))
            return;

        hosted.Status = TradeSessionStatus.Completed;
        RaiseStatusForLocalParticipant(hosted, TradeSessionStatus.Completed, null);
        _hostedTrades.Remove(tradeId);
    }

    private void HandleOfferedFromHost(TradeOfferNotice offer)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null ||
            lobby == null ||
            !_connection.IsLobbyMember(offer.InitiatorId) ||
            !_connection.IsLobbyMember(offer.CounterpartyId) ||
            (offer.InitiatorId != local.Id && offer.CounterpartyId != local.Id))
        {
            return;
        }

        if (offer.CounterpartyId == local.Id)
        {
            _receivedOffers[offer.TradeId] = offer;
            TradeOfferReceived?.Invoke(offer);
        }

        TradeStatusChanged?.Invoke(new TradeStatusUpdate(offer.TradeId, TradeSessionStatus.Offered, null));
    }

    private void HandlePrepareRequestFromHost(TradeTerms terms, string termsHash)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null ||
            lobby == null ||
            terms.LobbyId != lobby.LobbyId ||
            !TradeValidation.IsParticipant(terms, local.Id) ||
            !string.Equals(TradeTermsHasher.Compute(terms), termsHash, StringComparison.Ordinal))
        {
            return;
        }

        _localTerms[terms.TradeId] = (terms, termsHash);
        _receivedOffers.Remove(terms.TradeId);
        TradeStatusChanged?.Invoke(new TradeStatusUpdate(
            terms.TradeId,
            TradeSessionStatus.PreparingBundles,
            null));

        var outgoing = TradeValidation.AssetsFor(terms, local.Id);
        if (!_transfers.TryBuildTransferBundle(_stateAccessor(), outgoing, out var bundle, out var error))
        {
            SendReadyFailureToHost(terms.TradeId, termsHash, error ?? "Could not build trade transfer bundle.");
            return;
        }

        if (_connection.IsLocalHost)
        {
            HandleHostBundlePrepared(local.Id, terms.TradeId, termsHash, bundle);
            return;
        }

        var payload = TradeProtocol.EncodeBundlePrepared(local, terms.TradeId, termsHash, bundle);
        if (!_connection.TrySend(lobby.OwnerId, NetworkChannel.Trade, payload, DeliveryMode.Reliable))
        {
            SendReadyFailureToHost(
                terms.TradeId,
                termsHash,
                "Could not send the prepared trade bundle to the lobby host.");
        }
    }

    private void HandlePersistRequestFromHost(
        TradeTerms terms,
        string termsHash,
        TradeTransferBundle incomingBundle)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null ||
            lobby == null ||
            terms.LobbyId != lobby.LobbyId ||
            !TradeValidation.IsParticipant(terms, local.Id) ||
            !string.Equals(TradeTermsHasher.Compute(terms), termsHash, StringComparison.Ordinal))
        {
            return;
        }

        _localTerms[terms.TradeId] = (terms, termsHash);
        var outgoing = TradeValidation.AssetsFor(terms, local.Id);
        var counterparty = TradeValidation.CounterpartyFor(terms, local.Id);
        var result = _transfers.Prepare(
            _stateAccessor(),
            terms.TradeId,
            terms.LobbyId,
            counterparty.Value,
            termsHash,
            outgoing,
            incomingBundle);

        if (!result.Success)
        {
            SendReadyToHost(terms.TradeId, termsHash, false, result.Error);
            return;
        }

        if (!TrySaveState(out var saveError))
        {
            _transfers.AbortPrepared(_stateAccessor(), terms.TradeId);
            SendReadyToHost(terms.TradeId, termsHash, false, saveError);
            return;
        }

        TradeStatusChanged?.Invoke(new TradeStatusUpdate(
            terms.TradeId,
            TradeSessionStatus.PersistingPrepare,
            null));
        SendReadyToHost(terms.TradeId, termsHash, true, null);
    }

    private void HandleCommitFromHost(string tradeId, string termsHash)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null ||
            lobby == null ||
            !_localTerms.TryGetValue(tradeId, out var localTerms) ||
            !string.Equals(localTerms.Hash, termsHash, StringComparison.Ordinal))
        {
            return;
        }

        var result = _transfers.CommitPrepared(_stateAccessor(), tradeId);
        if (!result.Success)
        {
            SendCommittedToHost(tradeId, termsHash, false, result.Error);
            return;
        }

        if (!TrySaveState(out var saveError))
        {
            SendCommittedToHost(tradeId, termsHash, false, saveError);
            TradeStatusChanged?.Invoke(new TradeStatusUpdate(
                tradeId,
                TradeSessionStatus.Failed,
                saveError));
            return;
        }

        LocalStateChanged?.Invoke();
        LocalTradeCommitted?.Invoke(localTerms.Terms);
        TradeStatusChanged?.Invoke(new TradeStatusUpdate(
            tradeId,
            TradeSessionStatus.Committing,
            null));
        SendCommittedToHost(tradeId, termsHash, true, null);
    }

    private void HandleAbortFromHost(string tradeId, string? reason)
    {
        var state = _stateAccessor();
        var before = state.PendingTradeJournal.Count;
        _transfers.AbortPrepared(state, tradeId);
        if (state.PendingTradeJournal.Count != before)
            TrySaveState(out _);

        _receivedOffers.Remove(tradeId);
        _localTerms.Remove(tradeId);
        TradeStatusChanged?.Invoke(new TradeStatusUpdate(
            tradeId,
            TradeSessionStatus.Aborted,
            reason));
    }

    private void DispatchOffered(TradeOfferNotice offer)
    {
        var host = _connection.LocalUser!;
        foreach (var participant in new[] { offer.InitiatorId, offer.CounterpartyId })
        {
            if (participant == host.Id)
                HandleOfferedFromHost(offer);
            else
                _connection.TrySend(
                    participant,
                    NetworkChannel.Trade,
                    TradeProtocol.EncodeOffered(host, offer),
                    DeliveryMode.Reliable);
        }
    }

    private void DispatchPrepareRequest(HostedTrade hosted)
    {
        var host = _connection.LocalUser!;
        var terms = hosted.Terms!;
        var hash = hosted.TermsHash!;
        foreach (var participant in new[] { terms.InitiatorId, terms.CounterpartyId })
        {
            if (participant == host.Id)
            {
                HandlePrepareRequestFromHost(terms, hash);
                continue;
            }

            if (!_connection.TrySend(
                    participant,
                    NetworkChannel.Trade,
                    TradeProtocol.EncodePrepareRequest(host, terms, hash),
                    DeliveryMode.Reliable))
            {
                AbortHostTrade(hosted, "Could not send trade preparation to a participant.");
                return;
            }
        }
    }

    private void DispatchPersistRequests(HostedTrade hosted)
    {
        var host = _connection.LocalUser!;
        var terms = hosted.Terms!;
        var hash = hosted.TermsHash!;

        if (!DispatchPersistRequest(
                host,
                terms.InitiatorId,
                terms,
                hash,
                hosted.CounterpartyBundle!))
        {
            AbortHostTrade(hosted, "Could not send persisted trade preparation to the initiator.");
            return;
        }

        if (!DispatchPersistRequest(
                host,
                terms.CounterpartyId,
                terms,
                hash,
                hosted.InitiatorBundle!))
        {
            AbortHostTrade(hosted, "Could not send persisted trade preparation to the counterparty.");
        }
    }

    private bool DispatchPersistRequest(
        PlatformUser host,
        PlatformUserId participant,
        TradeTerms terms,
        string hash,
        TradeTransferBundle incoming)
    {
        if (participant == host.Id)
        {
            HandlePersistRequestFromHost(terms, hash, incoming);
            return true;
        }

        return _connection.TrySend(
            participant,
            NetworkChannel.Trade,
            TradeProtocol.EncodePersistRequest(host, terms, hash, incoming),
            DeliveryMode.Reliable);
    }

    private void DispatchCommit(HostedTrade hosted)
    {
        var host = _connection.LocalUser!;
        var terms = hosted.Terms!;
        var hash = hosted.TermsHash!;
        foreach (var participant in new[] { terms.InitiatorId, terms.CounterpartyId })
        {
            if (participant == host.Id)
            {
                HandleCommitFromHost(terms.TradeId, hash);
                continue;
            }

            if (!_connection.TrySend(
                    participant,
                    NetworkChannel.Trade,
                    TradeProtocol.EncodeCommit(host, terms.TradeId, hash),
                    DeliveryMode.Reliable))
            {
                hosted.Status = TradeSessionStatus.Failed;
                RaiseStatusForLocalParticipant(
                    hosted,
                    TradeSessionStatus.Failed,
                    "Could not send the trade commit to a participant. Manual recovery may be required.");
                return;
            }
        }
    }

    private void SendReadyFailureToHost(string tradeId, string termsHash, string error)
        => SendReadyToHost(tradeId, termsHash, false, error);

    private void SendReadyToHost(string tradeId, string termsHash, bool success, string? error)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null)
            return;

        if (_connection.IsLocalHost)
            HandleHostReady(local.Id, tradeId, termsHash, success, error);
        else
            _connection.TrySend(
                lobby.OwnerId,
                NetworkChannel.Trade,
                TradeProtocol.EncodeReady(local, tradeId, termsHash, success, error),
                DeliveryMode.Reliable);
    }

    private void SendCommittedToHost(string tradeId, string termsHash, bool success, string? error)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null)
            return;

        if (_connection.IsLocalHost)
            HandleHostCommitted(local.Id, tradeId, termsHash, success, error);
        else
            _connection.TrySend(
                lobby.OwnerId,
                NetworkChannel.Trade,
                TradeProtocol.EncodeCommitted(local, tradeId, termsHash, success, error),
                DeliveryMode.Reliable);
    }

    private void AbortHostTrade(
        HostedTrade hosted,
        string reason,
        TradeSessionStatus status = TradeSessionStatus.Aborted)
    {
        hosted.Status = status;
        SendAbortToParticipants(hosted, reason);
        RaiseStatusForLocalParticipant(hosted, status, reason);
        _hostedTrades.Remove(hosted.Offer.TradeId);
    }

    private void SendAbortToParticipants(HostedTrade hosted, string reason)
    {
        foreach (var participant in new[] { hosted.Offer.InitiatorId, hosted.Offer.CounterpartyId })
            SendAbortToPeer(participant, hosted.Offer.TradeId, reason);
    }

    private void SendAbortToPeer(PlatformUserId peer, string tradeId, string reason)
    {
        var host = _connection.LocalUser;
        if (host == null || !_connection.IsLobbyMember(peer))
            return;

        if (peer == host.Id)
            HandleAbortFromHost(tradeId, reason);
        else
            _connection.TrySend(
                peer,
                NetworkChannel.Trade,
                TradeProtocol.EncodeAbort(host, tradeId, reason),
                DeliveryMode.Reliable);
    }

    private void HandleLobbyChanged(LobbySnapshot lobby)
    {
        var hostChanged = _activeLobbyId == lobby.LobbyId &&
                          _observedHostId.Value != 0 &&
                          _observedHostId != lobby.OwnerId;
        var previousLobbyId = _activeLobbyId;
        _activeLobbyId = lobby.LobbyId;
        _observedHostId = lobby.OwnerId;

        if (hostChanged)
        {
            AbortLocalPreparedForLobby(lobby.LobbyId, "Steam lobby host changed; in-flight trades were cancelled.");
            _hostedTrades.Clear();
            _receivedOffers.Clear();
            _localTerms.Clear();
        }
        else if (previousLobbyId != 0 && previousLobbyId != lobby.LobbyId)
        {
            AbortLocalPreparedForLobby(previousLobbyId, "Connected Garden changed; in-flight trades were cancelled.");
            _hostedTrades.Clear();
            _receivedOffers.Clear();
            _localTerms.Clear();
        }

        if (!_connection.IsLocalHost)
            return;

        foreach (var hosted in _hostedTrades.Values.ToArray())
        {
            if (!_connection.IsLobbyMember(hosted.Offer.InitiatorId) ||
                !_connection.IsLobbyMember(hosted.Offer.CounterpartyId))
            {
                AbortHostTrade(hosted, "A trade participant left the connected Garden.");
            }
        }
    }

    private void HandleLobbyLeft()
    {
        if (_activeLobbyId != 0)
            AbortLocalPreparedForLobby(_activeLobbyId, "Left connected Garden; in-flight trades were cancelled.");
        _activeLobbyId = 0;
        _observedHostId = default;
        _hostedTrades.Clear();
        _receivedOffers.Clear();
        _localTerms.Clear();
        ClearRecentMessages();
    }

    private void AbortLocalPreparedForLobby(ulong lobbyId, string reason)
    {
        var state = _stateAccessor();
        var affected = state.PendingTradeJournal
            .Where(entry => entry.LobbyId == lobbyId)
            .Select(entry => entry.TradeId)
            .ToArray();
        if (_transfers.AbortPreparedForLobby(state, lobbyId) > 0)
            TrySaveState(out _);

        foreach (var tradeId in affected)
            TradeStatusChanged?.Invoke(new TradeStatusUpdate(tradeId, TradeSessionStatus.Aborted, reason));
    }

    private (PlatformUser? Local, LobbySnapshot? Lobby, string? Error) ValidateLocalTradeContext(
        PlatformUserId counterpartyId)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable)
            return (local, lobby, _connection.UnavailableReason ?? "Multiplayer is unavailable.");
        if (local == null || lobby == null)
            return (local, lobby, "Join a connected Garden before trading.");
        if (counterpartyId.Value == 0 || counterpartyId == local.Id || !_connection.IsLobbyMember(counterpartyId))
            return (local, lobby, "Trade counterparty is not a valid connected Garden member.");
        return (local, lobby, null);
    }

    private bool IsPacketFromCurrentHost(PlatformUserId sender)
        => _connection.CurrentLobby?.OwnerId == sender;

    private static bool IsParticipant(HostedTrade hosted, PlatformUserId userId)
        => hosted.Offer.InitiatorId == userId || hosted.Offer.CounterpartyId == userId;

    private static bool IsActive(TradeSessionStatus status)
        => status is TradeSessionStatus.Offered
            or TradeSessionStatus.PreparingBundles
            or TradeSessionStatus.PersistingPrepare
            or TradeSessionStatus.ReadyToCommit
            or TradeSessionStatus.Committing;

    private static bool BothParticipantsPresent(
        TradeTerms terms,
        IReadOnlySet<PlatformUserId> participants)
        => participants.Contains(terms.InitiatorId) && participants.Contains(terms.CounterpartyId);

    private static bool BundleMatchesReferences(
        TradeTransferBundle bundle,
        IReadOnlyCollection<TradeAssetReference> references)
    {
        if (bundle == null)
            return false;

        var actual = new HashSet<TradeAssetReference>();
        foreach (var creature in bundle.Voidlings ?? Array.Empty<VoidlingData>())
        {
            if (creature == null || !actual.Add(new TradeAssetReference(TradeAssetKind.Voidling, creature.Id)))
                return false;
        }
        foreach (var egg in bundle.Eggs ?? Array.Empty<EggData>())
        {
            if (egg == null || !actual.Add(new TradeAssetReference(TradeAssetKind.Egg, egg.Id)))
                return false;
        }

        return actual.SetEquals(references);
    }

    private bool TrySaveState(out string? error)
    {
        error = null;
        try
        {
            _repository.Save(_stateAccessor());
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not persist trade state: {exception.Message}";
            return false;
        }
    }

    private void RaiseStatusForLocalParticipant(
        HostedTrade hosted,
        TradeSessionStatus status,
        string? message)
    {
        var local = _connection.LocalUser;
        if (local != null && IsParticipant(hosted, local.Id))
            TradeStatusChanged?.Invoke(new TradeStatusUpdate(hosted.Offer.TradeId, status, message));
    }

    private bool RememberMessage(Guid messageId)
    {
        if (messageId == Guid.Empty || !_recentMessageIds.Add(messageId))
            return false;
        _recentMessageOrder.Enqueue(messageId);
        while (_recentMessageOrder.Count > RecentMessageLimit)
            _recentMessageIds.Remove(_recentMessageOrder.Dequeue());
        return true;
    }

    private void ClearRecentMessages()
    {
        _recentMessageOrder.Clear();
        _recentMessageIds.Clear();
    }

    private void Reject(string reason)
        => ProtocolRejected?.Invoke(reason);
}
