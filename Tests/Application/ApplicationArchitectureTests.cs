using System.Collections.Generic;
using Voidling.Application.Breeding;
using Voidling.Application.Persistence;
using Voidling.Application.Training;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class ApplicationArchitectureTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Migration_NormalizesLegacySaveWithoutRerollingGenome()
    {
        var genome = new GenomeFactory(Rules.Genetics).CreateRandom(12345UL);
        var creature = new VoidlingData
        {
            Id = "legacy",
            Genome = genome,
            TrainingPoints = new Dictionary<string, int> { ["run"] = 7 },
            RareTraits = null!
        };
        var state = new GameStateData
        {
            SaveVersion = 3,
            MasterVolume = 0.25f,
            AutoFinishRaces = false,
            Voidlings = new List<VoidlingData> { creature },
            DepartedVoidlings = null!,
            OwnedEggs = null!,
            StoreEggs = null!,
            EggShells = null!,
            TrainingItems = null!
        };

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal(1.0f, state.MasterVolume);
        Assert.True(state.AutoFinishRaces);
        Assert.Same(genome, creature.Genome);
        Assert.Equal(7, creature.TrainingPoints["run"]);
        Assert.NotNull(creature.RareTraits);
        Assert.Empty(state.DepartedVoidlings);
        Assert.Empty(state.OwnedEggs);
        Assert.Empty(state.StoreEggs);
        Assert.Empty(state.EggShells);

        foreach (var statId in Rules.Genetics.StatIds)
        {
            Assert.True(state.TrainingItems.ContainsKey(statId));
            Assert.True(creature.TrainingPoints.ContainsKey(statId));
        }
    }

    [Fact]
    public void Migration_CurrentSavePreservesExistingSettings()
    {
        var state = new GameStateData
        {
            SaveVersion = GameStateMigrationService.CurrentSaveVersion,
            MasterVolume = 0.35f,
            AutoFinishRaces = false
        };

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(0.35f, state.MasterVolume);
        Assert.False(state.AutoFinishRaces);
    }

    [Fact]
    public void Training_FailedUseDoesNotMutateInventoryOrStats()
    {
        var creature = CreateAdult("trainee", 100UL);
        var state = new GameStateData { Coins = 0 };
        state.Voidlings.Add(creature);
        state.TrainingItems["run"] = 0;
        creature.TrainingPoints["run"] = 11;

        var result = new TrainingUseCase(Rules).ApplyTrainingItem(state, creature.Id, "run", 999UL);

        Assert.Equal(TrainingFailure.NoItemOwned, result.Failure);
        Assert.Equal(0, state.TrainingItems["run"]);
        Assert.Equal(11, creature.TrainingPoints["run"]);
    }

    [Fact]
    public void Training_SameSeedProducesSameGain()
    {
        var first = CreateTrainingState();
        var second = CreateTrainingState();
        var useCase = new TrainingUseCase(Rules);

        var firstResult = useCase.ApplyTrainingItem(first, "trainee", "run", 4242UL);
        var secondResult = useCase.ApplyTrainingItem(second, "trainee", "run", 4242UL);

        Assert.True(firstResult.Succeeded);
        Assert.Equal(firstResult.Gain, secondResult.Gain);
        Assert.Equal(first.Voidlings[0].TrainingPoints["run"], second.Voidlings[0].TrainingPoints["run"]);
    }

    [Fact]
    public void Breeding_SameParentsAndSeedProducesSamePersistableEgg()
    {
        var firstState = CreateBreedingState();
        var secondState = CreateBreedingState();
        var useCase = new BreedVoidlingsUseCase(Rules);

        var first = useCase.Execute(firstState, "a", "b", 777UL, "egg", 10, 20);
        var second = useCase.Execute(secondState, "a", "b", 777UL, "egg", 10, 20);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Egg);
        Assert.NotNull(second.Egg);
        Assert.Equal(first.Egg!.TintHex, second.Egg!.TintHex);
        Assert.Equal(first.Egg.IsViable, second.Egg.IsViable);
        Assert.Equal(first.Egg.InbreedingBurdenLevel, second.Egg.InbreedingBurdenLevel);
        Assert.Equal("a", first.Egg.ParentAId);
        Assert.Equal("b", first.Egg.ParentBId);

        foreach (var statId in Rules.Genetics.StatIds)
        {
            var firstGene = first.Egg.Genome.AbilityGenes[statId];
            var secondGene = second.Egg.Genome.AbilityGenes[statId];
            Assert.Equal(firstGene.AlleleA, secondGene.AlleleA);
            Assert.Equal(firstGene.AlleleB, secondGene.AlleleB);
            Assert.Equal(firstGene.ExpressedAlleleIndex, secondGene.ExpressedAlleleIndex);
        }
    }

    private static GameStateData CreateTrainingState()
    {
        var state = new GameStateData();
        state.Voidlings.Add(CreateAdult("trainee", 100UL));
        state.TrainingItems["run"] = 1;
        return state;
    }

    private static GameStateData CreateBreedingState()
    {
        var state = new GameStateData();
        state.Voidlings.Add(CreateAdult("a", 101UL));
        state.Voidlings.Add(CreateAdult("b", 202UL));
        return state;
    }

    private static VoidlingData CreateAdult(string id, ulong seed)
    {
        var data = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed)
        };

        foreach (var statId in Rules.Genetics.StatIds)
            data.TrainingPoints[statId] = 0;

        return data;
    }
}
