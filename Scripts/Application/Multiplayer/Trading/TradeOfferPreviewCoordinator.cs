using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Trading;

/// <summary>
/// Presentation-only snapshot for the Voidling currently placed into a trading-room slot. The
/// authoritative durable trade still uses TradeAssetReference; none of these cosmetic fields are
/// trusted for ownership, lineage, persistence, or commit validation.
/// </summary>
public sealed record TradeVoidlingOfferPreview(
    string AssetId,
    string DisplayName,
    string TintHex,
    bool HasAngelMutation,
    int OtherMutationCount,
    string VisualTypeId = VoidlingAppearanceData.DefaultVisualTypeId,
    float PaletteHue = VoidlingAppearanceData.LegacyUninitializedPaletteHue,
    string LayerIdsKey = "");

public sealed class TradeOfferPreviewCoordinator
{
    private const int RecentMessageLimit = 256;
    private const int MaxLayerIdsKeyLength = 1024;

    private readonly MultiplayerConnectionService _connection;
    private readonly TradeNegotiationCoordinator _negotiation;
    private readonly Dictionary<(string NegotiationId, PlatformUserId OwnerId), PreviewState> _previews = new();
    private readonly Dictionary<(string NegotiationId, PlatformUserId OwnerId), int> _hostRevisions = new();
    private readonly Queue<Guid> _recentMessageOrder = new();
    private readonly HashSet<Guid> _recentMessageIds = new();

    private sealed record PreviewState(TradeVoidlingOfferPreview? Preview, int Revision);

