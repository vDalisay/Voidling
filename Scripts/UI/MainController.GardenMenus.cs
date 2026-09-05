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

    private void ShowGardenBuild()
    {
        var box = OpenModal(Tr("UI_GARDEN_BUILD"), new Vector2(290, 166));
        AddGardenMenuAction(box, "Land", "UI_GARDEN_BUILD_LAND", ShowGardenModules);
        AddGardenMenuAction(box, "Decorate", "UI_GARDEN_BUILD_DECORATE", ShowGardenDecorations);
        AddGardenMenuAction(box, "StoredLand", "UI_TOP_INVENTORY", ShowInventory);
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
