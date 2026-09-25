using Voidling.Presentation.Garden;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class GardenDecorationSizingTests
{
    [Theory]
    [InlineData(0.78f, GardenDecorationSize.Small)]
    [InlineData(1.0f, GardenDecorationSize.Medium)]
    [InlineData(1.22f, GardenDecorationSize.Large)]
    public void For_NamesTheCatalogueScales(float scale, GardenDecorationSize expected)
        => Assert.Equal(expected, GardenDecorationSizing.For(scale));

    [Fact]
    public void For_TreatsNearOneAsMedium()
    {
        Assert.Equal(GardenDecorationSize.Medium, GardenDecorationSizing.For(0.95f));
        Assert.Equal(GardenDecorationSize.Medium, GardenDecorationSizing.For(1.05f));
    }
}
