using System;
using System.Collections.Generic;
using Voidling.Application.Garden;
using Voidling.Application.Persistence;
using Voidling.Application.Training;
using Voidling.Domain.Rules;
using Voidling.Domain.Training;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class GardenModuleTrainingTests
{
    [Fact]
    public void BuyAndPlaceGardenModule_DeductsCurrencyAndUsesLogicalSlot()
    {
        var rules = Rules(purchaseCost: 40);
        var state = new GameStateData { Coins = 100 };
        var useCase = new TrainingUseCase(rules);

        var purchase = useCase.BuyGardenModule(state, "module-run", "run");
        var placement = useCase.PlaceGardenModule(state, "module-run", 2);

        Assert.True(purchase.Succeeded);
        Assert.True(placement.Succeeded);
        Assert.Equal(60, state.Coins);
        var module = Assert.Single(state.GardenModules);
        Assert.Equal("run", module.StatId);
        Assert.Equal(1, module.Level);
        Assert.Equal(2, module.SlotIndex);
    }

    [Fact]
    public void PlaceGardenModule_OccupiedSlotSwapsExistingPlacement()
    {
        var rules = Rules(purchaseCost: 0);
        var state = new GameStateData();
        state.GardenModules.Add(new GardenModuleData { Id = "a", StatId = "run", Level = 1, SlotIndex = 0 });
        state.GardenModules.Add(new GardenModuleData { Id = "b", StatId = "swim", Level = 1, SlotIndex = 1 });
        var useCase = new TrainingUseCase(rules);

        var result = useCase.PlaceGardenModule(state, "a", 1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, state.GardenModules[0].SlotIndex);
        Assert.Equal(0, state.GardenModules[1].SlotIndex);
    }

    [Fact]
    public void UpgradeGardenModule_RefreshesBoundCreatureRate()
    {
        var rules = Rules(levelRates: new[] { 1.0f, 2.0f, 3.0f }, upgradeCosts: new[] { 5, 10 });
        var creature = CreateCreature("v1");
        var state = new GameStateData { Coins = 20 };
        state.Voidlings.Add(creature);
        state.GardenModules.Add(new GardenModuleData { Id = "run-module", StatId = "run", Level = 1, SlotIndex = 0 });
        var useCase = new TrainingUseCase(rules);

        Assert.True(useCase.SetPassiveTraining(state, creature.Id, "run").Succeeded);
        Assert.Equal(1.0f, creature.PassiveTrainingPointsPerMinute);

        var upgrade = useCase.UpgradeGardenModule(state, "run-module");

        Assert.True(upgrade.Succeeded);
        Assert.Equal(2, state.GardenModules[0].Level);
        Assert.Equal(2.0f, creature.PassiveTrainingPointsPerMinute);
        Assert.Equal(15, state.Coins);
    }

    [Fact]
    public void PassiveTraining_UsesModuleRateAndPausesWhenModuleStored()
    {
        var rules = Rules(levelRates: new[] { 1.0f, 2.0f, 3.0f });
        var creature = CreateCreature("v1");
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        state.GardenModules.Add(new GardenModuleData { Id = "run-module", StatId = "run", Level = 2, SlotIndex = 0 });
        var useCase = new TrainingUseCase(rules);
        var passive = new PassiveTrainingService();

        Assert.True(useCase.SetPassiveTraining(state, creature.Id, "run").Succeeded);
        var activeStep = passive.Advance(creature, 60.0f, rules);
        Assert.Equal(2, activeStep.PointsGained);
        Assert.Equal(2, creature.TrainingPoints["run"]);

        Assert.True(useCase.PlaceGardenModule(state, "run-module", -1).Succeeded);
        Assert.Equal(0.0f, creature.PassiveTrainingPointsPerMinute);
        var pausedStep = passive.Advance(creature, 60.0f, rules);

        Assert.Equal(0, pausedStep.PointsGained);
        Assert.Equal(2, creature.TrainingPoints["run"]);
    }

    [Fact]
    public void SetPassiveTraining_ChoosesStrongestPlacedModuleForRequestedStat()
    {
        var rules = Rules();
        var creature = CreateCreature("v1");
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        state.GardenModules.Add(new GardenModuleData { Id = "weak", StatId = "run", Level = 1, SlotIndex = 0 });
        state.GardenModules.Add(new GardenModuleData { Id = "strong", StatId = "run", Level = 3, SlotIndex = 1 });
        var useCase = new TrainingUseCase(rules);

        var result = useCase.SetPassiveTraining(state, creature.Id, "run");

        Assert.True(result.Succeeded);
        Assert.Equal("strong", creature.PassiveTrainingModuleId);
        Assert.Equal(3.0f, creature.PassiveTrainingPointsPerMinute);
    }

    [Fact]
    public void Migration_PreservesLegacyDirectTrainingButNormalizesModuleAssignments()
    {
        var rules = Rules();
        var legacy = CreateCreature("legacy");
        legacy.PassiveTrainingStatId = "run";
        legacy.PassiveTrainingPointRemainder = 0.5;

        var moduleBacked = CreateCreature("module-backed");
        moduleBacked.PassiveTrainingStatId = "run";
        moduleBacked.PassiveTrainingModuleId = "run-module";
        moduleBacked.PassiveTrainingPointsPerMinute = 99.0f;

        var state = new GameStateData { SaveVersion = 18 };
        state.Voidlings.Add(legacy);
        state.Voidlings.Add(moduleBacked);
        state.GardenModules.Add(new GardenModuleData { Id = "run-module", StatId = "run", Level = 2, SlotIndex = 0 });

        new GameStateMigrationService(rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal("run", legacy.PassiveTrainingStatId);
        Assert.Equal(string.Empty, legacy.PassiveTrainingModuleId);
        Assert.Equal(0.5, legacy.PassiveTrainingPointRemainder, 5);
        Assert.Equal("run-module", moduleBacked.PassiveTrainingModuleId);
        Assert.Equal(2.0f, moduleBacked.PassiveTrainingPointsPerMinute);
    }

    private static VoidlingData CreateCreature(string id)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Stage = LifeStage.Adult,
            Genome = GeneticsService.CreateRandomGenome(123)
        };
        foreach (var statId in GameBalanceRules.DemoDefaults.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }

    private static GameBalanceRules Rules(
        int purchaseCost = 40,
        float[]? levelRates = null,
        int[]? upgradeCosts = null)
    {
        var defaults = GameBalanceRules.DemoDefaults;
        return defaults with
        {
            GardenModules = new GardenModuleRules(
                SlotCount: 4,
                PurchaseCost: purchaseCost,
                UpgradeCosts: Array.AsReadOnly(upgradeCosts ?? new[] { 5, 10 }),
                PointsPerMinuteByLevel: Array.AsReadOnly(levelRates ?? new[] { 1.0f, 2.0f, 3.0f }))
        };
    }
}
