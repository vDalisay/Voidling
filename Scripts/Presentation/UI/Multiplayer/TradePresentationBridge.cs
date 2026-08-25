using System;
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
        => StateChanged?.Invoke(state);

    private void HandleIncomingInvite(TradeInviteView invite)
        => IncomingInviteReceived?.Invoke(invite);

    private void HandleNegotiationActivated(TradeNegotiationView negotiation)
        => NegotiationActivated?.Invoke(negotiation);

    private void HandleLocalTradeCommitted(TradeCommittedView trade)
        => LocalTradeCommitted?.Invoke(trade);

    private TradeNegotiationFacade RequireNegotiation()
        => _negotiationFacade ?? throw new InvalidOperationException("Trade presentation bridge is not configured.");
}
