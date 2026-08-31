using Voidling.Application.Persistence;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class GameStateMigrationRobustnessTests
{
    [Fact]
    public void Normalize_RepairsMalformedCreatureCoreStateBeforeRuntimeUse()
    {
        var state = new GameStateData { SaveVersion = 18 };
        var creature = new VoidlingData
        {
            Id = "malformed",
            Name = "",
            Genome = null!,
            TrainingPoints = null!,
            Needs = null!,
            AgeSeconds = float.NaN,
            AdultAgeSeconds = float.PositiveInfinity,
            BreedCooldownSeconds = -5.0f,
            PassiveTrainingStatId = "unknown",
            PassiveTrainingPointRemainder = double.NaN
        };
        state.Voidlings.Add(creature);

        new GameStateMigrationService(GameBalanceRules.DemoDefaults).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.NotNull(creature.Genome);
        Assert.NotNull(creature.Genome.AbilityGenes);
        Assert.NotNull(creature.TrainingPoints);
        Assert.NotNull(creature.Needs);
        Assert.Equal("Voidling", creature.Name);
        Assert.Equal(0.0f, creature.AgeSeconds);
        Assert.Equal(0.0f, creature.AdultAgeSeconds);
        Assert.Equal(0.0f, creature.BreedCooldownSeconds);
        Assert.Equal(string.Empty, creature.PassiveTrainingStatId);
        Assert.Equal(0.0, creature.PassiveTrainingPointRemainder);
        foreach (var statId in GameBalanceRules.DemoDefaults.Genetics.StatIds)
        {
            Assert.True(creature.Genome.AbilityGenes.ContainsKey(statId));
            Assert.True(creature.TrainingPoints.ContainsKey(statId));
        }
    }
}
