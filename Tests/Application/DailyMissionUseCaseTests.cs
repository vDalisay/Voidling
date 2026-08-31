using System;
using System.Linq;
using Voidling.Application.Daily;
using Voidling.Application.Persistence;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class DailyMissionUseCaseTests
{
    private static readonly DailyMissionRules MissionRules = new(
        MissionsPerDay: 2,
        Definitions: Array.AsReadOnly(new[]
        {
            new DailyMissionDefinition("pet", DailyMissionEventKind.PetVoidling, 2, 5),
            new DailyMissionDefinition("train", DailyMissionEventKind.UseTrainingTreat, 1, 7),
            new DailyMissionDefinition("race", DailyMissionEventKind.CompleteStandardRace, 1, 9)
        }));

    [Fact]
    public void EnsureDay_SelectsDeterministicallyAndFreezesCurrentDayProgress()
    {
        var first = new GameStateData();
        var second = new GameStateData();
        var useCase = new DailyMissionUseCase();

        Assert.True(useCase.EnsureDay(first, 1000, MissionRules));
        Assert.True(useCase.EnsureDay(second, 1000, MissionRules));
        Assert.Equal(
            first.DailyMissions.Missions.Select(mission => mission.MissionId),
            second.DailyMissions.Missions.Select(mission => mission.MissionId));

        var selectedKind = MissionRules.Definitions
            .Single(definition => definition.Id == first.DailyMissions.Missions[0].MissionId)
            .EventKind;
        Assert.True(useCase.RecordEvent(first, 1000, MissionRules, selectedKind));
        var before = first.DailyMissions.Missions
            .Select(mission => (mission.MissionId, mission.Progress, mission.Claimed))
            .ToArray();

        Assert.False(useCase.EnsureDay(first, 1000, MissionRules));
        Assert.Equal(
            before,
            first.DailyMissions.Missions
                .Select(mission => (mission.MissionId, mission.Progress, mission.Claimed))
                .ToArray());
    }

    [Fact]
    public void EnsureDay_RotatesAndResetsProgressOnNextLocalDay()
    {
        var state = new GameStateData();
        var useCase = new DailyMissionUseCase();
        useCase.EnsureDay(state, 1000, MissionRules);
        var firstIds = state.DailyMissions.Missions.Select(mission => mission.MissionId).ToArray();
        var firstKind = MissionRules.Definitions.Single(definition => definition.Id == firstIds[0]).EventKind;
        useCase.RecordEvent(state, 1000, MissionRules, firstKind, amount: 99);

        Assert.True(useCase.EnsureDay(state, 1001, MissionRules));

        Assert.Equal(1001, state.DailyMissions.DayNumber);
        Assert.All(state.DailyMissions.Missions, mission => Assert.Equal(0, mission.Progress));
        Assert.All(state.DailyMissions.Missions, mission => Assert.False(mission.Claimed));
        Assert.NotEqual(firstIds, state.DailyMissions.Missions.Select(mission => mission.MissionId).ToArray());
    }

    [Fact]
    public void RecordEvent_OnlyAdvancesMatchingMissionAndCapsAtTarget()
    {
        var rules = new DailyMissionRules(
            MissionsPerDay: 2,
            Definitions: Array.AsReadOnly(new[]
            {
                new DailyMissionDefinition("pet", DailyMissionEventKind.PetVoidling, 2, 5),
                new DailyMissionDefinition("train", DailyMissionEventKind.UseTrainingTreat, 3, 7)
            }));
        var state = new GameStateData();
        var useCase = new DailyMissionUseCase();
        useCase.EnsureDay(state, 2000, rules);

        Assert.True(useCase.RecordEvent(state, 2000, rules, DailyMissionEventKind.PetVoidling, amount: 99));

        Assert.Equal(2, state.DailyMissions.Missions.Single(mission => mission.MissionId == "pet").Progress);
        Assert.Equal(0, state.DailyMissions.Missions.Single(mission => mission.MissionId == "train").Progress);
        Assert.False(useCase.RecordEvent(state, 2000, rules, DailyMissionEventKind.PetVoidling));
    }

    [Fact]
    public void Claim_AwardsExactlyOnceAfterTargetIsReached()
    {
        var rules = new DailyMissionRules(
            MissionsPerDay: 1,
            Definitions: Array.AsReadOnly(new[]
            {
                new DailyMissionDefinition("pet", DailyMissionEventKind.PetVoidling, 1, 11)
            }));
        var state = new GameStateData { Coins = 10 };
        var useCase = new DailyMissionUseCase();
        useCase.RecordEvent(state, 3000, rules, DailyMissionEventKind.PetVoidling);

        var first = useCase.Claim(state, 3000, rules, "pet");
        var repeated = useCase.Claim(state, 3000, rules, "pet");

        Assert.True(first.Claimed);
        Assert.Equal(11, first.CoinsAwarded);
        Assert.Equal(21, state.Coins);
        Assert.False(repeated.Claimed);
        Assert.Equal(0, repeated.CoinsAwarded);
        Assert.True(Assert.Single(repeated.Status.Missions).Claimed);
    }

    [Fact]
    public void Migration_InitializesDailyMissionStateForLegacySave()
    {
        var state = new GameStateData
        {
            SaveVersion = 16,
            DailyMissions = null!
        };

        new GameStateMigrationService(GameBalanceRules.DemoDefaults).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.NotNull(state.DailyMissions);
        Assert.NotNull(state.DailyMissions.Missions);
        Assert.Equal(0, state.DailyMissions.DayNumber);
        Assert.Empty(state.DailyMissions.Missions);
    }
}
