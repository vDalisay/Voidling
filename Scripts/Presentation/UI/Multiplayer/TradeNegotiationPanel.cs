using System;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Shared trading-room presentation. Each side exposes one Voidling slot and one independent Accept
/// button. Selection changes revoke both confirmations; finalization starts only after both accept.
/// </summary>
public partial class TradeNegotiationPanel : VBoxContainer
{
    public event Action<string?>? SelectVoidlingRequested;
    public event Action<bool>? AcceptedChanged;
    public event Action? CancelRequested;

    private TradeLobbyViewState? _state;
    private bool _ready;

    public void Configure(TradeLobbyViewState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("TradeNegotiationPanel must be configured before entering the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Render(TradeLobbyViewState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (_ready)
            Rebuild();
    }

    public override void _Ready()
    {
        if (_state?.ActiveNegotiation == null)
            throw new InvalidOperationException("TradeNegotiationPanel requires an active negotiation before AddChild.");
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
        var trade = state.ActiveNegotiation;
        if (trade == null)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_ROOM_CLOSED"), 8));
            return;
        }

        AddChild(UiFactory.CreateLabel(
            string.Format(Tr("UI_TRADE_ROOM_WITH"), trade.PartnerDisplayName),
            9));

        var slots = new HBoxContainer();
        slots.AddThemeConstantOverride("separation", 18);
        slots.Alignment = BoxContainer.AlignmentMode.Center;
        slots.AddChild(BuildLocalSlot(trade));
        slots.AddChild(BuildRemoteSlot(trade));
        AddChild(slots);

        if (trade.CanChangeOffer)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_CHOOSE_ONE"), 7));
            var scroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(500, 88),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            var choices = new HBoxContainer();
            choices.AddThemeConstantOverride("separation", 7);
            scroll.AddChild(choices);
            foreach (var choice in state.LocalVoidlings)
            {
                var selected = trade.LocalOffer != null &&
                               string.Equals(trade.LocalOffer.AssetId, choice.AssetId, StringComparison.Ordinal);
                var captured = choice;
                var cardContainer = UiFactory.CreateVoidlingCard(
                    choice.DisplayName,
                    UiFactory.ParseTint(choice.TintHex),
                    choice.HasAngelMutation,
                    choice.OtherMutationCount,
                    pressed =>
                    {
                        if (pressed)
                            SelectVoidlingRequested?.Invoke(captured.AssetId);
                        else if (selected)
                            SelectVoidlingRequested?.Invoke(null);
                    },
                    out var card);
                card.SetPressedNoSignal(selected);
                choices.AddChild(cardContainer);
            }
            AddChild(scroll);
        }

        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 8);
        actionRow.Alignment = BoxContainer.AlignmentMode.Center;

        var accept = UiFactory.CreateButton(
            trade.LocalAccepted ? Tr("UI_TRADE_UNACCEPT") : Tr("UI_COMMON_ACCEPT"));
        accept.CustomMinimumSize = new Vector2(142, 27);
        accept.Disabled = !trade.CanAccept && !trade.LocalAccepted;
        accept.Pressed += () => AcceptedChanged?.Invoke(!trade.LocalAccepted);
        actionRow.AddChild(accept);

        var cancel = UiFactory.CreateButton(Tr("UI_COMMON_CANCEL"));
        cancel.CustomMinimumSize = new Vector2(110, 27);
        cancel.Disabled = !trade.CanCancel;
        cancel.Pressed += () => CancelRequested?.Invoke();
        actionRow.AddChild(cancel);
        AddChild(actionRow);

        var status = trade.Phase == TradeNegotiationPhase.Finalizing
            ? Tr("UI_TRADE_FINALIZING")
            : trade.RemoteAccepted
                ? Tr("UI_TRADE_PARTNER_ACCEPTED")
                : Tr("UI_TRADE_PARTNER_WAITING");
        if (!string.IsNullOrWhiteSpace(trade.Message))
            status = trade.Message!;
        var statusLabel = UiFactory.CreateLabel(status, 7);
        statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(statusLabel);
    }

    private Control BuildLocalSlot(TradeNegotiationView trade)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(190, 84) };
        box.AddThemeConstantOverride("separation", 2);
        var title = UiFactory.CreateLabel(Tr("UI_TRADE_YOUR_OFFER"), 8);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        if (trade.LocalOffer == null)
        {
            var empty = UiFactory.CreateLabel(Tr("UI_TRADE_SLOT_EMPTY"), 7);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.CustomMinimumSize = new Vector2(180, 54);
            empty.VerticalAlignment = VerticalAlignment.Center;
            box.AddChild(empty);
        }
        else
        {
            var choice = trade.LocalOffer;
            var portrait = UiFactory.CreatePortrait(
                UiFactory.ParseTint(choice.TintHex),
                choice.HasAngelMutation,
                choice.OtherMutationCount,
                new Vector2(48, 48));
            var center = new CenterContainer { CustomMinimumSize = new Vector2(180, 50) };
            center.AddChild(portrait);
            box.AddChild(center);
            var name = UiFactory.CreateLabel(choice.DisplayName, 7);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(name);
        }
        return box;
    }

    private Control BuildRemoteSlot(TradeNegotiationView trade)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(190, 84) };
        box.AddThemeConstantOverride("separation", 2);
        var title = UiFactory.CreateLabel(
            string.Format(Tr("UI_TRADE_THEIR_OFFER"), trade.PartnerDisplayName),
            8);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        var text = trade.RemoteOfferAssetId == null
            ? Tr("UI_TRADE_SLOT_WAITING")
            : Tr("UI_TRADE_REMOTE_SELECTED");
        var offered = UiFactory.CreateLabel(text, 7);
        offered.HorizontalAlignment = HorizontalAlignment.Center;
        offered.VerticalAlignment = VerticalAlignment.Center;
        offered.CustomMinimumSize = new Vector2(180, 58);
        box.AddChild(offered);
        return box;
    }
}
