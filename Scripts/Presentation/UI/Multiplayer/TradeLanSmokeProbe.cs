using System;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Trading;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Command-line-only presentation integration probe for development LAN testing. It intentionally
/// drives the exact same invite/select/accept bridge API as the player-facing trade UI, then requires
/// the existing durable trade callback to confirm the expected Voidlings were exchanged locally.
/// </summary>
public partial class TradeLanSmokeProbe : Node
{
    private const double TimeoutSeconds = 25.0;

    private TradePresentationBridge? _bridge;
    private bool _hostMode;
    private bool _complete;
    private bool _advanceScheduled;
    private bool _inviteSent;
    private bool _inviteAccepted;
    private string? _negotiationId;
    private string? _localAssetId;
    private string? _remoteAssetId;

    public void Configure(TradePresentationBridge bridge)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("LAN trade smoke probe must be configured before entering the scene tree.");
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public override void _Ready()
    {
        if (_bridge == null)
            throw new InvalidOperationException("LAN trade smoke probe was not configured.");

        var args = OS.GetCmdlineUserArgs();
        _hostMode = Array.Exists(args, arg =>
            string.Equals(arg, "--voidling-lan-host", StringComparison.OrdinalIgnoreCase));

        _bridge.StateChanged += OnStateChanged;
        _bridge.LocalTradeCommitted += OnLocalTradeCommitted;
        GD.Print($"[trade-lan-smoke] waiting for peer as {(_hostMode ? "host" : "client")}...");
        ScheduleAdvance();
        _ = RunTimeoutAsync();
    }

    public override void _ExitTree()
    {
        if (_bridge == null)
            return;
        _bridge.StateChanged -= OnStateChanged;
        _bridge.LocalTradeCommitted -= OnLocalTradeCommitted;
    }

    private void OnStateChanged(TradeLobbyViewState _)
        => ScheduleAdvance();

    private void ScheduleAdvance()
    {
        if (_complete || _advanceScheduled || !IsInsideTree())
            return;

        _advanceScheduled = true;
        Callable.From(() =>
        {
            _advanceScheduled = false;
            Advance();
        }).CallDeferred();
    }

    private void Advance()
    {
        if (_complete || _bridge == null)
            return;

        var state = _bridge.Current;
        if (!state.Availability.IsAvailable || !state.IsConnected || state.LocalVoidlings.Count == 0)
            return;

        _localAssetId ??= state.LocalVoidlings[0].AssetId;
        var active = state.ActiveNegotiation;
        if (active == null)
        {
            if (_hostMode)
            {
                if (_inviteSent || !state.CanInvite || state.Partners.Count == 0)
                    return;

                var invited = _bridge.Invite(state.Partners[0].Key);
                if (!invited.Success || string.IsNullOrWhiteSpace(invited.NegotiationId))
                {
                    Fail(invited.Error ?? "host could not send trade invitation");
                    return;
                }

                _inviteSent = true;
                _negotiationId = invited.NegotiationId;
                GD.Print($"[trade-lan-smoke] invited peer for negotiation {_negotiationId}");
            }
            else
            {
                if (_inviteAccepted || state.IncomingInvites.Count == 0)
                    return;

                var incoming = state.IncomingInvites[0];
                var accepted = _bridge.AcceptInvite(incoming.NegotiationId);
                if (!accepted.Success)
                {
                    Fail(accepted.Error ?? "client could not accept trade invitation");
                    return;
                }

                _inviteAccepted = true;
                _negotiationId = incoming.NegotiationId;
                GD.Print($"[trade-lan-smoke] accepted negotiation {_negotiationId}");
            }
            return;
        }

        _negotiationId = active.NegotiationId;
        if (active.LocalOffer == null)
        {
            var selected = _bridge.SelectVoidling(active.NegotiationId, _localAssetId);
            if (!selected.Success)
            {
                Fail(selected.Error ?? "could not select local Voidling");
                return;
            }

            GD.Print($"[trade-lan-smoke] selected local Voidling {_localAssetId}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(active.RemoteOfferAssetId))
            _remoteAssetId = active.RemoteOfferAssetId;

        // Wait for the presentation preview as well as the authoritative asset reference. This
        // proves that both players can actually see what the other side placed into the room.
        if (string.IsNullOrWhiteSpace(_remoteAssetId) || active.RemoteOffer == null)
            return;

        if (!active.LocalAccepted)
        {
            var accepted = _bridge.SetAccepted(active.NegotiationId, true);
            if (!accepted.Success)
            {
                Fail(accepted.Error ?? "could not confirm trade");
                return;
            }

            GD.Print($"[trade-lan-smoke] accepted offer; partner preview is {active.RemoteOffer.DisplayName} ({_remoteAssetId})");
        }
    }

    private void OnLocalTradeCommitted(TradeCommittedView trade)
    {
        if (_complete)
            return;

        if (string.IsNullOrWhiteSpace(_localAssetId) || string.IsNullOrWhiteSpace(_remoteAssetId))
        {
            Fail("durable trade committed before the smoke probe observed both negotiated Voidling IDs");
            return;
        }

        var outgoingMatches = trade.OutgoingAssets.Any(asset =>
            asset.Kind == TradeAssetKind.Voidling &&
            string.Equals(asset.AssetId, _localAssetId, StringComparison.Ordinal));
        var incomingMatches = trade.IncomingAssets.Any(asset =>
            asset.Kind == TradeAssetKind.Voidling &&
            string.Equals(asset.AssetId, _remoteAssetId, StringComparison.Ordinal));
        if (!outgoingMatches || !incomingMatches)
        {
            Fail(
                $"durable commit did not match negotiated slots (outgoing={outgoingMatches}, incoming={incomingMatches})");
            return;
        }

        _complete = true;
        GD.Print(
            $"[trade-lan-smoke] LAN_TRADE_SMOKE_SUCCESS negotiation={_negotiationId} " +
            $"outgoing={_localAssetId} incoming={_remoteAssetId}");
        GetTree().Quit(0);
    }

    private async System.Threading.Tasks.Task RunTimeoutAsync()
    {
        await ToSignal(GetTree().CreateTimer(TimeoutSeconds), SceneTreeTimer.SignalName.Timeout);
        if (_complete || !IsInsideTree())
            return;
        Fail("timed out waiting for mutual confirmation and durable trade commit");
    }

    private void Fail(string reason)
    {
        if (_complete)
            return;
        _complete = true;
        GD.PrintErr($"[trade-lan-smoke] LAN_TRADE_SMOKE_FAILED: {reason}");
        GetTree().Quit(3);
    }
}
