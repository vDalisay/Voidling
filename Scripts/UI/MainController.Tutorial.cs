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
                    Tr("UI_TUTORIAL_WELCOME"),
                    Tr("UI_TUTORIAL_START"),
                    true,
                    null);
                break;
            case FirstLaunchTutorialStep.SelectVoidling:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_SELECT_VOIDLING"),
                    Tr("UI_COMMON_NEXT"),
                    false,
                    new Rect2(110, 82, 348, 180));
                break;
            case FirstLaunchTutorialStep.SelectedVoidling:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_GARDEN_PROFILE"),
                    Tr("UI_TUTORIAL_OPEN_DETAILS"),
                    true,
                    new Rect2(468, 82, 162, 268));
                break;
            case FirstLaunchTutorialStep.Details:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_DETAILS"),
                    Tr("UI_COMMON_NEXT"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Shop:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_GARDEN_SHOP"),
                    Tr("UI_GARDEN_BUILD"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Modules:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_GARDEN_BUILD"),
                    Tr("UI_COMMON_NEXT"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Inventory:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_INVENTORY"),
                    Tr("UI_COMMON_NEXT"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Breeding:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_BREEDING"),
                    Tr("UI_COMMON_NEXT"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Race:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_RACE"),
                    Tr("UI_COMMON_NEXT"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Online:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_ONLINE"),
                    Tr("UI_COMMON_NEXT"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Settings:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_SETTINGS"),
                    Tr("UI_COMMON_NEXT"),
                    true,
                    modalHighlight);
                break;
            case FirstLaunchTutorialStep.Complete:
                _tutorialOverlay.ShowStep(
                    Tr("UI_TUTORIAL_COMPLETE"),
                    Tr("UI_TUTORIAL_FINISH"),
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
