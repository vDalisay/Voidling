using System;
using Godot;

namespace VoidlingGame;

/// <summary>
/// Headless CI probe for family tree viewport geometry: how far the tree may be zoomed out, where
/// content sits once it is smaller than the viewport, and the sampler that walks a connection so the
/// inheritance-flow pulses travel along the drawn line instead of drifting off it.
/// </summary>
public partial class FamilyTreeSmokeProbe : Node
{
    public override void _Ready()
    {
        try
        {
            ValidateZoomOutReachesTheDocumentedFloor();
            ValidateSmallContentIsCentred();
            ValidateFlowPulsesFollowTheDrawnPath();
            ValidateBothParentsReadAsBoldWithTheirChild();

            GD.Print(
                "[family-tree-smoke] FAMILY_TREE_SMOKE_SUCCESS " +
                $"zoom={FamilyTreeView.MinZoom:0.##}..{FamilyTreeView.MaxZoom:0.##}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[family-tree-smoke] FAMILY_TREE_SMOKE_FAILED: {exception.Message}");
            GetTree().Quit(7);
        }
    }

    // A wide lineage is only readable if the view zooms out far enough to fit it. At the floor the
    // 425 px viewport must cover several thousand pixels of content.
    private static void ValidateZoomOutReachesTheDocumentedFloor()
    {
        if (FamilyTreeView.MinZoom >= 0.25f)
        {
            throw new InvalidOperationException(
                $"Family tree minimum zoom {FamilyTreeView.MinZoom} is too tight to survey a wide lineage.");
        }

        if (FamilyTreeView.MaxZoom <= 1.0f)
            throw new InvalidOperationException("Family tree must still zoom in past 1:1.");
    }

    // Once the scaled tree is smaller than the viewport it must centre rather than pin to the corner,
    // which is what zooming all the way out always produces.
    private static void ValidateSmallContentIsCentred()
    {
        const float viewport = 425.0f;

        var centred = FamilyTreeView.ClampAxis(offset: -900.0f, viewport, content: 125.0f);
        if (Math.Abs(centred - 150.0f) > 0.001f)
            throw new InvalidOperationException($"Content smaller than the viewport centred at {centred}, expected 150.");

        var pinned = FamilyTreeView.ClampAxis(offset: 400.0f, viewport, content: 1000.0f);
        if (Math.Abs(pinned) > 0.001f)
            throw new InvalidOperationException($"Oversized content must not pan past its leading edge (got {pinned}).");

        var clamped = FamilyTreeView.ClampAxis(offset: -5000.0f, viewport, content: 1000.0f);
        if (Math.Abs(clamped - (viewport - 1000.0f)) > 0.001f)
            throw new InvalidOperationException($"Oversized content must not pan past its trailing edge (got {clamped}).");
    }

    // The pulse sampler walks the real segment list, so a pulse at 0, midway and the end must land
    // on the corner points of an L-shaped parent-to-child connection.
    private static void ValidateFlowPulsesFollowTheDrawnPath()
    {
        var path = new FamilyTreeView.ConnectionPath { Key = "a>b", ParentId = "a", ChildId = "b" };
        path.Segments.Add(new FamilyTreeView.ConnectionSegment(new Vector2(0, 0), new Vector2(0, 40)));
        path.Segments.Add(new FamilyTreeView.ConnectionSegment(new Vector2(0, 40), new Vector2(60, 40)));

        RequirePoint(path, 0.0f, new Vector2(0, 0));
        RequirePoint(path, 40.0f, new Vector2(0, 40));
        RequirePoint(path, 70.0f, new Vector2(30, 40));
        RequirePoint(path, 100.0f, new Vector2(60, 40));

        if (FamilyTreeView.TryPointAlongPath(path, 140.0f, out _))
            throw new InvalidOperationException("Sampling past the end of a connection must not produce a point.");
    }

    // Selecting a child must emphasise the pairing that produced it, not just the child's own card.
    private void ValidateBothParentsReadAsBoldWithTheirChild()
    {
        var mother = Founder("mother", "Mother");
        var father = Founder("father", "Father");
        var child = Founder("child", "Child");
        child.ParentAId = mother.Id;
        child.ParentBId = father.Id;
        child.FamilyGeneration = 1;

        var view = new FamilyTreeView();
        AddChild(view);
        view.Build(child.Id, new[] { mother, father, child }, Array.Empty<VoidlingData>());

        var emphasised = view.EmphasisedMemberIds();
        foreach (var expected in new[] { child.Id, mother.Id, father.Id })
        {
            if (!emphasised.Contains(expected))
            {
                throw new InvalidOperationException(
                    $"Selecting '{child.Id}' should read '{expected}' as bold, but it was left unemphasised.");
            }
        }

        view.QueueFree();
    }

    private static VoidlingData Founder(string id, string name)
        => new() { Id = id, Name = name, Stage = LifeStage.Adult };

    private static void RequirePoint(FamilyTreeView.ConnectionPath path, float distance, Vector2 expected)
    {
        if (!FamilyTreeView.TryPointAlongPath(path, distance, out var point))
            throw new InvalidOperationException($"Connection flow sampler returned no point at {distance}.");
        if (point.DistanceTo(expected) > 0.001f)
            throw new InvalidOperationException($"Flow pulse at {distance} landed on {point}, expected {expected}.");
    }
}
