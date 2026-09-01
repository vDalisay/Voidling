using System;
using Godot;
using Voidling.Infrastructure.Persistence;

namespace Voidling.Bootstrap;

public partial class GameBootstrap
{
    public override void _EnterTree()
    {
        var args = OS.GetCmdlineUserArgs();
        if (!Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-persistence-recovery-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        AddChild(new PersistenceRecoverySmokeProbe
        {
            Name = nameof(PersistenceRecoverySmokeProbe)
        });
    }
}
