using System;
using System.Collections.Generic;
using System.Text.Json;
using Voidling.Application.Breeding;
using Voidling.Application.Ports;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class BreedVoidlingsTransactionTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Execute_ReportsTypedAdultAndCooldownFailuresWithoutCreatingEgg()
    {
        var state = CreateBreedingState();
        state.Voidlings[0].Stage = LifeStage.Child;
        var useCase = new BreedVoidlingsUseCase(Rules);

        var childFailure = useCase.Execute(state, "a", "b", 10UL, "egg-a", 0, 0);
        Assert.Equal(BreedingFailure.ParentNotAdult, childFailure.Failure);
        Assert.Empty(state.OwnedEggs);

        state.Voidlings[0].Stage = LifeStage.Adult;
        state.Voidlings[1].BreedCooldownSeconds = 1.0f;
        var cooldownFailure = useCase.Execute(state, "a", "b", 11UL, "egg-b", 0, 0);
        Assert.Equal(BreedingFailure.ParentOnCooldown, cooldownFailure.Failure);
        Assert.Empty(state.OwnedEggs);
    }

    [Fact]
    public void Execute_RejectsEggIdAlreadyReservedByLineage()
    {
        var state = CreateBreedingState();
        state.LineageArchive.Add(new LineageArchiveEntry(
            "reserved-id", "Former Voidling", "", "", 0, "#FFFFFF", false));
        var useCase = new BreedVoidlingsUseCase(Rules);

        var result = useCase.Execute(state, "a", "b", 12UL, "reserved-id", 0, 0);

        Assert.Equal(BreedingFailure.DuplicateAssetId, result.Failure);
        Assert.Empty(state.OwnedEggs);
        Assert.Equal(0.0f, state.Voidlings[0].BreedCooldownSeconds);
        Assert.Equal(0.0f, state.Voidlings[1].BreedCooldownSeconds);
    }

    [Fact]
    public void PreviewAndExecute_RejectBreedingWhenGardenIsFull()
    {
        var state = CreateBreedingState();
        while (state.Voidlings.Count < Rules.Garden.MaxPopulation)
            state.Voidlings.Add(CreateAdult($"extra-{state.Voidlings.Count}", (ulong)state.Voidlings.Count));
        var useCase = new BreedVoidlingsUseCase(Rules);

        Assert.Equal(BreedingFailure.GardenFull, useCase.Preview(state, "a", "b").Failure);
        Assert.Equal(BreedingFailure.GardenFull, useCase.Execute(state, "a", "b", 12UL, "egg", 0, 0).Failure);
        Assert.Empty(state.OwnedEggs);
    }

    [Fact]
    public void ExecuteAndPersist_SavesFrozenEggAndCooldownsExactlyOnce()
    {
        var state = CreateBreedingState();
        var repository = new RecordingRepository();
        var useCase = new BreedVoidlingsUseCase(Rules);

        var result = useCase.ExecuteAndPersist(
            state, "a", "b", 777UL, "egg", 10, 20, repository);

        Assert.True(result.Succeeded);
        Assert.Equal(1, repository.SaveCount);
        Assert.NotNull(repository.LastSavedJson);
        var saved = JsonSerializer.Deserialize<GameStateData>(repository.LastSavedJson!)!;
        var savedEgg = Assert.Single(saved.OwnedEggs);
        Assert.Equal("egg", savedEgg.Id);
        Assert.Equal(777UL, savedEgg.Seed);
        Assert.Equal("a", savedEgg.ParentAId);
        Assert.Equal("b", savedEgg.ParentBId);
        Assert.Equal(Rules.Breeding.CooldownSeconds, saved.Voidlings[0].BreedCooldownSeconds);
        Assert.Equal(Rules.Breeding.CooldownSeconds, saved.Voidlings[1].BreedCooldownSeconds);
    }

    [Fact]
    public void ExecuteAndPersist_SaveFailureRollsBackEggCooldownsAndLineageMutation()
    {
        var state = CreateBreedingState();
        var originalArchive = new LineageArchiveEntry(
            "historic", "Historic", "", "", 0, "#FFFFFF", false);
        state.LineageArchive.Add(originalArchive);
        var repository = new ThrowingRepository();
        var useCase = new BreedVoidlingsUseCase(Rules);

        var result = useCase.ExecuteAndPersist(
            state, "a", "b", 888UL, "egg", 10, 20, repository);

        Assert.Equal(BreedingFailure.PersistenceFailed, result.Failure);
        Assert.False(result.Succeeded);
        Assert.Empty(state.OwnedEggs);
        Assert.Equal(0.0f, state.Voidlings[0].BreedCooldownSeconds);
        Assert.Equal(0.0f, state.Voidlings[1].BreedCooldownSeconds);
        var remaining = Assert.Single(state.LineageArchive);
        Assert.Equal(originalArchive, remaining);
    }

    [Fact]
    public void Execute_FreezesEggGenomeBeforeParentsCanChange()
    {
        var state = CreateBreedingState();
        var useCase = new BreedVoidlingsUseCase(Rules);
        var result = useCase.Execute(state, "a", "b", 999UL, "egg", 0, 0);
        Assert.True(result.Succeeded);
        var egg = result.Egg!;
        var frozenJson = JsonSerializer.Serialize(egg.Genome);

        foreach (var statId in Rules.Genetics.StatIds)
        {
            state.Voidlings[0].Genome.AbilityGenes[statId].AlleleA = 5;
            state.Voidlings[0].Genome.AbilityGenes[statId].AlleleB = 5;
            state.Voidlings[1].Genome.AbilityGenes[statId].AlleleA = 0;
            state.Voidlings[1].Genome.AbilityGenes[statId].AlleleB = 0;
        }

        Assert.Equal(frozenJson, JsonSerializer.Serialize(egg.Genome));
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

    private sealed class RecordingRepository : IGameStateRepository
    {
        public int SaveCount { get; private set; }
        public string? LastSavedJson { get; private set; }

        public GameStateData? Load() => null;

        public void Save(GameStateData state)
        {
            SaveCount++;
            LastSavedJson = JsonSerializer.Serialize(state);
        }
    }

    private sealed class ThrowingRepository : IGameStateRepository
    {
        public GameStateData? Load() => null;
        public void Save(GameStateData state) => throw new InvalidOperationException("disk unavailable");
    }
}