    public TradeOfferPreviewCoordinator(
        MultiplayerConnectionService connection,
        TradeNegotiationCoordinator negotiation)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _negotiation = negotiation ?? throw new ArgumentNullException(nameof(negotiation));
        _connection.PacketReceived += HandlePacket;
        _connection.LobbyLeft += Reset;
        _negotiation.NegotiationChanged += HandleNegotiationChanged;
    }

    public event Action<string>? PreviewChanged;

    public TradeVoidlingOfferPreview? GetPreview(string negotiationId, PlatformUserId ownerId)
        => _previews.TryGetValue((negotiationId, ownerId), out var state) ? state.Preview : null;

    public bool Publish(string negotiationId, TradeVoidlingOfferPreview? preview)
    {
        var local = _connection.LocalUser;
        var state = _negotiation.Get(negotiationId);
        if (local == null || state == null || state.Phase != TradeNegotiationPhase.Negotiating ||
            !state.IsParticipant(local.Id) || !IsValidPreview(preview))
        {
            return false;
        }

        var selectedAsset = state.AssetFor(local.Id);
        if (preview != null &&
            (selectedAsset == null ||
             !string.Equals(selectedAsset.AssetId, preview.AssetId, StringComparison.Ordinal)))
        {
            if (_connection.IsLocalHost)
                return false;
        }

        if (_connection.IsLocalHost)
        {
            HandleHostPublish(local.Id, negotiationId, preview);
            return true;
        }

        var lobby = _connection.CurrentLobby;
        return lobby != null && _connection.TrySend(
            lobby.OwnerId,
            NetworkChannel.Session,
            PreviewWire.Encode(PreviewWire.Command(negotiationId, preview)),
            DeliveryMode.Reliable);
    }

    private void HandlePacket(NetworkPacket packet)
    {
        if (packet.Channel != NetworkChannel.Session ||
            !PreviewWire.TryDecode(packet.Payload.Span, out var message))
        {
            return;
        }

        if (_connection.IsLocalHost)
        {
            if (message.Type != PreviewWire.CommandType || !Remember(message.MessageId))
                return;
            HandleHostPublish(packet.Sender, message.NegotiationId, message.Preview);
            return;
        }

        var lobby = _connection.CurrentLobby;
        if (lobby == null || packet.Sender != lobby.OwnerId || message.Type != PreviewWire.StateType)
            return;
        ApplyState(
            message.NegotiationId,
            new PlatformUserId(message.OwnerId),
            message.Preview,
            message.Revision);
    }

    private void HandleHostPublish(
        PlatformUserId sender,
        string negotiationId,
        TradeVoidlingOfferPreview? preview)
    {
        var negotiation = _negotiation.Get(negotiationId);
        if (!_connection.IsLocalHost || negotiation == null ||
            negotiation.Phase != TradeNegotiationPhase.Negotiating ||
            !negotiation.IsParticipant(sender) || !IsValidPreview(preview))
        {
            return;
        }

        var selected = negotiation.AssetFor(sender);
        if (preview != null &&
            (selected == null ||
             !string.Equals(selected.AssetId, preview.AssetId, StringComparison.Ordinal)))
        {
            return;
        }

        var key = (negotiationId, sender);
        var revision = _hostRevisions.GetValueOrDefault(key) + 1;
        _hostRevisions[key] = revision;
        DispatchState(negotiation, sender, preview, revision);
    }

    private void DispatchState(
        TradeNegotiationState negotiation,
        PlatformUserId ownerId,
        TradeVoidlingOfferPreview? preview,
        int revision)
    {
        var local = _connection.LocalUser;
        if (local == null)
            return;

        foreach (var participant in new[] { negotiation.InitiatorId, negotiation.CounterpartyId })
        {
            if (participant == local.Id)
            {
                ApplyState(negotiation.NegotiationId, ownerId, preview, revision);
            }
            else
            {
                _connection.TrySend(
                    participant,
                    NetworkChannel.Session,
                    PreviewWire.Encode(PreviewWire.State(
                        negotiation.NegotiationId,
                        ownerId,
                        preview,
                        revision)),
                    DeliveryMode.Reliable);
            }
        }
    }

    private void ApplyState(
        string negotiationId,
        PlatformUserId ownerId,
        TradeVoidlingOfferPreview? preview,
        int revision)
    {
        var negotiation = _negotiation.Get(negotiationId);
        var local = _connection.LocalUser;
        if (local == null || negotiation == null ||
            negotiation.Phase != TradeNegotiationPhase.Negotiating ||
            !negotiation.IsParticipant(local.Id) || !negotiation.IsParticipant(ownerId) ||
            revision <= 0 || !IsValidPreview(preview))
        {
            return;
        }

        var selected = negotiation.AssetFor(ownerId);
        if (preview != null &&
            (selected == null ||
             !string.Equals(selected.AssetId, preview.AssetId, StringComparison.Ordinal)))
        {
            return;
        }

        var key = (negotiationId, ownerId);
        if (_previews.TryGetValue(key, out var current) && current.Revision >= revision)
            return;

        if (preview == null)
            _previews.Remove(key);
        else
            _previews[key] = new PreviewState(preview, revision);
        PreviewChanged?.Invoke(negotiationId);
    }

    private void HandleNegotiationChanged(TradeNegotiationState state)
    {
        if (state.Phase is TradeNegotiationPhase.Negotiating or TradeNegotiationPhase.Finalizing)
            return;

        var removed = _previews.Remove((state.NegotiationId, state.InitiatorId));
        removed |= _previews.Remove((state.NegotiationId, state.CounterpartyId));
        _hostRevisions.Remove((state.NegotiationId, state.InitiatorId));
        _hostRevisions.Remove((state.NegotiationId, state.CounterpartyId));
        if (removed)
            PreviewChanged?.Invoke(state.NegotiationId);
    }

    private bool Remember(Guid messageId)
    {
        if (messageId == Guid.Empty || !_recentMessageIds.Add(messageId))
            return false;
        _recentMessageOrder.Enqueue(messageId);
        while (_recentMessageOrder.Count > RecentMessageLimit)
            _recentMessageIds.Remove(_recentMessageOrder.Dequeue());
        return true;
    }

    private void Reset()
    {
        _previews.Clear();
        _hostRevisions.Clear();
        _recentMessageIds.Clear();
        _recentMessageOrder.Clear();
    }

    private static bool IsValidPreview(TradeVoidlingOfferPreview? preview)
    {
        if (preview == null)
            return true;
        if (string.IsNullOrWhiteSpace(preview.AssetId) || preview.AssetId.Length > 128 ||
            string.IsNullOrWhiteSpace(preview.DisplayName) || preview.DisplayName.Length > 64 ||
            string.IsNullOrWhiteSpace(preview.TintHex) || preview.TintHex.Length > 32 ||
            preview.OtherMutationCount is < 0 or > 64 ||
            !VoidlingAppearanceData.IsValidSemanticId(
                preview.VisualTypeId,
                VoidlingAppearanceData.MaxVisualTypeIdLength) ||
            !VoidlingAppearanceData.IsValidStoredHue(preview.PaletteHue) ||
            preview.LayerIdsKey == null || preview.LayerIdsKey.Length > MaxLayerIdsKeyLength)
        {
            return false;
        }

        var layerIds = VoidlingAppearanceData.ParseLayerIdsKey(preview.LayerIdsKey);
        return layerIds.Length <= VoidlingAppearanceData.MaxLayerCount &&
               layerIds.All(id => VoidlingAppearanceData.IsValidSemanticId(
                   id,
                   VoidlingAppearanceData.MaxLayerIdLength));
    }

    private sealed class PreviewWire
    {
        public const int CurrentVersion = 2;
        public const string CommandType = "trade.preview.publish";
        public const string StateType = "trade.preview.state";

        public int Version { get; init; } = CurrentVersion;
        public string Type { get; init; } = string.Empty;
        public Guid MessageId { get; init; } = Guid.NewGuid();
        public string NegotiationId { get; init; } = string.Empty;
        public ulong OwnerId { get; init; }
        public TradeVoidlingOfferPreview? Preview { get; init; }
        public int Revision { get; init; }

        public static PreviewWire Command(string negotiationId, TradeVoidlingOfferPreview? preview)
            => new() { Type = CommandType, NegotiationId = negotiationId, Preview = preview };

        public static PreviewWire State(
            string negotiationId,
            PlatformUserId ownerId,
            TradeVoidlingOfferPreview? preview,
            int revision)
            => new()
            {
                Type = StateType,
                NegotiationId = negotiationId,
                OwnerId = ownerId.Value,
                Preview = preview,
                Revision = revision
            };

        public static byte[] Encode(PreviewWire message)
            => JsonSerializer.SerializeToUtf8Bytes(message);

        public static bool TryDecode(ReadOnlySpan<byte> payload, out PreviewWire message)
        {
            message = null!;
            if (payload.Length is <= 0 or > MultiplayerProtocol.MaxPacketBytes)
                return false;
            try
            {
                var decoded = JsonSerializer.Deserialize<PreviewWire>(payload);
                if (decoded == null || decoded.Version != CurrentVersion || decoded.MessageId == Guid.Empty ||
                    decoded.Type is not (CommandType or StateType) ||
                    string.IsNullOrWhiteSpace(decoded.NegotiationId) || decoded.NegotiationId.Length > 64 ||
                    decoded.Revision < 0 || !IsValidPreview(decoded.Preview))
                {
                    return false;
                }
                message = decoded;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}
