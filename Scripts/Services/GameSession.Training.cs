using System;
using Voidling.Application.Training;

namespace VoidlingGame;

public partial class GameSession
{
    public void BuyTrainingItem(string statId)
    {
        var result = _training!.BuyTrainingItem(State, statId);
        switch (result.Failure)
        {
            case TrainingFailure.None:
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

        var message = $"{creature.Name} gained +{result.Gain} {DisplayStatId(statId)} training.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
    }

    public bool SetPassiveTraining(string creatureId, string statId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return false;

        var result = _training!.SetPassiveTraining(State, creatureId, statId);
        if (!result.Succeeded)
            return false;
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
