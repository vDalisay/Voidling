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
            foreach (var name in new[] { "GardenStatus", "GardenUtilities", "GardenDock" })
                RequireOnScreen(_uiRoot.GetNode<Control>(name));
            var sampleIcon = _uiRoot.GetNode<PanelContainer>("GardenDock").GetChild<HBoxContainer>(0).GetChild<Button>(0).Icon;
            if (sampleIcon == null || !sampleIcon.GetImage().GetUsedRect().HasArea())
                throw new InvalidOperationException("Premium dock icon is missing or empty.");
            await CaptureGardenUi("garden");

            _quickMenu.GetChildren().OfType<Button>().Single().EmitSignal(BaseButton.SignalName.Pressed);
            await SettleGardenUi();
            if (!_quickMenu.IsOpen) throw new InvalidOperationException("Roster did not open.");
            await CaptureGardenUi("roster");
            OnQuickMenuVoidlingPicked(_session.State.Voidlings.First().Id);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            RequireOnScreen(_detailsPanel!);
            if (_quickMenu.Visible) throw new InvalidOperationException("Roster overlaps the inspector.");
            await CaptureGardenUi("companion");
            DeselectVoidling();

            var dock = _uiRoot.GetNode<PanelContainer>("GardenDock").GetChild<HBoxContainer>(0);
            foreach (var button in dock.GetChildren().OfType<Button>())
            {
                if (button.FocusMode != Control.FocusModeEnum.All)
                    throw new InvalidOperationException("Dock action is not keyboard accessible.");
                button.EmitSignal(BaseButton.SignalName.Pressed);
                await SettleGardenUi();
                if (!_modalHost.IsOpen || _gardenEventLog.Visible)
                    throw new InvalidOperationException("Dock action did not open an unobstructed modal.");
                var center = _modalHost.GetChildren().OfType<CenterContainer>().Single();
                RequireOnScreen(center.GetChild<PanelContainer>(0));
                await CaptureGardenUi($"menu-{button.GetIndex()}");
                CloseModal();
                await SettleGardenUi();
            }
            ShowSettingsExtended();
            await SettleGardenUi();
            RequireOnScreen(_modalHost.GetChildren().OfType<CenterContainer>().Single().GetChild<PanelContainer>(0));
            await CaptureGardenUi("settings");
            CloseModal();
            await SettleGardenUi();
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

    private async Task SettleGardenUi()
    {
        for (var frame = 0; frame < 8; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void RequireOnScreen(Control control)
    {
        var rect = control.GetGlobalRect();
        if (rect.Position.X < -1 || rect.Position.Y < -1 || rect.End.X > ScreenWidth + 1 || rect.End.Y > ScreenHeight + 1)
            throw new InvalidOperationException($"{control.Name} exceeds the viewport: {rect}");
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
