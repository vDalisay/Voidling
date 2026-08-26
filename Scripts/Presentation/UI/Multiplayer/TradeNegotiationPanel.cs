using System;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Presentation.Voidlings;
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

        var heading = UiFactory.CreateLabel(
            string.Format(Tr("UI_TRADE_ROOM_WITH"), trade.PartnerDisplayName),
            9);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(heading);

        var slots = new HBoxContainer();
        slots.AddThemeConstantOverride("separation", 18);
        slots.Alignment = BoxContainer.AlignmentMode.Center;
        slots.AddChild(BuildOfferSlot(
            Tr("UI_TRADE_YOUR_OFFER"),
            trade.LocalOffer,
            trade.LocalOffer == null ? Tr("UI_TRADE_SLOT_EMPTY") : string.Empty,
            trade.LocalAccepted));
        slots.AddChild(BuildOfferSlot(
            string.Format(Tr("UI_TRADE_THEIR_OFFER"), trade.PartnerDisplayName),
            trade.RemoteOffer,
            trade.RemoteOfferAssetId == null
                ? Tr("UI_TRADE_SLOT_WAITING")
                : Tr("UI_TRADE_REMOTE_SYNCING"),
            trade.RemoteAccepted));
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
                var cardContainer = CreateChoiceCard(
                    choice,
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

        // Normal finalization text belongs to presentation/localization. The Application message is
        // still surfaced for actual failure/diagnostic states, but should not replace localized copy
        // just because the coordinator supplied its English debug description of Finalizing.
        if (trade.Phase != TradeNegotiationPhase.Finalizing && !string.IsNullOrWhiteSpace(trade.Message))
            status = trade.Message!;
        var statusLabel = UiFactory.CreateLabel(status, 7);
        statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(statusLabel);
    }

    private static VBoxContainer CreateChoiceCard(
        TradeVoidlingChoiceView choice,
        Action<bool> toggled,
        out Button card)
    {
        var entry = new VBoxContainer { CustomMinimumSize = new Vector2(84, 78) };
        entry.AddThemeConstantOverride("separation", 1);

        card = UiFactory.CreateButton(string.Empty);
        card.CustomMinimumSize = new Vector2(80, 58);
        card.ToggleMode = true;
        card.KeepPressedOutside = true;
        var portrait = VoidlingAppearancePresenter.CreatePortrait(
            choice.TintHex,
            choice.Appearance,
            choice.HasAngelMutation,
            choice.OtherMutationCount,
            new Vector2(48, 48));
        portrait.Position = new Vector2(16, 4);
        portrait.Size = new Vector2(48, 48);
        card.AddChild(portrait);
        card.Toggled += pressed => toggled(pressed);
        entry.AddChild(card);

        var label = UiFactory.CreateLabel(choice.DisplayName, 6);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.AddThemeColorOverride("font_color", Color.FromHtml("#2F4437"));
        entry.AddChild(label);
        return entry;
    }

    private Control BuildOfferSlot(
        string titleText,
        TradeVoidlingChoiceView? offer,
        string emptyText,
        bool accepted)
    {
        var panel = UiFactory.CreatePanel(new Vector2(205, 94));
        panel.CustomMinimumSize = new Vector2(205, 94);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 1);
        panel.AddChild(box);

        var title = UiFactory.CreateLabel(titleText, 7);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        if (offer == null)
        {
            var empty = UiFactory.CreateLabel(emptyText, 7);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.CustomMinimumSize = new Vector2(190, 52);
            box.AddChild(empty);
        }
        else
        {
            var portrait = VoidlingAppearancePresenter.CreatePortrait(
                offer.TintHex,
                offer.Appearance,
                offer.HasAngelMutation,
                offer.OtherMutationCount,
                new Vector2(46, 46));
            var portraitCenter = new CenterContainer { CustomMinimumSize = new Vector2(190, 48) };
            portraitCenter.AddChild(portrait);
            box.AddChild(portraitCenter);

            var name = UiFactory.CreateLabel(offer.DisplayName, 7);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(name);
        }

        if (accepted)
        {
            var ready = UiFactory.CreateLabel(Tr("UI_TRADE_ACCEPTED_MARK"), 6);
            ready.HorizontalAlignment = HorizontalAlignment.Center;
            ready.AddThemeColorOverride("font_color", Color.FromHtml("#4F7A54"));
            box.AddChild(ready);
        }

        return panel;
    }
}
