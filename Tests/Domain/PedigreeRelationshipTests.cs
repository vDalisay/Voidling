using System.Collections.Generic;
using Voidling.Domain.Breeding;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class PedigreeRelationshipTests
{
    [Fact]
    public void RelationshipService_DetectsDirectParentChild()
    {
        var parent = Entry("parent", "", "", 0);
        var child = Entry("child", "parent", "other", 1);
        var other = Entry("other", "", "", 0);
        var service = new RelationshipService(maxAncestorDepth: 3);

        Assert.True(service.AreRelated(ToVoidling(parent), ToVoidling(child), new[] { parent, child, other }));
    }

    [Fact]
    public void RelationshipService_DetectsFullSiblings()
    {
        var parentA = Entry("parent-a", "", "", 0);
        var parentB = Entry("parent-b", "", "", 0);
        var first = Entry("first", parentA.CreatureId, parentB.CreatureId, 1);
        var second = Entry("second", parentA.CreatureId, parentB.CreatureId, 1);
        var service = new RelationshipService(maxAncestorDepth: 3);

        Assert.True(service.AreRelated(
            ToVoidling(first),
            ToVoidling(second),
            new[] { parentA, parentB, first, second }));
    }

    [Fact]
    public void RelationshipService_DetectsHalfSiblings()
    {
        var shared = Entry("shared", "", "", 0);
        var otherA = Entry("other-a", "", "", 0);
        var otherB = Entry("other-b", "", "", 0);
        var first = Entry("first", shared.CreatureId, otherA.CreatureId, 1);
        var second = Entry("second", shared.CreatureId, otherB.CreatureId, 1);
        var service = new RelationshipService(maxAncestorDepth: 3);

        Assert.True(service.AreRelated(
            ToVoidling(first),
            ToVoidling(second),
            new[] { shared, otherA, otherB, first, second }));
    }

    [Fact]
    public void RelationshipService_DetectsSharedAncestorWithinConfiguredDepth()
    {
        var founder = Entry("founder", "", "", 0);
        var branchA = Entry("branch-a", founder.CreatureId, "", 1);
        var branchB = Entry("branch-b", founder.CreatureId, "", 1);
        var first = Entry("first", branchA.CreatureId, "", 2);
        var second = Entry("second", branchB.CreatureId, "", 2);
        var service = new RelationshipService(maxAncestorDepth: 2);

        Assert.True(service.AreRelated(
            ToVoidling(first),
            ToVoidling(second),
            new[] { founder, branchA, branchB, first, second }));
    }

    [Fact]
    public void RelationshipService_DoesNotReachPastConfiguredAncestorDepth()
    {
        var founder = Entry("founder", "", "", 0);
        var a1 = Entry("a1", founder.CreatureId, "", 1);
        var b1 = Entry("b1", founder.CreatureId, "", 1);
        var a2 = Entry("a2", a1.CreatureId, "", 2);
        var b2 = Entry("b2", b1.CreatureId, "", 2);
        var first = Entry("first", a2.CreatureId, "", 3);
        var second = Entry("second", b2.CreatureId, "", 3);
        var service = new RelationshipService(maxAncestorDepth: 2);

        Assert.False(service.AreRelated(
            ToVoidling(first),
            ToVoidling(second),
            new[] { founder, a1, b1, a2, b2, first, second }));
    }

    private static LineageArchiveEntry Entry(string id, string parentA, string parentB, int generation)
        => new(id, id, parentA, parentB, generation, "#FFFFFF", false);

    private static VoidlingData ToVoidling(LineageArchiveEntry entry)
        => new()
        {
            Id = entry.CreatureId,
            Name = entry.DisplayName,
            ParentAId = entry.ParentAId,
            ParentBId = entry.ParentBId,
            FamilyGeneration = entry.FamilyGeneration
        };
}
