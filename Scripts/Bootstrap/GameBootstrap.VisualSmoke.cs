using System;
using Godot;
using Voidling.Presentation.Voidlings;

namespace Voidling.Bootstrap;

public partial class GameBootstrap
{
    public override void _EnterTree()
    {
        var args = OS.GetCmdlineUserArgs();
        if (!Array.Exists(args, arg =>
                string.Equals(arg, "--voidling-visual-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        AddChild(new VoidlingVisualSmokeProbe
        {
            Name = nameof(VoidlingVisualSmokeProbe)
        });
    }
}
