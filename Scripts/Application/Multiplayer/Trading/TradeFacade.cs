using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Trading;

public sealed record TradeCounterpartyView(
    string Key,
    string DisplayName);

public sealed record TradeLocalAssetView(
    TradeAssetKind Kind,
    string AssetId,
    string DisplayName);

public sealed record TradeIncomingOfferView(
    string TradeId,
    string InitiatorDisplayName,
    int VoidlingCount,
    int EggCount);

public sealed record TradeStatusView(
    string TradeId,
    TradeSessionStatus Status,
    string Message);

public sealed record TradeHubViewState(
    MultiplayerAvailability Availability,
    bool IsConnected,
    bool CanOffer,
    IReadOnlyList<TradeCounterpartyView> Counterparties,
    IReadOnlyList<TradeLocalAssetView> LocalAssets,
    IReadOnlyList<TradeIncomingOfferView> IncomingOffers,
    IReadOnlyList<TradeStatusView> RecentStatuses);

/// <summary>
/// Presentation-safe trade façade. The UI can select local asset references and opaque lobby-member
/// keys, but platform IDs, transport messages, transfer bundles and prepare/commit journals stay below
/// this boundary. TradeNetworkCoordinator remains the only owner of the durable transfer protocol.
/// </summary>
public sealed class TradeFacade
{
    private const int RecentStatusLimit = 8;

    private readonly MultiplayerConnectionService _connection;
    private readonly TradeNetworkCoordinator _coordinator;
    private readonly Func<GameStateData> _stateProvider;
    private readonly Dictionary<PlatformUserId, string> _counterpartyKeys = new();
    private readonly Dictionary<string, TradeOfferNotice> _incomingOffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TradeStatusUpdate> _statuses = new(StringComparer.Ordinal);
    private readonly List<string> _statusOrder = new();

