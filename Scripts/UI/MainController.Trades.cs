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
    private TradeNegotiationPanel? _tradeNegotiationPanel;
    private TradeExchangeScreen? _tradeExchangeScreen;
    private bool _tradeBridgeSubscribed;
    private string? _openTradeNegotiationId;
    private string? _scheduledTradeNegotiationId;

    private sealed record PendingTradeExchange(
        string NegotiationId,
        string OutgoingAssetId,
        TradeExchangeAssetView Outgoing);

    private readonly Dictionary<string, PendingTradeExchange> _pendingTradeExchanges = new(StringComparer.Ordinal);

    private TradePresentationBridge TradeBridge
        => _tradeBridge ??= GetNode<TradePresentationBridge>(
            "/root/GameBootstrap/TradePresentationBridge");

    private void ComposeTradePresentation()
    {
        if (_tradeBridgeSubscribed)
            return;

        TradeBridge.StateChanged += OnTradeStateChanged;
        TradeBridge.IncomingInviteReceived += OnIncomingTradeInvite;
        TradeBridge.NegotiationActivated += OnTradeNegotiationActivated;
        TradeBridge.LocalTradeCommitted += OnLocalTradeCommitted;
        _tradeBridgeSubscribed = true;
    }

    private void ShowTrades()
    {
        ComposeTradePresentation();
        var current = TradeBridge.Current;
        if (current.ActiveNegotiation != null)
        {
            ShowTradeRoom(current.ActiveNegotiation.NegotiationId);
            return;
        }

        _openTradeNegotiationId = null;
        _scheduledTradeNegotiationId = null;
        _tradeNegotiationPanel = null;
        var box = OpenOnlineModal(Tr("UI_TRADE_TITLE"), new Vector2(548, 330), ShowConnectedZone);
        var panel = new TradeHubPanel();
        panel.Configure(current);
        panel.InviteRequested += InviteTrade;
        panel.AcceptInviteRequested += AcceptTradeInvite;
        panel.DeclineInviteRequested += DeclineTradeInvite;
        _tradeHubPanel = panel;
        box.AddChild(panel);
    }

    private void InviteTrade(string partnerKey)
    {
        var result = TradeBridge.Invite(partnerKey);
        if (!result.Success)
        {
            ShowTradeFailure(result.Error ?? "unknown trade invitation error");
            return;
        }

        ShowToast(Tr("UI_TRADE_INVITE_SENT"));
        RefreshTradeHub();
    }

    private void AcceptTradeInvite(string negotiationId)
    {
        var result = TradeBridge.AcceptInvite(negotiationId);
        if (!result.Success)
        {
            ShowTradeFailure(result.Error ?? "unknown trade invitation error");
            return;
        }

        // The accepting host can transition synchronously; a non-host receives the host echo shortly
        // afterwards. Try immediately and keep the state/event fallback below so both participants are
        // pulled into the same room without clicking Trades again.
        ScheduleTradeRoomOpen(negotiationId);
        RefreshTradeHub();
    }

    private void DeclineTradeInvite(string negotiationId)
    {
        var result = TradeBridge.DeclineInvite(negotiationId);
        if (!result.Success)
            ShowTradeFailure(result.Error ?? "unknown trade decline error");
        RefreshTradeHub();
    }

    private void ShowTradeRoom(string negotiationId)
    {
        _scheduledTradeNegotiationId = null;
        var current = TradeBridge.Current;
        var trade = current.ActiveNegotiation;
        if (trade == null || !string.Equals(trade.NegotiationId, negotiationId, StringComparison.Ordinal))
        {
            // A non-host may still be waiting for the authoritative host echo. Do not bounce it out
            // of the trade flow; NegotiationActivated/StateChanged will schedule another attempt.
            RefreshTradeHub();
            return;
        }

        if (_tradeNegotiationPanel != null && GodotObject.IsInstanceValid(_tradeNegotiationPanel) &&
            string.Equals(_openTradeNegotiationId, negotiationId, StringComparison.Ordinal))
        {
            _tradeNegotiationPanel.Render(current);
            return;
        }

        RememberPendingTrade(trade);
        _openTradeNegotiationId = negotiationId;
        var box = OpenOnlineModal(Tr("UI_TRADE_ROOM_TITLE"), new Vector2(548, 330), ShowTrades);
        var panel = new TradeNegotiationPanel();
        panel.Configure(current);
        panel.SelectVoidlingRequested += assetId => SelectTradeVoidling(negotiationId, assetId);
        panel.AcceptedChanged += accepted => SetTradeAccepted(negotiationId, accepted);
        panel.CancelRequested += () => CancelTradeNegotiation(negotiationId);
        _tradeNegotiationPanel = panel;
        _tradeHubPanel = null;
        box.AddChild(panel);
    }

    private void ScheduleTradeRoomOpen(string negotiationId)
    {
        if (string.IsNullOrWhiteSpace(negotiationId) ||
            string.Equals(_scheduledTradeNegotiationId, negotiationId, StringComparison.Ordinal) ||
            (_tradeNegotiationPanel != null && GodotObject.IsInstanceValid(_tradeNegotiationPanel) &&
             string.Equals(_openTradeNegotiationId, negotiationId, StringComparison.Ordinal)))
        {
            return;
        }

        _scheduledTradeNegotiationId = negotiationId;
        Callable.From(() => ShowTradeRoom(negotiationId)).CallDeferred();
    }

    private void SelectTradeVoidling(string negotiationId, string? assetId)
    {
        var result = TradeBridge.SelectVoidling(negotiationId, assetId);
        if (!result.Success)
            ShowTradeFailure(result.Error ?? "could not update the offered Voidling");
    }

    private void SetTradeAccepted(string negotiationId, bool accepted)
    {
        var result = TradeBridge.SetAccepted(negotiationId, accepted);
        if (!result.Success)
            ShowTradeFailure(result.Error ?? "could not update trade confirmation");
    }

    private void CancelTradeNegotiation(string negotiationId)
    {
        var result = TradeBridge.Cancel(negotiationId);
        if (!result.Success)
        {
            ShowTradeFailure(result.Error ?? "could not cancel the trade");
            return;
        }

        _pendingTradeExchanges.Remove(negotiationId);
        _openTradeNegotiationId = null;
        _scheduledTradeNegotiationId = null;
        _tradeNegotiationPanel = null;
        ShowToast(Tr("UI_TRADE_CANCELLED"));
        ShowTrades();
    }

    private void OnTradeStateChanged(TradeLobbyViewState state)
    {
        if (state.ActiveNegotiation != null)
        {
            RememberPendingTrade(state.ActiveNegotiation);
            if (_tradeNegotiationPanel != null && GodotObject.IsInstanceValid(_tradeNegotiationPanel))
            {
                _tradeNegotiationPanel.Render(state);
            }
            else if (state.ActiveNegotiation.Phase == TradeNegotiationPhase.Negotiating)
            {
                // Host-local transitions can already be canonical before a callback reaches the UI.
                // State reconciliation therefore also owns auto-entry, independently of event order.
                ScheduleTradeRoomOpen(state.ActiveNegotiation.NegotiationId);
            }
        }
        else if (_tradeNegotiationPanel != null && GodotObject.IsInstanceValid(_tradeNegotiationPanel))
        {
            // Do not destroy the pre-commit offer snapshot synchronously. A successful durable
            // commit can publish its terminal negotiation state immediately around the local commit
            // callback. Defer room cleanup one frame so the exchange animation gets first chance to
            // consume that snapshot; a real cancellation/failure will still return to the lobby.
            ScheduleTradeRoomEndedCheck();
        }

        if (_tradeHubPanel != null && GodotObject.IsInstanceValid(_tradeHubPanel))
            _tradeHubPanel.Render(state);
    }

    private void ScheduleTradeRoomEndedCheck()
    {
        var negotiationId = _openTradeNegotiationId;
        if (string.IsNullOrWhiteSpace(negotiationId))
            return;

        Callable.From(() => HandleTradeRoomEndedCheck(negotiationId!)).CallDeferred();
    }

    private void HandleTradeRoomEndedCheck(string negotiationId)
    {
        if (_tradeExchangeScreen != null ||
            !string.Equals(_openTradeNegotiationId, negotiationId, StringComparison.Ordinal))
        {
            return;
        }

        var current = TradeBridge.Current;
        if (current.ActiveNegotiation != null)
            return;

        _pendingTradeExchanges.Remove(negotiationId);
        _openTradeNegotiationId = null;
        _scheduledTradeNegotiationId = null;
        _tradeNegotiationPanel = null;
        ShowToast(Tr("UI_TRADE_ENDED"));
        ShowTrades();
    }

    private void OnIncomingTradeInvite(TradeInviteView invite)
    {
        _gardenEventLog.AppendAction(
            string.Format(Tr("UI_GARDEN_LOG_TRADE_OFFER"), invite.FromDisplayName),
            ShowTrades);
        ShowToast(string.Format(
            Tr("UI_TRADE_INCOMING_TOAST"),
            invite.FromDisplayName));
    }

    private void OnTradeNegotiationActivated(TradeNegotiationView negotiation)
    {
        RememberPendingTrade(negotiation);
        ScheduleTradeRoomOpen(negotiation.NegotiationId);
    }

    private void RememberPendingTrade(TradeNegotiationView negotiation)
    {
        var outgoing = negotiation.LocalOffer;
        if (outgoing == null)
        {
            _pendingTradeExchanges.Remove(negotiation.NegotiationId);
            return;
        }

        _pendingTradeExchanges[negotiation.NegotiationId] = new PendingTradeExchange(
            negotiation.NegotiationId,
            outgoing.AssetId,
            BuildTradeExchangeAsset(outgoing));
    }

    private void OnLocalTradeCommitted(TradeCommittedView trade)
    {
        if (_tradeExchangeScreen != null)
            return;

        var outgoingReference = trade.OutgoingAssets.FirstOrDefault(asset => asset.Kind == TradeAssetKind.Voidling);
        PendingTradeExchange? pending = null;
        if (outgoingReference != null)
        {
            pending = _pendingTradeExchanges.Values.FirstOrDefault(value =>
                string.Equals(value.OutgoingAssetId, outgoingReference.AssetId, StringComparison.Ordinal));
        }
        pending ??= _pendingTradeExchanges.Values.FirstOrDefault();
        if (pending == null)
            return;

        var incomingReference = trade.IncomingAssets
            .OrderBy(asset => asset.Kind == TradeAssetKind.Voidling ? 0 : 1)
            .FirstOrDefault();
        var incoming = BuildTradeExchangeAsset(incomingReference);
        if (incoming == null)
            return;

        _pendingTradeExchanges.Remove(pending.NegotiationId);
        _openTradeNegotiationId = null;
        _scheduledTradeNegotiationId = null;
        _tradeNegotiationPanel = null;
        _gardenEventLog.Append(string.Format(
            Tr("UI_GARDEN_LOG_TRADE_COMPLETE"),
            1,
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
            1,
            trade.IncomingAssets.Count));
        screen.ReturnRequested += EndTradeExchange;
        _tradeExchangeScreen = screen;
        AddChild(screen);
    }

    private TradeExchangeAssetView BuildTradeExchangeAsset(TradeVoidlingChoiceView voidling)
        => new(
            voidling.DisplayName,
            false,
            voidling.TintHex,
            voidling.HasAngelMutation,
            voidling.OtherMutationCount);

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
                Math.Max(0, voidling.RareTraits.Count - (hasAngel ? 1 : 0)));
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

    private void ShowTradeFailure(string error)
        => ShowToast(string.Format(Tr("UI_TRADE_ACTION_FAILED"), error));

    private void DetachTradePresentation()
    {
        if (!_tradeBridgeSubscribed || _tradeBridge == null)
            return;

        _tradeBridge.StateChanged -= OnTradeStateChanged;
        _tradeBridge.IncomingInviteReceived -= OnIncomingTradeInvite;
        _tradeBridge.NegotiationActivated -= OnTradeNegotiationActivated;
        _tradeBridge.LocalTradeCommitted -= OnLocalTradeCommitted;
        _tradeBridgeSubscribed = false;
    }
}
