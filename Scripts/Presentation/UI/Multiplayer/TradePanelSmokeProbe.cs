using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Headless presentation regression probe for the player-facing trading room. It drives the actual
/// TradeNegotiationPanel controls so a visually selected Voidling cannot silently emit an empty
/// selection again. This intentionally does not use networking; the separate LAN trade smoke covers
/// the full two-process negotiation and durable commit path.
/// </summary>
public partial class TradePanelSmokeProbe : Node
{
    private const string ProbeAssetId = "trade-panel-smoke-voidling";
    private bool _complete;

    public override async void _Ready()
    {
        var choice = new TradeVoidlingChoiceView(
            ProbeAssetId,
            "Panel Smoke",
            "#A4C8E8",
            false,
            0);
        var negotiation = new TradeNegotiationView(
            "trade-panel-smoke",
            TradeNegotiationPhase.Negotiating,
            "Remote tester",
            null,
            null,
            null,
            false,
            false,
            true,
            false,
            true,
            null);
        var state = new TradeLobbyViewState(
            MultiplayerAvailability.Available,
            true,
            false,
            Array.Empty<TradePartnerView>(),
            new[] { choice },
            Array.Empty<TradeInviteView>(),
            null,
            negotiation);

        string? selectedAssetId = null;
        bool? accepted = null;
        var cancelled = false;

        var panel = new TradeNegotiationPanel();
        panel.Configure(state);
        panel.SelectVoidlingRequested += assetId => selectedAssetId = assetId;
        panel.AcceptedChanged += value => accepted = value;
        panel.CancelRequested += () => cancelled = true;
        AddChild(panel);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree())
            return;

        var selectableCards = DescendantButtons(panel)
            .Where(button => button.ToggleMode)
            .ToArray();
        if (selectableCards.Length != 1)
        {
            Fail($"expected exactly one selectable Voidling card, found {selectableCards.Length}");
            return;
        }

        selectableCards[0].EmitSignal(BaseButton.SignalName.Toggled, true);
        if (!string.Equals(selectedAssetId, ProbeAssetId, StringComparison.Ordinal))
        {
            Fail($"Voidling card emitted '{selectedAssetId ?? "<null>"}' instead of '{ProbeAssetId}'");
            return;
        }

        var selectedNegotiation = negotiation with
        {
            LocalOffer = choice,
            CanAccept = true
        };
        panel.Render(state with { ActiveNegotiation = selectedNegotiation });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree())
            return;

        var actionButtons = DescendantButtons(panel)
            .Where(button => !button.ToggleMode && !button.Disabled)
            .ToArray();
        if (actionButtons.Length < 2)
        {
            Fail($"expected enabled Accept and Cancel controls, found {actionButtons.Length}");
            return;
        }

        actionButtons[0].EmitSignal(BaseButton.SignalName.Pressed);
        if (accepted != true)
        {
            Fail("Accept button did not emit AcceptedChanged(true)");
            return;
        }

        actionButtons[1].EmitSignal(BaseButton.SignalName.Pressed);
        if (!cancelled)
        {
            Fail("Cancel button did not emit CancelRequested");
            return;
        }

        _complete = true;
        GD.Print("[trade-panel-smoke] TRADE_PANEL_SMOKE_SUCCESS selection/accept/cancel controls emitted the expected intents.");
        GetTree().Quit(0);
    }

    private static IEnumerable<Button> DescendantButtons(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button)
                yield return button;

            foreach (var nested in DescendantButtons(child))
                yield return nested;
        }
    }

    private void Fail(string reason)
    {
        if (_complete)
            return;
        _complete = true;
        GD.PrintErr($"[trade-panel-smoke] TRADE_PANEL_SMOKE_FAILED: {reason}");
        GetTree().Quit(4);
    }
}
