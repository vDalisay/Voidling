using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController
{
    private Control CreateDailyLoginPanel()
    {
        var status = _session.GetDailyLoginStatus();
        var panel = UiFactory.CreatePanel(new Vector2(518, 52));
        panel.CustomMinimumSize = new Vector2(518, 52);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        panel.AddChild(column);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        column.AddChild(row);

        var summary = UiFactory.CreateLabel(
            status.CanClaim
                ? $"DAILY CHECK-IN  •  streak {status.CurrentStreak}  •  today +{status.ClaimReward} sprouts"
                : $"DAILY CHECK-IN  •  streak {status.CurrentStreak}  •  claimed today",
            7);
        summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(summary);

        var claim = UiFactory.CreateButton(status.CanClaim ? $"Claim +{status.ClaimReward}" : "Claimed");
        claim.CustomMinimumSize = new Vector2(92, 21);
        UiFactory.ApplyPixelFont(claim, 7);
        claim.Disabled = !status.CanClaim;
        claim.Pressed += () =>
        {
            if (_session.ClaimDailyLogin())
                ShowShop();
        };
        row.AddChild(claim);

        var lowerRow = new HBoxContainer();
        lowerRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(lowerRow);

        var cycle = string.Join("  •  ", status.RewardCycle.Select(reward => $"+{reward}"));
        var upcoming = UiFactory.CreateLabel(
            $"Reward cycle: {cycle}    Next: +{status.NextReward}",
            6);
        upcoming.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        upcoming.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        upcoming.TooltipText = "Prototype reward values are balance data and may change before release.";
        lowerRow.AddChild(upcoming);

        var missions = UiFactory.CreateButton("Missions");
        missions.CustomMinimumSize = new Vector2(82, 19);
        UiFactory.ApplyPixelFont(missions, 6);
        missions.Pressed += ShowDailyMissions;
        lowerRow.AddChild(missions);

        return panel;
    }
}
