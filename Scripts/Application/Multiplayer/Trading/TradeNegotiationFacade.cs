using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Trading;

public sealed record TradePartnerView(string Key, string DisplayName);

public sealed record TradeVoidlingChoiceView(
    string AssetId,
    string DisplayName,
    string TintHex,
    bool HasAngelMutation,
    int OtherMutationCount,
    string VisualTypeId = VoidlingAppearanceData.DefaultVisualTypeId,
    float PaletteHue = -1.0f,
    string LayerIdsKey = "");

public sealed record TradeInviteView(string NegotiationId, string FromDisplayName);

public sealed record TradeNegotiationView(
    string NegotiationId,
    TradeNegotiationPhase Phase,
    string PartnerDisplayName,
    TradeVoidlingChoiceView? LocalOffer,
    TradeVoidlingChoiceView? RemoteOffer,
    string? RemoteOfferAssetId,
    bool LocalAccepted,
    bool RemoteAccepted,
    bool CanChangeOffer,
    bool CanAccept,
    bool CanCancel,
    string? Message);

public sealed record TradeLobbyViewState(
    MultiplayerAvailability Availability,
    bool IsConnected,
    bool CanInvite,
    IReadOnlyList<TradePartnerView> Partners,
    IReadOnlyList<TradeVoidlingChoiceView> LocalVoidlings,
    IReadOnlyList<TradeInviteView> IncomingInvites,
    string? WaitingForPlayer,
    TradeNegotiationView? ActiveNegotiation);

/// <summary>
/// Presentation-safe façade for the mutual-confirmation negotiation. Platform IDs remain opaque,
/// and only locally owned Voidling IDs can be submitted as the player's offered slot. Remote visual
/// metadata is a presentation-only preview and never participates in durable ownership validation.
/// </summary>
public sealed class TradeNegotiationFacade
{
    private readonly MultiplayerConnectionService _connection;
    private readonly TradeNegotiationCoordinator _negotiation;
    private readonly TradeOfferPreviewCoordinator _previews;
    private readonly Func<GameStateData> _stateProvider;
    private readonly Dictionary<PlatformUserId, string> _partnerKeys = new();
    private readonly Dictionary<string, TradeNegotiationPhase> _observedPhases = new(StringComparer.Ordinal);

    public TradeNegotiationFacade(
        MultiplayerConnectionService connection,
        TradeNegotiationCoordinator negotiation,
        Func<GameStateData> stateProvider)
        : this(
            connection,
            negotiation,
            new TradeOfferPreviewCoordinator(connection, negotiation),
            stateProvider)
    {
    }

