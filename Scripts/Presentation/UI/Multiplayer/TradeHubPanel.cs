using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Connected-Garden trade entry screen. It only assembles partner/asset selections and renders
/// incoming offers/status; the durable transaction protocol remains entirely below presentation.
/// </summary>
public partial class TradeHubPanel : VBoxContainer
{
    public event Action<string, TradeAssetReference[]>? OfferRequested;
    public event Action<string>? RespondRequested;

    private TradeHubViewState? _state;
    private bool _ready;

    public void Configure(TradeHubViewState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("TradeHubPanel must be configured before entering the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Render(TradeHubViewState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (_ready)
            Rebuild();
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("TradeHubPanel must be configured before AddChild.");
        AddThemeConstantOverride("separation", 5);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _ready = true;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var state = _state!;
        if (!state.Availability.IsAvailable || !state.IsConnected)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_NEED_GARDEN"), 8));
            return;
        }

        BuildOfferSection(state);
        BuildIncomingSection(state);
        BuildStatusSection(state);
    }

    private void BuildOfferSection(TradeHubViewState state)
    {
        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_OFFER_TITLE"), 8));
        if (state.Counterparties.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_NO_PARTNERS"), 7));
            return;
        }

        var partnerRow = new HBoxContainer();
        partnerRow.AddThemeConstantOverride("separation", 6);
        var partner = new OptionButton
        {
            CustomMinimumSize = new Vector2(230, 25),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };
        UiFactory.ApplyPixelFont(partner, 7);
        UiFactory.ApplyButtonChrome(partner);
        for (var i = 0; i < state.Counterparties.Count; i++)
        {
            var counterparty = state.Counterparties[i];
            partner.AddItem(counterparty.DisplayName, i);
            partner.SetItemMetadata(i, counterparty.Key);
        }
        partnerRow.AddChild(partner);
        AddChild(partnerRow);

        var selectedAssets = new List<TradeAssetReference>();
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(500, 76),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var assets = new VBoxContainer();
        assets.AddThemeConstantOverride("separation", 1);
        assets.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(assets);
        AddChild(scroll);

        if (state.LocalAssets.Count == 0)
        {
            assets.AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_NO_ASSETS"), 7));
        }
        else
        {
            foreach (var asset in state.LocalAssets)
            {
                var check = new CheckBox
                {
                    Text = AssetLabel(asset),
                    FocusMode = Control.FocusModeEnum.None
                };
                UiFactory.ApplyPixelFont(check, 7);
                var captured = asset;
                check.Toggled += pressed =>
                {
                    var reference = new TradeAssetReference(captured.Kind, captured.AssetId);
                    if (pressed)
                    {
                        if (!selectedAssets.Contains(reference))
                            selectedAssets.Add(reference);
                    }
                    else
                    {
                        selectedAssets.Remove(reference);
                    }
                };
                assets.AddChild(check);
            }
        }

        var offer = UiFactory.CreateButton(Tr("UI_TRADE_SEND_OFFER"));
        offer.CustomMinimumSize = new Vector2(160, 25);
        offer.Disabled = !state.CanOffer || state.LocalAssets.Count == 0;
        offer.Pressed += () =>
        {
            var key = partner.GetItemMetadata(partner.Selected).AsString();
            if (!string.IsNullOrWhiteSpace(key))
                OfferRequested?.Invoke(key, selectedAssets.ToArray());
        };
        AddChild(offer);

        if (!state.CanOffer)
            AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_ACTIVE_HINT"), 6));
    }

    private void BuildIncomingSection(TradeHubViewState state)
    {
        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_INCOMING_TITLE"), 8));
        if (state.IncomingOffers.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_INCOMING_EMPTY"), 6));
            return;
        }

        foreach (var incoming in state.IncomingOffers)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            var summary = UiFactory.CreateLabel(
                string.Format(
                    Tr("UI_TRADE_INCOMING_SUMMARY"),
                    incoming.InitiatorDisplayName,
                    incoming.VoidlingCount,
                    incoming.EggCount),
                7);
            summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(summary);

            var respond = UiFactory.CreateButton(Tr("UI_TRADE_RESPOND"));
            respond.CustomMinimumSize = new Vector2(92, 24);
            respond.Pressed += () => RespondRequested?.Invoke(incoming.TradeId);
            row.AddChild(respond);
            AddChild(row);
        }
    }

    private void BuildStatusSection(TradeHubViewState state)
    {
        if (state.RecentStatuses.Count == 0)
            return;

        var latest = state.RecentStatuses[0];
        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_STATUS_TITLE"), 8));
        var status = UiFactory.CreateLabel(latest.Message, 6);
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(status);
    }

    private string AssetLabel(TradeLocalAssetView asset)
        => asset.Kind == TradeAssetKind.Egg
            ? string.Format(Tr("UI_TRADE_ASSET_EGG"), asset.DisplayName)
            : string.Format(Tr("UI_TRADE_ASSET_VOIDLING"), asset.DisplayName);
}
