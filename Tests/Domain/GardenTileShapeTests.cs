using System.Linq;
using Voidling.Domain.Garden;
using Xunit;

namespace Voidling.Tests.Domain;

/// <summary>
/// The shop sells one to three connected hexes. Rotation has to keep a piece connected and keep it
/// anchored under the cursor, or placement would jump away from where the player is aiming.
/// </summary>
public sealed class GardenTileShapeTests
{
    [Fact]
    public void CatalogPiecesAreConnectedAndAtMostThreeHexes()
    {
        Assert.All(GardenTileShape.Catalog, shape =>
        {
            Assert.InRange(shape.HexCount, 1, 3);
            Assert.Equal(shape.HexCount, shape.Cells.Distinct().Count());
            Assert.Equal((0, 0), shape.Cells[0]);

            // Every cell after the first touches one that came before it.
            for (var index = 1; index < shape.Cells.Count; index++)
            {
                var neighbours = GardenHexLayout.NeighboursOf(shape.Cells[index].Q, shape.Cells[index].R);
                Assert.Contains(shape.Cells.Take(index), earlier => neighbours.Contains(earlier));
            }
        });
    }

    [Fact]
    public void RotationKeepsTheAnchorAndComesBackAfterSixTurns()
    {
        foreach (var shape in GardenTileShape.Catalog)
        {
            for (var steps = 0; steps < 6; steps++)
            {
                var rotated = shape.CellsRotated(steps);
                Assert.Equal((0, 0), rotated[0]);
                Assert.Equal(shape.HexCount, rotated.Distinct().Count());
            }

            Assert.Equal(shape.Cells, shape.CellsRotated(6));
            Assert.Equal(shape.CellsRotated(1), shape.CellsRotated(-5));
        }
    }

    [Fact]
    public void ThreeHexPiecesActuallyTurn()
    {
        Assert.Equal(1, GardenTileShape.Single.RotationCount);
        Assert.NotEqual(GardenTileShape.Line.Cells, GardenTileShape.Line.CellsRotated(1));
        Assert.NotEqual(GardenTileShape.Bend.Cells, GardenTileShape.Bend.CellsRotated(1));
    }
}
