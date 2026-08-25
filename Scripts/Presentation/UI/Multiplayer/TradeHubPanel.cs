using System;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Entry lobby for trading. No assets are chosen here: a player invites a connected partner first,
/// and both move into the shared trading room only after the recipient accepts that invitation.
/// </summary>
public partial class TradeHubPanel : VBoxContainer
{
    public event Action<string>? InviteRequested;
    public event Action<string>? AcceptInviteRequested;
    public event Action<string>? DeclineInviteRequested;

    private TradeLobbyViewState? _state;
    private bool _ready;

    public void Configure(TradeLobbyViewState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("TradeHubPanel must be configured before entering the scene tree.");
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
        if (_state == null)
            throw new InvalidOperationException("TradeHubPanel must be configured before AddChild.");
        AddThemeConstantOverride("separation", 7);
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

        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_OFFER_TITLE"), 9));
        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_INVITE_HINT"), 7));

        if (state.WaitingForPlayer != null)
        {
            AddChild(UiFactory.CreateLabel(
                string.Format(Tr("UI_TRADE_WAITING_INVITE"), state.WaitingForPlayer),
                8));
        }
        else if (state.Partners.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_NO_PARTNERS"), 7));
        }
        else
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 7);

            var partner = new OptionButton
            {
                CustomMinimumSize = new Vector2(280, 26),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None
            };
            UiFactory.ApplyPixelFont(partner, 8);
            UiFactory.ApplyButtonChrome(partner);
            for (var i = 0; i < state.Partners.Count; i++)
            {
                partner.AddItem(state.Partners[i].DisplayName, i);
                partner.SetItemMetadata(i, state.Partners[i].Key);
            }
            row.AddChild(partner);

            var invite = UiFactory.CreateButton(Tr("UI_TRADE_INVITE"));
            invite.CustomMinimumSize = new Vector2(120, 26);
            invite.Disabled = !state.CanInvite;
            invite.Pressed += () =>
            {
                if (partner.Selected < 0)
                    return;
                var key = partner.GetItemMetadata(partner.Selected).AsString();
                if (!string.IsNullOrWhiteSpace(key))
                    InviteRequested?.Invoke(key);
            };
            row.AddChild(invite);
            AddChild(row);
        }

        if (state.IncomingInvites.Count == 0)
            return;

        AddChild(UiFactory.CreateLabel(Tr("UI_TRADE_INCOMING_TITLE"), 9));
        foreach (var incoming in state.IncomingInvites)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            var text = UiFactory.CreateLabel(
                string.Format(Tr("UI_TRADE_INVITE_FROM"), incoming.FromDisplayName),
                8);
            text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(text);

            var accept = UiFactory.CreateButton(Tr("UI_COMMON_ACCEPT"));
            accept.CustomMinimumSize = new Vector2(88, 25);
            accept.Pressed += () => AcceptInviteRequested?.Invoke(incoming.NegotiationId);
            row.AddChild(accept);

            var decline = UiFactory.CreateButton(Tr("UI_COMMON_CANCEL"));
            decline.CustomMinimumSize = new Vector2(88, 25);
            decline.Pressed += () => DeclineInviteRequested?.Invoke(incoming.NegotiationId);
            row.AddChild(decline);
            AddChild(row);
        }
    }
}
