using System;
using Godot;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowGardenMenu()
    {
        var box = OpenModal(Tr("UI_GARDEN_MENU_TITLE"), new Vector2(270, 158));
        var note = UiFactory.CreateLabel(Tr("UI_GARDEN_MENU_RUNNING"), 9);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(note);
        var resume = AddGardenMenuAction(box, "Resume", "UI_GARDEN_RETURN", CloseModal);
        resume.CallDeferred(Control.MethodName.GrabFocus);
        AddGardenMenuAction(box, "Settings", "UI_TOP_SETTINGS", ShowSettingsExtended);
    }

    /// <summary>
    /// The Build chooser. Each destination carries the same premium icon it has on the rail, so
    /// the three entries read as places rather than as three identical buttons.
    /// </summary>
    private void ShowGardenBuild()
    {
        var box = OpenModal(Tr("UI_GARDEN_BUILD"), new Vector2(290, 166));
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
        button.AddThemeConstantOverride("icon_max_width", 16);
    }

    private void ShowGardenActivities()
    {
        var status = _session.GetDailyLoginStatus();
        var box = OpenModal(Tr("UI_GARDEN_ACTIVITIES"), new Vector2(330, 195));
        var summary = UiFactory.CreateLabel(string.Format(Tr("UI_GARDEN_CHECK_IN_STREAK"), status.CurrentStreak), 10);
        box.AddChild(summary);
        var claim = AddGardenMenuAction(box, "Claim", "UI_GARDEN_CHECK_IN_CLAIM", () =>
        {
            if (_session.ClaimDailyLogin()) ShowGardenActivities();
        });
        claim.Text = status.CanClaim
            ? string.Format(Tr("UI_GARDEN_CHECK_IN_CLAIM"), status.ClaimReward)
            : Tr("UI_GARDEN_CHECK_IN_CLAIMED");
        claim.Disabled = !status.CanClaim;
        var cycle = UiFactory.CreateLabel(string.Format(Tr("UI_GARDEN_CHECK_IN_NEXT"), status.NextReward), 9);
        box.AddChild(cycle);
        AddGardenMenuAction(box, "Missions", "UI_GARDEN_MISSIONS", ShowDailyMissions);
    }

    private static Button AddGardenMenuAction(VBoxContainer box, string name, string key, Action action)
    {
        var button = UiFactory.CreateButton(TranslationServer.Translate(key));
        button.Name = name;
        button.Pressed += action;
        box.AddChild(button);
        return button;
    }
}
