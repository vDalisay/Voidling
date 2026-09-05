using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace VoidlingGame;

public partial class MainController
{
    // Run with --voidling-garden-ui-smoke --voidling-dev-profile=cozy_ui.
    // Add --voidling-garden-ui-shots with a renderer to save the real screens for art review.
    private async void RunGardenUiSmoke()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(arg => arg.StartsWith("--voidling-dev-profile=", StringComparison.Ordinal)))
                throw new InvalidOperationException("Garden UI smoke requires an isolated development save profile.");
            SkipFirstLaunchTutorial();
            await SettleGardenUi();
            var rail = _uiRoot.GetNode<PanelContainer>("GardenRail");
            var actions = rail.GetNode<VBoxContainer>("Actions");
            foreach (var name in new[] { "GardenStatus", "GardenUtilities", "GardenRail", "GardenCamera" })
                RequireOnScreen(_uiRoot.GetNode<Control>(name));
            foreach (var button in actions.GetChildren().OfType<Button>())
            {
                RequireOnScreen(button);
                if (button.Icon == null || !button.Icon.GetImage().GetUsedRect().HasArea())
                    throw new InvalidOperationException("Premium rail icon is missing or empty.");
                if (button.FocusMode != Control.FocusModeEnum.All)
                    throw new InvalidOperationException("Rail action is not keyboard accessible.");
            }
            RequireSeparate(rail, _gardenEventLog);
            await CaptureGardenUi("garden");

            RequireOnScreen(_dayNightDial);
            RequireSeparate(_dayNightDial, _uiRoot.GetNode<Control>("GardenStatus"));
            foreach (var (hour, key) in new[] { (0, "NIGHT"), (5, "DAWN"), (9, "DAY"), (18, "DUSK"), (22, "NIGHT") })
            {
                _dayNightDial.ShowTime(DateTime.Today.AddHours(hour));
                if (_dayNightDial.FindChildren("*", "Label", true, false).OfType<Label>().Single().Text != Tr("UI_GARDEN_" + key))
                    throw new InvalidOperationException("Garden clock shows the wrong period.");
                await CaptureGardenUi("garden-" + key.ToLowerInvariant());
            }
            _dayNightDial.ShowTime(_garden.EnvironmentLocalTime);
            await ClickGardenControl(_railToggle);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            RequireOnScreen(_railToggle);
            if (rail.Visible || !_railToggle.HasFocus() || actions.GetChildren().OfType<Button>().Any(b => b.FocusMode != Control.FocusModeEnum.None))
                throw new InvalidOperationException("Collapsed navigation is still visible or keyboard focus is lost.");
            await CaptureGardenUi("garden-collapsed");
            await ClickGardenControl(_railToggle);
            ToggleGardenRail();
            ToggleGardenRail();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            RequireOnScreen(rail);
            if (!rail.Visible || actions.GetChildren().OfType<Button>().Any(b => b.FocusMode != Control.FocusModeEnum.All))
                throw new InvalidOperationException("Navigation did not recover from interrupted animation.");

            var history = _gardenEventLog.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single();
            const string notification = "Garden notification probe";
            ShowToast(notification);
            AppendGardenEvent(notification);
            await SettleGardenUi();
            if (history.GetParsedText().Split(notification).Length != 2)
                throw new InvalidOperationException("Garden notification was duplicated.");
            AppendGardenEvent(notification);
            ShowToast(notification);
            await SettleGardenUi();
            if (history.GetParsedText().Split(notification).Length != 3)
                throw new InvalidOperationException("A later notification was incorrectly suppressed.");

            await ClickGardenControl(_rosterButton);
            await SettleGardenUi();
            if (!_quickMenu.IsOpen) throw new InvalidOperationException("Roster did not open.");
            RequireOnScreen(_quickMenu);
            await CaptureGardenUi("roster");
            await PressGardenEscape();
            if (_quickMenu.IsOpen || _modalHost.IsOpen || !_rosterButton.HasFocus())
                throw new InvalidOperationException("Escape did not close roster and return rail focus.");
            OnQuickMenuVoidlingPicked(_session.State.Voidlings.First().Id);
            await SettleGardenUi();
            RequireOnScreen(_detailsPanel!);
            RequireSeparate(_detailsPanel!, rail);
            RequireSeparate(_detailsPanel!, _gardenEventLog);
            RequireSeparate(_detailsPanel!, _uiRoot.GetNode<Control>("GardenCamera"));
            await CaptureGardenUi("companion");
            var inspector = _detailsPanel!;
            var expandedProfile = _session.CreateCreatureProfileProjection(inspector.CreatureId)! with
            {
                Name = "MMMMMMMMMMMMMMMMMM",
                DiscoveredFavoriteFoodId = GameRules.StatIds[0],
                CareDemeanor = Voidling.Application.Roster.CreatureCareDemeanor.NeedsCare
            };
            inspector.Render(expandedProfile, GameRules.StatIds[0], true);
            await SettleGardenUi();
            RequireOnScreen(inspector);
            RequireSeparate(inspector, _saveStatusLabel);
            await CaptureGardenUi("companion-expanded");
            RefreshUi();
            await SettleGardenUi();
            var giveTreat = FindGardenButton(inspector, "GiveTreat");
            giveTreat.GrabFocus();
            for (var refresh = 0; refresh < 3; refresh++)
            {
                RefreshUi();
                await SettleGardenUi();
                if (_detailsPanel != inspector || !giveTreat.HasFocus())
                    throw new InvalidOperationException("Simulation refresh replaced inspector or lost keyboard focus.");
            }
            _session.BuyTrainingItem(GameRules.StatIds[0]);
            var stock = _session.State.TrainingItems[GameRules.StatIds[0]];
            giveTreat.EmitSignal(BaseButton.SignalName.Pressed);
            await SettleGardenUi();
            RequireOnScreen(ModalPanel());
            await CaptureGardenUi("treats");
            FindGardenButton(_modalHost, "Give_" + GameRules.StatIds[0]).EmitSignal(BaseButton.SignalName.Pressed);
            await SettleGardenUi();
            if (_session.State.TrainingItems[GameRules.StatIds[0]] != stock - 1 || _modalHost.IsOpen)
                throw new InvalidOperationException("Treat chooser did not consume one owned treat and return.");
            if (!giveTreat.HasFocus()) throw new InvalidOperationException("Treat action lost focus on return.");
            ShowDetails();
            await SettleGardenUi();
            RequireOnScreen(ModalPanel());
            FindGardenButton(_modalHost, "Goodbye");
            await CaptureGardenUi("details");
            CloseModal();
            OnQuickMenuVoidlingPicked(_session.State.Voidlings.Last().Id);
            await SettleGardenUi();
            if (_detailsPanel!.CreatureId != _session.State.Voidlings.Last().Id)
                throw new InvalidOperationException("Inspector retained previous creature.");
            DeselectVoidling();
            await SettleGardenUi();

            foreach (var button in actions.GetChildren().OfType<Button>().Skip(1))
            {
                button.GrabFocus();
                await ClickGardenControl(button);
                await SettleGardenUi();
                if (!_modalHost.IsOpen || _gardenEventLog.Visible)
                    throw new InvalidOperationException("Rail destination did not open an unobstructed modal.");
                RequireOnScreen(ModalPanel());
                await CaptureGardenUi($"menu-{button.Name}");
                CloseModal();
                await SettleGardenUi();
                if (!button.HasFocus()) throw new InvalidOperationException("Destination return lost rail focus.");
            }
            var decorationCount = _session.State.GardenDecorations.Count;
            _garden.BeginDecorationPlacement("tree");
            if (!_garden.IsPlacingDecoration) throw new InvalidOperationException("Decoration probe did not enter placement.");
            await ClickGardenControl(actions.GetNode<Button>("Build"));
            if (!_modalHost.IsOpen || _session.State.GardenDecorations.Count != decorationCount || !_garden.IsPlacingDecoration)
                throw new InvalidOperationException("Rail click placed a decoration through the UI.");
            CloseModal();
            await PressGardenEscape();
            if (_garden.IsPlacingDecoration || _modalHost.IsOpen)
                throw new InvalidOperationException("Escape did not cancel decoration placement before opening a menu.");
            ShowGardenBuild();
            await SettleGardenUi();
            FindGardenButton(_modalHost, "Land").EmitSignal(BaseButton.SignalName.Pressed);
            await SettleGardenUi();
            RequireOnScreen(ModalPanel());
            CloseModal();
            ShowGardenActivities();
            await SettleGardenUi();
            RequireOnScreen(ModalPanel());
            await CaptureGardenUi("activities");
            FindGardenButton(_modalHost, "Missions").EmitSignal(BaseButton.SignalName.Pressed);
            await SettleGardenUi();
            await PressGardenEscape();
            FindGardenButton(_modalHost, "Claim");
            CloseModal();
            await PressGardenEscape();
            FindGardenButton(_modalHost, "Resume");
            var modalControls = new System.Collections.Generic.List<Control>();
            CollectModalControls(_modalHost, modalControls);
            foreach (var control in modalControls)
            {
                if (!modalControls.Contains(control.GetNode<Control>(control.FocusNext)) ||
                    !modalControls.Contains(control.GetNode<Control>(control.FocusPrevious)))
                    throw new InvalidOperationException("Tab navigation escapes the modal.");
            }
            await CaptureGardenUi("garden-menu");
            var age = _session.State.Voidlings.First().AdultAgeSeconds;
            await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
            if (_session.State.Voidlings.First().AdultAgeSeconds <= age)
                throw new InvalidOperationException("Garden menu paused simulation.");
            FindGardenButton(_modalHost, "Settings").EmitSignal(BaseButton.SignalName.Pressed);
            await SettleGardenUi();
            RequireOnScreen(ModalPanel());
            await CaptureGardenUi("settings");
            await PressGardenEscape();
            FindGardenButton(_modalHost, "Resume");
            await PressGardenEscape();
            if (_modalHost.IsOpen || !_gardenEventLog.Visible)
                throw new InvalidOperationException("Escape did not return from menu to Garden.");
            // This probe rebuilds many Godot collections in a few frames. Finalize their native
            // wrappers while the engine is alive, before the standalone probe shuts it down.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GD.Print("GARDEN_UI_SMOKE_SUCCESS");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"GARDEN_UI_SMOKE_FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private PanelContainer ModalPanel()
        => _modalHost.GetChildren().OfType<CenterContainer>().Single(child => !child.IsQueuedForDeletion()).GetChild<PanelContainer>(0);

    private static Button FindGardenButton(Node node, string name)
        => node.FindChildren(name, "Button", true, false).OfType<Button>().First(button => !button.IsQueuedForDeletion() && button.IsVisibleInTree());

    private async Task PressGardenEscape()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        await SettleGardenUi();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false });
        await SettleGardenUi();
    }

    private async Task ClickGardenControl(Control control)
    {
        var position = control.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await SettleGardenUi();
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await SettleGardenUi();
    }

    private static void RequireSeparate(Control first, Control second)
    {
        if (first.GetGlobalRect().Intersects(second.GetGlobalRect()))
            throw new InvalidOperationException($"{first.Name} {first.GetGlobalRect()} overlaps {second.Name} {second.GetGlobalRect()}.");
    }

    private async Task SettleGardenUi()
    {
        for (var frame = 0; frame < 8; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void RequireOnScreen(Control control)
    {
        var rect = control.GetGlobalRect();
        if (rect.Position.X < -1 || rect.Position.Y < -1 || rect.End.X > ScreenWidth + 1 || rect.End.Y > ScreenHeight + 1)
        {
            foreach (var child in control.FindChildren("*", "Control", true, false).OfType<Control>())
                GD.Print($"LAYOUT {child.GetPath()}: min {child.GetCombinedMinimumSize()} size {child.Size}");
            throw new InvalidOperationException($"{control.Name} exceeds the viewport: {rect}");
        }
    }

    private async Task CaptureGardenUi(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--voidling-garden-ui-shots")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        const string directory = "res://.godot/cozy-ui-shots";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
        var result = GetViewport().GetTexture().GetImage().SavePng($"{directory}/{name}.png");
        if (result != Error.Ok) throw new InvalidOperationException($"Screenshot failed: {result}");
    }
}
