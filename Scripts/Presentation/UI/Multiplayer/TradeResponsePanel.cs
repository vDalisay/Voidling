using System;
using System.Collections.Generic;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

public sealed record TradeResponsePanelState(
    TradeIncomingOfferView Offer,
    IReadOnlyList<TradeLocalAssetView> LocalAssets);

/// <summary>
/// Recipient-side trade response view. Zero selected return assets is valid and represents accepting
/// a gift. All transfer validation and durable prepare/commit behavior remains below presentation.
/// </summary>
public partial class TradeResponsePanel : VBoxContainer
{
    public event Action<TradeAssetReference[]>? AcceptRequested;
    public event Action? DeclineRequested;

    private TradeResponsePanelState? _state;

    public void Configure(TradeResponsePanelState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("TradeResponsePanel must be configured before entering the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("TradeResponsePanel must be configured before AddChild.");

        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Build(_state);
    }

    private void Build(TradeResponsePanelState state)
    {
        AddChild(UiFactory.CreateLabel(
            string.Format(
                Tr("UI_TRADE_RESPONSE_OFFER"),
                state.Offer.InitiatorDisplayName,
                state.Offer.VoidlingCount,
                state.Offer.EggCount),
            8));

        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_RESPONSE_PICK"), 7));
        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_ACCEPT_GIFT_HINT"), 6));

        var selected = new List<TradeAssetReference>();
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(470, 105),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var assets = new VBoxContainer();
        assets.AddThemeConstantOverride("separation", 2);
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
                    Text = asset.Kind == TradeAssetKind.Egg
                        ? string.Format(Tr("UI_TRADE_ASSET_EGG"), asset.DisplayName)
                        : string.Format(Tr("UI_TRADE_ASSET_VOIDLING"), asset.DisplayName),
                    FocusMode = Control.FocusModeEnum.None
                };
                UiFactory.ApplyPixelFont(check, 7);
                var captured = asset;
                check.Toggled += pressed =>
                {
                    var reference = new TradeAssetReference(captured.Kind, captured.AssetId);
                    if (pressed)
                    {
                        if (!selected.Contains(reference))
                            selected.Add(reference);
                    }
                    else
                    {
                        selected.Remove(reference);
                    }
                };
                assets.AddChild(check);
            }
        }

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 7);
        var accept = UiFactory.CreateButton(Tr("UI_TRADE_ACCEPT"));
        accept.CustomMinimumSize = new Vector2(150, 26);
        accept.Pressed += () => AcceptRequested?.Invoke(selected.ToArray());
        actions.AddChild(accept);

        var decline = UiFactory.CreateButton(Tr("UI_TRADE_DECLINE"));
        decline.CustomMinimumSize = new Vector2(120, 26);
        decline.Pressed += () => DeclineRequested?.Invoke();
        actions.AddChild(decline);
        AddChild(actions);
    }
}
