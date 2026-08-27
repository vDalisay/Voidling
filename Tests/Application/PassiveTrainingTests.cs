using System;
using Voidling.Application.Persistence;
using Voidling.Application.Simulation;
using Voidling.Application.Training;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class PassiveTrainingTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults with
    {
        PassiveTraining = new PassiveTrainingRules(PointsPerMinute: 2.0f),
        Reincarnation = GameBalanceRules.DemoDefaults.Reincarnation with { AdultLifespanSeconds = 10_000.0f }
    };

    [Fact]
    public void Assignment_UsesOnlyKnownStatIds_AndCanBeStopped()
    {
        var creature = CreateCreature("one", LifeStage.Adult, rank: 5);
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        var training = new TrainingUseCase(Rules);

        var unknown = training.SetPassiveTraining(state, creature.Id, "luck");
        Assert.False(unknown.Succeeded);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);

        var assigned = training.SetPassiveTraining(state, creature.Id, "run");
        Assert.True(assigned.Succeeded);
        Assert.True(assigned.Changed);
        Assert.Equal("run", creature.PassiveTrainingStatId);

        creature.PassiveTrainingPointRemainder = 0.75;
        var stopped = training.SetPassiveTraining(state, creature.Id, string.Empty);
        Assert.True(stopped.Succeeded);
        Assert.True(stopped.Changed);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);
        Assert.Equal(0.0, creature.PassiveTrainingPointRemainder);
    }

    [Fact]
    public void Advance_PassiveTraining_IsElapsedChunkIndependent()
    {
        var first = CreateStateWithPassiveRun("first");
        var second = CreateStateWithPassiveRun("second");
        var simulation = new AdvanceSimulationUseCase(Rules);

        simulation.Advance(first, 90.0f);
        simulation.Advance(second, 30.0f);
        simulation.Advance(second, 20.0f);
        simulation.Advance(second, 40.0f);

        Assert.Equal(3, first.Voidlings[0].TrainingPoints["run"]);
        Assert.Equal(first.Voidlings[0].TrainingPoints["run"], second.Voidlings[0].TrainingPoints["run"]);
        Assert.Equal(first.Voidlings[0].PassiveTrainingPointRemainder,
            second.Voidlings[0].PassiveTrainingPointRemainder, 8);
    }

    [Fact]
    public void Advance_PassiveTraining_StopsAtDnaRankCap_AndCapEventIsOneShot()
    {
        var creature = CreateCreature("cap", LifeStage.Adult, rank: 0);
        creature.PassiveTrainingStatId = "run";
        creature.TrainingPoints["run"] = Rules.Stats.RankCaps.E - 1;
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        var fastRules = Rules with { PassiveTraining = new PassiveTrainingRules(PointsPerMinute: 120.0f) };
        var simulation = new AdvanceSimulationUseCase(fastRules);

        var first = simulation.Advance(state, 60.0f);
        var second = simulation.Advance(state, 60.0f);

        Assert.Equal(Rules.Stats.RankCaps.E, creature.TrainingPoints["run"]);
        Assert.Single(first.Events, e => e is CreaturePassiveTrainingCappedEvent);
        Assert.DoesNotContain(second.Events, e => e is CreaturePassiveTrainingCappedEvent);
    }

    [Fact]
    public void Advance_ChildPassiveTraining_FeedsSameEvolutionInfluenceAsActiveTraining()
    {
        var creature = CreateCreature("child", LifeStage.Child, rank: 5);
        creature.PassiveTrainingStatId = "run";
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        var rules = Rules with
        {
            PassiveTraining = new PassiveTrainingRules(PointsPerMinute: 4.0f),
            Lifecycle = Rules.Lifecycle with { ChildToAdultSeconds = 1_000.0f }
        };

        new AdvanceSimulationUseCase(rules).Advance(state, 30.0f);

        Assert.Equal(2, creature.TrainingPoints["run"]);
        Assert.True(creature.RunPowerInfluence < 0.0f);
        Assert.Equal(LifeStage.Child, creature.Stage);
    }

    [Fact]
    public void Migration_V12_DisablesUnknownAssignmentAndInvalidRemainder()
    {
        var creature = CreateCreature("legacy", LifeStage.Adult, rank: 5);
        creature.PassiveTrainingStatId = "unknown";
        creature.PassiveTrainingPointRemainder = double.NaN;
        var state = new GameStateData { SaveVersion = 11 };
        state.Voidlings.Add(creature);

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);
        Assert.Equal(0.0, creature.PassiveTrainingPointRemainder);
    }

    private static GameStateData CreateStateWithPassiveRun(string id)
    {
        var state = new GameStateData();
        var creature = CreateCreature(id, LifeStage.Adult, rank: 5);
        creature.PassiveTrainingStatId = "run";
        state.Voidlings.Add(creature);
        return state;
    }

    private static VoidlingData CreateCreature(string id, LifeStage stage, int rank)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = stage,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(123UL)
        };

        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = rank,
                AlleleB = rank,
                ExpressedAlleleIndex = 0
            };
            creature.TrainingPoints[statId] = 0;
        }

        return creature;
    }
}