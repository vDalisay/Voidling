using System;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Presentation.UI.Multiplayer;

namespace VoidlingGame;

public partial class MainController
{
    private TradePresentationBridge? _tradeBridge;
    private TradeHubPanel? _tradeHubPanel;
    private bool _tradeBridgeSubscribed;

    private TradePresentationBridge TradeBridge
        => _tradeBridge ??= GetNode<TradePresentationBridge>(
            "/root/GameBootstrap/TradePresentationBridge");

    private void ComposeTradePresentation()
    {
        if (_tradeBridgeSubscribed)
            return;

        TradeBridge.StateChanged += OnTradeStateChanged;
        TradeBridge.IncomingOfferReceived += OnIncomingTradeOffer;
        _tradeBridgeSubscribed = true;
    }

    private void ShowTrades()
    {
        ComposeTradePresentation();
        var box = OpenModal(Tr("UI_TRADE_TITLE"), new Vector2(548, 330));
        var panel = new TradeHubPanel();
        panel.Configure(TradeBridge.Current);
        panel.OfferRequested += OfferTrade;
        panel.RespondRequested += ShowTradeResponse;
        _tradeHubPanel = panel;
        box.AddChild(panel);
    }

    private void OfferTrade(string counterpartyKey, TradeAssetReference[] assets)
    {
        var result = TradeBridge.Offer(counterpartyKey, assets);
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_TRADE_ACTION_FAILED"),
                result.Error ?? "unknown trade error"));
        }
        RefreshTradeHub();
    }

    private void ShowTradeResponse(string tradeId)
    {
        var offer = TradeBridge.GetIncomingOffer(tradeId);
        if (offer == null)
        {
            ShowToast(Tr("UI_TRADE_OFFER_GONE"));
            RefreshTradeHub();
            return;
        }

        var current = TradeBridge.Current;
        var box = OpenModal(Tr("UI_TRADE_RESPONSE_TITLE"), new Vector2(520, 292));
        var panel = new TradeResponsePanel();
        panel.Configure(new TradeResponsePanelState(offer, current.LocalAssets));
        panel.AcceptRequested += assets => AcceptTrade(tradeId, assets);
        panel.DeclineRequested += () => DeclineTrade(tradeId);
        box.AddChild(panel);
    }

    private void AcceptTrade(string tradeId, TradeAssetReference[] assets)
    {
        var result = TradeBridge.Accept(tradeId, assets);
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_TRADE_ACTION_FAILED"),
                result.Error ?? "unknown trade acceptance error"));
            return;
        }

        ShowToast(Tr("UI_TRADE_ACCEPTED_PENDING"));
        ShowTrades();
    }

    private void DeclineTrade(string tradeId)
    {
        var result = TradeBridge.Decline(tradeId);
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_TRADE_ACTION_FAILED"),
                result.Error ?? "unknown trade decline error"));
            return;
        }

        ShowTrades();
    }

    private void OnTradeStateChanged(TradeHubViewState state)
    {
        if (_tradeHubPanel == null || !GodotObject.IsInstanceValid(_tradeHubPanel))
            return;

        _tradeHubPanel.Render(state);
    }

    private void OnIncomingTradeOffer(TradeIncomingOfferView offer)
    {
        ShowToast(string.Format(
            Tr("UI_TRADE_INCOMING_TOAST"),
            offer.InitiatorDisplayName));
    }

    private void RefreshTradeHub()
    {
        if (_tradeHubPanel == null || !GodotObject.IsInstanceValid(_tradeHubPanel))
            return;

        _tradeHubPanel.Render(TradeBridge.Current);
    }

    private void DetachTradePresentation()
    {
        if (!_tradeBridgeSubscribed || _tradeBridge == null)
            return;

        _tradeBridge.StateChanged -= OnTradeStateChanged;
        _tradeBridge.IncomingOfferReceived -= OnIncomingTradeOffer;
        _tradeBridgeSubscribed = false;
    }
}
