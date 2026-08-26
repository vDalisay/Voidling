using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Trading;

public enum TradeNegotiationPhase
{
    Invited,
    Negotiating,
    Finalizing,
    Completed,
    Cancelled,
    Failed
}

public sealed record TradeNegotiationState(
    string NegotiationId,
    ulong LobbyId,
    PlatformUserId InitiatorId,
    PlatformUserId CounterpartyId,
    TradeNegotiationPhase Phase,
    TradeAssetReference? InitiatorAsset,
    TradeAssetReference? CounterpartyAsset,
    bool InitiatorAccepted,
    bool CounterpartyAccepted,
    string? DurableTradeId,
    string? Message,
    int Revision)
{
    public bool IsParticipant(PlatformUserId id)
        => id == InitiatorId || id == CounterpartyId;

    public TradeAssetReference? AssetFor(PlatformUserId id)
        => id == InitiatorId ? InitiatorAsset : id == CounterpartyId ? CounterpartyAsset : null;

    public bool AcceptedFor(PlatformUserId id)
        => id == InitiatorId ? InitiatorAccepted : id == CounterpartyId && CounterpartyAccepted;
}

public sealed record TradeNegotiationOperationResult(bool Success, string? NegotiationId, string? Error)
{
    public static TradeNegotiationOperationResult Succeeded(string negotiationId)
        => new(true, negotiationId, null);

    public static TradeNegotiationOperationResult Failed(string error)
        => new(false, null, error);
}

/// <summary>
/// Pokémon-style presentation negotiation in front of TradeNetworkCoordinator's durable two-phase
/// transfer. Invite, one-Voidling selection and each player's confirmation are synchronized first.
/// Only after both confirmations does this coordinator invoke the existing journaled transfer path.
/// </summary>
public sealed class TradeNegotiationCoordinator
{
    private const int RecentMessageLimit = 512;

    private readonly MultiplayerConnectionService _connection;
    private readonly TradeNetworkCoordinator _durable;
    private readonly Dictionary<string, TradeNegotiationState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _durableToNegotiation = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<PlatformUserId>> _hostCommitObservers = new(StringComparer.Ordinal);
    private readonly Queue<Guid> _recentMessageOrder = new();
    private readonly HashSet<Guid> _recentMessageIds = new();

    public TradeNegotiationCoordinator(
        MultiplayerConnectionService connection,
        TradeNetworkCoordinator durable)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _durable = durable ?? throw new ArgumentNullException(nameof(durable));

