using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Genetics;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class MultiplayerRaceStartCoordinatorTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void HostStartsOnlyAfterEveryParticipantAcknowledgesExactPayload()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var lobby = Lobby(host, host, remote);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            new FakeLobby(lobby),
            transport);
        var challenges = new ChallengeCoordinator(connection);
        var races = new MultiplayerRaceStartCoordinator(connection, challenges, Rules);

        var offered = challenges.OfferChallenge(ChallengeKind.Race, 2);
        Assert.True(offered.Success, offered.Error);
        Join(transport, remote, offered.ChallengeId!);

        var hostState = StateWith(CreateAdult("host-creature", 101UL));
        var remoteState = StateWith(CreateAdult("remote-creature", 202UL));
        Assert.True(races.SubmitSelection(hostState, offered.ChallengeId!, "host-creature").Success);

        var selections = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(selections.TryCreate(
            remoteState,
            remote.Id,
            "remote-creature",
            out var remoteEntrant,
            out var selectionError), selectionError);
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceProtocol.EncodeSelection(remote, offered.ChallengeId!, remoteEntrant)));

        ResolvedMultiplayerRace? launched = null;
        races.RaceReadyToLaunch += race => launched = race;
        transport.Sent.Clear();

        var requested = races.RequestStart(offered.ChallengeId!);

        Assert.True(requested.Success, requested.Error);
        Assert.Equal(ChallengePhase.Ready, Assert.Single(challenges.Challenges).Phase);
        var proposal = Assert.Single(transport.Sent.Where(message =>
            message.Peer == remote.Id &&
            MultiplayerRaceProtocol.TryDecodeStartProposal(
                message.Payload.Span,
                host.Id,
                out _,
                out _,
                out _,
                out _)));
        Assert.True(MultiplayerRaceProtocol.TryDecodeStartProposal(
            proposal.Payload.Span,
            host.Id,
            out _,
            out var challengeId,
            out var startHash,
            out var startBytes));
        Assert.Equal(offered.ChallengeId, challengeId);
        Assert.Equal(MultiplayerRaceStartCodec.ComputeHash(startBytes), startHash);
        Assert.Null(launched);

        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceProtocol.EncodeStartAck(
                remote,
                challengeId,
                startHash,
                true,
                null)));

        var running = Assert.Single(challenges.Challenges);
        Assert.Equal(ChallengePhase.Running, running.Phase);
        Assert.Equal(startBytes, running.StartPayload);
        Assert.NotNull(launched);
        Assert.Equal(2, launched!.Entry.Entrants.Count);
        Assert.Equal(startHash, MultiplayerRaceStartCodec.ComputeHash(running.StartPayload));

        var first = CreateSimulation(launched);
        var second = CreateSimulation(launched);
        first.FastForwardToFinish();
        second.FastForwardToFinish();
        Assert.Equal(first.FinishOrder, second.FinishOrder);
    }

    [Fact]
    public void ClientRejectsProposalWhenRaceRulesFingerprintDoesNotMatchLocalBuild()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var lobby = Lobby(host, host, client);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(client),
            new FakeLobby(lobby),
            transport);
        var challenges = new ChallengeCoordinator(connection);
        var races = new MultiplayerRaceStartCoordinator(connection, challenges, Rules);
        transport.Sent.Clear();

        var challengeId = Guid.NewGuid().ToString("N");
        var ready = new ChallengeSnapshot(
            challengeId,
            lobby.LobbyId,
            ChallengeKind.Race,
            host.Id,
            2,
            ChallengePhase.Ready,
            new[] { host.Id, client.Id },
            Array.Empty<byte>());
        transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeState(host, ready)));

        var selectionFactory = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(selectionFactory.TryCreate(
            StateWith(CreateAdult("host-creature", 303UL)),
            host.Id,
            "host-creature",
            out var hostEntrant,
            out _));
        Assert.True(selectionFactory.TryCreate(
            StateWith(CreateAdult("client-creature", 404UL)),
            client.Id,
            "client-creature",
            out var clientEntrant,
            out _));
        var startFactory = new MultiplayerRaceEntryFactory(Rules);
        var valid = startFactory.CreateStartPayload(challengeId, new[] { hostEntrant, clientEntrant });
        var tampered = valid with { RaceRulesHash = new string('0', 64) };
        var bytes = MultiplayerRaceStartCodec.Encode(tampered);
        var hash = MultiplayerRaceStartCodec.ComputeHash(bytes);

        string? challengeRejection = null;
        challenges.ProtocolRejected += reason => challengeRejection = reason;
        transport.Sent.Clear();
        transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceProtocol.EncodeStartProposal(host, challengeId, hash, bytes)));

        Assert.Null(challengeRejection);
        var ack = Assert.Single(transport.Sent);
        Assert.Equal(host.Id, ack.Peer);
        Assert.True(MultiplayerRaceProtocol.TryDecodeStartAck(
            ack.Payload.Span,
            client.Id,
            out _,
            out var ackChallengeId,
            out var ackHash,
            out var success,
            out var error));
        Assert.Equal(challengeId, ackChallengeId);
        Assert.Equal(hash, ackHash);
        Assert.False(success);
        Assert.Contains("rules", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostRejectsSelectionThatClaimsAnotherPlayersNamespace()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var lobby = Lobby(host, host, remote);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            new FakeLobby(lobby),
            transport);
        var challenges = new ChallengeCoordinator(connection);
        var races = new MultiplayerRaceStartCoordinator(connection, challenges, Rules);
        var offered = challenges.OfferChallenge(ChallengeKind.Race, 2);
        Join(transport, remote, offered.ChallengeId!);

        var selectionFactory = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(selectionFactory.TryCreate(
            StateWith(CreateAdult("forged", 505UL)),
            host.Id,
            "forged",
            out var forged,
            out _));
        var accepted = 0;
        races.SelectionAccepted += (_, _) => accepted++;

        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceProtocol.EncodeSelection(remote, offered.ChallengeId!, forged)));

        Assert.Equal(0, accepted);
        var start = races.RequestStart(offered.ChallengeId!);
        Assert.False(start.Success);
        Assert.Contains("select", start.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RaceFingerprintsAndSeedAreStableAndTamperDetectionRejectsDifferentRules()
    {
        var challengeId = Guid.NewGuid().ToString("N");
        Assert.Equal(
            StableRaceSeed.FromChallengeId(challengeId),
            StableRaceSeed.FromChallengeId(challengeId));
        Assert.Equal(
            RaceCourseFingerprint.Compute(RaceCourse.Demo),
            RaceCourseFingerprint.Compute(RaceCourse.Demo));
        Assert.Equal(
            RaceRulesFingerprint.Compute(Rules.Racing),
            RaceRulesFingerprint.Compute(Rules.Racing));

        var first = CreateEntrant(User(1, "One"), "one", 1UL);
        var second = CreateEntrant(User(2, "Two"), "two", 2UL);
        var factory = new MultiplayerRaceEntryFactory(Rules);
        var payload = factory.CreateStartPayload(challengeId, new[] { first, second });
        var changed = payload with { CourseHash = new string('0', 64) };

        Assert.False(factory.TryResolve(changed, out _, out var error));
        Assert.Contains("course", error!, StringComparison.OrdinalIgnoreCase);
    }

    private static MultiplayerRaceEntrant CreateEntrant(
        PlatformUser owner,
        string creatureId,
        ulong seed)
    {
        var factory = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(factory.TryCreate(
            StateWith(CreateAdult(creatureId, seed)),
            owner.Id,
            creatureId,
            out var entrant,
            out var error), error);
        return entrant;
    }

    private static RaceSimulation CreateSimulation(ResolvedMultiplayerRace race)
        => new(
            race.Course,
            race.Entry.Rules,
            race.Entry.Entrants.Select(entrant => entrant.Participant).ToArray(),
            race.Entry.SimulationSeed);

    private static GameStateData StateWith(VoidlingData creature)
    {
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        return state;
    }

    private static VoidlingData CreateAdult(string id, ulong seed)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            TintHex = "#ABCDEF"
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }

    private static void Join(FakeTransport transport, PlatformUser user, string challengeId)
        => transport.Emit(new NetworkPacket(
            user.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeJoinCommand(user, challengeId)));

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
}
