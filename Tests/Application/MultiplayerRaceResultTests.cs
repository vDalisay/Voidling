using System;
using System.Linq;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Application.Persistence;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class MultiplayerRaceResultTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Apply_FirstPlaceAwardsOnceAndIncrementsPersistentWinTotal()
    {
        var state = new GameStateData { Coins = 10 };
        var local = new PlatformUserId(1);
        var result = Result(local, new PlatformUserId(2));
        var useCase = new MultiplayerRaceResultUseCase(Rules);

        var first = useCase.Apply(state, local, result);
        var coinsAfterFirst = state.Coins;
        var second = useCase.Apply(state, local, result);

        Assert.True(first.Success, first.Error);
        Assert.False(first.AlreadyApplied);
        Assert.True(first.Won);
        Assert.Equal(1, first.Place);
        Assert.Equal(Rules.Racing.PlacementRewards[0], first.CoinReward);
        Assert.Equal(1, state.MultiplayerWins);
        Assert.Contains(result.ChallengeId, state.AppliedMultiplayerRaceIds);

        Assert.True(second.Success, second.Error);
        Assert.True(second.AlreadyApplied);
        Assert.Equal(0, second.CoinReward);
        Assert.Equal(coinsAfterFirst, state.Coins);
        Assert.Equal(1, state.MultiplayerWins);
    }

    [Fact]
    public void Apply_ResultWithoutLocalPlayerFailsWithoutMutation()
    {
        var state = new GameStateData { Coins = 25, MultiplayerWins = 3 };
        var result = Result(new PlatformUserId(1), new PlatformUserId(2));
        var useCase = new MultiplayerRaceResultUseCase(Rules);

        var applied = useCase.Apply(state, new PlatformUserId(99), result);

        Assert.False(applied.Success);
        Assert.Equal(25, state.Coins);
        Assert.Equal(3, state.MultiplayerWins);
        Assert.Empty(state.AppliedMultiplayerRaceIds);
    }

    [Fact]
    public void MigrationNormalizesMultiplayerRaceProgressWithoutSteam()
    {
        var state = new GameStateData
        {
            SaveVersion = 6,
            MultiplayerWins = -5,
            AppliedMultiplayerRaceIds = Enumerable.Range(0, 270)
                .Select(index => $"race-{index}")
                .Concat(new[] { "", "race-269" })
                .ToList()
        };

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal(0, state.MultiplayerWins);
        Assert.Equal(MultiplayerRaceResultUseCase.MaxAppliedRaceIds, state.AppliedMultiplayerRaceIds.Count);
        Assert.Equal("race-14", state.AppliedMultiplayerRaceIds[0]);
        Assert.Equal("race-269", state.AppliedMultiplayerRaceIds[^1]);
        Assert.Equal(state.AppliedMultiplayerRaceIds.Count, state.AppliedMultiplayerRaceIds.Distinct().Count());
    }

    [Fact]
    public void ResultFactoryMapsDeterministicFinishOrderBackToPlatformOwners()
    {
        var first = new PlatformUserId(1);
        var second = new PlatformUserId(2);
        var challengeId = Guid.NewGuid().ToString("N");
        var selectionFactory = new MultiplayerRaceSelectionFactory(Rules);
        Assert.True(selectionFactory.TryCreate(
            StateWith(CreateAdult("first", 101)),
            first,
            "first",
            out var firstEntrant,
            out var firstError), firstError);
        Assert.True(selectionFactory.TryCreate(
            StateWith(CreateAdult("second", 202)),
            second,
            "second",
            out var secondEntrant,
            out var secondError), secondError);
        var entryFactory = new MultiplayerRaceEntryFactory(Rules);
        var start = entryFactory.CreateStartPayload(challengeId, new[] { firstEntrant, secondEntrant });
        Assert.True(entryFactory.TryResolve(start, out var resolved, out var resolveError), resolveError);
        var session = new MultiplayerRaceLockstepSession(resolved);

        while (!session.IsComplete)
            session.AdvanceFixedSteps(120);

        var result = new MultiplayerRaceResultFactory().Create(resolved, session);

        Assert.Equal(challengeId, result.ChallengeId);
        Assert.Equal(session.CurrentTick, result.FinalTick);
        Assert.Equal(session.ComputeDeterministicChecksum(), result.FinalChecksum);
        Assert.Equal(2, result.Placements.Length);
        Assert.Equal(new[] { 1, 2 }, result.Placements.Select(value => value.Place));
        foreach (var placement in result.Placements)
        {
            var entrant = resolved.Start.Entrants.Single(value => value.OwnerId == placement.OwnerId);
            Assert.Equal(entrant.Participant.CreatureId, placement.ParticipantId);
        }
    }

    private static MultiplayerRaceResult Result(PlatformUserId first, PlatformUserId second)
        => new(
            Guid.NewGuid().ToString("N"),
            500,
            new string('a', 64),
            new[]
            {
                new MultiplayerRacePlacement(first, $"{first.Value}:first", 1),
                new MultiplayerRacePlacement(second, $"{second.Value}:second", 2)
            });

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
}
