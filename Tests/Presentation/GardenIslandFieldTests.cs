using System;
using Voidling.Presentation.Garden.Atmosphere;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class GardenIslandFieldTests
{
    // A 40x40 square of land in the middle of a 200x200 sea.
    private static GardenIslandField SquareIsland(float cellSize = 2.0f, float maxDistance = 64.0f)
        => GardenIslandField.Build(
            (x, y) => x is >= 80.0f and < 120.0f && y is >= 80.0f and < 120.0f,
            0.0f,
            0.0f,
            200.0f,
            200.0f,
            cellSize,
            maxDistance);

    [Fact]
    public void Build_MarksLandAndWater()
    {
        var field = SquareIsland();

        Assert.True(field.IsLand(100.0f, 100.0f));
        Assert.False(field.IsLand(20.0f, 20.0f));
        Assert.False(field.IsLand(125.0f, 100.0f));
    }

    [Fact]
    public void SignedDistance_IsNegativeInlandAndPositiveOutToSea()
    {
        var field = SquareIsland();

        Assert.True(field.SignedDistance(100.0f, 100.0f) < -15.0f);
        Assert.True(field.SignedDistance(140.0f, 100.0f) > 15.0f);
    }

    [Theory]
    [InlineData(130.0f, 10.0f)]
    [InlineData(150.0f, 30.0f)]
    [InlineData(110.0f, -10.0f)]
    public void SignedDistance_MeasuresFromTheCoastInWorldPixels(float x, float expected)
    {
        var field = SquareIsland();

        Assert.InRange(field.SignedDistance(x, 100.0f), expected - 2.5f, expected + 2.5f);
    }

    [Fact]
    public void SignedDistance_IsClampedToTheRange()
    {
        var field = SquareIsland(maxDistance: 10.0f);

        Assert.Equal(10.0f, field.SignedDistance(5.0f, 5.0f));
        Assert.Equal(-10.0f, field.SignedDistance(100.0f, 100.0f), 3);
    }

    [Fact]
    public void SignedDistance_OutsideTheFieldIsOpenWater()
    {
        var field = SquareIsland();

        Assert.Equal(field.MaxDistance, field.SignedDistance(-500.0f, 900.0f));
        Assert.False(field.IsLand(-500.0f, 900.0f));
    }

    [Fact]
    public void EncodeBytes_PutsTheCoastAtMidGrey()
    {
        var field = SquareIsland(cellSize: 4.0f, maxDistance: 64.0f);
        var bytes = field.EncodeBytes();

        Assert.Equal(field.Columns * field.Rows, bytes.Length);
        // A cell just outside the coast, a cell well inside and a cell far out to sea.
        var coast = bytes[(100 / 4) * field.Columns + 120 / 4];
        var inland = bytes[(100 / 4) * field.Columns + 100 / 4];
        var sea = bytes[5 * field.Columns + 5];
        Assert.InRange(coast, 120, 140);
        Assert.True(inland < coast);
        Assert.True(sea > coast);
    }

    [Fact]
    public void Build_RejectsEmptyBounds()
        => Assert.Throws<ArgumentException>(() =>
            GardenIslandField.Build((_, _) => true, 10.0f, 10.0f, 10.0f, 20.0f, 1.0f, 8.0f));

    [Fact]
    public void Empty_IsAllWater()
    {
        Assert.False(GardenIslandField.Empty.IsLand(0.0f, 0.0f));
        Assert.True(GardenIslandField.Empty.SignedDistance(0.0f, 0.0f) > 0.0f);
    }
}
