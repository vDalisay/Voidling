using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Garden;
using Voidling.Domain.Rules;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class GardenHexLayoutTests
{
    private static readonly GardenHexLayout Layout = GameBalanceRules.DemoDefaults.GardenModules.Hex;

    [Fact]
    public void HexCenter_RoundTripsBackToItsOwnCoordinate()
    {
        for (var q = -6; q <= 6; q++)
        {
            for (var r = -6; r <= 6; r++)
            {
                var (x, y) = Layout.CenterOf(q, r);
                Assert.Equal((q, r), Layout.At(x, y));

                // A point well inside the hex must still resolve to it, or the pointer would
                // snap to a neighbour anywhere but dead centre.
                Assert.Equal((q, r), Layout.At(x + Layout.TopEdgeWidth * 0.4f, y));
            }
        }
    }

    [Fact]
    public void EveryPointOfAHexResolvesToThatHex()
    {
        var (centerX, centerY) = Layout.CenterOf(2, -1);
        for (var angle = 0; angle < 360; angle += 15)
        {
            var radians = angle * System.MathF.PI / 180.0f;
            var x = centerX + System.MathF.Cos(radians) * Layout.InnerRadius * 0.9f;
            var y = centerY + System.MathF.Sin(radians) * Layout.InnerRadius * 0.9f;
            Assert.Equal((2, -1), Layout.At(x, y));
        }
    }

    [Fact]
    public void TileIsTwiceItsTopEdgeWideAndHoldsACircleInsideIt()
    {
        Assert.Equal(Layout.TopEdgeWidth * 2.0f, Layout.Width);

        // Neighbouring tiles touch without overlapping: one column step is 1.5 top edges across,
        // one row step is a full tile height down.
        var (originX, originY) = Layout.CenterOf(0, 0);
        var (rightX, rightY) = Layout.CenterOf(1, 0);
        Assert.Equal(Layout.TopEdgeWidth * 1.5f, rightX - originX, 3);
        Assert.Equal(Layout.Height * 0.5f, rightY - originY, 3);

        var (belowX, belowY) = Layout.CenterOf(0, 1);
        Assert.Equal(0.0f, belowX - originX, 3);
        Assert.Equal(Layout.Height, belowY - originY, 3);

        Assert.True(Layout.InnerRadius > 0.0f);
        Assert.True(Layout.InnerRadius <= Layout.Height * 0.5f);
    }

    /// <summary>A hex is big enough for a Voidling to live on: three of the old tiles wide.</summary>
    [Fact]
    public void OneHexIsThreeTimesTheOriginalTileAcross()
    {
        Assert.Equal(210.0f, Layout.Width, 3);
        Assert.Equal(180.0f, Layout.Height, 3);
    }

    [Fact]
    public void LandOnlyFitsWhereItTouchesTheIslandAndNothingIsUnderIt()
    {
        var placed = new HashSet<(int Q, int R)> { (0, 0) };
        bool Occupied(int q, int r) => placed.Contains((q, r));

        var single = GardenTileShape.Single.Cells;
        Assert.False(GardenHexLayout.CanPlaceShape(single, 0, 0, Occupied));
        Assert.True(GardenHexLayout.CanPlaceShape(single, 1, 0, Occupied));

        // Out at sea, with nothing to touch, is not a place the island can reach.
        Assert.False(GardenHexLayout.CanPlaceShape(single, 9, 9, Occupied));
    }

    [Fact]
    public void APieceFitsOnlyWhenEveryOneOfItsHexesDoes()
    {
        var placed = new HashSet<(int Q, int R)> { (0, 0), (2, 0) };
        bool Occupied(int q, int r) => placed.Contains((q, r));

        // The row of three would have to cover the hex that is already down at (2, 0).
        Assert.False(GardenHexLayout.CanPlaceShape(GardenTileShape.Line.Cells, 0, 0, Occupied));
        Assert.False(GardenHexLayout.CanPlaceShape(GardenTileShape.Line.Cells, 1, 0, Occupied));

        // Turned a sixth of a turn it clears both and still touches the island.
        Assert.True(GardenHexLayout.CanPlaceShape(GardenTileShape.Line.CellsRotated(1), 1, 0, Occupied));
    }

    [Fact]
    public void NeighboursSurroundTheTileWithoutRepeatingIt()
    {
        var neighbours = GardenHexLayout.NeighboursOf(3, -2).ToList();
        Assert.Equal(6, neighbours.Distinct().Count());
        Assert.DoesNotContain((3, -2), neighbours);
        Assert.All(neighbours, neighbour => Assert.Contains((3, -2), GardenHexLayout.NeighboursOf(neighbour.Q, neighbour.R)));
    }
}
