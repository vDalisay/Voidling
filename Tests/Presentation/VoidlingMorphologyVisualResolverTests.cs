using Voidling.Domain.Evolution;
using Voidling.Presentation.Voidlings;
using Xunit;

namespace Voidling.Tests.Presentation;

public sealed class VoidlingMorphologyVisualResolverTests
{
    [Fact]
    public void Resolve_BlankMappingsPreserveCurrentVisualType()
    {
        var mapping = VoidlingMorphologyVisualMapping.Empty;

        Assert.Equal("normal", VoidlingMorphologyVisualResolver.Resolve(
            EvolutionSpecialization.Run,
            "normal",
            "normal",
            mapping));
        Assert.Equal("normal", VoidlingMorphologyVisualResolver.Resolve(
            EvolutionSpecialization.Swim,
            "normal",
            "normal",
            mapping));
    }

    [Fact]
    public void Resolve_UsesOnlyTheExplicitlyConfiguredSpecializationMapping()
    {
        var mapping = new VoidlingMorphologyVisualMapping(
            GeneralistVisualTypeId: string.Empty,
            RunVisualTypeId: " Run-Form ",
            SwimVisualTypeId: string.Empty,
            FlyVisualTypeId: string.Empty,
            PowerVisualTypeId: string.Empty);

        Assert.Equal("run-form", VoidlingMorphologyVisualResolver.Resolve(
            EvolutionSpecialization.Run,
            "normal",
            "normal",
            mapping));
        Assert.Equal("normal", VoidlingMorphologyVisualResolver.Resolve(
            EvolutionSpecialization.Swim,
            "normal",
            "normal",
            mapping));
    }

    [Fact]
    public void Resolve_NoneNeverSelectsAMorphologyMapping()
    {
        var mapping = new VoidlingMorphologyVisualMapping(
            GeneralistVisualTypeId: "generalist-form",
            RunVisualTypeId: "run-form",
            SwimVisualTypeId: "swim-form",
            FlyVisualTypeId: "fly-form",
            PowerVisualTypeId: "power-form");

        Assert.Equal("legacy-form", VoidlingMorphologyVisualResolver.Resolve(
            EvolutionSpecialization.None,
            " Legacy-Form ",
            "normal",
            mapping));
    }

    [Fact]
    public void Resolve_FallsBackToCatalogDefaultWhenCurrentVisualTypeIsMissing()
    {
        Assert.Equal("normal", VoidlingMorphologyVisualResolver.Resolve(
            EvolutionSpecialization.Power,
            fallbackVisualTypeId: null,
            defaultVisualTypeId: " Normal ",
            VoidlingMorphologyVisualMapping.Empty));
    }
}
