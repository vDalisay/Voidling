using System;
using Voidling.Application.Shop;
using Voidling.Application.Training;

namespace VoidlingGame;

/// <summary>
/// Central player-facing wording for explicit local action failures. Application/Domain continue to
/// return typed failures; this presentation-facing mapper prevents stale UI actions from failing
/// silently while keeping raw enum/internal details out of the UI.
/// </summary>
public static class PlayerActionFailureText
{
    public const string MissingVoidling = "That Voidling is no longer in the Garden.";
    public const string MissingFailedEgg = "That failed egg is no longer in the Garden.";

    public static string ForShop(ShopFailure failure)
        => failure switch
        {
            ShopFailure.None => string.Empty,
            ShopFailure.EggNotFound => "That mystery egg is no longer available.",
            ShopFailure.EggShellNotFound => "That eggshell is no longer in your inventory.",
            ShopFailure.RareOfferNotFound => "That rare Shop offer is no longer available.",
            ShopFailure.UtilityItemNotOwned => "No incubation skips are available.",
            ShopFailure.EggNotIncubating => "Choose an egg that is still incubating.",
            ShopFailure.EggAlreadyReady => "That egg is already ready to hatch.",
            ShopFailure.NotEnoughCurrency => "Not enough sprouts.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };

    public static string ForTraining(TrainingFailure failure, string statLabel)
        => failure switch
        {
            TrainingFailure.None => string.Empty,
            TrainingFailure.UnknownStat => "That training option is unavailable.",
            TrainingFailure.CreatureNotFound => MissingVoidling,
            TrainingFailure.NotEnoughCurrency => "Not enough sprouts.",
            TrainingFailure.NoItemOwned => $"Buy a {statLabel} treat first.",
            TrainingFailure.StatAtCap => $"That Voidling's {statLabel} training is capped by its current DNA rank.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };

    public static string ForGardenModule(GardenModuleFailure failure)
        => failure switch
        {
            GardenModuleFailure.None => string.Empty,
            GardenModuleFailure.UnknownStat => "That Garden training option is unavailable.",
            GardenModuleFailure.DuplicateModuleId => "Could not create that land tile. Please try again.",
            GardenModuleFailure.ModuleNotFound => "That land tile is no longer available.",
            GardenModuleFailure.AlreadyPlaced => "That land tile is already part of the island.",
            GardenModuleFailure.DoesNotFit => "Land has to touch the island and cannot overlap another tile.",
            GardenModuleFailure.NotEnoughCurrency => "Not enough sprouts.",
            GardenModuleFailure.MaxLevel => "That land tile is already at its current maximum level.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };

    public static string ForPassiveTraining(PassiveTrainingFailure failure)
        => failure switch
        {
            PassiveTrainingFailure.None => string.Empty,
            PassiveTrainingFailure.UnknownStat => "That passive training option is unavailable.",
            PassiveTrainingFailure.CreatureNotFound => MissingVoidling,
            PassiveTrainingFailure.LandNotPlaced => "That land tile is not on the island yet.",
            PassiveTrainingFailure.LandFull => "That land tile is already taken. One Voidling trains per tile.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };
}
