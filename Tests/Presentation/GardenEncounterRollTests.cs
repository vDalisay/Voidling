using System;
using System.Linq;
using Voidling.Presentation.Voidlings;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class GardenEncounterRollTests
{
    [Fact]
    public void Pick_CoversEveryEncounterAcrossTheRollRange()
    {
        var picked = Enumerable.Range(0, 100)
            .Select(i => GardenEncounterRoll.Pick(i / 100.0f))
            .Distinct()
            .ToList();

        Assert.Equal(Enum.GetValues<GardenEncounterKind>().Length, picked.Count);
    }

    [Theory]
    [InlineData(0.0f, GardenEncounterKind.Greet)]
    [InlineData(0.44f, GardenEncounterKind.Greet)]
    [InlineData(0.45f, GardenEncounterKind.Chase)]
    [InlineData(0.77f, GardenEncounterKind.Chase)]
    [InlineData(0.78f, GardenEncounterKind.Pounce)]
    [InlineData(0.999f, GardenEncounterKind.Pounce)]
    public void Pick_MapsRollToWeightedEncounter(float roll, GardenEncounterKind expected)
        => Assert.Equal(expected, GardenEncounterRoll.Pick(roll));

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(-1.0f)]
    [InlineData(5.0f)]
    public void Pick_ClampsRollsOutsideTheUnitRange(float roll)
        => Assert.True(Enum.IsDefined(GardenEncounterRoll.Pick(roll)));
}
