using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Garden;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowGardenMenu()
    {
        var box = OpenModal(Tr("UI_GARDEN_MENU_TITLE"), new Vector2(270, 158), ScreenIcons.GardenMenu);
        var note = UiFactory.CreateLabel(Tr("UI_GARDEN_MENU_RUNNING"), 9);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(note);
        var resume = AddGardenMenuAction(box, "Resume", "UI_GARDEN_RETURN", CloseModal);
        UiFactory.ApplyPrimaryStyle(resume);
        resume.CallDeferred(Control.MethodName.GrabFocus);
        AddGardenMenuAction(box, "Settings", "UI_TOP_SETTINGS", ShowSettingsExtended);
    }

    /// <summary>
    /// The Build chooser. Each destination carries the same premium icon it has on the rail, so
    /// the three entries read as places rather than as three identical buttons.
    /// </summary>
    private void ShowGardenBuild()
    {
        var box = OpenModal(Tr("UI_GARDEN_BUILD"), new Vector2(290, 166), ScreenIcons.Build);
        BuildDestination(box, "Land", "UI_GARDEN_BUILD_LAND", new Vector2I(15, 1), ShowGardenModules);
        BuildDestination(box, "Decorate", "UI_GARDEN_BUILD_DECORATE", new Vector2I(12, 1), ShowGardenDecorations);
        BuildDestination(box, "StoredLand", "UI_GARDEN_BUILD_STORED", new Vector2I(13, 5), ShowInventory);
    }

    private static void BuildDestination(VBoxContainer box, string name, string key, Vector2I icon, Action action)
    {
        var button = AddGardenMenuAction(box, name, key, action);
        button.Icon = UiFactory.CreateGardenIcon(icon.X, icon.Y);
        button.ExpandIcon = false;
        button.IconAlignment = HorizontalAlignment.Left;
        button.Alignment = HorizontalAlignment.Left;
        button.CustomMinimumSize = new Vector2(0, 28);
        button.AddThemeConstantOverride("icon_max_width", 16);
        button.AddThemeConstantOverride("h_separation", 6);
    }

    /// <summary>
    /// Daily check-in and the way to the missions. Claiming redraws this window in place, with the
    /// purse still showing the old balance, and the reward flies from the Claim button into it.
    /// </summary>
    private void ShowGardenActivities()
    {
        var status = _session.GetDailyLoginStatus();
        var box = OpenModal(Tr("UI_GARDEN_ACTIVITIES"), new Vector2(330, 195), ScreenIcons.Activities);
        _modalHost.ShowWallet(WalletToShow);
        var screen = new ActivitiesScreen();
        screen.Configure(
            new CheckInViewState(status.CanClaim, status.CurrentStreak, status.ClaimReward, status.NextReward, status.RewardCycle),
            _session.GetDailyMissionStatus().Missions.Any(mission => mission.CanClaim));
        screen.ClaimRequested += () =>
        {
            var origin = screen.ClaimOrigin;
            var before = _session.State.Coins;
            if (!_session.ClaimDailyLogin()) return;
            _walletShownBeforeReward = before;
            ShowGardenActivities();
            _walletShownBeforeReward = null;
            FlyRewardToWallet(origin, before, (int)(_session.State.Coins - before));
        };
        screen.MissionsRequested += ShowDailyMissions;
        box.AddChild(screen);
    }

    private static Button AddGardenMenuAction(VBoxContainer box, string name, string key, Action action)
    {
        var button = UiFactory.CreateButton(TranslationServer.Translate(key));
        button.Name = name;
        button.CustomMinimumSize = new Vector2(0, 26);
        button.Pressed += action;
        box.AddChild(button);
        return button;
    }
}
