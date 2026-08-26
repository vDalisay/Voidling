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

public sealed class MultiplayerRaceLockstepCoordinatorTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void HostCheer_IsScheduledTwelveTicksAheadAndAppliedAtCanonicalTick()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var fixture = CreateHostRace(host, remote);
        fixture.Transport.Sent.Clear();

        var requested = fixture.Lockstep.RequestCheer(fixture.ChallengeId);

        Assert.True(requested.Success, requested.Error);
        Assert.True(fixture.Lockstep.TryGetSession(fixture.ChallengeId, out var session));
        var packet = Assert.Single(fixture.Transport.Sent.Where(message => message.Peer == remote.Id));
        Assert.True(MultiplayerRaceLockstepProtocol.TryDecodeScheduledCommand(
            packet.Payload.Span,
            host.Id,
            out _,
            out var command));
        Assert.Equal(fixture.ChallengeId, command.ChallengeId);
        Assert.Equal(host.Id, command.OwnerId);
        Assert.Equal(MultiplayerRaceLockstepSession.DefaultInputDelayTicks, command.Tick);
        Assert.Equal(1, command.Sequence);

        Assert.True(fixture.Lockstep.TryAdvanceFixedSteps(
            fixture.ChallengeId,
            MultiplayerRaceLockstepSession.DefaultInputDelayTicks,
            out var before,
            out var beforeError), beforeError);
        Assert.Empty(before.CommandApplications);
        Assert.Equal(MultiplayerRaceLockstepSession.DefaultInputDelayTicks, session.CurrentTick);

        Assert.True(fixture.Lockstep.TryAdvanceFixedSteps(
            fixture.ChallengeId,
            1,
            out var applied,
            out var appliedError), appliedError);
        var application = Assert.Single(applied.CommandApplications);
        Assert.Equal(command, application.Command);
        Assert.True(application.Applied);
    }

    [Fact]
    public void Client_AcceptsScheduledCommandOnlyFromCurrentLobbyHost()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var fixture = CreateClientRace(host, client);
        Assert.True(fixture.Lockstep.TryGetSession(fixture.ChallengeId, out var session));
        Assert.True(session.TryGetParticipantId(client.Id, out var participantId));

        var forged = new ScheduledRaceCommand(
            fixture.ChallengeId,
            12,
            1,
            client.Id,
            participantId,
            MultiplayerRaceCommandKind.Cheer);
        fixture.Transport.Emit(new NetworkPacket(
            client.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceLockstepProtocol.EncodeScheduledCommand(client, forged)));

        Assert.True(fixture.Lockstep.TryAdvanceFixedSteps(
            fixture.ChallengeId,
            13,
            out var ignored,
            out var ignoredError), ignoredError);
        Assert.Empty(ignored.CommandApplications);

        var valid = forged with { Tick = session.CurrentTick + 12, Sequence = 2 };
        fixture.Transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceLockstepProtocol.EncodeScheduledCommand(host, valid)));
        Assert.True(fixture.Lockstep.TryAdvanceFixedSteps(
            fixture.ChallengeId,
            13,
            out var accepted,
            out var acceptedError), acceptedError);
        Assert.Single(accepted.CommandApplications);
    }

    [Fact]
    public void Client_ReportsLateHostCommandAsSyncIssueWithoutRollback()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var fixture = CreateClientRace(host, client);
        Assert.True(fixture.Lockstep.TryGetSession(fixture.ChallengeId, out var session));
        Assert.True(session.TryGetParticipantId(client.Id, out var participantId));
        Assert.True(fixture.Lockstep.TryAdvanceFixedSteps(
            fixture.ChallengeId,
            20,
            out _,
            out var advanceError), advanceError);

        string? issue = null;
        fixture.Lockstep.SyncIssue += (_, reason) => issue = reason;
        var late = new ScheduledRaceCommand(
            fixture.ChallengeId,
            10,
            1,
            client.Id,
            participantId,
            MultiplayerRaceCommandKind.Cheer);
        fixture.Transport.Emit(new NetworkPacket(
            host.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceLockstepProtocol.EncodeScheduledCommand(host, late)));

        Assert.NotNull(issue);
        Assert.Contains("after tick", issue!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(20, session.CurrentTick);
    }

    [Fact]
    public void Host_ComparesPeriodicPeerChecksumAndSurfacesMismatch()
    {
        var host = User(1, "Host");
        var remote = User(2, "Remote");
        var fixture = CreateHostRace(host, remote);

        long reportedTick = -1;
        string? hostChecksum = null;
        fixture.Lockstep.LocalChecksumReported += (_, tick, checksum) =>
        {
            if (tick == MultiplayerRaceLockstepCoordinator.ChecksumIntervalTicks)
            {
                reportedTick = tick;
                hostChecksum = checksum;
            }
        };
        Assert.True(fixture.Lockstep.TryAdvanceFixedSteps(
            fixture.ChallengeId,
            MultiplayerRaceLockstepCoordinator.ChecksumIntervalTicks,
            out _,
            out var advanceError), advanceError);
        Assert.Equal(MultiplayerRaceLockstepCoordinator.ChecksumIntervalTicks, reportedTick);
        Assert.NotNull(hostChecksum);

        PlatformUserId matchedPeer = default;
        fixture.Lockstep.PeerChecksumMatched += (_, tick, peer) =>
        {
            if (tick == reportedTick)
                matchedPeer = peer;
        };
        fixture.Transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceLockstepProtocol.EncodeChecksum(
                remote,
                fixture.ChallengeId,
                reportedTick,
                hostChecksum!)));
        Assert.Equal(remote.Id, matchedPeer);

        MultiplayerRaceDesync? desync = null;
        fixture.Lockstep.DesyncDetected += value => desync = value;
        var different = hostChecksum![0] == '0'
            ? "1" + hostChecksum[1..]
            : "0" + hostChecksum[1..];
        fixture.Transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceLockstepProtocol.EncodeChecksum(
                remote,
                fixture.ChallengeId,
                reportedTick,
                different)));

        Assert.NotNull(desync);
        Assert.Equal(fixture.ChallengeId, desync!.ChallengeId);
        Assert.Equal(reportedTick, desync.Tick);
        Assert.Equal(remote.Id, desync.PeerId);
        Assert.Equal(hostChecksum, desync.HostChecksum);
        Assert.Equal(different, desync.PeerChecksum);
    }

    [Fact]
    public void Client_PeriodicChecksumIsSentReliablyToHost()
    {
        var host = User(1, "Host");
        var client = User(2, "Client");
        var fixture = CreateClientRace(host, client);
        fixture.Transport.Sent.Clear();

        Assert.True(fixture.Lockstep.TryAdvanceFixedSteps(
            fixture.ChallengeId,
            MultiplayerRaceLockstepCoordinator.ChecksumIntervalTicks,
            out _,
            out var error), error);

        var checksumPacket = Assert.Single(fixture.Transport.Sent.Where(message =>
            message.Peer == host.Id &&
            message.Channel == NetworkChannel.Challenge &&
            MultiplayerRaceLockstepProtocol.TryDecodeChecksum(
                message.Payload.Span,
                client.Id,
                out _,
                out _,
                out var tick,
                out _) &&
            tick == MultiplayerRaceLockstepCoordinator.ChecksumIntervalTicks));
        Assert.Equal(DeliveryMode.Reliable, checksumPacket.Delivery);
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
        Assert.True(lockstep.AttachRace(resolved, out var error), error);
        return new RaceFixture(offered.ChallengeId!, transport, lockstep);
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
        Assert.True(lockstep.AttachRace(resolved, out var error), error);
        return new RaceFixture(challengeId, transport, lockstep);
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
        MultiplayerRaceLockstepCoordinator Lockstep);

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
