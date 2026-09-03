using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Racing;
using Voidling.Domain.Genetics;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class RacingArchitectureTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Snapshot_DoesNotChangeWhenLiveCreatureChangesAfterRaceEntry()
    {
        var creature = CreateCreature();
        var factory = new RaceParticipantSnapshotFactory(Rules);
        var snapshot = factory.Create(creature);
        var originalRun = snapshot.Run;

        creature.TrainingPoints["run"] = 120;

        Assert.Equal(originalRun, snapshot.Run);
        Assert.True(factory.Create(creature).Run > originalRun);
    }

    [Fact]
    public void Snapshot_FreezesSemanticAppearanceWithoutMutatingLiveCreature()
    {
        var creature = CreateCreature();
        creature.Appearance = new VoidlingAppearanceData
        {
            VisualTypeId = "Flying",
            PaletteHue = 0.725f,
            LayerIds = new List<string> { "wing.large", "crystal.blue" }
        };
        var factory = new RaceParticipantSnapshotFactory(Rules);

        var snapshot = factory.Create(creature);

        Assert.Equal("flying", snapshot.VisualTypeId);
        Assert.Equal(0.725f, snapshot.PaletteHue);
        Assert.Equal(new[] { "crystal.blue", "wing.large" }, snapshot.LayerIds);
        Assert.Equal("Flying", creature.Appearance.VisualTypeId);
        Assert.Equal(new[] { "wing.large", "crystal.blue" }, creature.Appearance.LayerIds);
    }

    [Fact]
    public void RaceEntryFactory_CpuRacersFreezeContinuousPaletteHueDeterministically()
    {
        var factory = new RaceEntryFactory(Rules);

        var first = factory.Create(CreateCreature(), 123456UL);
        var second = factory.Create(CreateCreature(), 123456UL);
        var firstCpu = first.Entrants.Skip(1).Select(entry => entry.Participant).ToArray();
        var secondCpu = second.Entrants.Skip(1).Select(entry => entry.Participant).ToArray();

        Assert.Equal(3, firstCpu.Length);
        Assert.All(firstCpu, cpu =>
        {
            Assert.Equal(VoidlingAppearanceData.DefaultVisualTypeId, cpu.VisualTypeId);
            Assert.True(VoidlingAppearanceData.IsValidHue(cpu.PaletteHue));
        });
        Assert.Equal(firstCpu.Select(cpu => cpu.PaletteHue), secondCpu.Select(cpu => cpu.PaletteHue));
    }

    [Fact]
    public void PerformanceModel_PreservesMvpGroundAndSwimFormulas()
    {
        var participant = new RaceParticipantSnapshot("id", "name", "#FFFFFF", 50, 40, 30, 20, 60);
        var model = new RacePerformanceModel(Rules.Racing);
        var maxStamina = model.GetMaxStamina(participant);

        var ground = model.GetMovement(participant, RaceTerrain.Ground, maxStamina, maxStamina, false);
        var swim = model.GetMovement(participant, RaceTerrain.Swim, maxStamina, maxStamina, false);

        Assert.Equal(49.0f, ground.Speed, 3);
        Assert.Equal(2.1f, ground.StaminaDrainPerSecond, 3);
        Assert.Equal(38.0f, swim.Speed, 3);
        Assert.Equal(3.2f, swim.StaminaDrainPerSecond, 3);
    }

    [Fact]
    public void PerformanceModel_ClimbUsesPowerAndAuthoredClimbDrain()
    {
        var lowPower = new RaceParticipantSnapshot("low", "Low", "#FFFFFF", 100, 40, 30, 10, 60);
        var highPower = new RaceParticipantSnapshot("high", "High", "#FFFFFF", 0, 40, 30, 90, 60);
        var model = new RacePerformanceModel(Rules.Racing);
        var lowMaxStamina = model.GetMaxStamina(lowPower);
        var highMaxStamina = model.GetMaxStamina(highPower);

        var lowClimb = model.GetMovement(lowPower, RaceTerrain.Climb, lowMaxStamina, lowMaxStamina, false);
        var highClimb = model.GetMovement(highPower, RaceTerrain.Climb, highMaxStamina, highMaxStamina, false);
        var lowGround = model.GetMovement(lowPower, RaceTerrain.Ground, lowMaxStamina, lowMaxStamina, false);
        var highGround = model.GetMovement(highPower, RaceTerrain.Ground, highMaxStamina, highMaxStamina, false);

        Assert.Equal(18.4f, lowClimb.Speed, 3);
        Assert.Equal(45.6f, highClimb.Speed, 3);
        Assert.Equal(3.55f, lowClimb.StaminaDrainPerSecond, 3);
        Assert.True(highClimb.Speed > lowClimb.Speed);
        Assert.True(lowGround.Speed > highGround.Speed);
    }

    [Fact]
    public void PerformanceModel_AppliesLowStaminaExhaustionAndCheerInStableOrder()
    {
        var participant = new RaceParticipantSnapshot("id", "name", "#FFFFFF", 50, 40, 30, 20, 60);
        var model = new RacePerformanceModel(Rules.Racing);
        var maxStamina = model.GetMaxStamina(participant);

        var movement = model.GetMovement(participant, RaceTerrain.Ground, 0.0f, maxStamina, true);
        var expected = 49.0f * 0.90f * 0.84f * 1.22f;

        Assert.Equal(expected, movement.Speed, 3);
    }

    [Fact]
    public void PerformanceModel_ObstacleAndGlideRulesAreBoundedAndDeterministic()
    {
        var participant = new RaceParticipantSnapshot("id", "name", "#FFFFFF", 100, 40, 100, 20, 60);
        var model = new RacePerformanceModel(Rules.Racing);

        Assert.Equal(0.95f, model.GetObstacleAvoidChance(participant), 3);
        Assert.True(model.AvoidsObstacle(participant, 0.949));
        Assert.False(model.AvoidsObstacle(participant, 0.951));
        Assert.Equal(337.0f, model.GetGlideDistance(participant), 3);
        Assert.Equal(0.62f, model.GetObstacleDelaySeconds(participant), 3);
    }

    private static VoidlingData CreateCreature()
    {
        var creature = new VoidlingData
        {
            Id = "runner",
            Name = "Runner",
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(9001UL)
        };

        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;

        return creature;
    }
}
