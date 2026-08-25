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

public sealed class MultiplayerRaceFacadeTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void ProductionFlowFreezesSelectionsLaunchesAndProjectsLockstepFrames()
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
        var starts = new MultiplayerRaceStartCoordinator(connection, challenges, Rules);
        var lockstep = new MultiplayerRaceLockstepCoordinator(connection, challenges);

        string? attachError = null;
        starts.RaceReadyToLaunch += race =>
        {
            if (!lockstep.AttachRace(race, out var error))
                attachError = error;
        };

        var hostCreature = CreateAdult("host-creature", "Host Sprout", 101UL);
        var hostState = StateWith(hostCreature);
        var facade = new MultiplayerRaceFacade(
            connection,
            challenges,
            starts,
            lockstep,
            () => hostState);

        var offered = challenges.OfferChallenge(ChallengeKind.Race, 2);
        Assert.True(offered.Success, offered.Error);
        Join(transport, remote, offered.ChallengeId!);

        var initial = facade.GetPreparation(offered.ChallengeId!);
        Assert.True(initial.Exists);
        Assert.Equal(2, initial.ParticipantCount);
        Assert.True(initial.CanSelectVoidling);
        Assert.False(initial.CanRequestStart);
        Assert.True(initial.IsLocalCreator);
        Assert.True(initial.IsLocalHost);

        var selected = facade.SubmitSelection(offered.ChallengeId!, hostCreature.Id);
        Assert.True(selected.Success, selected.Error);
        var afterLocal = facade.GetPreparation(offered.ChallengeId!);
        Assert.Equal(hostCreature.Id, afterLocal.SelectedCreatureId);
        Assert.Equal(hostCreature.Name, afterLocal.SelectedCreatureName);
        Assert.False(afterLocal.AllSelectionsReady);
        Assert.False(afterLocal.CanRequestStart);

        var remoteCreature = CreateAdult("remote-creature", "Remote Sprout", 202UL);
        var selectionFactory = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(selectionFactory.TryCreate(
            StateWith(remoteCreature),
            remote.Id,
            remoteCreature.Id,
            out var remoteEntrant,
            out var selectionError), selectionError);
        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceProtocol.EncodeSelection(remote, offered.ChallengeId!, remoteEntrant)));

        var readyToRequest = facade.GetPreparation(offered.ChallengeId!);
        Assert.True(readyToRequest.AllSelectionsReady);
        Assert.True(readyToRequest.CanRequestStart);

        ResolvedMultiplayerRace? launched = null;
        facade.RaceReadyToLaunch += race => launched = race;
        transport.Sent.Clear();
        var requested = facade.RequestStart(offered.ChallengeId!);
        Assert.True(requested.Success, requested.Error);
        Assert.Equal(ChallengePhase.Ready, Assert.Single(challenges.Challenges).Phase);

        var proposal = Assert.Single(transport.Sent, message =>
            message.Peer == remote.Id &&
            MultiplayerRaceProtocol.TryDecodeStartProposal(
                message.Payload.Span,
                host.Id,
                out _,
                out _,
                out _,
                out _));
        Assert.True(MultiplayerRaceProtocol.TryDecodeStartProposal(
            proposal.Payload.Span,
            host.Id,
            out _,
            out var challengeId,
            out var startHash,
            out _));

        transport.Emit(new NetworkPacket(
            remote.Id,
            NetworkChannel.Challenge,
            MultiplayerRaceProtocol.EncodeStartAck(
                remote,
                challengeId,
                startHash,
                true,
                null)));

        Assert.Null(attachError);
        Assert.NotNull(launched);
        Assert.Equal(ChallengePhase.Running, Assert.Single(challenges.Challenges).Phase);
        Assert.True(facade.TryGetFrame(challengeId, out var frame));
        Assert.Equal(0, frame.CurrentTick);
        Assert.False(frame.IsComplete);
        Assert.Equal(2, frame.Participants.Count);
        var local = Assert.Single(frame.Participants, participant => participant.IsLocal);
        Assert.Equal("Host Sprout", local.DisplayName);
        Assert.InRange(local.Progress, 0.0f, 1.0f);

        var advanced = facade.AdvanceFixedSteps(challengeId, 15);
        Assert.True(advanced.Success, advanced.Error);
        Assert.True(facade.TryGetFrame(challengeId, out var advancedFrame));
        Assert.Equal(15, advancedFrame.CurrentTick);

        var cheer = facade.RequestCheer(challengeId);
        Assert.True(cheer.Success, cheer.Error);
        var afterCheerAdvance = facade.AdvanceFixedSteps(challengeId, 13);
        Assert.True(afterCheerAdvance.Success, afterCheerAdvance.Error);
        Assert.True(facade.TryGetFrame(challengeId, out var cheeredFrame));
        Assert.Equal(28, cheeredFrame.CurrentTick);
        Assert.True(Assert.Single(cheeredFrame.Participants, participant => participant.IsLocal).CheerSeconds > 0.0f);
    }

    [Fact]
    public void UnknownChallengeReturnsNonLaunchablePreparation()
    {
        var host = User(1, "Host");
        var lobby = Lobby(host, host);
        var connection = new MultiplayerConnectionService(
            new FakeIdentity(host),
            new FakeLobby(lobby),
            new FakeTransport());
        var challenges = new ChallengeCoordinator(connection);
        var starts = new MultiplayerRaceStartCoordinator(connection, challenges, Rules);
        var lockstep = new MultiplayerRaceLockstepCoordinator(connection, challenges);
        var facade = new MultiplayerRaceFacade(
            connection,
            challenges,
            starts,
            lockstep,
            () => new GameStateData());

        var preparation = facade.GetPreparation(Guid.NewGuid().ToString("N"));

        Assert.False(preparation.Exists);
        Assert.False(preparation.CanSelectVoidling);
        Assert.False(preparation.CanRequestStart);
        Assert.NotNull(preparation.Error);
    }

    private static GameStateData StateWith(VoidlingData creature)
    {
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        return state;
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
