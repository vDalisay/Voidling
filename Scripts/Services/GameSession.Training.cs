using System;
using System.Linq;
using Voidling.Application.Garden;
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

    /// <summary>
    /// Puts a treat on the ground. Nothing is spent yet — the item leaves the satchel only when a
    /// Voidling eats it — so the ground may never hold more of one treat than the player owns, and
    /// a drop that survives a quit costs nothing.
    /// </summary>
    public string? DropTreat(string statId, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(statId))
            return null;

        var owned = State.TrainingItems.TryGetValue(statId, out var stock) ? stock : 0;
        if (owned <= DroppedTreatCount(statId))
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForTraining(TrainingFailure.NoItemOwned, DisplayStatId(statId)));
            return null;
        }

        var drop = new DroppedTreatData { Id = NewId(), StatId = statId, X = x, Y = y };
        State.DroppedTreats.Add(drop);
        SaveAndNotify($"Put a {DisplayStatId(statId)} treat on the ground.");
        return drop.Id;
    }

    /// <summary>How many treats of one stat are already lying on the ground unclaimed.</summary>
    public int DroppedTreatCount(string statId)
        => State.DroppedTreats.Count(drop => string.Equals(drop.StatId, statId, StringComparison.Ordinal));

    /// <summary>
    /// A Voidling reached a dropped treat. The drop is taken off the ground first so a second
    /// claim in the same frame, or a quit mid-animation, cannot spend the treat twice.
    /// </summary>
    public bool ClaimDroppedTreat(string dropId, string creatureId)
    {
        var drop = State.DroppedTreats.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, dropId, StringComparison.Ordinal));
        if (drop == null || !State.DroppedTreats.Remove(drop))
            return false;

        UseTrainingItem(creatureId, drop.StatId);
        return true;
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

    /// <summary>Starts a provisional land purchase; placement UI either commits or cancels it.</summary>
    public string? BuyLandShape(string shapeId)
    {
        var moduleId = NewId();
        var result = _training!.BuyLandShape(State, moduleId, shapeId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForGardenModule(result.Failure));
            return null;
        }

        Save();
        StateChanged?.Invoke();
        return moduleId;
    }

    public void CommitLandPurchase(string moduleId)
    {
        var module = State.GardenModules.Find(candidate => candidate.Id == moduleId);
        if (module == null)
            return;

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        SaveAndNotify("Bought a piece of land.");
        if (!module.Placed)
            RaiseGardenEvent("A new piece of land is waiting in your inventory.");
    }

    public bool CancelLandPurchase(string moduleId)
    {
        var result = _training!.CancelLandPurchase(State, moduleId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForGardenModule(result.Failure));
            return false;
        }

        SaveAndNotify("Land purchase cancelled.");
        return true;
    }

    public bool PlaceGardenModule(string moduleId, int hexQ, int hexR, int rotationSteps = 0)
    {
        var result = _training!.PlaceGardenModule(State, moduleId, hexQ, hexR, rotationSteps);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForGardenModule(result.Failure));
            return false;
        }
        if (!result.Changed)
            return true;

        const string message = "The island just got bigger.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    /// <summary>Builds training ground for one stat on a placed empty hex.</summary>
    public bool ConvertHexToTrainingGround(string moduleId, string statId)
    {
        var result = _training!.ConvertHexToTrainingGround(State, moduleId, statId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForGardenModule(result.Failure));
            return false;
        }

        var message = $"Built {DisplayStatId(statId)} training ground.";
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
