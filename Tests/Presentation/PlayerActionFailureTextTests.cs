using System;
using Voidling.Application.Shop;
using Voidling.Application.Training;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class PlayerActionFailureTextTests
{
    [Fact]
    public void ShopFailures_AllHavePlayerFacingText()
    {
        foreach (var failure in Enum.GetValues<ShopFailure>())
        {
            if (failure == ShopFailure.None)
                continue;
            Assert.False(string.IsNullOrWhiteSpace(PlayerActionFailureText.ForShop(failure)));
        }
    }

    [Fact]
    public void TrainingFailures_AllHavePlayerFacingText()
    {
        foreach (var failure in Enum.GetValues<TrainingFailure>())
        {
            if (failure == TrainingFailure.None)
                continue;
            Assert.False(string.IsNullOrWhiteSpace(PlayerActionFailureText.ForTraining(failure, "Run")));
        }
    }

    [Fact]
    public void GardenModuleFailures_AllHavePlayerFacingText()
    {
        foreach (var failure in Enum.GetValues<GardenModuleFailure>())
        {
            if (failure == GardenModuleFailure.None)
                continue;
            Assert.False(string.IsNullOrWhiteSpace(PlayerActionFailureText.ForGardenModule(failure)));
        }
    }

    [Fact]
    public void PassiveTrainingFailures_AllHavePlayerFacingText()
    {
        foreach (var failure in Enum.GetValues<PassiveTrainingFailure>())
        {
            if (failure == PassiveTrainingFailure.None)
                continue;
            Assert.False(string.IsNullOrWhiteSpace(PlayerActionFailureText.ForPassiveTraining(failure, "Run")));
        }
    }
}
