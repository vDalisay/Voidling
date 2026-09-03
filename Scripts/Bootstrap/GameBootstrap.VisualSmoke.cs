using System;
using Godot;
using Voidling.Infrastructure.Persistence;
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
                string.Equals(arg, "--voidling-persistence-recovery-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            AddChild(new PersistenceRecoverySmokeProbe
            {
                Name = nameof(PersistenceRecoverySmokeProbe)
            });
        }
    }
}