        _connection.PacketReceived += HandlePacket;
        _connection.LobbyLeft += Reset;
        _connection.PeerSessionFailed += HandlePeerFailure;
        _durable.TradeOfferReceived += HandleDurableOfferReceived;
        _durable.TradeStatusChanged += HandleDurableStatusChanged;
        _durable.LocalTradeCommitted += HandleLocalDurableCommit;
    }

    public event Action<TradeNegotiationState>? NegotiationChanged;
    public event Action<TradeNegotiationState>? IncomingInvite;
    public event Action<string>? ProtocolRejected;

    public IReadOnlyCollection<TradeNegotiationState> States => _states.Values.ToArray();

    public TradeNegotiationState? Get(string negotiationId)
        => !string.IsNullOrWhiteSpace(negotiationId) && _states.TryGetValue(negotiationId, out var state)
            ? state
            : null;

    public TradeNegotiationOperationResult Invite(PlatformUserId counterparty)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsAvailable || local == null || lobby == null)
            return TradeNegotiationOperationResult.Failed("Join a connected Garden before trading.");
        if (counterparty == local.Id || !_connection.IsLobbyMember(counterparty))
            return TradeNegotiationOperationResult.Failed("Selected trade partner is no longer in the connected Garden.");
        if (HasActiveNegotiation(local.Id) || HasActiveNegotiation(counterparty))
            return TradeNegotiationOperationResult.Failed("One of the players is already in another trade.");

        var negotiationId = Guid.NewGuid().ToString("N");
        if (_connection.IsLocalHost)
        {
            HandleHostInvite(local.Id, negotiationId, lobby.LobbyId, counterparty);
            return TradeNegotiationOperationResult.Succeeded(negotiationId);
        }

        var sent = SendToHost(TradeNegotiationWire.Invite(negotiationId, lobby.LobbyId, counterparty));
        return sent
            ? TradeNegotiationOperationResult.Succeeded(negotiationId)
            : TradeNegotiationOperationResult.Failed("Could not send the trade invitation to the lobby host.");
    }

    public TradeNegotiationOperationResult AcceptInvite(string negotiationId)
        => SendParticipantCommand(negotiationId, TradeNegotiationWire.AcceptInvite(negotiationId),
            localHandler: (local, _) => HandleHostAcceptInvite(local, negotiationId));

    public TradeNegotiationOperationResult Cancel(string negotiationId)
        => SendParticipantCommand(negotiationId, TradeNegotiationWire.Cancel(negotiationId),
            localHandler: (local, _) => HandleHostCancel(local, negotiationId, "Trade cancelled."));

    public TradeNegotiationOperationResult SelectVoidling(string negotiationId, string? assetId)
    {
        var state = Get(negotiationId);
        var local = _connection.LocalUser;
        if (state == null || local == null || !state.IsParticipant(local.Id))
            return TradeNegotiationOperationResult.Failed("Trade is no longer available.");
        if (state.Phase != TradeNegotiationPhase.Negotiating)
            return TradeNegotiationOperationResult.Failed("Trade selection is locked.");
        if (assetId != null && (string.IsNullOrWhiteSpace(assetId) || assetId.Length > 128))
            return TradeNegotiationOperationResult.Failed("Selected Voidling is invalid.");

        var message = TradeNegotiationWire.Select(negotiationId, assetId);
        if (_connection.IsLocalHost)
            HandleHostSelect(local.Id, negotiationId, assetId);
        else if (!SendToHost(message))
            return TradeNegotiationOperationResult.Failed("Could not update the offered Voidling.");
        return TradeNegotiationOperationResult.Succeeded(negotiationId);
    }

    public TradeNegotiationOperationResult SetAccepted(string negotiationId, bool accepted)
    {
        var state = Get(negotiationId);
        var local = _connection.LocalUser;
        if (state == null || local == null || !state.IsParticipant(local.Id))
            return TradeNegotiationOperationResult.Failed("Trade is no longer available.");
        if (state.Phase != TradeNegotiationPhase.Negotiating)
            return TradeNegotiationOperationResult.Failed("Trade confirmation is locked.");
        if (accepted && state.AssetFor(local.Id) == null)
            return TradeNegotiationOperationResult.Failed("Choose a Voidling before accepting the trade.");

        var message = TradeNegotiationWire.Ready(negotiationId, accepted);
        if (_connection.IsLocalHost)
            HandleHostReady(local.Id, negotiationId, accepted);
        else if (!SendToHost(message))
            return TradeNegotiationOperationResult.Failed("Could not update trade confirmation.");
        return TradeNegotiationOperationResult.Succeeded(negotiationId);
    }

    private TradeNegotiationOperationResult SendParticipantCommand(
        string negotiationId,
        TradeNegotiationWire message,
        Action<PlatformUserId, TradeNegotiationState> localHandler)
    {
        var state = Get(negotiationId);
        var local = _connection.LocalUser;
        if (state == null || local == null || !state.IsParticipant(local.Id))
            return TradeNegotiationOperationResult.Failed("Trade is no longer available.");

        if (_connection.IsLocalHost)
            localHandler(local.Id, state);
        else if (!SendToHost(message))
            return TradeNegotiationOperationResult.Failed("Could not send the trade action to the lobby host.");
        return TradeNegotiationOperationResult.Succeeded(negotiationId);
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Session ||
            !TradeNegotiationWire.TryDecode(packet.Payload.Span, out var message))
        {
            return;
        }

        if (_connection.IsLocalHost)
        {
            if (!RememberMessage(message.MessageId))
                return;

            switch (message.Type)
            {
                case TradeNegotiationWire.TypeInvite:
                    HandleHostInvite(packet.Sender, message.NegotiationId, message.LobbyId, new PlatformUserId(message.CounterpartyId));
                    break;
                case TradeNegotiationWire.TypeAcceptInvite:
                    HandleHostAcceptInvite(packet.Sender, message.NegotiationId);
                    break;
                case TradeNegotiationWire.TypeCancel:
                    HandleHostCancel(packet.Sender, message.NegotiationId, message.Reason ?? "Trade cancelled.");
                    break;
                case TradeNegotiationWire.TypeSelect:
                    HandleHostSelect(packet.Sender, message.NegotiationId, message.AssetId);
                    break;
                case TradeNegotiationWire.TypeReady:
                    HandleHostReady(packet.Sender, message.NegotiationId, message.Accepted);
                    break;
                case TradeNegotiationWire.TypeDurableStarted:
                    HandleHostDurableStarted(packet.Sender, message.NegotiationId, message.DurableTradeId);
                    break;
                case TradeNegotiationWire.TypeCommitObserved:
                    HandleHostCommitObserved(packet.Sender, message.NegotiationId, message.DurableTradeId);
                    break;
                case TradeNegotiationWire.TypeDurableFailed:
                    HandleHostDurableFailed(packet.Sender, message.NegotiationId, message.DurableTradeId, message.Reason);
                    break;
            }
            return;
        }

        var lobby = _connection.CurrentLobby;
        if (lobby == null || packet.Sender != lobby.OwnerId)
            return;

        if (message.Type == TradeNegotiationWire.TypeState && message.State != null)
            ApplyState(message.State);
        else if (message.Type == TradeNegotiationWire.TypeStartDurable && message.State != null)
        {
            ApplyState(message.State);
            BeginDurableTrade(message.State);
        }
    }

    private void HandleHostInvite(
        PlatformUserId sender,
        string negotiationId,
        ulong lobbyId,
        PlatformUserId counterparty)
    {
        var lobby = _connection.CurrentLobby;
        if (!_connection.IsLocalHost ||
            lobby == null ||
            lobby.LobbyId != lobbyId ||
            string.IsNullOrWhiteSpace(negotiationId) ||
            negotiationId.Length > 64 ||
            sender == counterparty ||
            !_connection.IsLobbyMember(sender) ||
            !_connection.IsLobbyMember(counterparty) ||
            HasActiveNegotiation(sender) ||
            HasActiveNegotiation(counterparty))
        {
            return;
        }

        var state = new TradeNegotiationState(
            negotiationId,
            lobbyId,
            sender,
            counterparty,
            TradeNegotiationPhase.Invited,
            null,
            null,
            false,
            false,
            null,
            null,
            1);
        _states[negotiationId] = state;
        DispatchState(state);
    }

    private void HandleHostAcceptInvite(PlatformUserId sender, string negotiationId)
    {
        if (!TryGetHosted(negotiationId, out var state) ||
            state.Phase != TradeNegotiationPhase.Invited ||
            sender != state.CounterpartyId)
        {
            return;
        }

        state = state with
        {
            Phase = TradeNegotiationPhase.Negotiating,
            Message = null,
            Revision = state.Revision + 1
        };
        _states[negotiationId] = state;
        DispatchState(state);
    }

    private void HandleHostCancel(PlatformUserId sender, string negotiationId, string reason)
    {
        if (!TryGetHosted(negotiationId, out var state) ||
            !state.IsParticipant(sender) ||
            state.Phase is TradeNegotiationPhase.Finalizing or TradeNegotiationPhase.Completed or TradeNegotiationPhase.Cancelled or TradeNegotiationPhase.Failed)
        {
            return;
        }

        state = state with
        {
            Phase = TradeNegotiationPhase.Cancelled,
            InitiatorAccepted = false,
            CounterpartyAccepted = false,
            Message = string.IsNullOrWhiteSpace(reason) ? "Trade cancelled." : reason,
            Revision = state.Revision + 1
        };
        _states[negotiationId] = state;
        DispatchState(state);
    }

    private void HandleHostSelect(PlatformUserId sender, string negotiationId, string? assetId)
    {
        if (!TryGetHosted(negotiationId, out var state) ||
            state.Phase != TradeNegotiationPhase.Negotiating ||
            !state.IsParticipant(sender) ||
            (assetId != null && (string.IsNullOrWhiteSpace(assetId) || assetId.Length > 128)))
        {
            return;
        }

        var asset = assetId == null ? null : new TradeAssetReference(TradeAssetKind.Voidling, assetId);
        state = sender == state.InitiatorId
            ? state with
            {
                InitiatorAsset = asset,
                InitiatorAccepted = false,
                CounterpartyAccepted = false,
                Revision = state.Revision + 1
            }
            : state with
            {
                CounterpartyAsset = asset,
                InitiatorAccepted = false,
                CounterpartyAccepted = false,
                Revision = state.Revision + 1
            };
        _states[negotiationId] = state;
        DispatchState(state);
    }

    private void HandleHostReady(PlatformUserId sender, string negotiationId, bool accepted)
    {
        if (!TryGetHosted(negotiationId, out var state) ||
            state.Phase != TradeNegotiationPhase.Negotiating ||
            !state.IsParticipant(sender) ||
            (accepted && state.AssetFor(sender) == null))
        {
            return;
        }

        state = sender == state.InitiatorId
            ? state with { InitiatorAccepted = accepted, Revision = state.Revision + 1 }
            : state with { CounterpartyAccepted = accepted, Revision = state.Revision + 1 };
        _states[negotiationId] = state;
        DispatchState(state);

        if (!state.InitiatorAccepted || !state.CounterpartyAccepted ||
            state.InitiatorAsset == null || state.CounterpartyAsset == null)
        {
            return;
        }

        state = state with
        {
            Phase = TradeNegotiationPhase.Finalizing,
            Message = "Both players accepted. Finalizing safely...",
            Revision = state.Revision + 1
        };
        _states[negotiationId] = state;
        DispatchState(state);
        DispatchStartDurable(state);
    }

    private void DispatchStartDurable(TradeNegotiationState state)
    {
        var local = _connection.LocalUser;
        if (local == null)
            return;

        if (state.InitiatorId == local.Id)
        {
            BeginDurableTrade(state);
            return;
        }

        _connection.TrySend(
            state.InitiatorId,
            NetworkChannel.Session,
            TradeNegotiationWire.Encode(TradeNegotiationWire.StartDurable(state)),
            DeliveryMode.Reliable);
    }

    private void BeginDurableTrade(TradeNegotiationState state)
    {
        var local = _connection.LocalUser;
        if (local == null ||
            local.Id != state.InitiatorId ||
            state.Phase != TradeNegotiationPhase.Finalizing ||
            state.InitiatorAsset == null ||
            state.CounterpartyAsset == null)
        {
            return;
        }

        var result = _durable.OfferTrade(state.CounterpartyId, new[] { state.InitiatorAsset });
        if (!result.Success || string.IsNullOrWhiteSpace(result.TradeId))
        {
            NotifyHostDurableFailed(state, null, result.Error ?? "Could not start durable trade transfer.");
            return;
        }

        _durableToNegotiation[result.TradeId!] = state.NegotiationId;
        NotifyHostDurableStarted(state.NegotiationId, result.TradeId!);
    }

    private void HandleDurableOfferReceived(TradeOfferNotice offer)
    {
        var local = _connection.LocalUser;
        if (local == null || offer.CounterpartyId != local.Id)
            return;

        var state = _states.Values.FirstOrDefault(candidate =>
            candidate.Phase == TradeNegotiationPhase.Finalizing &&
            candidate.InitiatorId == offer.InitiatorId &&
            candidate.CounterpartyId == offer.CounterpartyId &&
            candidate.InitiatorAsset != null &&
            offer.InitiatorAssets.Length == 1 &&
            offer.InitiatorAssets[0] == candidate.InitiatorAsset);
        if (state == null || state.CounterpartyAsset == null)
            return;

        _durableToNegotiation[offer.TradeId] = state.NegotiationId;
        var result = _durable.AcceptTrade(offer.TradeId, new[] { state.CounterpartyAsset });
        if (!result.Success)
            NotifyHostDurableFailed(state, offer.TradeId, result.Error ?? "Could not accept durable trade transfer.");
    }

    private void NotifyHostDurableStarted(string negotiationId, string durableTradeId)
    {
        var local = _connection.LocalUser;
        if (local == null)
            return;
        if (_connection.IsLocalHost)
            HandleHostDurableStarted(local.Id, negotiationId, durableTradeId);
        else
            SendToHost(TradeNegotiationWire.DurableStarted(negotiationId, durableTradeId));
    }

    private void HandleHostDurableStarted(PlatformUserId sender, string negotiationId, string? durableTradeId)
    {
        if (!TryGetHosted(negotiationId, out var state) ||
            state.Phase != TradeNegotiationPhase.Finalizing ||
            sender != state.InitiatorId ||
            string.IsNullOrWhiteSpace(durableTradeId) ||
            durableTradeId.Length > 64)
        {
            return;
        }

        state = state with
        {
            DurableTradeId = durableTradeId,
            Revision = state.Revision + 1
        };
        _states[negotiationId] = state;
        _durableToNegotiation[durableTradeId] = negotiationId;
        DispatchState(state);
    }

    private void HandleLocalDurableCommit(TradeTerms terms)
    {
        if (!TryResolveNegotiationForDurable(terms.TradeId, terms.InitiatorId, terms.CounterpartyId, out var state))
            return;

        _durableToNegotiation[terms.TradeId] = state.NegotiationId;
        var local = _connection.LocalUser;
        if (local == null)
            return;
        if (_connection.IsLocalHost)
            HandleHostCommitObserved(local.Id, state.NegotiationId, terms.TradeId);
        else
            SendToHost(TradeNegotiationWire.CommitObserved(state.NegotiationId, terms.TradeId));
    }

    private void HandleHostCommitObserved(PlatformUserId sender, string negotiationId, string? durableTradeId)
    {
        if (!TryGetHosted(negotiationId, out var state) ||
            state.Phase != TradeNegotiationPhase.Finalizing ||
            !state.IsParticipant(sender) ||
            string.IsNullOrWhiteSpace(durableTradeId) ||
            (state.DurableTradeId != null && !string.Equals(state.DurableTradeId, durableTradeId, StringComparison.Ordinal)))
        {
            return;
        }

        if (state.DurableTradeId == null)
        {
            state = state with { DurableTradeId = durableTradeId, Revision = state.Revision + 1 };
            _states[negotiationId] = state;
        }

        if (!_hostCommitObservers.TryGetValue(negotiationId, out var observers))
        {
            observers = new HashSet<PlatformUserId>();
            _hostCommitObservers[negotiationId] = observers;
        }
        observers.Add(sender);
        if (!observers.Contains(state.InitiatorId) || !observers.Contains(state.CounterpartyId))
            return;

        state = state with
        {
            Phase = TradeNegotiationPhase.Completed,
            Message = "Trade complete.",
            Revision = state.Revision + 1
        };
        _states[negotiationId] = state;
        _hostCommitObservers.Remove(negotiationId);
        DispatchState(state);
    }

    private void HandleDurableStatusChanged(TradeStatusUpdate update)
    {
        if (update.Status is not (TradeSessionStatus.Failed or TradeSessionStatus.Aborted or TradeSessionStatus.Declined))
            return;
        if (!_durableToNegotiation.TryGetValue(update.TradeId, out var negotiationId) ||
            !_states.TryGetValue(negotiationId, out var state) ||
            state.Phase != TradeNegotiationPhase.Finalizing)
        {
            return;
        }

        NotifyHostDurableFailed(state, update.TradeId, update.Message ?? "Trade transfer failed.");
    }

    private void NotifyHostDurableFailed(TradeNegotiationState state, string? durableTradeId, string reason)
    {
        var local = _connection.LocalUser;
        if (local == null)
            return;
        if (_connection.IsLocalHost)
            HandleHostDurableFailed(local.Id, state.NegotiationId, durableTradeId, reason);
        else
            SendToHost(TradeNegotiationWire.DurableFailed(state.NegotiationId, durableTradeId, reason));
    }

    private void HandleHostDurableFailed(
        PlatformUserId sender,
        string negotiationId,
        string? durableTradeId,
        string? reason)
    {
        if (!TryGetHosted(negotiationId, out var state) ||
            state.Phase != TradeNegotiationPhase.Finalizing ||
            !state.IsParticipant(sender))
        {
            return;
        }

        state = state with
        {
            Phase = TradeNegotiationPhase.Failed,
            DurableTradeId = durableTradeId ?? state.DurableTradeId,
            Message = string.IsNullOrWhiteSpace(reason) ? "Trade transfer failed." : reason,
            Revision = state.Revision + 1
        };
        _states[negotiationId] = state;
        DispatchState(state);
    }

    private void DispatchState(TradeNegotiationState state)
    {
        var local = _connection.LocalUser;
        if (local == null)
            return;

        foreach (var participant in new[] { state.InitiatorId, state.CounterpartyId })
        {
            if (participant == local.Id)
                ApplyState(state);
            else
                _connection.TrySend(
                    participant,
                    NetworkChannel.Session,
                    TradeNegotiationWire.Encode(TradeNegotiationWire.StateSnapshot(state)),
                    DeliveryMode.Reliable);
        }
    }

    private void ApplyState(TradeNegotiationState incoming)
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null ||
            incoming.LobbyId != lobby.LobbyId ||
            !incoming.IsParticipant(local.Id) ||
            (incoming.InitiatorId != local.Id && !_connection.IsLobbyMember(incoming.InitiatorId)) ||
            (incoming.CounterpartyId != local.Id && !_connection.IsLobbyMember(incoming.CounterpartyId)))
        {
            return;
        }

        if (_states.TryGetValue(incoming.NegotiationId, out var existing) && existing.Revision >= incoming.Revision)
            return;

        var isNewInvite = incoming.Phase == TradeNegotiationPhase.Invited &&
                          incoming.CounterpartyId == local.Id &&
                          (!_states.TryGetValue(incoming.NegotiationId, out existing) || existing.Phase != TradeNegotiationPhase.Invited);
        _states[incoming.NegotiationId] = incoming;
        if (!string.IsNullOrWhiteSpace(incoming.DurableTradeId))
            _durableToNegotiation[incoming.DurableTradeId!] = incoming.NegotiationId;
        NegotiationChanged?.Invoke(incoming);
        if (isNewInvite)
            IncomingInvite?.Invoke(incoming);
    }

    private bool TryResolveNegotiationForDurable(
        string durableTradeId,
        PlatformUserId initiator,
        PlatformUserId counterparty,
        out TradeNegotiationState state)
    {
        if (_durableToNegotiation.TryGetValue(durableTradeId, out var negotiationId) &&
            _states.TryGetValue(negotiationId, out state!))
        {
            return true;
        }

        state = _states.Values.FirstOrDefault(candidate =>
            candidate.Phase == TradeNegotiationPhase.Finalizing &&
            candidate.InitiatorId == initiator &&
            candidate.CounterpartyId == counterparty)!;
        return state != null;
    }

    private bool HasActiveNegotiation(PlatformUserId participant)
        => _states.Values.Any(state =>
            state.IsParticipant(participant) &&
            state.Phase is TradeNegotiationPhase.Invited or TradeNegotiationPhase.Negotiating or TradeNegotiationPhase.Finalizing);

    private bool TryGetHosted(string negotiationId, out TradeNegotiationState state)
    {
        state = null!;
        return _connection.IsLocalHost &&
               !string.IsNullOrWhiteSpace(negotiationId) &&
               _states.TryGetValue(negotiationId, out state);
    }

    private bool SendToHost(TradeNegotiationWire message)
    {
        var lobby = _connection.CurrentLobby;
        return lobby != null && _connection.TrySend(
            lobby.OwnerId,
            NetworkChannel.Session,
            TradeNegotiationWire.Encode(message),
            DeliveryMode.Reliable);
    }

    private void HandlePeerFailure(PlatformUserId peer)
    {
        if (!_connection.IsLocalHost)
            return;
        foreach (var state in _states.Values.Where(value =>
                     value.IsParticipant(peer) &&
                     value.Phase is TradeNegotiationPhase.Invited or TradeNegotiationPhase.Negotiating).ToArray())
        {
            HandleHostCancel(peer, state.NegotiationId, "Trade cancelled because a player disconnected.");
        }
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

    private void Reset()
    {
        _states.Clear();
        _durableToNegotiation.Clear();
        _hostCommitObservers.Clear();
        _recentMessageOrder.Clear();
        _recentMessageIds.Clear();
    }

    private sealed class TradeNegotiationWire
    {
        public const string TypeInvite = "trade.negotiation.invite";
        public const string TypeAcceptInvite = "trade.negotiation.invite.accept";
        public const string TypeCancel = "trade.negotiation.cancel";
        public const string TypeSelect = "trade.negotiation.select";
        public const string TypeReady = "trade.negotiation.ready";
        public const string TypeState = "trade.negotiation.state";
        public const string TypeStartDurable = "trade.negotiation.finalize";
        public const string TypeDurableStarted = "trade.negotiation.durable.started";
        public const string TypeCommitObserved = "trade.negotiation.commit.observed";
        public const string TypeDurableFailed = "trade.negotiation.durable.failed";

        public int Version { get; init; } = 1;
        public string Type { get; init; } = string.Empty;
        public Guid MessageId { get; init; } = Guid.NewGuid();
        public string NegotiationId { get; init; } = string.Empty;
        public ulong LobbyId { get; init; }
        public ulong CounterpartyId { get; init; }
        public string? AssetId { get; init; }
        public bool Accepted { get; init; }
        public string? DurableTradeId { get; init; }
        public string? Reason { get; init; }
        public TradeNegotiationState? State { get; init; }

        public static TradeNegotiationWire Invite(string id, ulong lobbyId, PlatformUserId counterparty)
            => new() { Type = TypeInvite, NegotiationId = id, LobbyId = lobbyId, CounterpartyId = counterparty.Value };
        public static TradeNegotiationWire AcceptInvite(string id)
            => new() { Type = TypeAcceptInvite, NegotiationId = id };
        public static TradeNegotiationWire Cancel(string id)
            => new() { Type = TypeCancel, NegotiationId = id, Reason = "Trade cancelled." };
        public static TradeNegotiationWire Select(string id, string? assetId)
            => new() { Type = TypeSelect, NegotiationId = id, AssetId = assetId };
        public static TradeNegotiationWire Ready(string id, bool accepted)
            => new() { Type = TypeReady, NegotiationId = id, Accepted = accepted };
        public static TradeNegotiationWire StateSnapshot(TradeNegotiationState state)
            => new() { Type = TypeState, NegotiationId = state.NegotiationId, State = state };
        public static TradeNegotiationWire StartDurable(TradeNegotiationState state)
            => new() { Type = TypeStartDurable, NegotiationId = state.NegotiationId, State = state };
        public static TradeNegotiationWire DurableStarted(string id, string tradeId)
            => new() { Type = TypeDurableStarted, NegotiationId = id, DurableTradeId = tradeId };
        public static TradeNegotiationWire CommitObserved(string id, string tradeId)
            => new() { Type = TypeCommitObserved, NegotiationId = id, DurableTradeId = tradeId };
        public static TradeNegotiationWire DurableFailed(string id, string? tradeId, string reason)
            => new() { Type = TypeDurableFailed, NegotiationId = id, DurableTradeId = tradeId, Reason = reason };

        public static byte[] Encode(TradeNegotiationWire message)
            => JsonSerializer.SerializeToUtf8Bytes(message);

        public static bool TryDecode(ReadOnlySpan<byte> payload, out TradeNegotiationWire message)
        {
            message = null!;
            if (payload.Length is <= 0 or > MultiplayerProtocol.MaxPacketBytes)
                return false;
            try
            {
                var decoded = JsonSerializer.Deserialize<TradeNegotiationWire>(payload);
                if (decoded == null || decoded.Version != 1 || decoded.MessageId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(decoded.Type) ||
                    !decoded.Type.StartsWith("trade.negotiation.", StringComparison.Ordinal) ||
                    decoded.NegotiationId.Length > 64 ||
                    (decoded.AssetId?.Length ?? 0) > 128 ||
                    (decoded.DurableTradeId?.Length ?? 0) > 64 ||
                    (decoded.Reason?.Length ?? 0) > 256)
                {
                    return false;
                }
                message = decoded;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
