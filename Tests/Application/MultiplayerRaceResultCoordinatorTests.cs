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
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class MultiplayerRaceResultCoordinatorTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void HostPublishesCanonicalResultAndCompletesOnlyAfterParticipantAck()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var fixture = CreateHostRace(host, remote);
        MultiplayerRaceResult? localResult = null;
        string? completedChallenge = null;
        fixture.Results.ValidatedResultReady += result => localResult = result;
        fixture.Results.ResultConsensusCompleted += challengeId => completedChallenge = challengeId;
        fixture.Transport.Sent.Clear();

        Finish(fixture.Lockstep, fixture.ChallengeId);

        Assert.NotNull(localResult);
        Assert.Equal(ChallengePhase.Running, Assert.Single(fixture.Challenges.Challenges).Phase);
        var resultPacket = Assert.Single(fixture.Transport.Sent.Where(message =>
            message.Peer == remote.Id &&
            MultiplayerRaceResultProtocol.TryDecodeFinalResult(
                message.Payload.Span,
                host.Id,
                out _,
                out _)));
        Assert.True(MultiplayerRaceResultProtocol.TryDecodeFinalResult(
            resultPacket.Payload.Span,
            host.Id,
            out _,
            out var result));
        Assert.Equal(fixture.ChallengeId, result.ChallengeId);
        Assert.Equal(localResult, result);

        fixture.Transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceResultProtocol.EncodeResultAck(
                remote,
                fixture.ChallengeId,
                true,
                null)));

        Assert.Equal(fixture.ChallengeId, completedChallenge);
        Assert.Equal(ChallengePhase.Completed, Assert.Single(fixture.Challenges.Challenges).Phase);
    }

    [Fact]
    public void ClientHoldsEarlyHostResultUntilLocalSimulationCompletesThenAcknowledges()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var fixture = CreateClientRace(host, client);
        var hostResult = BuildCompletedResult(fixture.Resolved);
        MultiplayerRaceResult? accepted = null;
        fixture.Results.ValidatedResultReady += result => accepted = result;
        fixture.Transport.Sent.Clear();

        fixture.Transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceResultProtocol.EncodeFinalResult(host, hostResult)));

        Assert.Null(accepted);
        Assert.DoesNotContain(fixture.Transport.Sent, message =>
            MultiplayerRaceResultProtocol.TryDecodeResultAck(
                message.Payload.Span,
                client.Id,
                out _,
                out _,
                out _,
                out _));

        Finish(fixture.Lockstep, fixture.ChallengeId);

        Assert.Equal(hostResult, accepted);
        var ack = Assert.Single(fixture.Transport.Sent.Where(message =>
            message.Peer == host.Id &&
            MultiplayerRaceResultProtocol.TryDecodeResultAck(
                message.Payload.Span,
                client.Id,
                out _,
                out _,
                out _,
                out _)));
        Assert.True(MultiplayerRaceResultProtocol.TryDecodeResultAck(
            ack.Payload.Span,
            client.Id,
            out _,
            out var challengeId,
            out var acceptedAck,
            out var error));
        Assert.Equal(fixture.ChallengeId, challengeId);
        Assert.True(acceptedAck);
        Assert.Null(error);
    }

    [Fact]
    public void ClientSurfacesChecksumMismatchButStillAcceptsHostResult()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var fixture = CreateClientRace(host, client);
        Finish(fixture.Lockstep, fixture.ChallengeId);
        var hostResult = BuildCompletedResult(fixture.Resolved);
        var tamperedChecksum = hostResult.FinalChecksum[0] == '0'
            ? "1" + hostResult.FinalChecksum[1..]
            : "0" + hostResult.FinalChecksum[1..];
        hostResult = hostResult with { FinalChecksum = tamperedChecksum };
        MultiplayerRaceResultChecksumMismatch? mismatch = null;
        MultiplayerRaceResult? accepted = null;
        fixture.Results.ChecksumMismatch += value => mismatch = value;
        fixture.Results.ValidatedResultReady += result => accepted = result;
        fixture.Transport.Sent.Clear();

        fixture.Transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceResultProtocol.EncodeFinalResult(host, hostResult)));

        Assert.NotNull(mismatch);
        Assert.Equal(fixture.ChallengeId, mismatch!.ChallengeId);
        Assert.Equal(hostResult, accepted);
        Assert.Contains(fixture.Transport.Sent, message =>
            message.Peer == host.Id &&
            MultiplayerRaceResultProtocol.TryDecodeResultAck(
                message.Payload.Span,
                client.Id,
                out _,
                out _,
                out var ackAccepted,
                out _) &&
            ackAccepted);
    }

    [Fact]
    public void ClientIgnoresFinalResultFromNonHostPeer()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var fixture = CreateClientRace(host, client);
        var forgedResult = BuildCompletedResult(fixture.Resolved);
        MultiplayerRaceResult? accepted = null;
        fixture.Results.ValidatedResultReady += result => accepted = result;
        fixture.Transport.Sent.Clear();

        fixture.Transport.Emit(new NetworkPacket(
            client.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceResultProtocol.EncodeFinalResult(client, forgedResult)));

        Assert.Null(accepted);
        Assert.Empty(fixture.Transport.Sent);
    }

    private static RaceFixture CreateHostRace(PlatformUser host, PlatformUser remote)
    {
        var lobby = Lobby(host, host, remote);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            new FakeLobby(lobby),
            transport);
        var challenges = new ChallengeCoordinator(connection);
        var lockstep = new MultiplayerRaceLockstepCoordinator(connection, challenges);
        var results = new MultiplayerRaceResultCoordinator(connection, challenges, lockstep);

        var offered = challenges.OfferChallenge(ChallengeKind.Race, 2);
        Assert.True(offered.Success, offered.Error);
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeJoinCommand(remote, offered.ChallengeId!)));
        var resolved = CreateResolvedRace(offered.ChallengeId!, host, remote);
        Assert.True(challenges.MarkChallengeReady(offered.ChallengeId!).Success);
        Assert.True(challenges.StartChallenge(
            offered.ChallengeId!,
            MultiplayerRaceStartCodec.Encode(resolved.Start)).Success);
        Assert.True(lockstep.AttachRace(resolved, out var lockstepError), lockstepError);
        Assert.True(results.AttachRace(resolved, out var resultError), resultError);
        return new RaceFixture(offered.ChallengeId!, transport, challenges, lockstep, results, resolved);
    }

    private static RaceFixture CreateClientRace(PlatformUser host, PlatformUser client)
    {
        var lobby = Lobby(host, host, client);
        var transport = new FakeTransport();
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(client),
            new FakeLobby(lobby),
            transport);
        var challenges = new ChallengeCoordinator(connection);
        var lockstep = new MultiplayerRaceLockstepCoordinator(connection, challenges);
        var results = new MultiplayerRaceResultCoordinator(connection, challenges, lockstep);
        transport.Sent.Clear();

        var challengeId = Guid.NewGuid().ToString("N");
        var resolved = CreateResolvedRace(challengeId, host, client);
        var running = new ChallengeSnapshot(
            challengeId,
            lobby.LobbyId,
            ChallengeKind.Race,
            host.Id,
            2,
            ChallengePhase.Running,
            new[] { host.Id, client.Id },
            MultiplayerRaceStartCodec.Encode(resolved.Start));
        transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            ChallengeProtocol.EncodeState(host, running)));
        Assert.True(lockstep.AttachRace(resolved, out var lockstepError), lockstepError);
        Assert.True(results.AttachRace(resolved, out var resultError), resultError);
        return new RaceFixture(challengeId, transport, challenges, lockstep, results, resolved);
    }

    private static void Finish(MultiplayerRaceLockstepCoordinator lockstep, string challengeId)
    {
        Assert.True(lockstep.TryGetSession(challengeId, out var session));
        var guard = 0;
        while (!session.IsComplete && guard++ < 1000)
        {
            Assert.True(lockstep.TryAdvanceFixedSteps(
                challengeId,
                120,
                out _,
                out var error), error);
        }
        Assert.True(session.IsComplete);
    }

    private static MultiplayerRaceResult BuildCompletedResult(ResolvedMultiplayerRace resolved)
    {
        var session = new MultiplayerRaceLockstepSession(resolved);
        var guard = 0;
        while (!session.IsComplete && guard++ < 1000)
            session.AdvanceFixedSteps(120);
        Assert.True(session.IsComplete);
        return new MultiplayerRaceResultFactory().Create(resolved, session);
    }

    private static ResolvedMultiplayerRace CreateResolvedRace(
        string challengeId,
        PlatformUser first,
        PlatformUser second)
    {
        var selectionFactory = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(selectionFactory.TryCreate(
            StateWith(CreateAdult($"creature-{first.Id.Value}", first.Id.Value + 100)),
            first.Id,
            $"creature-{first.Id.Value}",
            out var firstEntrant,
            out var firstError), firstError);
        Assert.True(selectionFactory.TryCreate(
            StateWith(CreateAdult($"creature-{second.Id.Value}", second.Id.Value + 200)),
            second.Id,
            $"creature-{second.Id.Value}",
            out var secondEntrant,
            out var secondError), secondError);

        var factory = new MultiplayerRaceEntryFactory(Rules);
        var start = factory.CreateStartPayload(challengeId, new[] { firstEntrant, secondEntrant });
        Assert.True(factory.TryResolve(start, out var resolved, out var error), error);
        return resolved;
    }

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

    private static PlatformUser User(ulong id, string name)
        => new(new PlatformUserId(id), name);

    private static LobbySnapshot Lobby(PlatformUser owner, params PlatformUser[] members)
        => new(
            77,
            owner.Id,
            members.Select(user => new LobbyMember(user, user.Id == owner.Id)).ToArray());

    private sealed record RaceFixture(
        string ChallengeId,
        FakeTransport Transport,
        ChallengeCoordinator Challenges,
        MultiplayerRaceLockstepCoordinator Lockstep,
        MultiplayerRaceResultCoordinator Results,
        ResolvedMultiplayerRace Resolved);

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