    public TradeNegotiationFacade(
        MultiplayerConnectionService connection,
        TradeNegotiationCoordinator negotiation,
        TradeOfferPreviewCoordinator previews,
        Func<GameStateData> stateProvider)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _negotiation = negotiation ?? throw new ArgumentNullException(nameof(negotiation));
        _previews = previews ?? throw new ArgumentNullException(nameof(previews));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));

        _connection.LobbyChanged += _ =>
        {
            ReconcilePartnerKeys();
            RaiseStateChanged();
        };
        _connection.LobbyLeft += Reset;
        _negotiation.NegotiationChanged += HandleNegotiationChanged;
        _negotiation.IncomingInvite += HandleIncomingInvite;
        _previews.PreviewChanged += _ => RaiseStateChanged();
    }

    public event Action<TradeLobbyViewState>? StateChanged;
    public event Action<TradeInviteView>? IncomingInviteReceived;
    public event Action<TradeNegotiationView>? NegotiationActivated;

    public TradeLobbyViewState Current => BuildState();

    public TradeNegotiationOperationResult Invite(string partnerKey)
    {
        if (!TryResolvePartner(partnerKey, out var partner))
            return TradeNegotiationOperationResult.Failed("Selected trade partner is no longer in the connected Garden.");
        return _negotiation.Invite(partner);
    }

    public TradeNegotiationOperationResult AcceptInvite(string negotiationId)
        => _negotiation.AcceptInvite(negotiationId);

    public TradeNegotiationOperationResult DeclineInvite(string negotiationId)
        => _negotiation.Cancel(negotiationId);

    public TradeNegotiationOperationResult Cancel(string negotiationId)
        => _negotiation.Cancel(negotiationId);

    public TradeNegotiationOperationResult SelectVoidling(string negotiationId, string? assetId)
    {
        var choices = BuildLocalVoidlings();
        TradeVoidlingChoiceView? selected = null;
        if (assetId != null)
        {
            selected = choices.FirstOrDefault(voidling =>
                string.Equals(voidling.AssetId, assetId, StringComparison.Ordinal));
            if (selected == null)
                return TradeNegotiationOperationResult.Failed("That Voidling is no longer in your Garden.");
        }

        var result = _negotiation.SelectVoidling(negotiationId, assetId);
        if (!result.Success)
            return result;

        var preview = selected == null
            ? null
            : new TradeVoidlingOfferPreview(
                selected.AssetId,
                selected.DisplayName,
                selected.TintHex,
                selected.HasAngelMutation,
                selected.OtherMutationCount,
                selected.VisualTypeId,
                selected.PaletteHue,
                selected.LayerIdsKey);
        if (!_previews.Publish(negotiationId, preview))
        {
            return TradeNegotiationOperationResult.Failed(
                "The Voidling was selected, but its trade-room preview could not be synchronized. Select it again to retry.");
        }

        return result;
    }

    public TradeNegotiationOperationResult SetAccepted(string negotiationId, bool accepted)
        => _negotiation.SetAccepted(negotiationId, accepted);

    private TradeLobbyViewState BuildState()
    {
        var availability = _connection.IsAvailable
            ? MultiplayerAvailability.Available
            : MultiplayerAvailability.Unavailable(_connection.UnavailableReason ?? "Multiplayer is unavailable.");
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        var choices = BuildLocalVoidlings();
        if (local == null || lobby == null)
        {
            return new TradeLobbyViewState(
                availability,
                false,
                false,
                Array.Empty<TradePartnerView>(),
                choices,
                Array.Empty<TradeInviteView>(),
                null,
                null);
        }

        ReconcilePartnerKeys();
        var partners = lobby.Members
            .Where(member => member.User.Id != local.Id)
            .OrderBy(member => member.User.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.User.Id.Value)
            .Select(member => new TradePartnerView(
                _partnerKeys[member.User.Id],
                DisplayName(member.User.Id)))
            .ToArray();

        var participantStates = _negotiation.States
            .Where(state => state.IsParticipant(local.Id))
            .OrderByDescending(state => state.Revision)
            .ToArray();
        var active = participantStates.FirstOrDefault(state =>
            state.Phase is TradeNegotiationPhase.Negotiating or TradeNegotiationPhase.Finalizing);
        var incoming = participantStates
            .Where(state => state.Phase == TradeNegotiationPhase.Invited && state.CounterpartyId == local.Id)
            .Select(state => new TradeInviteView(state.NegotiationId, DisplayName(state.InitiatorId)))
            .ToArray();
        var waiting = participantStates.FirstOrDefault(state =>
            state.Phase == TradeNegotiationPhase.Invited && state.InitiatorId == local.Id);
        var hasBlocking = participantStates.Any(state =>
            state.Phase is TradeNegotiationPhase.Invited or TradeNegotiationPhase.Negotiating or TradeNegotiationPhase.Finalizing);

        return new TradeLobbyViewState(
            availability,
            true,
            partners.Length > 0 && !hasBlocking,
            partners,
            choices,
            incoming,
            waiting == null ? null : DisplayName(waiting.CounterpartyId),
            active == null ? null : MapNegotiation(active, choices));
    }

    private TradeNegotiationView MapNegotiation(
        TradeNegotiationState state,
        IReadOnlyList<TradeVoidlingChoiceView>? choices = null)
    {
        var local = _connection.LocalUser;
        if (local == null)
            throw new InvalidOperationException("Trade negotiation has no local player.");
        choices ??= BuildLocalVoidlings();
        var localIsInitiator = local.Id == state.InitiatorId;
        var localAsset = localIsInitiator ? state.InitiatorAsset : state.CounterpartyAsset;
        var remoteAsset = localIsInitiator ? state.CounterpartyAsset : state.InitiatorAsset;
        var localAccepted = localIsInitiator ? state.InitiatorAccepted : state.CounterpartyAccepted;
        var remoteAccepted = localIsInitiator ? state.CounterpartyAccepted : state.InitiatorAccepted;
        var partner = localIsInitiator ? state.CounterpartyId : state.InitiatorId;
        var localOffer = localAsset == null
            ? null
            : choices.FirstOrDefault(choice => string.Equals(choice.AssetId, localAsset.AssetId, StringComparison.Ordinal));

        if (localAsset != null && localOffer == null)
        {
            var localPreview = _previews.GetPreview(state.NegotiationId, local.Id);
            if (localPreview != null &&
                string.Equals(localPreview.AssetId, localAsset.AssetId, StringComparison.Ordinal))
            {
                localOffer = FromPreview(localPreview);
            }
        }

        TradeVoidlingChoiceView? remoteOffer = null;
        var preview = _previews.GetPreview(state.NegotiationId, partner);
        if (remoteAsset != null && preview != null &&
            string.Equals(remoteAsset.AssetId, preview.AssetId, StringComparison.Ordinal))
        {
            remoteOffer = FromPreview(preview);
        }

        return new TradeNegotiationView(
            state.NegotiationId,
            state.Phase,
            DisplayName(partner),
            localOffer,
            remoteOffer,
            remoteAsset?.AssetId,
            localAccepted,
            remoteAccepted,
            state.Phase == TradeNegotiationPhase.Negotiating && !localAccepted,
            state.Phase == TradeNegotiationPhase.Negotiating && localOffer != null,
            state.Phase is TradeNegotiationPhase.Invited or TradeNegotiationPhase.Negotiating,
            state.Message);
    }

    private TradeVoidlingChoiceView[] BuildLocalVoidlings()
        => _stateProvider().Voidlings
            .OrderBy(voidling => voidling.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(voidling => voidling.Id, StringComparer.Ordinal)
            .Select(voidling =>
            {
                var hasAngel = voidling.RareTraits.Any(trait =>
                    string.Equals(trait.TraitId, "Angel", StringComparison.OrdinalIgnoreCase));
                var appearance = voidling.Appearance ?? new VoidlingAppearanceData();
                appearance.Normalize();
                return new TradeVoidlingChoiceView(
                    voidling.Id,
                    voidling.Name,
                    voidling.TintHex,
                    hasAngel,
                    Math.Max(0, voidling.RareTraits.Count - (hasAngel ? 1 : 0)),
                    appearance.VisualTypeId,
                    appearance.PaletteHue,
                    VoidlingAppearanceData.BuildLayerIdsKey(appearance.LayerIds));
            })
            .ToArray();

    private static TradeVoidlingChoiceView FromPreview(TradeVoidlingOfferPreview preview)
        => new(
            preview.AssetId,
            preview.DisplayName,
            preview.TintHex,
            preview.HasAngelMutation,
            preview.OtherMutationCount,
            preview.VisualTypeId,
            preview.PaletteHue,
            preview.LayerIdsKey);

    private void HandleNegotiationChanged(TradeNegotiationState state)
    {
        var local = _connection.LocalUser;
        if (local == null || !state.IsParticipant(local.Id))
            return;

        var previous = _observedPhases.GetValueOrDefault(state.NegotiationId, (TradeNegotiationPhase)(-1));
        _observedPhases[state.NegotiationId] = state.Phase;
        RaiseStateChanged();
        if (state.Phase == TradeNegotiationPhase.Negotiating && previous != TradeNegotiationPhase.Negotiating)
            NegotiationActivated?.Invoke(MapNegotiation(state));
    }

    private void HandleIncomingInvite(TradeNegotiationState state)
    {
        IncomingInviteReceived?.Invoke(new TradeInviteView(
            state.NegotiationId,
            DisplayName(state.InitiatorId)));
        RaiseStateChanged();
    }

    private string DisplayName(PlatformUserId id)
    {
        var member = _connection.CurrentLobby?.Members.FirstOrDefault(candidate => candidate.User.Id == id);
        return member == null || string.IsNullOrWhiteSpace(member.User.DisplayName)
            ? "Connected player"
            : member.User.DisplayName;
    }

    private bool TryResolvePartner(string key, out PlatformUserId partner)
    {
        partner = default;
        if (string.IsNullOrWhiteSpace(key))
            return false;
        ReconcilePartnerKeys();
        foreach (var pair in _partnerKeys)
        {
            if (string.Equals(pair.Value, key, StringComparison.Ordinal))
            {
                partner = pair.Key;
                return true;
            }
        }
        return false;
    }

    private void ReconcilePartnerKeys()
    {
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null)
        {
            _partnerKeys.Clear();
            return;
        }

        var current = lobby.Members
            .Where(member => member.User.Id != local.Id)
            .Select(member => member.User.Id)
            .ToHashSet();
        foreach (var stale in _partnerKeys.Keys.Where(id => !current.Contains(id)).ToArray())
            _partnerKeys.Remove(stale);
        foreach (var id in current)
        {
            if (!_partnerKeys.ContainsKey(id))
                _partnerKeys[id] = Guid.NewGuid().ToString("N");
        }
    }

    private void RaiseStateChanged()
        => StateChanged?.Invoke(BuildState());

    private void Reset()
    {
        _partnerKeys.Clear();
        _observedPhases.Clear();
        RaiseStateChanged();
    }
}
