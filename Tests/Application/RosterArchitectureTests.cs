using Voidling.Application.Roster;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class RosterArchitectureTests
{
    [Fact]
    public void Rename_SanitizesControlWhitespaceAndTrims()
    {
        var state = WithCreature("old");
        var roster = new VoidlingRosterUseCase();

        var result = roster.Rename(state, "creature", "  New\tName\nHere  ");

        Assert.True(result.Succeeded);
        Assert.True(result.Changed);
        Assert.Equal("New Name Here", result.Name);
        Assert.Equal("New Name Here", state.Voidlings[0].Name);
    }

    [Fact]
    public void Rename_EnforcesExistingEighteenCharacterLimit()
    {
        var state = WithCreature("old");
        var roster = new VoidlingRosterUseCase();

        var result = roster.Rename(state, "creature", "abcdefghijklmnopqr trailing");

        Assert.True(result.Succeeded);
        Assert.Equal(VoidlingRosterUseCase.MaxNameLength, result.Name.Length);
        Assert.Equal("abcdefghijklmnopqr", result.Name);
    }

    [Fact]
    public void Rename_EmptyNameFailsWithoutMutation()
    {
        var state = WithCreature("Pip");
        var result = new VoidlingRosterUseCase().Rename(state, "creature", " \n\t ");

        Assert.Equal(RenameFailure.EmptyName, result.Failure);
        Assert.False(result.Changed);
        Assert.Equal("Pip", state.Voidlings[0].Name);
    }

    [Fact]
    public void SayGoodbye_RemovesFromActiveButPreservesLineageLookup()
    {
        var state = WithCreature("Pip");
        var roster = new VoidlingRosterUseCase();

        var result = roster.SayGoodbye(state, "creature");

        Assert.True(result.Succeeded);
        Assert.Empty(state.Voidlings);
        Assert.Single(state.DepartedVoidlings);
        Assert.True(roster.IsDeparted(state, "creature"));
        Assert.Equal("Pip", roster.FindLineage(state, "creature")?.Name);
    }

    [Fact]
    public void DiscardFailedEgg_DoesNotRemoveIncubatingEgg()
    {
        var state = new GameStateData();
        var incubating = new EggData { Id = "egg", State = EggState.Incubating };
        state.OwnedEggs.Add(incubating);

        var removed = new VoidlingRosterUseCase().DiscardFailedEgg(state, "egg");

        Assert.False(removed);
        Assert.Same(incubating, Assert.Single(state.OwnedEggs));
    }

    [Fact]
    public void Move_OnlyChangesPersistedWorldPosition()
    {
        var state = WithCreature("Pip");
        var roster = new VoidlingRosterUseCase();

        Assert.True(roster.Move(state, "creature", 12.5f, 33.25f));
        Assert.Equal(12.5f, state.Voidlings[0].WorldX);
        Assert.Equal(33.25f, state.Voidlings[0].WorldY);
    }

    private static GameStateData WithCreature(string name)
    {
        var state = new GameStateData();
        state.Voidlings.Add(new VoidlingData
        {
            Id = "creature",
            Name = name,
            Stage = LifeStage.Adult
        });
        return state;
    }
}
