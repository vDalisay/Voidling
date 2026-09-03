using Godot;
using Voidling.Application.Daily;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowDailyMissions()
    {
        var status = _session.GetDailyMissionStatus();
        var box = OpenModal("Daily missions", new Vector2(438, 244));
        box.AddThemeConstantOverride("separation", 5);

        var intro = UiFactory.CreateLabel(
            "Complete today’s goals while raising your Voidlings. Rewards reset with your local calendar day.",
            6);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.CustomMinimumSize = new Vector2(398, 30);
        box.AddChild(intro);

        foreach (var mission in status.Missions)
            box.AddChild(CreateDailyMissionRow(mission));

        var back = UiFactory.CreateButton("Back to Shop");
        back.CustomMinimumSize = new Vector2(112, 22);
        back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        UiFactory.ApplyPixelFont(back, 7);
        back.Pressed += RenderShop;
        box.AddChild(back);
    }

    private Control CreateDailyMissionRow(DailyMissionView mission)
    {
        var panel = UiFactory.CreatePanel(new Vector2(398, 48));
        panel.CustomMinimumSize = new Vector2(398, 48);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 7);
        panel.AddChild(row);

        var textColumn = new VBoxContainer();
        textColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        textColumn.AddThemeConstantOverride("separation", 1);
        row.AddChild(textColumn);

        var description = UiFactory.CreateLabel(DailyMissionText(mission.MissionId), 7);
        description.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        textColumn.AddChild(description);

        var progress = UiFactory.CreateLabel(
            $"Progress {mission.Progress}/{mission.Target}  •  Reward +{mission.CoinReward} sprouts",
            6);
        progress.AddThemeColorOverride("font_color", Color.FromHtml("#786C5B"));
        textColumn.AddChild(progress);

        var claimText = mission.Claimed
            ? "Claimed"
            : mission.CanClaim
                ? $"Claim +{mission.CoinReward}"
                : $"{mission.Progress}/{mission.Target}";
        var claim = UiFactory.CreateButton(claimText);
        claim.CustomMinimumSize = new Vector2(94, 23);
        claim.Disabled = !mission.CanClaim;
        UiFactory.ApplyPixelFont(claim, 6);
        claim.Pressed += () =>
        {
            if (_session.ClaimDailyMission(mission.MissionId))
                ShowDailyMissions();
        };
        row.AddChild(claim);
        return panel;
    }

    private static string DailyMissionText(string missionId)
        => missionId switch
        {
            "pet-2" => "Pet a Voidling twice",
            "train-1" => "Use a training treat",
            "breed-1" => "Breed a new egg",
            "hatch-1" => "Hatch an egg",
            "race-1" => "Finish a standard race",
            "shop-1" => "Buy something from the shop",
            _ => "Daily Garden goal"
        };
}
