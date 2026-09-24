using System;
using Godot;

namespace Voidling.Bootstrap;

public partial class GameBootstrap
{
    // Headroom for the managed work Godot still does after this node leaves the tree: releasing
    // script instances and disposing every wrapper it tracks. Far more than that work allocates.
    private const long ShutdownAllocationBudgetBytes = 32L * 1024 * 1024;

    /// <summary>
    /// The autoload leaves the tree last: the rest of the game has already shut down, but Godot has
    /// not yet freed its native side. Godot collection wrappers that were never disposed, such as
    /// every <c>GetChildren()</c> result, are otherwise finalized on the .NET finalizer thread
    /// whenever a collection happens to run during engine teardown, and destroying one after Godot
    /// has freed its native state crashes the process on exit. So finalize everything that is
    /// already garbage while the engine is alive, then hold off further collections: Godot's own
    /// shutdown then disposes the remaining wrappers itself, in order.
    /// </summary>
    public override void _ExitTree()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        bool holdingCollections;
        try
        {
            holdingCollections = GC.TryStartNoGCRegion(ShutdownAllocationBudgetBytes);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or InvalidOperationException)
        {
            holdingCollections = false;
        }

        if (!holdingCollections)
            GD.PushWarning("Could not hold off garbage collection during shutdown; exit may still race engine teardown.");

        // Entering the region may itself have collected; finalize that too while the engine is alive.
        GC.WaitForPendingFinalizers();
    }
}
