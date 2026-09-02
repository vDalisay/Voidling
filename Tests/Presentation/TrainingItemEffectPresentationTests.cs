using System;
using System.Linq;
using Voidling.Application.Training;
using Voidling.Domain.Rules;
using Voidling.Presentation.UI.Shop;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class TrainingItemEffectPresentationTests
{
    [Fact]
    public void AdvertisedBaseGainRange_MatchesLiveTrainingUseCase()
    {
        var rules = GameBalanceRules.DemoDefaults;
        var training = new TrainingUseCase(rules);
        var observedMin = int.MaxValue;
        var observedMax = int.MinValue;

        for (ulong seed = 1; seed <= 512; seed++)
        {
            var state = CreateState();
            var result = training.ApplyTrainingItem(state, "runner", "run", seed);

            Assert.True(result.Succeeded);
            Assert.InRange(
                result.Gain,
                TrainingItemEffectPresentation.MinimumBaseGain,
                TrainingItemEffectPresentation.MaximumBaseGain);
            observedMin = Math.Min(observedMin, result.Gain);
            observedMax = Math.Max(observedMax, result.Gain);
        }

        Assert.Equal(TrainingItemEffectPresentation.MinimumBaseGain, observedMin);
        Assert.Equal(TrainingItemEffectPresentation.MaximumBaseGain, observedMax);
        Assert.Equal("+5-9", TrainingItemEffectPresentation.BaseEffectText);
    }

    private static GameStateData CreateState()
    {
        var state = new GameStateData();
        state.TrainingItems["run"] = 1;
        state.Voidlings.Add(new VoidlingData
        {
            Id = "runner",
            FavoriteFoodId = "swim",
            Genome = new GenomeData
            {
                AbilityGenes = GameBalanceRules.DemoDefaults.Genetics.StatIds.ToDictionary(
                    statId => statId,
                    _ => new GenePairData { AlleleA = 5, AlleleB = 5, ExpressedAlleleIndex = 0 },
                    StringComparer.Ordinal)
            },
            TrainingPoints = GameBalanceRules.DemoDefaults.Genetics.StatIds.ToDictionary(
                statId => statId,
                _ => 0,
                StringComparer.Ordinal)
        });
        return state;
    }
}
