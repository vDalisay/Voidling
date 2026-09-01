using System;
using Voidling.Application.Training;
using Voidling.Domain.Rules;

namespace VoidlingGame;

public partial class GameSession
{
    public void BuyTrainingItem(string statId)
    {
        var result = _training!.BuyTrainingItem(State, statId);
        switch (result.Failure)
        {
            case TrainingFailure.None:
                RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
                SaveAndNotify($"Bought a {DisplayStatId(statId)} treat.");
                break;
            case TrainingFailure.NotEnoughCurrency:
                ToastRequested?.Invoke("Not enough sprouts.");
                break;
        }
    }

    public void UseTrainingItem(string creatureId, string statId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return;

        var failure = _training!.ValidateTrainingItem(State, creatureId, statId);
        if (failure == TrainingFailure.NoItemOwned)
        {
            ToastRequested?.Invoke($"Buy a {DisplayStatId(statId)} treat first.");
            return;
        }
        if (failure == TrainingFailure.StatAtCap)
        {
            ToastRequested?.Invoke($"{creature.Name}'s {DisplayStatId(statId)} training is capped by its current DNA rank.");
            return;
        }
        if (failure != TrainingFailure.None)
            return;

        var result = _training.ApplyTrainingItem(State, creatureId, statId, NextSeed());
        if (!result.Succeeded)
            return;

        RecordDailyMissionEvent(DailyMissionEventKind.UseTrainingTreat);
        var message = result.FavoriteFoodDiscoveredNow
            ? $"{creature.Name} loved that {DisplayStatId(statId)} treat — favorite food discovered! +{result.Gain} training."
            : $"{creature.Name} gained +{result.Gain} {DisplayStatId(statId)} training.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
    }

    public bool BuyGardenModule(string statId)
    {
        var result = _training!.BuyGardenModule(State, NewId(), statId);
        if (!result.Succeeded)
        {
            if (result.Failure == GardenModuleFailure.NotEnoughCurrency)
                ToastRequested?.Invoke("Not enough sprouts.");
            return false;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        SaveAndNotify($"Bought a {DisplayStatId(statId)} Garden module.");
        RaiseGardenEvent($"A {DisplayStatId(statId)} training module was added to Garden storage.");
        return true;
    }

    public bool PlaceGardenModule(string moduleId, int slotIndex)
    {
        var result = _training!.PlaceGardenModule(State, moduleId, slotIndex);
        if (!result.Succeeded)
            return false;
        if (!result.Changed)
            return true;

        var module = State.GardenModules.Find(candidate => candidate.Id == moduleId);
        var message = slotIndex < 0
            ? $"Stored the {DisplayStatId(module?.StatId ?? string.Empty)} module."
            : $"Placed the {DisplayStatId(module?.StatId ?? string.Empty)} module in Garden slot {slotIndex + 1}.";
        SaveAndNotify(message);
        return true;
    }

    public bool UpgradeGardenModule(string moduleId)
    {
        var result = _training!.UpgradeGardenModule(State, moduleId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(result.Failure switch
            {
                GardenModuleFailure.NotEnoughCurrency => "Not enough sprouts.",
                GardenModuleFailure.MaxLevel => "That Garden module is already at its current maximum level.",
                _ => "Could not upgrade that Garden module."
            });
            return false;
        }

        var module = State.GardenModules.Find(candidate => candidate.Id == moduleId);
        SaveAndNotify($"Upgraded {DisplayStatId(module?.StatId ?? string.Empty)} module to level {module?.Level ?? 1}.");
        return true;
    }

    public bool SetPassiveTraining(string creatureId, string statId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return false;

        var result = _training!.SetPassiveTrainingFromPlacedModule(State, creatureId, statId);
        if (!result.Succeeded)
        {
            if (result.Failure == PassiveTrainingFailure.NoPlacedModule)
            {
                ToastRequested?.Invoke($"Place a {DisplayStatId(statId)} Garden module first.");
                StateChanged?.Invoke();
            }
            return false;
        }
        if (!result.Changed)
            return true;

        var message = string.IsNullOrEmpty(result.StatId)
            ? $"{creature.Name} stopped passive training."
            : $"{creature.Name} started passive {DisplayStatId(result.StatId)} training.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    private static string DisplayStatId(string statId)
    {
        if (string.IsNullOrEmpty(statId))
            return statId;

        return char.ToUpperInvariant(statId[0]) + statId[1..];
    }
}
