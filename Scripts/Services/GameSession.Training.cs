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
                SaveAndNotify($"Bought a {GameRules.StatDisplayNames[statId]} treat.");
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

        // Validate before allocating a persistent seed. Failed UI actions must not shift
        // later deterministic genetics/training/race streams.
        var failure = _training!.ValidateTrainingItem(State, creatureId, statId);
        if (failure == TrainingFailure.NoItemOwned)
        {
            ToastRequested?.Invoke($"Buy a {GameRules.StatDisplayNames[statId]} treat first.");
            return;
        }
        if (failure != TrainingFailure.None)
            return;

        var result = _training.ApplyTrainingItem(State, creatureId, statId, NextSeed());
        if (!result.Succeeded)
            return;

        SaveAndNotify($"{creature.Name} gained +{result.Gain} {GameRules.StatDisplayNames[statId]} training.");
    }
}
