using Godot;
using Voidling.Presentation.UI.Tutorial;

namespace VoidlingGame;

public partial class MainController
{
    private enum FirstLaunchTutorialStep
    {
        None,
        Welcome,
        SelectVoidling,
        SelectedVoidling,
        Details,
        Shop,
        Modules,
        Inventory,
        Breeding,
        Race,
        Online,
        Settings,
        Complete
    }

    private FirstLaunchTutorialOverlay? _tutorialOverlay;
    private FirstLaunchTutorialStep _tutorialStep;

    private void StartFirstLaunchTutorialIfNeeded()
    {
        if (!_session.ShouldStartTutorial() || _tutorialOverlay != null)
            return;

        _tutorialOverlay = new FirstLaunchTutorialOverlay();
        _tutorialOverlay.ContinueRequested += AdvanceFirstLaunchTutorial;
        _tutorialOverlay.SkipRequested += SkipFirstLaunchTutorial;
        _uiRoot.AddChild(_tutorialOverlay);

        _tutorialStep = FirstLaunchTutorialStep.Welcome;
        RenderFirstLaunchTutorialStep();
    }

    private void OnTutorialVoidlingSelected()
    {
        if (_tutorialStep != FirstLaunchTutorialStep.SelectVoidling ||
            _tutorialOverlay == null ||
            string.IsNullOrWhiteSpace(_selectedId))
        {
            return;
        }

        _tutorialStep = FirstLaunchTutorialStep.SelectedVoidling;
        RenderFirstLaunchTutorialStep();
    }

    private void AdvanceFirstLaunchTutorial()
    {
        switch (_tutorialStep)
        {
            case FirstLaunchTutorialStep.Welcome:
                _tutorialStep = FirstLaunchTutorialStep.SelectVoidling;
                break;
            case FirstLaunchTutorialStep.SelectedVoidling:
                ShowDetails();
                _tutorialStep = FirstLaunchTutorialStep.Details;
                break;
            case FirstLaunchTutorialStep.Details:
                ShowShop();
                _tutorialStep = FirstLaunchTutorialStep.Shop;
                break;
            case FirstLaunchTutorialStep.Shop:
                ShowGardenModules();
                _tutorialStep = FirstLaunchTutorialStep.Modules;
                break;
            case FirstLaunchTutorialStep.Modules:
                ShowInventory();
                _tutorialStep = FirstLaunchTutorialStep.Inventory;
                break;
            case FirstLaunchTutorialStep.Inventory:
                ShowBreeding();
                _tutorialStep = FirstLaunchTutorialStep.Breeding;
                break;
            case FirstLaunchTutorialStep.Breeding:
                ShowRacePickerWithCourses();
                _tutorialStep = FirstLaunchTutorialStep.Race;
                break;
            case FirstLaunchTutorialStep.Race:
                ShowConnectedZone();
                _tutorialStep = FirstLaunchTutorialStep.Online;
                break;
            case FirstLaunchTutorialStep.Online:
                ShowSettingsExtended();
                _tutorialStep = FirstLaunchTutorialStep.Settings;
                break;
            case FirstLaunchTutorialStep.Settings:
                CloseModal();
                _tutorialStep = FirstLaunchTutorialStep.Complete;
                break;
            case FirstLaunchTutorialStep.Complete:
                FinishFirstLaunchTutorial();
                return;
            default:
                return;
        }

        RenderFirstLaunchTutorialStep();
    }

    private void RenderFirstLaunchTutorialStep()
    {
        if (_tutorialOverlay == null || !GodotObject.IsInstanceValid(_tutorialOverlay))
            return;

        var modalHighlight = new Rect2(38, 17, 564, 326);
        switch (_tutorialStep)
        {
            case FirstLaunchTutorialStep.Welcome:
                _tutorialOverlay.ShowStep(
                    "Welcome to your Garden. This short tour shows the main places you will use; you can skip it at any time.",
                    "Start",
                    true,
                    null);
                break;
            case FirstLaunchTutorialStep.SelectVoidling:
                _tutorialOverlay.ShowStep(
                    "Click any Voidling in the Garden. Selecting one opens its quick profile without pausing the world.",
                    "Next",
                    false,
                    new Rect2(12, 55, 382, 210));
                break;
            case FirstLaunchTutorialStep.SelectedVoidling:
                _tutorialOverlay.ShowStep(
                    "This side panel is the quick profile. Training, passive training and the deeper Details view all start here.",
                    "Open Details",
                    true,
                    new Rect2(399, 54, 237, 298));
                break;
            case FirstLaunchTutorialStep.Details:
                _tutorialOverlay.ShowStep(
                    "Details separates trained stats, DNA potential and inherited visual traits so you can inspect a Voidling before racing or breeding.",
                    "Next",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Shop:
                _tutorialOverlay.ShowStep(
                    "The Shop sells permanent training treats and rotating mystery eggs. Daily check-ins, missions and occasional rare offers also live here.",
                    "See Modules",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Modules:
                _tutorialOverlay.ShowStep(
                    "Garden Modules are your slow open-game training system. Buy a stat module, place it in a logical slot, upgrade it, then assign a Voidling from its quick profile.",
                    "Next",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Inventory:
                _tutorialOverlay.ShowStep(
                    "Inventory shows treats, eggs and eggshells. Rare convenience items such as an incubation skip are used here on the egg you choose.",
                    "Next",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Breeding:
                _tutorialOverlay.ShowStep(
                    "Breeding lets two adults create an egg. The preview warns about related pairings and hatch-failure risk before you commit.",
                    "Next",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Race:
                _tutorialOverlay.ShowStep(
                    "Race lets you choose a Voidling and course. Race outcomes use stats and deterministic simulation, not hidden personality bonuses.",
                    "Next",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Online:
                _tutorialOverlay.ShowStep(
                    "Online contains connected Gardens, challenges, friend races and trading when multiplayer transport is available.",
                    "Next",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Settings:
                _tutorialOverlay.ShowStep(
                    "Settings controls audio, camera behavior and race presentation. Opening menus does not stop the Garden simulation.",
                    "Next",
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Complete:
                _tutorialOverlay.ShowStep(
                    "That is the core loop: care for individuals, train them actively or through Modules, breed toward goals and race the results. The rest is yours to discover.",
                    "Finish",
                    true,
                    null);
                break;
        }
    }

    private void SkipFirstLaunchTutorial()
    {
        if (_modalHost.IsOpen)
            CloseModal();
        FinishFirstLaunchTutorial();
    }

    private void FinishFirstLaunchTutorial()
    {
        _session.CompleteTutorial();
        _tutorialStep = FirstLaunchTutorialStep.None;

        if (_tutorialOverlay != null && GodotObject.IsInstanceValid(_tutorialOverlay))
        {
            _tutorialOverlay.ContinueRequested -= AdvanceFirstLaunchTutorial;
            _tutorialOverlay.SkipRequested -= SkipFirstLaunchTutorial;
            _tutorialOverlay.QueueFree();
        }

        _tutorialOverlay = null;
    }
}