    public TradeFacade(
        MultiplayerConnectionService connection,
        TradeNetworkCoordinator coordinator,
        Func<GameStateData> stateProvider)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));

        _coordinator.TradeOfferReceived += HandleOfferReceived;
        _coordinator.TradeStatusChanged += HandleStatusChanged;
        _coordinator.LocalStateChanged += RaiseStateChanged;
        _connection.LobbyChanged += _ =>
        {
            ReconcileCounterpartyKeys();
            RaiseStateChanged();
        };
        _connection.LobbyLeft += Reset;
    }

    public event Action<TradeHubViewState>? StateChanged;
    public event Action<TradeIncomingOfferView>? IncomingOfferReceived;

    public TradeHubViewState Current => BuildState();

    public TradeNetworkOperationResult Offer(
        string counterpartyKey,
        IReadOnlyCollection<TradeAssetReference> assets)
    {
        if (!TryResolveCounterparty(counterpartyKey, out var counterparty))
            return TradeNetworkOperationResult.Failed("Selected trade partner is no longer in the connected Garden.");

        var selected = assets?.ToArray() ?? Array.Empty<TradeAssetReference>();
        if (selected.Length == 0)
            return TradeNetworkOperationResult.Failed("Select at least one Voidling or egg to offer.");

        var result = _coordinator.OfferTrade(counterparty, selected);
        if (result.Success && !string.IsNullOrWhiteSpace(result.TradeId))
        {
            RememberStatus(new TradeStatusUpdate(
                result.TradeId!,
                TradeSessionStatus.Offered,
                "Trade offer sent."));
            RaiseStateChanged();
        }
        return result;
    }

    public TradeNetworkOperationResult Accept(
        string tradeId,
        IReadOnlyCollection<TradeAssetReference> assets)
    {
        var result = _coordinator.AcceptTrade(
            tradeId,
            assets?.ToArray() ?? Array.Empty<TradeAssetReference>());
        if (result.Success)
            RaiseStateChanged();
        return result;
    }

    public TradeNetworkOperationResult Decline(string tradeId)
    {
        var result = _coordinator.DeclineTrade(tradeId);
        if (result.Success)
            RaiseStateChanged();
        return result;
    }

    public TradeIncomingOfferView? GetIncomingOffer(string tradeId)
    {
        if (!_incomingOffers.TryGetValue(tradeId, out var offer))
            return null;
        return MapIncomingOffer(offer);
    }

    private TradeHubViewState BuildState()
    {
        var availability = _connection.IsAvailable
            ? MultiplayerAvailability.Available
            : MultiplayerAvailability.Unavailable(
                _connection.UnavailableReason ?? "Multiplayer is unavailable.");
        var local = _connection.LocalUser;
        var lobby = _connection.CurrentLobby;
        if (local == null || lobby == null)
        {
            return new TradeHubViewState(
                availability,
                IsConnected: false,
                CanOffer: false,
                Array.Empty<TradeCounterpartyView>(),
                BuildLocalAssets(),
                Array.Empty<TradeIncomingOfferView>(),
                BuildRecentStatuses());
        }

        ReconcileCounterpartyKeys();
        var counterparties = lobby.Members
            .Where(member => member.User.Id != local.Id)
            .OrderBy(member => member.User.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.User.Id.Value)
            .Select(member => new TradeCounterpartyView(
                _counterpartyKeys[member.User.Id],
                string.IsNullOrWhiteSpace(member.User.DisplayName)
                    ? "Connected friend"
                    : member.User.DisplayName))
            .ToArray();
        var incoming = _incomingOffers.Values
            .Where(offer => offer.CounterpartyId == local.Id)
            .OrderBy(offer => offer.TradeId, StringComparer.Ordinal)
            .Select(MapIncomingOffer)
            .ToArray();
        var hasActiveTrade = _statuses.Values.Any(status => IsActive(status.Status));

        return new TradeHubViewState(
            availability,
            IsConnected: true,
            CanOffer: counterparties.Length > 0 && !hasActiveTrade,
            counterparties,
            BuildLocalAssets(),
            incoming,
            BuildRecentStatuses());
    }

    private TradeLocalAssetView[] BuildLocalAssets()
    {
        var state = _stateProvider();
        var voidlings = state.Voidlings
            .OrderBy(voidling => voidling.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(voidling => voidling.Id, StringComparer.Ordinal)
            .Select(voidling => new TradeLocalAssetView(
                TradeAssetKind.Voidling,
                voidling.Id,
                voidling.Name))
            .ToList();

        for (var i = 0; i < state.OwnedEggs.Count; i++)
        {
            var egg = state.OwnedEggs[i];
            voidlings.Add(new TradeLocalAssetView(
                TradeAssetKind.Egg,
                egg.Id,
                $"Egg {i + 1} • {egg.Source}"));
        }

        return voidlings.ToArray();
    }

    private TradeStatusView[] BuildRecentStatuses()
        => _statusOrder
            .AsEnumerable()
            .Reverse()
            .Where(_statuses.ContainsKey)
            .Take(RecentStatusLimit)
            .Select(tradeId =>
            {
                var update = _statuses[tradeId];
                return new TradeStatusView(
                    tradeId,
                    update.Status,
                    string.IsNullOrWhiteSpace(update.Message)
                        ? StatusText(update.Status)
                        : update.Message!);
            })
            .ToArray();

    private TradeIncomingOfferView MapIncomingOffer(TradeOfferNotice offer)
    {
        var names = _connection.CurrentLobby?.Members.ToDictionary(
            member => member.User.Id,
            member => member.User.DisplayName) ?? new Dictionary<PlatformUserId, string>();
        var initiatorName = names.TryGetValue(offer.InitiatorId, out var displayName) &&
                            !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : "Connected friend";
        var assets = offer.InitiatorAssets ?? Array.Empty<TradeAssetReference>();
        return new TradeIncomingOfferView(
            offer.TradeId,
            initiatorName,
            assets.Count(asset => asset.Kind == TradeAssetKind.Voidling),
            assets.Count(asset => asset.Kind == TradeAssetKind.Egg));
    }

    private bool TryResolveCounterparty(string key, out PlatformUserId counterparty)
    {
        counterparty = default;
        if (string.IsNullOrWhiteSpace(key))
            return false;
        var lobby = _connection.CurrentLobby;
        var local = _connection.LocalUser;
        if (lobby == null || local == null)
            return false;

        ReconcileCounterpartyKeys();
        foreach (var member in lobby.Members)
        {
            if (member.User.Id == local.Id)
                continue;
            if (_counterpartyKeys.TryGetValue(member.User.Id, out var candidate) &&
                string.Equals(candidate, key, StringComparison.Ordinal))
            {
                counterparty = member.User.Id;
                return true;
            }
        }
        return false;
    }

    private void ReconcileCounterpartyKeys()
    {
        var lobby = _connection.CurrentLobby;
        var local = _connection.LocalUser;
        if (lobby == null || local == null)
        {
            _counterpartyKeys.Clear();
            return;
        }

        var current = lobby.Members
            .Where(member => member.User.Id != local.Id)
            .Select(member => member.User.Id)
            .ToHashSet();
        foreach (var stale in _counterpartyKeys.Keys.Where(id => !current.Contains(id)).ToArray())
            _counterpartyKeys.Remove(stale);
        foreach (var id in current)
        {
            if (!_counterpartyKeys.ContainsKey(id))
                _counterpartyKeys[id] = Guid.NewGuid().ToString("N");
        }
    }

    private void HandleOfferReceived(TradeOfferNotice offer)
    {
        _incomingOffers[offer.TradeId] = offer;
        RememberStatus(new TradeStatusUpdate(offer.TradeId, TradeSessionStatus.Offered, "Trade offer received."));
        var view = MapIncomingOffer(offer);
        IncomingOfferReceived?.Invoke(view);
        RaiseStateChanged();
    }

    private void HandleStatusChanged(TradeStatusUpdate update)
    {
        RememberStatus(update);
        if (update.Status != TradeSessionStatus.Offered)
            _incomingOffers.Remove(update.TradeId);
        RaiseStateChanged();
    }

    private void RememberStatus(TradeStatusUpdate update)
    {
        if (!_statuses.ContainsKey(update.TradeId))
            _statusOrder.Add(update.TradeId);
        _statuses[update.TradeId] = update;
        while (_statusOrder.Count > RecentStatusLimit)
        {
            var oldest = _statusOrder[0];
            _statusOrder.RemoveAt(0);
            _statuses.Remove(oldest);
            _incomingOffers.Remove(oldest);
        }
    }

    private static bool IsActive(TradeSessionStatus status)
        => status is TradeSessionStatus.Offered
            or TradeSessionStatus.PreparingBundles
            or TradeSessionStatus.PersistingPrepare
            or TradeSessionStatus.ReadyToCommit
            or TradeSessionStatus.Committing;

    private static string StatusText(TradeSessionStatus status)
        => status switch
        {
            TradeSessionStatus.Offered => "Trade offer pending.",
            TradeSessionStatus.PreparingBundles => "Preparing trade assets.",
            TradeSessionStatus.PersistingPrepare => "Saving trade preparation.",
            TradeSessionStatus.ReadyToCommit => "Trade is ready to commit.",
            TradeSessionStatus.Committing => "Committing trade.",
            TradeSessionStatus.Completed => "Trade completed.",
            TradeSessionStatus.Declined => "Trade declined.",
            TradeSessionStatus.Aborted => "Trade aborted.",
            TradeSessionStatus.Failed => "Trade failed.",
            _ => status.ToString()
        };

    private void RaiseStateChanged()
        => StateChanged?.Invoke(BuildState());

    private void Reset()
    {
        _counterpartyKeys.Clear();
        _incomingOffers.Clear();
        _statuses.Clear();
        _statusOrder.Clear();
        RaiseStateChanged();
    }
}
