using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Racing;

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
            foreach (var name in new[] { "GardenStatus", "GardenUtilities", "GardenRail" })
                RequireOnScreen(_uiRoot.GetNode<Control>(name));
            foreach (var button in actions.GetChildren().OfType<Button>())
            {
                RequireOnScreen(button);
                if (button.Icon == null || !button.Icon.GetImage().GetUsedRect().HasArea())
                    throw new InvalidOperationException("Premium rail icon is missing or empty.");
                if (button.FocusMode != Control.FocusModeEnum.All)
                    throw new InvalidOperationException("Rail action is not keyboard accessible.");
            }
            FindGardenButton(actions, "Online");
            var resizeHandle = _uiRoot.GetNode<Control>("GardenRailResize");
            if (resizeHandle.MouseDefaultCursorShape != Control.CursorShape.Hsize || _railToggle.Visible)
                throw new InvalidOperationException("Expanded rail must use its resize edge without showing the collapse arrow.");
            HandleRailResizeInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            HandleRailResizeInput(new InputEventMouseMotion { Relative = new Vector2(24, 0) });
            HandleRailResizeInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            if (Mathf.Abs(_expandedRailWidth - 120) > 1 || Mathf.Abs(_dayNightDial.Position.X - 134) > 1 ||
                Mathf.Abs(_railResizeHandle.Position.X - 117) > 1)
                throw new InvalidOperationException("Dragging the rail edge did not resize the sidebar and adjacent Garden HUD.");
            var settingsButton = FindGardenButton(_railUtilities, "Settings");
            if (_session.State.MasterVolume <= 0.001f) _session.SetMasterVolume(1);
            var originalVolume = _session.State.MasterVolume;
            await ClickGardenControl(_muteButton);
            if (_session.State.MasterVolume > 0.001f) throw new InvalidOperationException("Rail mute did not mute audio.");
            await ClickGardenControl(_muteButton);
            if (Mathf.Abs(_session.State.MasterVolume - originalVolume) > 0.001f) throw new InvalidOperationException("Rail mute did not restore audio.");
            await ClickGardenControl(settingsButton);
            if (!_modalHost.IsOpen) throw new InvalidOperationException("Rail settings button did not open Settings.");
            await ClickGardenPosition(new Vector2(4, 4));
            if (_modalHost.IsOpen) throw new InvalidOperationException("Clicking outside a submenu did not return to the Garden.");
            RequireSeparate(rail, _gardenEventLog);
            await CaptureGardenUi("garden");

            RequireOnScreen(_dayNightDial);
            RequireSeparate(_dayNightDial, _uiRoot.GetNode<Control>("GardenStatus"));
            var arrow = (TextureRect)(_dayNightDial.FindChild("DayNightArrow", true, false)
                ?? throw new InvalidOperationException("Garden clock is missing its day/night arrow."));
            foreach (var hour in new[] { 0, 5, 9, 18, 22 })
            {
                var time = DateTime.Today.AddHours(hour);
                _dayNightDial.ShowTime(time);
                if (_dayNightDial.FindChildren("*", "Label", true, false).OfType<Label>().Single().Text != time.ToShortTimeString())
                    throw new InvalidOperationException("Garden clock does not show the user's local time.");
                await CaptureGardenUi("garden-" + hour);
            }
            var nightArrow = ((AtlasTexture)arrow.Texture).Region;
            _dayNightDial.ShowTime(DateTime.Today.AddHours(12));
            if (((AtlasTexture)arrow.Texture).Region == nightArrow)
                throw new InvalidOperationException("Garden clock arrow did not react to day and night.");
            _dayNightDial.ShowTime(_garden.EnvironmentLocalTime);
            HandleRailResizeInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            HandleRailResizeInput(new InputEventMouseMotion { Relative = new Vector2(-64, 0) });
            HandleRailResizeInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            RequireOnScreen(_railToggle);
            if (rail.Visible || !_gardenStatus.Visible || !_dayNightDial.Visible || !_gardenEventLog.Visible ||
                !_railToggle.HasFocus() || actions.GetChildren().OfType<Button>().Any(b => b.FocusMode != Control.FocusModeEnum.None))
                throw new InvalidOperationException("Collapsed navigation hid persistent Garden HUD or keyboard focus is lost.");
            foreach (var control in new Control[] { _dayNightDial, _gardenEventLog })
                if (Mathf.Abs(control.Position.X - 10) > 1)
                    throw new InvalidOperationException($"{control.Name} did not move into the free left-side space.");
            if (Mathf.Abs(_railToggle.Position.Y - (ScreenHeight - _railToggle.Size.Y) / 2) > 1)
                throw new InvalidOperationException("Navigation handle is not centered on the screen edge.");
            await CaptureGardenUi("garden-collapsed");
            await ClickGardenControl(_railToggle);
            SetGardenRailCollapsed(true);
            SetGardenRailCollapsed(false);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            RequireOnScreen(rail);
            if (!rail.Visible || !_gardenStatus.Visible || !_dayNightDial.Visible || !_gardenEventLog.Visible ||
                Mathf.Abs(_gardenStatus.Position.X - 510) > 1 ||
                Mathf.Abs(_dayNightDial.Position.X - (_expandedRailWidth + 14)) > 1 || Mathf.Abs(_dayNightDial.Position.Y - 10) > 1 ||
                Mathf.Abs(_gardenEventLog.Position.X - (_expandedRailWidth + 14)) > 1 || _railToggle.Visible ||
                actions.GetChildren().OfType<Button>().Any(b => b.FocusMode != Control.FocusModeEnum.All))
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

            var logToggle = FindGardenButton(_gardenEventLog, "ToggleHeight");
            var logBottom = _gardenEventLog.Position.Y + _gardenEventLog.Size.Y;
            if (Mathf.Abs(logBottom - (ScreenHeight - 10)) > 1)
                throw new InvalidOperationException("Expanded Garden log does not keep its screen margin.");
            await ClickGardenControl(logToggle);
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            if (!_gardenEventLog.IsCompact || Mathf.Abs(_gardenEventLog.Size.Y - 57) > 1 || history.Size.Y > 25 ||
                !history.GetParsedText().Contains(notification, StringComparison.Ordinal) ||
                history.GetParsedText().Trim().Contains('\n') ||
                Mathf.Abs(_gardenEventLog.Position.Y + _gardenEventLog.Size.Y - logBottom) > 1 || !logToggle.HasFocus())
                throw new InvalidOperationException("Garden log did not collapse to one readable line.");
            await CaptureGardenUi("garden-log-compact");
            await ClickGardenControl(logToggle);
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            if (_gardenEventLog.IsCompact || Mathf.Abs(_gardenEventLog.Size.Y - 80) > 1 ||
                Mathf.Abs(_gardenEventLog.Position.Y + _gardenEventLog.Size.Y - logBottom) > 1)
                throw new InvalidOperationException("Garden log did not expand again.");

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
            await CaptureGardenUi("companion");
            var inspector = _detailsPanel!;
            var expandedProfile = _session.CreateCreatureProfileProjection(inspector.CreatureId)! with
            {
                Name = "MMMMMMMMMMMMMMMMMM",
                DiscoveredFavoriteFoodId = GameRules.StatIds[0],
                CareDemeanor = Voidling.Application.Roster.CreatureCareDemeanor.NeedsCare,
                Stats = _session.CreateCreatureProfileProjection(inspector.CreatureId)!.Stats
                    .Select((stat, index) => index == 0
                        ? stat with { TrainingProgress = 0.42, TrainingPointsPerSecond = 0.05 }
                        : stat)
                    .ToArray()
            };
            inspector.Render(expandedProfile, GameRules.StatIds[0], true);
            await SettleGardenUi();
            RequireOnScreen(inspector);
            RequireSeparate(inspector, _saveStatusLabel);
            foreach (var stat in expandedProfile.Stats)
            {
                var progress = inspector.FindChild("Progress_" + stat.StatId, true, false) as ProgressBar;
                if (progress == null || Math.Abs(progress.Value - stat.TrainingProgress) > 0.001)
                    throw new InvalidOperationException($"Inspector did not render {stat.StatId} training progress.");
            }
            if (inspector.FindChild("StopTraining", true, false) != null)
                throw new InvalidOperationException("Inspector still renders the redundant passive-training row.");
            var activeRate = inspector.FindChild("Rate_" + GameRules.StatIds[0], true, false) as Label;
            if (activeRate is not { Visible: true } || !activeRate.Text.Contains("EXP/s", StringComparison.Ordinal))
                throw new InvalidOperationException("Inspector did not show the active training rate.");
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
            var inspectedHex = _session.State.GardenModules.FirstOrDefault(module => module.Placed && module.StatId.Length == 0)
                ?? _session.State.GardenModules.First(module => module.Placed);
            ShowLandHexMenu(inspectedHex.Id);
            await SettleGardenUi();
            if (_modalHost.IsOpen || _landInspector == null || !_landInspector.IsVisibleInTree())
                throw new InvalidOperationException("Hex selection did not open the non-blocking Garden inspector.");
            RequireOnScreen(_landInspector);
            await CaptureGardenUi("land-inspector");
            await ClickGardenControl(FindGardenButton(_landInspector, "CloseLandInspector"));
            if (_landInspector != null) throw new InvalidOperationException("Hex inspector did not close.");

            foreach (var button in actions.GetChildren().OfType<Button>().Skip(1))
            {
                button.GrabFocus();
                await ClickGardenControl(button);
                await SettleGardenUi();
                if (!_modalHost.IsOpen || _gardenEventLog.Visible)
                    throw new InvalidOperationException("Rail destination did not open an unobstructed modal.");
                RequireOnScreen(ModalPanel());
                if (button.Name == "Shop") await VerifyShopUi(rail);
                if (button.Name == "Races") await VerifyRaceEntryUi();
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

    private async Task VerifyShopUi(Control rail)
    {
        if (_session.State.StoreEggs.Count == 0) _session.RefillStoreEggs();
        _session.State.Coins = Math.Max(
            _session.State.Coins,
            GameRules.TrainingItemPrice + GameRules.StoreEggPrice +
            GameRules.GardenModuleRules.EmptyHexCost * 2 + 10);
        RenderShop();
        await SettleGardenUi();
        RequireSeparate(rail, ModalPanel());
        var ledger = _modalHost.FindChildren("ShopLedger", string.Empty, true, false).OfType<Voidling.Presentation.UI.Shop.ShopScreen>().Single();
        RequireOnScreen(ledger);
        foreach (var category in new[] { "Treats", "Eggs", "Land" })
            FindGardenButton(ledger, "Category" + category);
        if (ledger.FindChild("Categories", true, false) == null || ledger.FindChild("Catalogue", true, false) == null ||
            ledger.FindChild("Receipt", true, false) == null)
            throw new InvalidOperationException("Keeper ledger is missing a layout band.");
        FindGardenButton(ledger, "OpenInventory");
        var shade = _modalHost.GetChildren().OfType<ColorRect>().First(control => control.Color.A > 0);
        if (shade.GetGlobalRect() != new Rect2(Vector2.Zero, new Vector2(ScreenWidth, ScreenHeight)))
            throw new InvalidOperationException("Shop shade does not cover the full viewport.");

        var statId = GameRules.StatIds[0];
        var coins = _session.State.Coins;
        var owned = _session.State.TrainingItems[statId];
        await CaptureGardenUi("shop-treats");
        await ClickGardenControl(FindShopBuy(ledger));
        await SettleGardenUi();
        if (_session.State.Coins != coins - GameRules.TrainingItemPrice || _session.State.TrainingItems[statId] != owned + 1 || !_modalHost.IsOpen)
            throw new InvalidOperationException("Keeper ledger treat purchase did not use the existing transaction.");
        ledger = _modalHost.FindChildren("ShopLedger", string.Empty, true, false).OfType<Voidling.Presentation.UI.Shop.ShopScreen>().Single();
        if (!FindGardenButton(ledger, "CategoryTreats").ButtonPressed || !FindGardenButton(ledger, "Product_treat_run*").HasFocus())
            throw new InvalidOperationException("Shop selection or focus was not preserved after purchase.");

        var eggIds = _session.State.StoreEggs.Select(egg => egg.Id).ToArray();
        await ClickGardenControl(FindGardenButton(ledger, "CategoryEggs"));
        await CaptureGardenUi("shop-eggs");
        await ClickGardenControl(FindShopBuy(ledger));
        await SettleGardenUi();
        if (_session.State.StoreEggs.Count != eggIds.Length - 1 || _session.State.StoreEggs.Any(egg => egg.Id == eggIds[0]))
            throw new InvalidOperationException("Bought egg slot was refilled or changed identity during the Shop visit.");
        ledger = _modalHost.FindChildren("ShopLedger", string.Empty, true, false).OfType<Voidling.Presentation.UI.Shop.ShopScreen>().Single();
        if (!FindGardenButton(ledger, "CategoryEggs").ButtonPressed)
            throw new InvalidOperationException("Shop category was not preserved after egg purchase.");
        await ClickGardenControl(FindGardenButton(ledger, "CategoryLand"));
        await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
        await CaptureGardenUi("shop-land");

        var landCount = _session.State.GardenModules.Count;
        var landCoins = _session.State.Coins;
        await ClickGardenControl(FindShopBuy(ledger));
        await SettleGardenUi();
        if (_modalHost.IsOpen || !_garden.IsPlacingLand || !_landPurchaseActions.Visible ||
            _session.State.GardenModules.Count != landCount + 1 ||
            _session.State.Coins != landCoins - GameRules.GardenModuleRules.EmptyHexCost)
            throw new InvalidOperationException("Buying land did not move directly into placement mode.");
        RequireOnScreen(_landPurchaseActions);
        await ClickGardenControl(FindGardenButton(_landPurchaseActions, "CancelLandPurchase"));
        await SettleGardenUi();
        if (!_modalHost.IsOpen || _garden.IsPlacingLand || _session.State.GardenModules.Count != landCount ||
            _session.State.Coins != landCoins)
            throw new InvalidOperationException("Cancelling land placement did not refund and reopen the Shop.");

        ledger = _modalHost.FindChildren("ShopLedger", string.Empty, true, false).OfType<Voidling.Presentation.UI.Shop.ShopScreen>().Single();
        await ClickGardenControl(FindShopBuy(ledger));
        await SettleGardenUi();
        await ClickGardenControl(FindGardenButton(_landPurchaseActions, "PutLandInInventory"));
        await SettleGardenUi();
        if (!_modalHost.IsOpen || _garden.IsPlacingLand ||
            _session.State.GardenModules.Count(module => !module.Placed) == 0)
            throw new InvalidOperationException("Putting bought land in inventory did not reopen the Shop.");

        ledger = _modalHost.FindChildren("ShopLedger", string.Empty, true, false).OfType<Voidling.Presentation.UI.Shop.ShopScreen>().Single();
        await ClickGardenControl(FindShopBuy(ledger));
        await SettleGardenUi();
        var directions = new[]
        {
            (Q: 1, R: 0), (Q: 0, R: 1), (Q: -1, R: 1),
            (Q: -1, R: 0), (Q: 0, R: -1), (Q: 1, R: -1)
        };
        var target = _session.State.GardenModules.Where(module => module.Placed)
            .SelectMany(module => directions.Select(direction =>
                (Q: module.HexQ + direction.Q, R: module.HexR + direction.R)))
            .First(candidate => !_session.State.GardenModules.Any(module =>
                module.Placed && module.HexQ == candidate.Q && module.HexR == candidate.R));
        if (!_session.PlaceGardenModule(_shopLandPurchaseId, target.Q, target.R))
            throw new InvalidOperationException("Placement-delay probe could not place its land.");
        _garden.CancelLandPlacement();
        if (_modalHost.IsOpen)
            throw new InvalidOperationException("Shop reopened before the land placement animation.");
        await ToSignal(GetTree().CreateTimer(GardenController.LandPlacementAnimationSeconds + 0.1), SceneTreeTimer.SignalName.Timeout);
        if (_modalHost.IsOpen)
            throw new InvalidOperationException("Shop reopened without the post-animation delay.");
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        if (!_modalHost.IsOpen)
            throw new InvalidOperationException("Shop did not reopen after the placement animation delay.");
    }

    // Race entry is a three-step full-screen flow. The probe walks course -> racer -> confirm,
    // checking each step's bands and selections, and stops at Start rather than starting a race.
    private async Task VerifyRaceEntryUi()
    {
        var entry = FindRaceEntry();
        RequireOnScreen(entry);
        if (entry.Step != RaceEntryStep.Course)
            throw new InvalidOperationException("Race entry did not open on course select.");
        foreach (var band in new[] { "CourseList", "CourseRecord", "Minimap" })
        {
            if (entry.FindChild(band, true, false) == null)
                throw new InvalidOperationException($"Course select is missing the {band} band.");
        }

        var courses = RaceCourseCatalog.All.ToArray();
        var lastCourse = courses[^1];
        await ClickGardenControl(FindGardenButton(entry, $"Level_{lastCourse.Id}_{RaceEntryScreen.MaxLevel}"));
        await SettleGardenUi();
        entry = FindRaceEntry();
        if (!FindGardenButton(entry, "Course_" + lastCourse.Id).ButtonPressed ||
            !FindGardenButton(entry, $"Level_{lastCourse.Id}_{RaceEntryScreen.MaxLevel}").ButtonPressed ||
            courses.Take(courses.Length - 1).Any(course => FindGardenButton(entry, "Course_" + course.Id).ButtonPressed))
            throw new InvalidOperationException("Course select did not keep exactly one course and level.");
        await CaptureGardenUi("race-course-select");

        await ClickGardenControl(FindGardenButton(entry, "EntryPrimary"));
        await SettleGardenUi();
        entry = FindRaceEntry();
        if (entry.Step != RaceEntryStep.Racer)
            throw new InvalidOperationException("Course select did not advance to racer select.");
        foreach (var band in new[] { "RosterGrid", "RacerStats" })
        {
            if (entry.FindChild(band, true, false) == null)
                throw new InvalidOperationException($"Racer select is missing the {band} band.");
        }
        var grid = (GridContainer)entry.FindChild("RosterGrid", true, false);
        if (grid.Columns != 3 || grid.GetChildCount() != 9)
            throw new InvalidOperationException("Racer roster is not a 3x3 page.");
        FindGardenButton(entry, "RosterPrev");
        FindGardenButton(entry, "RosterNext");

        var racers = _session.State.Voidlings.ToArray();
        if (racers.Length > 1)
        {
            await ClickGardenControl(FindGardenButton(entry, "Racer_" + racers[1].Id));
            await SettleGardenUi();
            entry = FindRaceEntry();
            if (!FindGardenButton(entry, "Racer_" + racers[1].Id).ButtonPressed ||
                FindGardenButton(entry, "Racer_" + racers[0].Id).ButtonPressed)
                throw new InvalidOperationException("Racer select did not keep exactly one selected racer.");
            var stats = entry.FindChild("RacerStats", true, false);
            if (stats.FindChildren("*", "Label", true, false).OfType<Label>().All(label => label.Text != racers[1].Name))
                throw new InvalidOperationException("Racer stats did not follow the selected racer.");
            if (GameRules.StatIds.Any(statId => stats.FindChild("Progress_" + statId, true, false) == null))
                throw new InvalidOperationException("Racer stats are missing a per-stat bar.");
        }
        await CaptureGardenUi("race-racer-select");

        await ClickGardenControl(FindGardenButton(entry, "EntryPrimary"));
        await SettleGardenUi();
        entry = FindRaceEntry();
        if (entry.Step != RaceEntryStep.Confirm)
            throw new InvalidOperationException("Racer select did not advance to the confirmation step.");
        foreach (var band in new[] { "ConfirmCourse", "RacerStats", "Minimap" })
        {
            if (entry.FindChild(band, true, false) == null)
                throw new InvalidOperationException($"Confirmation is missing the {band} band.");
        }
        var start = FindGardenButton(entry, "EntryPrimary");
        start.GrabFocus();
        if (!start.HasFocus()) throw new InvalidOperationException("Race entry Start action is not focusable.");
        await CaptureGardenUi("race-confirm");

        // Back must unwind step by step rather than dropping the player out of the flow.
        await ClickGardenControl(FindGardenButton(entry, "EntryBack"));
        await SettleGardenUi();
        if (FindRaceEntry().Step != RaceEntryStep.Racer)
            throw new InvalidOperationException("Back did not return to racer select.");
        await ClickGardenControl(FindGardenButton(FindRaceEntry(), "EntryBack"));
        await SettleGardenUi();
        if (FindRaceEntry().Step != RaceEntryStep.Course)
            throw new InvalidOperationException("Back did not return to course select.");
    }

    private RaceEntryScreen FindRaceEntry()
        => _modalHost.FindChildren("RaceEntry", string.Empty, true, false)
            .OfType<RaceEntryScreen>().Single(screen => !screen.IsQueuedForDeletion());

    private static Button FindGardenButton(Node node, string name)
        => node.FindChildren(name, "Button", true, false).OfType<Button>().First(button => !button.IsQueuedForDeletion() && button.IsVisibleInTree());

    private static Button FindShopBuy(Node ledger)
        => ledger.FindChild("Receipt", true, false).FindChildren("*", "Button", true, false)
            .OfType<Button>().First(button => !button.IsQueuedForDeletion() && button.IsVisibleInTree());

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

    private async Task ClickGardenPosition(Vector2 position)
    {
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
