using System;
using Godot;
using Voidling.Infrastructure.Persistence;
using Voidling.Presentation.Racing;
using VoidlingGame;
using Voidling.Presentation.Voidlings;

namespace Voidling.Bootstrap;

public partial class GameBootstrap
{
    public override void _EnterTree()
    {
        var args = OS.GetCmdlineUserArgs();

        if (Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-visual-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            AddChild(new VoidlingVisualSmokeProbe
            {
                Name = nameof(VoidlingVisualSmokeProbe)
            });
        }

        if (Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-race-presentation-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            AddChild(new RacePresentationSmokeProbe
            {
                Name = nameof(RacePresentationSmokeProbe)
            });
        }

        if (Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-race-completion-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            AddChild(new RaceCompletionSmokeProbe
            {
                Name = nameof(RaceCompletionSmokeProbe)
            });
        }

        if (Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-race-shots", StringComparison.OrdinalIgnoreCase)))
        {
            AddChild(new RaceTrackShotProbe
            {
                Name = nameof(RaceTrackShotProbe)
            });
        }

        if (Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-family-tree-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            AddChild(new FamilyTreeSmokeProbe
            {
                Name = nameof(FamilyTreeSmokeProbe)
            });
        }

        if (Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-persistence-recovery-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            AddChild(new PersistenceRecoverySmokeProbe
            {
                Name = nameof(PersistenceRecoverySmokeProbe)
            });
        }
    }
}
