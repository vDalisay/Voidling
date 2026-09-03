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

    [Fact]
    public void IslandTilesArePlaceableAndOccupiedTilesAreNot()
    {
        Assert.True(Layout.IsBaseIsland(0, 0));
        Assert.True(Layout.CanPlace(0, 0, (_, _) => false));
        Assert.False(Layout.CanPlace(0, 0, (q, r) => q == 0 && r == 0));
    }

    [Fact]
    public void LandOverWaterOnlyFitsWhenItTouchesExistingLand()
    {
        var farQ = FirstColumnOffIsland();
        Assert.False(Layout.IsBaseIsland(farQ, 0));

        // Nothing placed yet: the tile beyond the island edge still touches the island itself.
        Assert.True(Layout.CanPlace(farQ, 0, (_, _) => false));

        // One further out is orphaned until its neighbour is filled in.
        Assert.False(Layout.CanPlace(farQ + 1, 0, (_, _) => false));
        var placed = new HashSet<(int Q, int R)> { (farQ, 0) };
        Assert.True(Layout.CanPlace(farQ + 1, 0, (q, r) => placed.Contains((q, r))));
    }

    [Fact]
    public void NeighboursSurroundTheTileWithoutRepeatingIt()
    {
        var neighbours = GardenHexLayout.NeighboursOf(3, -2).ToList();
        Assert.Equal(6, neighbours.Distinct().Count());
        Assert.DoesNotContain((3, -2), neighbours);
        Assert.All(neighbours, neighbour => Assert.Contains((3, -2), GardenHexLayout.NeighboursOf(neighbour.Q, neighbour.R)));
    }

    private static int FirstColumnOffIsland()
    {
        var q = 0;
        while (Layout.IsBaseIsland(q, 0))
            q++;
        return q;
    }
}
