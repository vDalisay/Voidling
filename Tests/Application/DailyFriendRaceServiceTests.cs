using System;
using System.Linq;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Persistence;
using Voidling.Application.Racing;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class DailyFriendRaceServiceTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void UtcDayAndSeedAreStableAcrossEquivalentOffsets()
    {
        var first = new DateTimeOffset(2026, 8, 25, 23, 30, 0, TimeSpan.FromHours(2));
        var sameInstant = first.ToOffset(TimeSpan.FromHours(-5));

        var firstKey = DailyFriendRaceService.GetDailyKey(first);
        var secondKey = DailyFriendRaceService.GetDailyKey(sameInstant);

        Assert.Equal("2026-08-25", firstKey);
        Assert.Equal(firstKey, secondKey);
        Assert.Equal(
            DailyFriendRaceService.ComputeDailySeed(firstKey, DailyFriendRaceService.CurrentRulesVersion),
            DailyFriendRaceService.ComputeDailySeed(secondKey, DailyFriendRaceService.CurrentRulesVersion));
    }

    [Fact]
    public void BeginConsumesOneAttemptAndFreezesSelectedEntrant()
    {
        var state = new GameStateData();
        var first = CreateAdult("first", 100);
        var second = CreateAdult("second", 200);
        state.Voidlings.Add(first);
        state.Voidlings.Add(second);
        var service = new DailyFriendRaceService(new RaceEntryFactory(Rules));
        var now = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

        var started = service.Begin(state, first.Id, now);

        Assert.True(started.Success, started.Error);
        Assert.False(started.AlreadyStarted);
        Assert.NotNull(started.Attempt);
        Assert.NotNull(started.Entry);
        Assert.Equal(first.Id, started.Attempt!.SelectedEntrant!.Participant.CreatureId);
        Assert.Equal(4, started.Entry!.Entrants.Count);
        var frozen = started.Attempt.SelectedEntrant.Participant;

        first.TrainingPoints["run"] = Rules.Stats.MaxTrainingPoints;
        var repeated = service.Begin(state, second.Id, now);

        Assert.True(repeated.AlreadyStarted);
        Assert.True(repeated.Success, repeated.Error);
        Assert.Single(state.DailyRaceAttempts);
        Assert.Equal(first.Id, repeated.Attempt!.SelectedEntrant!.Participant.CreatureId);
        Assert.Equal(frozen, repeated.Entry!.Entrants[0].Participant);
        Assert.NotEqual(
            new RaceEntryFactory(Rules).CreateOwnedEntrant(first).Participant.Run,
            repeated.Entry.Entrants[0].Participant.Run);
    }

    [Fact]
    public void StartedAttemptResumesWithSameSeedAndCpuField()
    {
        var state = new GameStateData();
        var creature = CreateAdult("runner", 300);
        state.Voidlings.Add(creature);
        var service = new DailyFriendRaceService(new RaceEntryFactory(Rules));
        var now = new DateTimeOffset(2026, 8, 25, 8, 0, 0, TimeSpan.Zero);

        var started = service.Begin(state, creature.Id, now);
        var resumed = service.ResumeToday(state, now.AddHours(10));

        Assert.True(started.Success, started.Error);
        Assert.True(resumed.Success, resumed.Error);
        Assert.True(resumed.AlreadyStarted);
        Assert.Equal(started.Attempt!.SimulationSeed, resumed.Attempt!.SimulationSeed);
        Assert.Equal(
            started.Entry!.Entrants.Select(value => value.Participant).ToArray(),
            resumed.Entry!.Entrants.Select(value => value.Participant).ToArray());
    }

    [Fact]
    public void CompleteIsIdempotentAndPreventsResumeAsFreshAttempt()
    {
        var state = new GameStateData();
        var creature = CreateAdult("runner", 400);
        state.Voidlings.Add(creature);
        var service = new DailyFriendRaceService(new RaceEntryFactory(Rules));
        var now = new DateTimeOffset(2026, 8, 25, 8, 0, 0, TimeSpan.Zero);
        var started = service.Begin(state, creature.Id, now);

        var completed = service.Complete(state, started.Attempt!.DailyKey, 51_234);
        var duplicate = service.Complete(state, started.Attempt.DailyKey, 49_000);
        var resume = service.ResumeToday(state, now);

        Assert.True(completed.Success, completed.Error);
        Assert.False(completed.AlreadyCompleted);
        Assert.True(duplicate.Success, duplicate.Error);
        Assert.True(duplicate.AlreadyCompleted);
        Assert.Equal(51_234, duplicate.Attempt!.FinishedMilliseconds);
        Assert.False(resume.Success);
        Assert.True(resume.AlreadyStarted);
        Assert.Contains("completed", resume.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VersionEightMigrationInitializesAndBoundsDailyHistoryWithoutSteam()
    {
        var state = new GameStateData
        {
            SaveVersion = 7,
            DailyRaceAttempts = null!
        };
        var migration = new GameStateMigrationService(Rules);

        migration.Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.NotNull(state.DailyRaceAttempts);
        Assert.Empty(state.DailyRaceAttempts);
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
