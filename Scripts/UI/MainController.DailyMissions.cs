using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Garden;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowDailyMissions()
    {
        var status = _session.GetDailyMissionStatus();
        var box = OpenModal(Tr("UI_GARDEN_MISSIONS"), new Vector2(438, 244), ShowGardenActivities, ScreenIcons.Missions);
        box.AddThemeConstantOverride("separation", 5);
        _modalHost.ShowWallet(WalletToShow);

        var screen = new DailyMissionsScreen();
        screen.Configure(status.Missions
            .Select(mission => new MissionViewState(
                mission.MissionId,
                Tr(DailyMissionTextKey(mission.MissionId)),
                mission.Progress,
                mission.Target,
                mission.CoinReward,
                mission.CanClaim,
                mission.Claimed))
            .ToArray());
        screen.ClaimRequested += missionId =>
        {
            var origin = screen.ClaimOrigin(missionId);
            var before = _session.State.Coins;
            if (!_session.ClaimDailyMission(missionId)) return;
            _walletShownBeforeReward = before;
            ShowDailyMissions();
            _walletShownBeforeReward = null;
            FlyRewardToWallet(origin, before, (int)(_session.State.Coins - before));
        };
        screen.BackRequested += ShowGardenActivities;
        box.AddChild(screen);
    }

    private static string DailyMissionTextKey(string missionId)
        => missionId switch
        {
            "pet-2" => "UI_MISSION_PET_2",
            "train-1" => "UI_MISSION_TRAIN_1",
            "breed-1" => "UI_MISSION_BREED_1",
            "hatch-1" => "UI_MISSION_HATCH_1",
            "race-1" => "UI_MISSION_RACE_1",
            "shop-1" => "UI_MISSION_SHOP_1",
            _ => "UI_MISSION_DEFAULT"
        };
}
