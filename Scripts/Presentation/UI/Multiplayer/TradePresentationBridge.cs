using System;
using Godot;
using Voidling.Application.Multiplayer.Trading;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Godot boundary for trade UI. Platform IDs, transfer bundles, persistence journals and network
/// protocol callbacks remain in Application; presentation receives only view state and typed intent.
/// </summary>
public partial class TradePresentationBridge : Node
{
    private TradeFacade? _facade;

    public event Action<TradeHubViewState>? StateChanged;
    public event Action<TradeIncomingOfferView>? IncomingOfferReceived;
    public event Action<TradeCommittedView>? LocalTradeCommitted;

    public TradeHubViewState Current => RequireFacade().Current;

    public void Configure(TradeFacade facade)
    {
        if (_facade != null)
            throw new InvalidOperationException("Trade presentation bridge is already configured.");
        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        _facade.StateChanged += HandleStateChanged;
        _facade.IncomingOfferReceived += HandleIncomingOfferReceived;
        _facade.LocalTradeCommitted += HandleLocalTradeCommitted;
    }

    public TradeNetworkOperationResult Offer(
        string counterpartyKey,
        TradeAssetReference[] assets)
        => RequireFacade().Offer(counterpartyKey, assets ?? Array.Empty<TradeAssetReference>());

    public TradeNetworkOperationResult Accept(
        string tradeId,
        TradeAssetReference[] assets)
        => RequireFacade().Accept(tradeId, assets ?? Array.Empty<TradeAssetReference>());

    public TradeNetworkOperationResult Decline(string tradeId)
        => RequireFacade().Decline(tradeId);

    public TradeIncomingOfferView? GetIncomingOffer(string tradeId)
        => RequireFacade().GetIncomingOffer(tradeId);

    public override void _ExitTree()
    {
        if (_facade == null)
            return;
        _facade.StateChanged -= HandleStateChanged;
        _facade.IncomingOfferReceived -= HandleIncomingOfferReceived;
        _facade.LocalTradeCommitted -= HandleLocalTradeCommitted;
    }

    private void HandleStateChanged(TradeHubViewState state)
        => StateChanged?.Invoke(state);

    private void HandleIncomingOfferReceived(TradeIncomingOfferView offer)
        => IncomingOfferReceived?.Invoke(offer);

    private void HandleLocalTradeCommitted(TradeCommittedView trade)
        => LocalTradeCommitted?.Invoke(trade);

    private TradeFacade RequireFacade()
        => _facade ?? throw new InvalidOperationException("Trade presentation bridge is not configured.");
}
