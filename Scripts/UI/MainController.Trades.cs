using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Presentation.UI.Multiplayer;

namespace VoidlingGame;

public partial class MainController
{
    private TradePresentationBridge? _tradeBridge;
    private TradeHubPanel? _tradeHubPanel;
    private TradeExchangeScreen? _tradeExchangeScreen;
    private bool _tradeBridgeSubscribed;

    private sealed record PendingTradeExchange(
        TradeExchangeAssetView? Outgoing,
        int OutgoingCount);

    private readonly Dictionary<string, PendingTradeExchange> _pendingTradeExchanges = new(StringComparer.Ordinal);

    private TradePresentationBridge TradeBridge
        => _tradeBridge ??= GetNode<TradePresentationBridge>(
            "/root/GameBootstrap/TradePresentationBridge");

    private void ComposeTradePresentation()
    {
        if (_tradeBridgeSubscribed)
            return;

        TradeBridge.StateChanged += OnTradeStateChanged;
        TradeBridge.IncomingOfferReceived += OnIncomingTradeOffer;
        TradeBridge.LocalTradeCommitted += OnLocalTradeCommitted;
        _tradeBridgeSubscribed = true;
    }

    private void ShowTrades()
    {
        ComposeTradePresentation();
        var box = OpenOnlineModal(Tr("UI_TRADE_TITLE"), new Vector2(548, 330), ShowConnectedZone);
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
        else if (!string.IsNullOrWhiteSpace(result.TradeId))
        {
            _pendingTradeExchanges[result.TradeId!] = CreatePendingTradeExchange(
                assets);
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
        var box = OpenOnlineModal(Tr("UI_TRADE_RESPONSE_TITLE"), new Vector2(520, 292), ShowTrades);
        var panel = new TradeResponsePanel();
        panel.Configure(new TradeResponsePanelState(offer, current.LocalAssets));
        panel.AcceptRequested += assets => AcceptTrade(tradeId, assets);
        panel.DeclineRequested += () => DeclineTrade(tradeId);
        box.AddChild(panel);
    }

    private void AcceptTrade(string tradeId, TradeAssetReference[] assets)
    {
        var offer = TradeBridge.GetIncomingOffer(tradeId);
        if (offer == null)
        {
            ShowToast(Tr("UI_TRADE_OFFER_GONE"));
            return;
        }

        _pendingTradeExchanges[tradeId] = CreatePendingTradeExchange(
            assets);
        var result = TradeBridge.Accept(tradeId, assets);
        if (!result.Success)
        {
            _pendingTradeExchanges.Remove(tradeId);
            ShowToast(string.Format(
                Tr("UI_TRADE_ACTION_FAILED"),
                result.Error ?? "unknown trade acceptance error"));
            return;
        }

        ShowToast(Tr("UI_TRADE_ACCEPTED_PENDING"));
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
        foreach (var terminal in state.RecentStatuses.Where(status =>
                     status.Status is TradeSessionStatus.Failed or TradeSessionStatus.Aborted or TradeSessionStatus.Declined))
        {
            _pendingTradeExchanges.Remove(terminal.TradeId);
        }
        if (_tradeHubPanel == null || !GodotObject.IsInstanceValid(_tradeHubPanel))
            return;

        _tradeHubPanel.Render(state);
    }

    private void OnIncomingTradeOffer(TradeIncomingOfferView offer)
    {
        _gardenEventLog.AppendAction(
            string.Format(Tr("UI_GARDEN_LOG_TRADE_OFFER"), offer.InitiatorDisplayName),
            () => ShowTradeResponse(offer.TradeId));
        ShowToast(string.Format(
            Tr("UI_TRADE_INCOMING_TOAST"),
            offer.InitiatorDisplayName));
    }

    private PendingTradeExchange CreatePendingTradeExchange(
        TradeAssetReference[] outgoing)
        => new(
            BuildTradeExchangeAsset(outgoing
                .OrderBy(asset => asset.Kind == TradeAssetKind.Voidling ? 0 : 1)
                .FirstOrDefault()),
            outgoing.Length);

    private void OnLocalTradeCommitted(TradeCommittedView trade)
    {
        if (!_pendingTradeExchanges.TryGetValue(trade.TradeId, out var pending) ||
            _tradeExchangeScreen != null)
            return;

        var incomingReference = trade.IncomingAssets
            .OrderBy(asset => asset.Kind == TradeAssetKind.Voidling ? 0 : 1)
            .FirstOrDefault();
        var incoming = BuildTradeExchangeAsset(incomingReference);
        if (incoming == null && pending.Outgoing == null)
            return;

        _pendingTradeExchanges.Remove(trade.TradeId);
        _gardenEventLog.Append(string.Format(
            Tr("UI_GARDEN_LOG_TRADE_COMPLETE"),
            pending.OutgoingCount,
            trade.IncomingAssets.Count));
        if (_modalHost.IsOpen)
            CloseModal(false);
        _garden.SetGameplayActive(false);
        _garden.Visible = false;
        _uiRoot.Visible = false;

        var screen = new TradeExchangeScreen();
        screen.Configure(new TradeExchangeScreenState(
            pending.Outgoing,
            incoming,
            pending.OutgoingCount,
            trade.IncomingAssets.Count));
        screen.ReturnRequested += EndTradeExchange;
        _tradeExchangeScreen = screen;
        AddChild(screen);
    }

    private TradeExchangeAssetView? BuildTradeExchangeAsset(TradeAssetReference? asset)
    {
        if (asset == null)
            return null;

        if (asset.Kind == TradeAssetKind.Voidling)
        {
            var voidling = _session.State.Voidlings.FirstOrDefault(value =>
                string.Equals(value.Id, asset.AssetId, StringComparison.Ordinal));
            if (voidling == null)
                return null;
            var hasAngel = voidling.RareTraits.Any(trait =>
                string.Equals(trait.TraitId, "Angel", StringComparison.OrdinalIgnoreCase));
            return new TradeExchangeAssetView(
                voidling.Name,
                false,
                voidling.TintHex,
                hasAngel,
                voidling.RareTraits.Count - (hasAngel ? 1 : 0));
        }

        var eggIndex = _session.State.OwnedEggs.FindIndex(egg =>
            string.Equals(egg.Id, asset.AssetId, StringComparison.Ordinal));
        return eggIndex < 0
            ? null
            : new TradeExchangeAssetView(
                string.Format(Tr("UI_TRADE_EXCHANGE_EGG"), eggIndex + 1),
                true,
                string.Empty,
                false,
                0);
    }

    private void EndTradeExchange()
    {
        if (_tradeExchangeScreen != null && GodotObject.IsInstanceValid(_tradeExchangeScreen))
        {
            _tradeExchangeScreen.ReturnRequested -= EndTradeExchange;
            _tradeExchangeScreen.QueueFree();
        }
        _tradeExchangeScreen = null;
        _garden.Visible = true;
        _garden.SetGameplayActive(true);
        _uiRoot.Visible = true;
        RefreshUi();
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
        _tradeBridge.LocalTradeCommitted -= OnLocalTradeCommitted;
        _tradeBridgeSubscribed = false;
    }
}
