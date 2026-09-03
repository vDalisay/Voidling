using System;
using Voidling.Application.Training;
using Voidling.Domain.Rules;

namespace VoidlingGame;

public partial class GameSession
{
    public void BuyTrainingItem(string statId)
    {
        var result = _training!.BuyTrainingItem(State, statId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForTraining(result.Failure, DisplayStatId(statId)));
            return;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        SaveAndNotify($"Bought a {DisplayStatId(statId)} treat.");
    }

    public void UseTrainingItem(string creatureId, string statId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.MissingVoidling);
            return;
        }

        var failure = _training!.ValidateTrainingItem(State, creatureId, statId);
        if (failure != TrainingFailure.None)
        {
            var failureMessage = failure == TrainingFailure.StatAtCap
                ? $"{creature.Name}'s {DisplayStatId(statId)} training is capped by its current DNA rank."
                : PlayerActionFailureText.ForTraining(failure, DisplayStatId(statId));
            ToastRequested?.Invoke(failureMessage);
            return;
        }

        var result = _training.ApplyTrainingItem(State, creatureId, statId, NextSeed());
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForTraining(result.Failure, DisplayStatId(statId)));
            return;
        }

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
            ToastRequested?.Invoke(PlayerActionFailureText.ForGardenModule(result.Failure));
            return false;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        SaveAndNotify($"Bought a {DisplayStatId(statId)} land tile.");
        RaiseGardenEvent($"A {DisplayStatId(statId)} land tile is waiting in your inventory.");
        return true;
    }

    public bool PlaceGardenModule(string moduleId, int hexQ, int hexR)
    {
        var result = _training!.PlaceGardenModule(State, moduleId, hexQ, hexR);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForGardenModule(result.Failure));
            return false;
        }
        if (!result.Changed)
            return true;

        var module = State.GardenModules.Find(candidate => candidate.Id == moduleId);
        var message = $"Placed the {DisplayStatId(module?.StatId ?? string.Empty)} land tile.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    public bool UpgradeGardenModule(string moduleId)
    {
        var result = _training!.UpgradeGardenModule(State, moduleId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForGardenModule(result.Failure));
            return false;
        }

        var module = State.GardenModules.Find(candidate => candidate.Id == moduleId);
        SaveAndNotify($"Upgraded {DisplayStatId(module?.StatId ?? string.Empty)} module to level {module?.Level ?? 1}.");
        return true;
    }

    /// <summary>Drops a Voidling onto a placed land tile so it trains that tile's stat.</summary>
    public bool SetPassiveTrainingLand(string creatureId, string moduleId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.MissingVoidling);
            return false;
        }

        var result = _training!.SetPassiveTrainingLand(State, creatureId, moduleId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForPassiveTraining(result.Failure));
            return false;
        }
        if (!result.Changed)
            return true;

        var message = $"{creature.Name} started passive {DisplayStatId(result.StatId)} training.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    /// <summary>Whether one more Voidling still fits on a placed land tile.</summary>
    public bool HasRoomOnLand(string moduleId, string creatureId)
        => _training!.HasRoomFor(State, moduleId, creatureId);

    public bool StopPassiveTraining(string creatureId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.MissingVoidling);
            return false;
        }

        var result = _training!.StopPassiveTraining(State, creatureId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForPassiveTraining(result.Failure));
            return false;
        }
        if (!result.Changed)
            return true;

        var message = $"{creature.Name} stopped passive training.";
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
