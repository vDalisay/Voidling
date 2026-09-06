using System;
using System.Collections.Generic;
using Godot;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Garden;
using Voidling.Presentation.UI.Multiplayer;
using Voidling.Presentation.UI.Shop;

using Voidling.Application.Persistence;

namespace VoidlingGame;

public partial class MainController : Node
{
    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;

    private GameSession _session = null!;
    private GardenController _garden = null!;
    private ConnectedZonePresentationBridge _connectedZoneBridge = null!;
    private FriendsLeaderboardPresentationBridge _friendsLeaderboardBridge = null!;
    private CanvasLayer _uiLayer = null!;
    private Control _uiRoot = null!;
    private ModalHost _modalHost = null!;
    private Label _coinsLabel = null!;
    private GardenInspector? _detailsPanel;
    private GardenEventLog _gardenEventLog = null!;
    private LineEdit _gardenNameField = null!;
    private readonly List<string> _pendingGardenMessages = new();
    private PanelContainer _gardenStatus = null!;
    private GardenDayNightDial _dayNightDial = null!;
    private PanelContainer _gardenRail = null!;
    private Button _railToggle = null!;
    private bool _railCollapsed;
    private Tween? _railTween;
    private string _selectedId = "";
    private RaceScreen? _race;
    private Action? _modalBack;
    private Control? _modalReturnFocus;
    private Button _rosterButton = null!;
    private string _shopCategory = ShopScreen.TreatsCategory;
    private string _shopSelection = string.Empty;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameBootstrap/GameSession");
        _connectedZoneBridge = GetNode<ConnectedZonePresentationBridge>(
            "/root/GameBootstrap/ConnectedZonePresentationBridge");
        _friendsLeaderboardBridge = GetNode<FriendsLeaderboardPresentationBridge>(
            "/root/GameBootstrap/FriendsLeaderboardPresentationBridge");
        _garden = GetNode<GardenController>("Garden");
        _garden.VoidlingSelected += OnVoidlingSelected;
        _connectedZoneBridge.StateChanged += OnConnectedZoneStateChanged;
        ComposeConnectedZoneGardenPresentation();

        _uiLayer = new CanvasLayer { Layer = 10 };
        AddChild(_uiLayer);

