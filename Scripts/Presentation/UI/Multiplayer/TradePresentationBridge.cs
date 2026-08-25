using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Trading;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Godot boundary for the trade lobby and mutual-confirmation trading room. Durable platform IDs,
/// transfer bundles, persistence journals and packet callbacks remain below presentation.
/// </summary>
public partial class TradePresentationBridge : Node
{
    private TradeFacade? _durableFacade;
    private TradeNegotiationFacade? _negotiationFacade;
    private readonly HashSet<string> _announcedInviteIds = new(StringComparer.Ordinal);
    private string _lastStateSignature = string.Empty;
    private string _lastActivatedNegotiation = string.Empty;

    public event Action<TradeLobbyViewState>? StateChanged;
    public event Action<TradeInviteView>? IncomingInviteReceived;
    public event Action<TradeNegotiationView>? NegotiationActivated;
    public event Action<TradeCommittedView>? LocalTradeCommitted;

    public TradeLobbyViewState Current => RequireNegotiation().Current;

    public void Configure(TradeFacade durableFacade, TradeNegotiationFacade negotiationFacade)
    {
        if (_durableFacade != null || _negotiationFacade != null)
            throw new InvalidOperationException("Trade presentation bridge is already configured.");
        _durableFacade = durableFacade ?? throw new ArgumentNullException(nameof(durableFacade));
        _negotiationFacade = negotiationFacade ?? throw new ArgumentNullException(nameof(negotiationFacade));

        _negotiationFacade.StateChanged += HandleStateChanged;
        _negotiationFacade.IncomingInviteReceived += HandleIncomingInvite;
        _negotiationFacade.NegotiationActivated += HandleNegotiationActivated;
        _durableFacade.LocalTradeCommitted += HandleLocalTradeCommitted;
    }

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (!Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-lan-trade-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var probe = new TradeLanSmokeProbe
        {
            Name = nameof(TradeLanSmokeProbe)
        };
        probe.Configure(this);
        AddChild(probe);
    }

    public override void _Process(double delta)
    {
        if (_negotiationFacade == null)
            return;

        // Host-authoritative coordinators update their canonical dictionary before dispatching the
        // same snapshot back to a host-local participant. The coordinator's revision guard can
        // therefore suppress a host-local callback even though Current already contains the new
        // state. Reconcile here as a presentation safety net so invites and accepted rooms can never
        // exist silently until the user manually reopens the Trades screen.
        PublishState(_negotiationFacade.Current);
    }

    public TradeNegotiationOperationResult Invite(string partnerKey)
        => RequireNegotiation().Invite(partnerKey);

    public TradeNegotiationOperationResult AcceptInvite(string negotiationId)
        => RequireNegotiation().AcceptInvite(negotiationId);

    public TradeNegotiationOperationResult DeclineInvite(string negotiationId)
        => RequireNegotiation().DeclineInvite(negotiationId);

    public TradeNegotiationOperationResult Cancel(string negotiationId)
        => RequireNegotiation().Cancel(negotiationId);

    public TradeNegotiationOperationResult SelectVoidling(string negotiationId, string? assetId)
        => RequireNegotiation().SelectVoidling(negotiationId, assetId);

    public TradeNegotiationOperationResult SetAccepted(string negotiationId, bool accepted)
        => RequireNegotiation().SetAccepted(negotiationId, accepted);

    public override void _ExitTree()
    {
        if (_negotiationFacade != null)
        {
            _negotiationFacade.StateChanged -= HandleStateChanged;
            _negotiationFacade.IncomingInviteReceived -= HandleIncomingInvite;
            _negotiationFacade.NegotiationActivated -= HandleNegotiationActivated;
        }
        if (_durableFacade != null)
            _durableFacade.LocalTradeCommitted -= HandleLocalTradeCommitted;
    }

    private void HandleStateChanged(TradeLobbyViewState state)
        => PublishState(state);

    private void HandleIncomingInvite(TradeInviteView invite)
    {
        if (_announcedInviteIds.Add(invite.NegotiationId))
            IncomingInviteReceived?.Invoke(invite);
    }

    private void HandleNegotiationActivated(TradeNegotiationView negotiation)
        => PublishNegotiationActivated(negotiation);

    private void HandleLocalTradeCommitted(TradeCommittedView trade)
        => LocalTradeCommitted?.Invoke(trade);

    private void PublishState(TradeLobbyViewState state)
    {
        foreach (var invite in state.IncomingInvites)
        {
            if (_announcedInviteIds.Add(invite.NegotiationId))
                IncomingInviteReceived?.Invoke(invite);
        }

        var active = state.ActiveNegotiation;
        if (active != null && active.Phase == TradeNegotiationPhase.Negotiating)
            PublishNegotiationActivated(active);
        else if (active == null)
            _lastActivatedNegotiation = string.Empty;

        var signature = BuildStateSignature(state);
        if (string.Equals(signature, _lastStateSignature, StringComparison.Ordinal))
            return;

        _lastStateSignature = signature;
        StateChanged?.Invoke(state);
    }

    private void PublishNegotiationActivated(TradeNegotiationView negotiation)
    {
        var marker = $"{negotiation.NegotiationId}:{(int)negotiation.Phase}";
        if (string.Equals(marker, _lastActivatedNegotiation, StringComparison.Ordinal))
            return;

        _lastActivatedNegotiation = marker;
        NegotiationActivated?.Invoke(negotiation);
    }

    private static string BuildStateSignature(TradeLobbyViewState state)
    {
        var active = state.ActiveNegotiation;
        var activeSignature = active == null
            ? "-"
            : string.Join(':',
                active.NegotiationId,
                ((int)active.Phase).ToString(),
                active.LocalOffer?.AssetId ?? string.Empty,
                active.RemoteOffer?.AssetId ?? active.RemoteOfferAssetId ?? string.Empty,
                active.LocalAccepted ? "1" : "0",
                active.RemoteAccepted ? "1" : "0",
                active.Message ?? string.Empty);

        return string.Join('|',
            state.Availability.IsAvailable ? "1" : "0",
            state.Availability.Reason ?? string.Empty,
            state.IsConnected ? "1" : "0",
            state.CanInvite ? "1" : "0",
            state.WaitingForPlayer ?? string.Empty,
            string.Join(',', state.Partners.Select(value => value.Key)),
            string.Join(',', state.IncomingInvites.Select(value => value.NegotiationId)),
            string.Join(',', state.LocalVoidlings.Select(value => value.AssetId)),
            activeSignature);
    }

    private TradeNegotiationFacade RequireNegotiation()
        => _negotiationFacade ?? throw new InvalidOperationException("Trade presentation bridge is not configured.");
}
