using System;
using System.Linq;
using Voidling.Domain.Racing;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class CupCatalogTests
{
    [Fact]
    public void Catalog_HasStableUniqueCupAndNpcIdsWithValidCourses()
    {
        Assert.NotEmpty(CupCatalog.All);
        Assert.Equal(
            CupCatalog.All.Count,
            CupCatalog.All.Select(cup => cup.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var cup in CupCatalog.All)
        {
            Assert.True(RaceCourseCatalog.TryGet(cup.Course.Id, cup.Course.Version, out var resolved));
            Assert.Same(cup.Course, resolved);
            Assert.NotEmpty(cup.Cast);
            Assert.Equal(
                cup.Cast.Count,
                cup.Cast.Select(npc => npc.Id).Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void Catalog_PrerequisitesReferenceEarlierAuthoredCups()
    {
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var cup in CupCatalog.All)
        {
            if (!string.IsNullOrEmpty(cup.PrerequisiteCupId))
                Assert.Contains(cup.PrerequisiteCupId, seen);
            seen.Add(cup.Id);
        }
    }
}
