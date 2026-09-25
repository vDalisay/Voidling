using System;
using System.Linq;
using Godot;

namespace VoidlingGame;

/// <summary>
/// Development review hooks for <see cref="GardenMenuShotProbe"/>: open any Garden menu by name so
/// every screen can be photographed without clicking through the whole flow. Nothing here runs in
/// normal play; each hook calls the same method the real button calls.
/// </summary>
public partial class MainController
{
    internal Control ReviewUiRoot => _uiRoot;

    internal bool ReviewModalOpen => _modalHost.IsOpen;

    internal Control ReviewModalHost => _modalHost;

    /// <summary>Skips the tutorial and pins the dial to one time, so shots differ only by design.</summary>
    internal void PrepareMenuReview(DateTime clock)
    {
        SkipFirstLaunchTutorial();
        _garden.EnvironmentTimeChanged -= _dayNightDial.ShowTime;
        _dayNightDial.ShowTime(clock);
        RefreshUi();
    }

    internal void ReviewSelect(string creatureId) => OnQuickMenuVoidlingPicked(creatureId);

    internal void ReviewCloseAll()
    {
        if (_modalHost.IsOpen) CloseModal();
        _quickMenu.Close();
        CloseLandInspector();
        if (_selectedId.Length > 0) DeselectVoidling();
    }

    internal void ReviewShowTutorial()
    {
        _session.State.TutorialCompleted = false;
        StartFirstLaunchTutorialIfNeeded();
    }

    internal void ReviewSkipTutorial() => SkipFirstLaunchTutorial();

    internal void ReviewEvent(string message) => AppendGardenEvent(message);

    internal void ReviewCollapseRail(bool collapsed) => SetGardenRailCollapsed(collapsed);

    /// <summary>Opens one Garden destination by review name. Returns false for an unknown name.</summary>
    internal bool ReviewOpen(string target)
    {
        switch (target)
        {
            case "roster": _quickMenu.Toggle(); return true;
            case "garden-menu": ShowGardenMenu(); return true;
            case "settings": ShowSettingsFromRail(); return true;
            case "build": ShowGardenBuild(); return true;
            case "land": ShowGardenModules(); return true;
            case "decorate": ShowGardenDecorations(); return true;
            case "activities": ShowGardenActivities(); return true;
            case "missions": ShowDailyMissions(); return true;
            case "journal": ShowEncyclopedia(); return true;
            case "inventory": ShowInventory(); return true;
            case "shop": ShowShop(); return true;
            case "breed": ShowBreeding(); return true;
            case "race": ShowRacePickerWithCourses(); return true;
            case "details": ShowDetails(); return true;
            case "family": ShowFamilyTree(); return true;
            case "treats": ShowTreatChooser(); return true;
            case "goodbye": ShowGoodbyeFirst(_selectedId); return true;
            case "reset": ShowResetConfirm(); return true;
            case "online": ShowConnectedZone(); return true;
            case "leaderboard": ShowFriendsLeaderboards(); return true;
            case "daily-race": ShowDailyRace(); return true;
            case "challenges": ShowChallenges(); return true;
            case "trades": ShowTrades(); return true;
            case "land-inspector":
                var hex = _session.State.GardenModules.FirstOrDefault(module => module.Placed && module.BiomeId.Length > 0)
                    ?? _session.State.GardenModules.First(module => module.Placed);
                ShowLandHexMenu(hex.Id);
                return true;
            default:
                return false;
        }
    }
}
