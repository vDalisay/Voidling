using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Application.Ports;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class TradeFacadeTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void CurrentProjectsOpaquePartnerKeyAndOwnedTransferableAssets()
    {
        var host = User(1, "Host Friend");
        var client = User(2, "Client");
        var lobby = Lobby(host, host, client);
        var state = new GameStateData();
        state.Voidlings.Add(CreateAdult("sprout", "Sprout", 101));
        state.OwnedEggs.Add(new EggData
        {
            Id = "egg-1",
            Source = EggSource.Bred,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(202),
            RequiredIncubationSeconds = 100
        });
        var facade = CreateFacade(client, lobby, state, new FakeTransport());

        var current = facade.Current;

        Assert.True(current.IsConnected);
        Assert.True(current.CanOffer);
        var partner = Assert.Single(current.Counterparties);
        Assert.Equal("Host Friend", partner.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(partner.Key));
        Assert.NotEqual(host.Id.Value.ToString(), partner.Key);
        Assert.Contains(current.LocalAssets, asset =>
            asset.Kind == TradeAssetKind.Voidling && asset.AssetId == "sprout" && asset.DisplayName == "Sprout");
        Assert.Contains(current.LocalAssets, asset =>
            asset.Kind == TradeAssetKind.Egg && asset.AssetId == "egg-1");
    }

    [Fact]
    public void OfferResolvesOpaqueKeyToRealLobbyMemberWithoutExposingIdToPresentation()
    {
        var host = User(10, "Host");
        var client = User(20, "Client");
        var lobby = Lobby(host, host, client);
        var state = new GameStateData();
        state.Voidlings.Add(CreateAdult("local", "Local", 303));
        var transport = new FakeTransport();
        var facade = CreateFacade(client, lobby, state, transport);
        var partner = Assert.Single(facade.Current.Counterparties);

        var result = facade.Offer(
            partner.Key,
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, "local") });

        Assert.True(result.Success, result.Error);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal(host.Id, sent.Peer);
        Assert.Equal(NetworkChannel.Trade, sent.Channel);
        Assert.True(TradeProtocol.TryDecodeOfferCommand(
            sent.Payload.Span,
            client.Id,
            out _,
            out var tradeId,
            out var lobbyId,
            out var counterpartyId,
            out var assets));
        Assert.Equal(result.TradeId, tradeId);
        Assert.Equal(lobby.LobbyId, lobbyId);
        Assert.Equal(host.Id, counterpartyId);
        var asset = Assert.Single(assets);
        Assert.Equal(TradeAssetKind.Voidling, asset.Kind);
        Assert.Equal("local", asset.AssetId);
    }

    [Fact]
    public void IncomingOfferProjectsFriendNameAndCountsAndCanBeAcceptedAsGift()
    {
        var host = User(100, "Host Friend");
        var client = User(200, "Client");
        var lobby = Lobby(host, host, client);
        var state = new GameStateData();
        var transport = new FakeTransport();
        var facade = CreateFacade(client, lobby, state, transport);
        TradeIncomingOfferView? notified = null;
        facade.IncomingOfferReceived += offer => notified = offer;
        var tradeId = Guid.NewGuid().ToString("N");
        var notice = new TradeOfferNotice(
            tradeId,
            host.Id,
            client.Id,
            new[]
            {
                new TradeAssetReference(TradeAssetKind.Voidling, "remote-a"),
                new TradeAssetReference(TradeAssetKind.Voidling, "remote-b"),
                new TradeAssetReference(TradeAssetKind.Egg, "remote-egg")
            });

        transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Trade,
            TradeProtocol.EncodeOffered(host, notice)));

        Assert.NotNull(notified);
        Assert.Equal("Host Friend", notified!.InitiatorDisplayName);
        Assert.Equal(2, notified.VoidlingCount);
        Assert.Equal(1, notified.EggCount);
        Assert.Equal(3, notified.Assets.Count);
        Assert.Contains(notified.Assets, asset =>
            asset.Kind == TradeAssetKind.Voidling && asset.AssetId == "remote-a");
        var incoming = Assert.Single(facade.Current.IncomingOffers);
        Assert.Equal(tradeId, incoming.TradeId);

        transport.Sent.Clear();
        var accepted = facade.Accept(tradeId, Array.Empty<TradeAssetReference>());

        Assert.True(accepted.Success, accepted.Error);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal(host.Id, sent.Peer);
        Assert.True(TradeProtocol.TryDecodeAcceptCommand(
            sent.Payload.Span,
            client.Id,
            out _,
            out var acceptedTradeId,
            out var returnAssets));
        Assert.Equal(tradeId, acceptedTradeId);
        Assert.Empty(returnAssets);
    }

    private static TradeFacade CreateFacade(
        PlatformUser local,
        LobbySnapshot lobby,
        GameStateData state,
        FakeTransport transport)
    {
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(local),
            new FakeLobby(lobby),
            transport);
        var coordinator = new TradeNetworkCoordinator(
            connection,
            new TradeTransferService(Rules),
            new FakeRepository(),
            () => state);
        return new TradeFacade(connection, coordinator, () => state);
    }

    private static VoidlingData CreateAdult(string id, string name, ulong seed)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = name,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            TintHex = "#ABCDEF"
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }

    private static PlatformUser User(ulong id, string name)
        => new(new PlatformUserId(id), name);

    private static LobbySnapshot Lobby(PlatformUser owner, params PlatformUser[] members)
        => new(
            77,
            owner.Id,
            members.Select(user => new LobbyMember(user, user.Id == owner.Id)).ToArray());

    private sealed class FakeIdentity : IPlatformIdentityService
    {
        public FakeIdentity(PlatformUser local) => LocalUser = local;
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public PlatformUser? LocalUser { get; }
    }

    private sealed class FakeLobby : ILobbyService
    {
        public FakeLobby(LobbySnapshot lobby) => CurrentLobby = lobby;
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public LobbySnapshot? CurrentLobby { get; private set; }
        public event Action<LobbySnapshot>? LobbyChanged;
        public event Action<LobbyJoinRequest>? JoinRequested;

        public Task<LobbyOperationResult> CreateFriendsLobbyAsync(
            int maxMembers,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Succeeded(CurrentLobby!));

        public Task<LobbyOperationResult> JoinAsync(
            ulong lobbyId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LobbyOperationResult.Succeeded(CurrentLobby!));

        public Task LeaveAsync(CancellationToken cancellationToken = default)
        {
            CurrentLobby = null;
            return Task.CompletedTask;
        }

        public void OpenInviteOverlay() { }
    }

    private sealed record SentMessage(
        PlatformUserId Peer,
        NetworkChannel Channel,
        ReadOnlyMemory<byte> Payload,
        DeliveryMode Delivery);

    private sealed class FakeTransport : IMultiplayerTransport
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public List<SentMessage> Sent { get; } = new();
        public event Action<NetworkPacket>? PacketReceived;
        public event Action<PlatformUserId>? PeerSessionFailed;

        public bool TrySend(
            PlatformUserId peer,
            NetworkChannel channel,
            ReadOnlyMemory<byte> payload,
            DeliveryMode delivery)
        {
            Sent.Add(new SentMessage(peer, channel, payload, delivery));
            return true;
        }

        public void Poll() { }
        public void Close(PlatformUserId peer) { }
        public void Emit(NetworkPacket packet) => PacketReceived?.Invoke(packet);
    }

    private sealed class FakeRepository : IGameStateRepository
    {
        public GameStateData? Load() => null;
        public void Save(GameStateData state) { }
    }
}