        _uiRoot = new Control { MouseFilter = Control.MouseFilterEnum.Pass };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_uiRoot);

        BuildTopBar();
        BuildGardenEventLog();
        BuildSaveFeedbackIndicator();

        _modalHost = new ModalHost { ZIndex = 100 };
        _uiRoot.AddChild(_modalHost);
        BuildQuickMenu();
        ComposeTradePresentation();
        ComposeChallengePresentation();

        _session.StateChanged += RefreshUi;
        _session.ToastRequested += ShowToast;
        _session.GardenEventRaised += AppendGardenEvent;
        _gardenEventLog.Append(Tr("UI_GARDEN_LOG_STARTED"));
        RefreshUi();
        Callable.From(StartFirstLaunchTutorialIfNeeded).CallDeferred();
        if (Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--voidling-garden-ui-smoke"))
            Callable.From(RunGardenUiSmoke).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_session))
        {
            _session.StateChanged -= RefreshUi;
            _session.ToastRequested -= ShowToast;
            _session.GardenEventRaised -= AppendGardenEvent;
        }

        if (GodotObject.IsInstanceValid(_connectedZoneBridge))
            _connectedZoneBridge.StateChanged -= OnConnectedZoneStateChanged;

        DetachSaveFeedbackIndicator();
        if (GodotObject.IsInstanceValid(_garden))
            _garden.EnvironmentTimeChanged -= _dayNightDial.ShowTime;
        DetachMultiplayerRacePresentation();
        DetachTradePresentation();
    }

    public override void _Input(InputEvent inputEvent)
    {
        // A running race owns Escape: it opens its own pause menu so the player can leave mid-race.
        if (_race != null || _multiplayerRaceScreen != null || _tradeExchangeScreen != null ||
            !inputEvent.IsActionPressed("ui_cancel"))
            return;

        if (_modalHost.IsOpen)
            NavigateModalBack();
        else if (_quickMenu.IsOpen)
        {
            _quickMenu.Close();
            _rosterButton.GrabFocus();
        }
        else if (_garden.IsPlacingEgg)
            _garden.CancelEggPlacement();
        else if (_garden.IsPlacingLand)
            _garden.CancelLandPlacement();
        else if (_garden.IsPlacingDecoration)
            _garden.CancelDecorationPlacement();
        else if (GetViewport().GuiGetFocusOwner() is LineEdit editing)
        {
            editing.Text = editing == _gardenNameField ? _session.State.GardenName : _session.FindVoidling(_selectedId)?.Name ?? editing.Text;
            editing.ReleaseFocus();
        }
        else
            ShowGardenMenu();
        GetViewport().SetInputAsHandled();
    }

    private void BuildTopBar()
    {
        _gardenStatus = UiFactory.CreatePanel(new Vector2(120, 50));
        _gardenStatus.Name = "GardenStatus";
        _gardenStatus.Position = new Vector2(110, 10);
        _gardenStatus.Size = new Vector2(120, 50);
        _uiRoot.AddChild(_gardenStatus);

        // Name over sprouts rather than side by side: the island is the player's to name, so the
        // name gets the top line and the wallet reads underneath it.
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 1);
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _gardenStatus.AddChild(column);

        _gardenNameField = BuildGardenNameField();
        column.AddChild(_gardenNameField);

        _coinsLabel = UiFactory.CreateLabel(string.Format(Tr("UI_TOP_SPROUTS"), 0), 8);
        _coinsLabel.VerticalAlignment = VerticalAlignment.Center;
        column.AddChild(_coinsLabel);

        _dayNightDial = new GardenDayNightDial { Position = new Vector2(110, 66) };
        _uiRoot.AddChild(_dayNightDial);
        _dayNightDial.ShowTime(_garden.EnvironmentLocalTime);
        _garden.EnvironmentTimeChanged += _dayNightDial.ShowTime;

        var utilities = new HBoxContainer { Name = "GardenUtilities", Position = new Vector2(514, 14) };
        utilities.AddThemeConstantOverride("separation", 6);
        _uiRoot.AddChild(utilities);
        AddTopButton(utilities, Tr("UI_TOP_ONLINE"), ShowConnectedZone, -1, 54);
        AddTopButton(utilities, Tr("UI_GARDEN_MENU_HINT"), ShowGardenMenu, -1, 54);

        var camera = UiFactory.CreateButton(Tr("UI_TOP_CENTER"));
        camera.Name = "GardenCamera";
        camera.Position = new Vector2(590, 50);
        camera.CustomMinimumSize = new Vector2(40, 20);
        UiFactory.ApplyPixelFont(camera, 8);
        camera.Pressed += _garden.ResetCamera;
        _uiRoot.AddChild(camera);

        var dock = UiFactory.CreatePanel(new Vector2(96, ScreenHeight), wood: true);
        _gardenRail = dock;
        dock.Name = "GardenRail";
        dock.Position = Vector2.Zero;
        dock.Size = new Vector2(96, ScreenHeight);
        _uiRoot.AddChild(dock);
        var actions = new VBoxContainer { Name = "Actions", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        actions.AddThemeConstantOverride("separation", 6);
        dock.AddChild(actions);
        var destinations = new (string name, string key, Action action, Vector2I glyph)[]
        {
            ("Voidlings", "UI_GARDEN_VOIDLINGS", () => _quickMenu.Toggle(), new(14, 5)),
            ("Inventory", "UI_TOP_INVENTORY", ShowInventory, new(13, 5)),
            ("Shop", "UI_TOP_SHOP", ShowShop, new(14, 6)),
            ("Breed", "UI_TOP_BREED", ShowBreeding, new(12, 4)),
            ("Races", "UI_TOP_RACE", ShowRacePickerWithCourses, new(13, 1)),
            ("Build", "UI_GARDEN_BUILD", ShowGardenBuild, new(15, 1))
        };
        foreach (var destination in destinations)
        {
            var button = UiFactory.CreateButton(Tr(destination.key));
            button.Name = destination.name;
            button.CustomMinimumSize = new Vector2(72, 27);
            button.Alignment = HorizontalAlignment.Left;
            UiFactory.ApplyPixelFont(button, 8);
            button.Icon = UiFactory.CreateGardenIcon(destination.glyph.X, destination.glyph.Y);
            button.ExpandIcon = false;
            button.IconAlignment = HorizontalAlignment.Left;
            button.AddThemeConstantOverride("icon_max_width", 10);
            button.Pressed += destination.action;
            actions.AddChild(button);
            if (destination.name == "Voidlings") _rosterButton = button;
        }
        _railToggle = UiFactory.CreateButton("‹");
        _railToggle.Name = "GardenRailToggle";
        _railToggle.Position = new Vector2(84, (ScreenHeight - 24) / 2);
        _railToggle.CustomMinimumSize = new Vector2(24, 24);
        _railToggle.ZIndex = 20;
        _railToggle.TooltipText = Tr("UI_GARDEN_HIDE_RAIL");
        _railToggle.Pressed += ToggleGardenRail;
        _uiRoot.AddChild(_railToggle);
    }

    private void ToggleGardenRail()
    {
        _railCollapsed = !_railCollapsed;
        _railTween?.Kill();
        _quickMenu.Close();
        _gardenRail.Visible = true;
        _gardenStatus.Visible = true;
        _dayNightDial.Visible = true;
        _gardenEventLog.Visible = true;
        foreach (var node in _gardenRail.FindChildren("*", "Button", true, false))
            ((Button)node).FocusMode = _railCollapsed ? Control.FocusModeEnum.None : Control.FocusModeEnum.All;
        _railToggle.Text = _railCollapsed ? "›" : "‹";
        _railToggle.TooltipText = Tr(_railCollapsed ? "UI_GARDEN_SHOW_RAIL" : "UI_GARDEN_HIDE_RAIL");
        _railToggle.GrabFocus();
        _railTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _railTween.TweenProperty(_gardenRail, "position:x", _railCollapsed ? -96f : 0f, 0.22);
        _railTween.TweenProperty(_railToggle, "position:x", _railCollapsed ? 4f : 84f, 0.22);
        _railTween.TweenProperty(_gardenStatus, "position:x", _railCollapsed ? 10f : 110f, 0.22);
        _railTween.TweenProperty(_dayNightDial, "position:x", _railCollapsed ? 10f : 110f, 0.22);
        _railTween.TweenProperty(_gardenEventLog, "position:x", _railCollapsed ? 10f : 110f, 0.22);
        _railTween.Finished += () =>
        {
            _gardenRail.Visible = !_railCollapsed;
            _gardenEventLog.Visible = !_modalHost.IsOpen;
        };
    }

    /// <summary>
    /// The island's name, edited in place. It is player-authored text, so it is never translated
    /// and the localized default only stands in while the player has not named the garden.
    /// </summary>
    private LineEdit BuildGardenNameField()
    {
        var field = new LineEdit
        {
            Text = _session.State.GardenName,
            PlaceholderText = Tr("UI_GARDEN_HOME"),
            MaxLength = GameStateMigrationService.GardenNameMaxLength,
            Alignment = HorizontalAlignment.Left,
            ExpandToTextLength = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = Tr("UI_GARDEN_NAME_HINT")
        };
        UiFactory.ApplyPixelFont(field, 12);
        field.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        field.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        field.AddThemeStyleboxOverride("read_only", new StyleBoxEmpty());
        field.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        field.AddThemeColorOverride("font_placeholder_color", Color.FromHtml("#3B5044"));
        field.AddThemeColorOverride("caret_color", Color.FromHtml("#3B5044"));
        field.TextSubmitted += name =>
        {
            _session.SetGardenName(name);
            field.ReleaseFocus();
        };
        field.FocusExited += () => _session.SetGardenName(field.Text);
        return field;
    }

    private static void AddTopButton(HBoxContainer row, string text, Action action, int iconIndex, float width)
    {
        var button = UiFactory.CreateButton(text, iconIndex);
        // Quiet utilities stay separate from the recurring destinations in the rail.
        button.Alignment = HorizontalAlignment.Center;
        button.CustomMinimumSize = new Vector2(width, 24);
        UiFactory.ApplyPixelFont(button, 7);
        button.Pressed += action;
        row.AddChild(button);
    }

    private void BuildGardenEventLog()
    {
        // Wide and tall enough to read the last handful of entries without scrolling.
        _gardenEventLog = new GardenEventLog
        {
            Position = new Vector2(110, 270),
            Size = new Vector2(300, 80),
            CustomMinimumSize = new Vector2(300, 45),
            ZIndex = 6
        };
        _gardenEventLog.ActivitiesRequested += ShowGardenActivities;
        _uiRoot.AddChild(_gardenEventLog);
    }

    private void RefreshUi()
    {
        _coinsLabel.Text = string.Format(Tr("UI_TOP_SPROUTS"), _session.State.Coins);
        if (!_gardenNameField.HasFocus() && !string.Equals(_gardenNameField.Text, _session.State.GardenName, StringComparison.Ordinal))
            _gardenNameField.Text = _session.State.GardenName;

        if (_selectedId.Length > 0 && _session.FindVoidling(_selectedId) == null)
            _selectedId = "";

        _garden.Select(_selectedId);
        RebuildDetailsPanel();
        RefreshConnectedZonePanel();
        RefreshQuickMenu();

        if (_gardenEventLog != null && GodotObject.IsInstanceValid(_gardenEventLog))
            _gardenEventLog.Visible = !_modalHost.IsOpen;

        if (_modalHost.IsOpen)
            HideGardenHudPanels();
    }

    private VBoxContainer OpenModal(string title, Vector2 size)
        => OpenModal(title, size, null, 0);

    private VBoxContainer OpenModal(string title, Vector2 size, Action? backRequested)
        => OpenModal(title, size, backRequested, 0);

    private VBoxContainer OpenOnlineModal(string title, Vector2 size, Action backRequested)
        => OpenModal(title, size, backRequested, 0);

    private VBoxContainer OpenRailModal(string title, Vector2 size)
        => OpenModal(title, size, null, 108);

    private VBoxContainer OpenModal(string title, Vector2 size, Action? backRequested, float leftInset)
    {
        if (_modalHost.IsOpen)
            CloseModal(false);
        else
            _modalReturnFocus = GetViewport().GuiGetFocusOwner();
        _modalBack = backRequested;
        var box = _modalHost.Open(title, size, NavigateModalBack, backRequested, leftInset);
        HideGardenHudPanels();
        Callable.From(() => FocusFirstModalControl(box)).CallDeferred();
        return box;
    }

    private void HideGardenHudPanels()
    {
        if (_quickMenu != null && GodotObject.IsInstanceValid(_quickMenu))
        {
            _quickMenu.Close();
            _quickMenu.Visible = false;
        }
        if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            _detailsPanel.Visible = false;
        if (_gardenEventLog != null && GodotObject.IsInstanceValid(_gardenEventLog))
            _gardenEventLog.Visible = false;
    }

    private void CloseModal() => CloseModal(true);

    private void CloseModal(bool restoreGardenHud)
    {
        _modalHost.Close();
        _modalBack = null;

        if (restoreGardenHud && _race == null && _multiplayerRaceScreen == null && _uiRoot != null && _uiRoot.Visible)
        {
            RefreshUi();
            if (_modalReturnFocus != null && GodotObject.IsInstanceValid(_modalReturnFocus) && _modalReturnFocus.IsVisibleInTree())
                _modalReturnFocus.GrabFocus();
            _modalReturnFocus = null;
        }
    }

    private void NavigateModalBack()
    {
        var back = _modalBack;
        if (back != null) back();
        else CloseModal();
    }

    private static void FocusFirstModalControl(Node node)
    {
        if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion()) return;
        var controls = new List<Control>();
        CollectModalControls(node, controls);
        for (var i = 0; i < controls.Count; i++)
        {
            controls[i].FocusNext = controls[(i + 1) % controls.Count].GetPath();
            controls[i].FocusPrevious = controls[(i + controls.Count - 1) % controls.Count].GetPath();
        }
        if (controls.Count > 0) controls[0].GrabFocus();
    }

    private static void CollectModalControls(Node node, List<Control> controls)
    {
        foreach (var child in node.GetChildren())
        {
            if (child.IsQueuedForDeletion()) continue;
            if (child is Control control && control.IsVisibleInTree() && control.FocusMode == Control.FocusModeEnum.All &&
                child is not BaseButton { Disabled: true }) controls.Add(control);
            CollectModalControls(child, controls);
        }
    }

    private void StartRace(VoidlingData selected)
    {
        var entry = _session.CreateRaceEntryFor(selected.Id);
        var autoFinish = _session.State.AutoFinishRaces;

        _garden.SetGameplayActive(false);
        _garden.Visible = false;
        _uiRoot.Visible = false;

        var race = new RaceScreen();
        race.Configure(entry, autoFinish);
        race.RaceCompleted += OnRaceCompleted;
        race.ReturnRequested += EndRace;
        _race = race;
        AddChild(race);
    }

    private void OnRaceCompleted(int placement)
    {
        _gardenEventLog.Append(string.Format(Tr("UI_GARDEN_LOG_RACE_RESULT"), placement));
        _session.ApplyRacePlacementReward(placement);

        if (_race != null &&
            GodotObject.IsInstanceValid(_race) &&
            _race.TryGetPlayerFinishMilliseconds(out var finishedMilliseconds))
        {
            ProjectSinglePlayerCourseBestTime(finishedMilliseconds);
        }
    }

    private void EndRace()
    {
        if (_race != null && GodotObject.IsInstanceValid(_race))
        {
            _race.RaceCompleted -= OnRaceCompleted;
            _race.ReturnRequested -= EndRace;
            _race.QueueFree();
        }
        _race = null;

        _garden.Visible = true;
        _garden.SetGameplayActive(true);
        _uiRoot.Visible = true;
        RefreshUi();
    }

    private void OnVoidlingSelected(string creatureId)
    {
        if (_selectedId != creatureId)
            _garden.StopFollowing();

        _selectedId = creatureId;
        RefreshUi();
        OnTutorialVoidlingSelected();
    }

    private void DeselectVoidling()
    {
        _selectedId = "";
        _garden.ClearSelection();
        _garden.StopFollowing();
        RefreshUi();
    }

    private void ShowToast(string text)
        => AppendGardenEvent(text);

    private void AppendGardenEvent(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _pendingGardenMessages.Contains(text)) return;
        _pendingGardenMessages.Add(text);
        if (_pendingGardenMessages.Count != 1) return;
        Callable.From(() =>
        {
            foreach (var message in _pendingGardenMessages) _gardenEventLog.Append(message);
            _pendingGardenMessages.Clear();
        }).CallDeferred();
    }

    private static void StyleOption(OptionButton option)
    {
        option.CustomMinimumSize = new Vector2(165, 24);
        UiFactory.ApplyPixelFont(option, 8);
        UiFactory.ApplyButtonChrome(option);
        option.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
    }
}
