using Voidling.Application.Creatures;
using Voidling.Application.Persistence;
using Voidling.Application.Training;
using Voidling.Domain.Genetics;
using Voidling.Domain.Preferences;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class FavoriteFoodPreferenceTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Resolve_IsStableAndUsesOnlyAuthoredFoodIds()
    {
        var preferences = new FavoriteFoodPreferenceService();

        var first = preferences.Resolve("stable-creature", Rules.Genetics.StatIds);
        var second = preferences.Resolve("stable-creature", Rules.Genetics.StatIds);

        Assert.Equal(first, second);
        Assert.Contains(first, Rules.Genetics.StatIds);
    }

    [Fact]
    public void FavoriteTreat_DiscoversPreferenceAndAddsOnlyConfiguredBonus()
    {
        var rules = Rules with { FavoriteFood = new FavoriteFoodRules(BonusTrainingPoints: 2) };
        var favoriteState = CreateTrainingState("run");
        var ordinaryState = CreateTrainingState("swim");
        var training = new TrainingUseCase(rules);

        var favorite = training.ApplyTrainingItem(favoriteState, "trainee", "run", 4242UL);
        var ordinary = training.ApplyTrainingItem(ordinaryState, "trainee", "run", 4242UL);

        Assert.True(favorite.Succeeded);
        Assert.True(ordinary.Succeeded);
        Assert.True(favorite.WasFavoriteFood);
        Assert.True(favorite.FavoriteFoodDiscoveredNow);
        Assert.False(ordinary.WasFavoriteFood);
        Assert.False(ordinary.FavoriteFoodDiscoveredNow);
        Assert.Equal(ordinary.Gain + 2, favorite.Gain);
        Assert.True(favoriteState.Voidlings[0].FavoriteFoodDiscovered);
        Assert.False(ordinaryState.Voidlings[0].FavoriteFoodDiscovered);
    }

    [Fact]
    public void Profile_DoesNotRevealFavoriteUntilPlayerDiscoversIt()
    {
        var state = CreateTrainingState("run");
        var creature = state.Voidlings[0];
        var profiles = new VoidlingProfileProjectionService(Rules);

        var hidden = profiles.Create(state, creature.Id);
        creature.FavoriteFoodDiscovered = true;
        var discovered = profiles.Create(state, creature.Id);

        Assert.NotNull(hidden);
        Assert.Null(hidden!.DiscoveredFavoriteFoodId);
        Assert.NotNull(discovered);
        Assert.Equal("run", discovered!.DiscoveredFavoriteFoodId);
    }

    [Fact]
    public void Migration_AssignsMissingPreferenceWithoutMarkingItDiscovered()
    {
        var state = new GameStateData { SaveVersion = 19 };
        var creature = CreateAdult("legacy-favorite");
        creature.FavoriteFoodId = "not-a-food";
        creature.FavoriteFoodDiscovered = true;
        state.Voidlings.Add(creature);

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Contains(creature.FavoriteFoodId, Rules.Genetics.StatIds);
        Assert.False(creature.FavoriteFoodDiscovered);
    }

    private static GameStateData CreateTrainingState(string favoriteFoodId)
    {
        var state = new GameStateData();
        var creature = CreateAdult("trainee");
        creature.FavoriteFoodId = favoriteFoodId;
        creature.FavoriteFoodDiscovered = false;
        state.Voidlings.Add(creature);
        state.TrainingItems["run"] = 1;
        return state;
    }

    private static VoidlingData CreateAdult(string id)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(1234UL)
        };

        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }
}
