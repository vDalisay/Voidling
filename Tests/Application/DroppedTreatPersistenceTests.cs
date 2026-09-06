using System.Collections.Generic;
using System.Text.Json;
using Voidling.Application.Garden;
using Voidling.Application.Persistence;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// A treat put on the ground is owned state, not a visual. It has to survive the same JSON round
/// trip the repository performs, and it must not spend the training item that is still in the
/// satchel until something eats it.
/// </summary>
public sealed class DroppedTreatPersistenceTests
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [Fact]
    public void DroppedTreats_SurviveTheRepositoryRoundTrip()
    {
        var state = new GameStateData();
        state.TrainingItems["run"] = 2;
        state.DroppedTreats.Add(new DroppedTreatData { Id = "drop", StatId = "run", X = 128.5f, Y = -64.25f });

        var reloaded = JsonSerializer.Deserialize<GameStateData>(
            JsonSerializer.Serialize(state, Options), Options);

        Assert.NotNull(reloaded);
        var drop = Assert.Single(reloaded!.DroppedTreats);
        Assert.Equal("drop", drop.Id);
        Assert.Equal("run", drop.StatId);
        Assert.Equal(128.5f, drop.X);
        Assert.Equal(-64.25f, drop.Y);
        // Still unspent: the treat leaves the satchel only when a Voidling eats it.
        Assert.Equal(2, reloaded.TrainingItems["run"]);
    }

    [Fact]
    public void SavesWrittenBeforeDroppedTreatsLoadWithBareGround()
    {
        var state = new GameStateData { DroppedTreats = null! };

        new GameStateMigrationService(GameBalanceRules.DemoDefaults).Normalize(state);

        Assert.NotNull(state.DroppedTreats);
        Assert.Empty(state.DroppedTreats);
    }

    [Fact]
    public void Migration_DropsTreatsThatLostTheirStat()
    {
        var state = new GameStateData
        {
            DroppedTreats = new List<DroppedTreatData>
            {
                new() { Id = "good", StatId = "run" },
                new() { Id = "blank", StatId = "" },
                null!
            }
        };

        new GameStateMigrationService(GameBalanceRules.DemoDefaults).Normalize(state);

        var kept = Assert.Single(state.DroppedTreats);
        Assert.Equal("good", kept.Id);
    }
}
