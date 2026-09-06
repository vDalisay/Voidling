using System;
using System.Collections.Generic;
using System.Linq;
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
    private const float MinimumRailWidth = 96.0f;
    private const float MaximumRailWidth = 150.0f;
    private const float RailCollapseThreshold = 72.0f;

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
    private Control _railResizeHandle = null!;
    private HBoxContainer _railUtilities = null!;
    private Button _muteButton = null!;
    private bool _railCollapsed;
    private bool _resizingRail;
    private bool _modalUsesRail;
    private float _railWidth = MinimumRailWidth;
    private float _expandedRailWidth = MinimumRailWidth;
    private float _volumeBeforeMute = 1.0f;
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
        _gardenStatus.Position = new Vector2(510, 10);
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

        var wallet = new HBoxContainer();
        wallet.AddThemeConstantOverride("separation", 3);
        wallet.AddChild(new TextureRect
        {
            Texture = UiFactory.CreateSproutIcon(),
            CustomMinimumSize = new Vector2(14, 14),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _coinsLabel = UiFactory.CreateLabel("0", 8);
        _coinsLabel.VerticalAlignment = VerticalAlignment.Center;
        wallet.AddChild(_coinsLabel);
        column.AddChild(wallet);

        var dock = UiFactory.CreatePanel(new Vector2(0, ScreenHeight), wood: true);
        _gardenRail = dock;
        dock.Name = "GardenRail";
        dock.Position = Vector2.Zero;
        dock.Size = new Vector2(_railWidth, ScreenHeight);
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
            ("Build", "UI_GARDEN_BUILD", ShowGardenBuild, new(15, 1)),
            ("Online", "UI_TOP_ONLINE", ShowConnectedZone, new(12, 2))
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
        _dayNightDial = new GardenDayNightDial { Position = new Vector2(_railWidth + 14, 10), ZIndex = 15 };
        _uiRoot.AddChild(_dayNightDial);
        _dayNightDial.ShowTime(_garden.EnvironmentLocalTime);
        _garden.EnvironmentTimeChanged += _dayNightDial.ShowTime;

        _railUtilities = new HBoxContainer { Name = "GardenUtilities", Position = new Vector2(12, 326), ZIndex = 15 };
        _railUtilities.AddThemeConstantOverride("separation", 4);
        _uiRoot.AddChild(_railUtilities);
        var settings = CreateRailUtilityButton("Settings", UiFactory.CreateSettingsIcon(0, 4), Tr("UI_TOP_SETTINGS"));
        settings.Pressed += ShowSettingsFromRail;
        _railUtilities.AddChild(settings);
        _muteButton = CreateRailUtilityButton("Mute", UiFactory.CreateSettingsIcon(0, 14), Tr("UI_GARDEN_MUTE"));
        _muteButton.Pressed += ToggleMute;
        _railUtilities.AddChild(_muteButton);

        _railResizeHandle = new Control
        {
            Name = "GardenRailResize",
            Position = new Vector2(_railWidth - 3, 0),
            Size = new Vector2(6, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.Hsize,
            ZIndex = 19
        };
        _railResizeHandle.GuiInput += HandleRailResizeInput;
        _uiRoot.AddChild(_railResizeHandle);

        _railToggle = new Button
        {
            Name = "GardenRailToggle",
            Text = "›",
            Position = new Vector2(0, (ScreenHeight - 34) / 2),
            CustomMinimumSize = new Vector2(22, 34),
            Size = new Vector2(22, 34),
            ZIndex = 20,
            Visible = false,
            FocusMode = Control.FocusModeEnum.All,
            TooltipText = Tr("UI_GARDEN_SHOW_RAIL")
        };
        UiFactory.ApplyPixelFont(_railToggle, 10);
        _railToggle.Pressed += ToggleGardenRail;
        ApplyCollapsedHandleStyle(_railToggle);
        _uiRoot.AddChild(_railToggle);
    }

    private void ToggleGardenRail()
        => SetGardenRailCollapsed(!_railCollapsed);

    private void SetGardenRailCollapsed(bool collapsed)
    {
        if (_railCollapsed == collapsed) return;
        _railCollapsed = collapsed;
        _railTween?.Kill();
        _quickMenu.Close();
        _gardenRail.Visible = true;
        _railUtilities.Visible = true;
        _railResizeHandle.Visible = true;
        _gardenStatus.Visible = true;
        _dayNightDial.Visible = true;
        _gardenEventLog.Visible = true;
        foreach (var node in _gardenRail.FindChildren("*", "Button", true, false))
            ((Button)node).FocusMode = _railCollapsed ? Control.FocusModeEnum.None : Control.FocusModeEnum.All;
        foreach (var button in _railUtilities.GetChildren().OfType<Button>())
            button.FocusMode = _railCollapsed ? Control.FocusModeEnum.None : Control.FocusModeEnum.All;
        _railToggle.Visible = _railCollapsed;
        if (_railCollapsed) _railToggle.GrabFocus();
        _railTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _railTween.TweenProperty(_gardenRail, "position:x", _railCollapsed ? -_expandedRailWidth : 0f, 0.22);
        _railTween.TweenProperty(_railUtilities, "position:x", _railCollapsed ? -_expandedRailWidth : (_expandedRailWidth - 68) / 2, 0.22);
        _railTween.TweenProperty(_railResizeHandle, "position:x", _railCollapsed ? -6f : _expandedRailWidth - 3, 0.22);
        _railTween.TweenProperty(_dayNightDial, "position:x", _railCollapsed ? 10f : _expandedRailWidth + 14, 0.22);
        _railTween.TweenProperty(_gardenEventLog, "position:x", _railCollapsed ? 10f : _expandedRailWidth + 14, 0.22);
        if (_modalUsesRail) _modalHost.SetLeftInset(_railCollapsed ? 0 : _expandedRailWidth + 12);
        _railTween.Finished += () =>
        {
            _gardenRail.Visible = !_railCollapsed;
            _railUtilities.Visible = !_railCollapsed;
            _railResizeHandle.Visible = !_railCollapsed;
            _gardenEventLog.Visible = !_modalHost.IsOpen;
        };
    }

    private void HandleRailResizeInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _resizingRail = button.Pressed;
            if (!button.Pressed)
            {
                if (_railWidth < RailCollapseThreshold)
                {
                    ApplyRailWidth(_expandedRailWidth);
                    SetGardenRailCollapsed(true);
                }
                else
                    ApplyRailWidth(Mathf.Clamp(_railWidth, MinimumRailWidth, MaximumRailWidth));
            }
            _railResizeHandle.AcceptEvent();
        }
        else if (_resizingRail && inputEvent is InputEventMouseMotion motion)
        {
            ApplyRailWidth(Mathf.Clamp(_railWidth + motion.Relative.X, 36, MaximumRailWidth));
            _railResizeHandle.AcceptEvent();
        }
    }

    private void ApplyRailWidth(float width)
    {
        _railWidth = width;
        if (width >= MinimumRailWidth) _expandedRailWidth = width;
        _gardenRail.Size = new Vector2(width, ScreenHeight);
        _railResizeHandle.Position = new Vector2(width - 3, 0);
        _railUtilities.Position = new Vector2((width - 68) / 2, 326);
        _dayNightDial.Position = new Vector2(width + 14, 10);
        _gardenEventLog.Position = new Vector2(width + 14, _gardenEventLog.Position.Y);
        if (_modalUsesRail) _modalHost.SetLeftInset(width + 12);
    }

    private static Button CreateRailUtilityButton(string name, Texture2D icon, string tooltip)
    {
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = name;
        button.CustomMinimumSize = new Vector2(32, 24);
        button.Icon = icon;
        button.ExpandIcon = true;
        button.TooltipText = tooltip;
        return button;
    }

    private static void ApplyCollapsedHandleStyle(Button button)
    {
        static StyleBoxFlat Style(Color color) => new()
        {
            BgColor = color,
            BorderColor = Color.FromHtml("#A96F4F"),
            BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopRight = 15, CornerRadiusBottomRight = 15,
            ContentMarginLeft = 4, ContentMarginRight = 5
        };
        button.AddThemeStyleboxOverride("normal", Style(Color.FromHtml("#F2D79F")));
        button.AddThemeStyleboxOverride("hover", Style(Color.FromHtml("#FFF0C9")));
        button.AddThemeStyleboxOverride("pressed", Style(Color.FromHtml("#D9B47E")));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private void ToggleMute()
    {
        var current = _session.State.MasterVolume;
        if (current > 0.001f) _volumeBeforeMute = current;
        _session.SetMasterVolume(current > 0.001f ? 0 : Mathf.Max(0.05f, _volumeBeforeMute));
        RefreshMuteButton();
    }

    private void RefreshMuteButton()
    {
        if (_muteButton == null || !GodotObject.IsInstanceValid(_muteButton)) return;
        var muted = _session.State.MasterVolume <= 0.001f;
        _muteButton.Icon = UiFactory.CreateSettingsIcon(muted ? 2 : 0, muted ? 13 : 14);
        _muteButton.TooltipText = Tr(muted ? "UI_GARDEN_UNMUTE" : "UI_GARDEN_MUTE");
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
        _coinsLabel.Text = _session.State.Coins.ToString();
        if (!_gardenNameField.HasFocus() && !string.Equals(_gardenNameField.Text, _session.State.GardenName, StringComparison.Ordinal))
            _gardenNameField.Text = _session.State.GardenName;
        RefreshMuteButton();

        if (_selectedId.Length > 0 && _session.FindVoidling(_selectedId) == null)
            _selectedId = "";

        _garden.Select(_selectedId);
        RebuildDetailsPanel();
        RefreshConnectedZonePanel();
        RefreshQuickMenu();

        if (_gardenEventLog != null && GodotObject.IsInstanceValid(_gardenEventLog))
            _gardenEventLog.Visible = !_modalHost.IsOpen;
        if (_landInspector != null && GodotObject.IsInstanceValid(_landInspector))
            _landInspector.Visible = !_modalHost.IsOpen;

        if (_modalHost.IsOpen)
            HideGardenHudPanels();
    }

    private VBoxContainer OpenModal(string title, Vector2 size)
        => OpenModal(title, size, null, 0, false);

    private VBoxContainer OpenModal(string title, Vector2 size, Action? backRequested)
        => OpenModal(title, size, backRequested, 0, false);

    private VBoxContainer OpenOnlineModal(string title, Vector2 size, Action backRequested)
        => OpenModal(title, size, backRequested, 0, false);

    private VBoxContainer OpenRailModal(string title, Vector2 size, string eyebrow = "", Color? panelTint = null)
    {
        var inset = _railCollapsed ? 0 : _expandedRailWidth + 12;
        size.X = Mathf.Min(size.X, ScreenWidth - inset - 8);
        return OpenModal(title, size, null, inset, true, eyebrow, panelTint);
    }

    private VBoxContainer OpenModal(string title, Vector2 size, Action? backRequested, float leftInset, bool usesRail,
        string eyebrow = "", Color? panelTint = null)
    {
        if (_modalHost.IsOpen)
            CloseModal(false);
        else
            _modalReturnFocus = GetViewport().GuiGetFocusOwner();
        _modalBack = backRequested;
        _modalUsesRail = usesRail;
        var box = _modalHost.Open(title, size, NavigateModalBack, backRequested, leftInset, eyebrow, panelTint);
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
        if (_landInspector != null && GodotObject.IsInstanceValid(_landInspector))
            _landInspector.Visible = false;
    }

    private void CloseModal() => CloseModal(true);

    private void CloseModal(bool restoreGardenHud)
    {
        _modalHost.Close();
        _modalBack = null;
        _modalUsesRail = false;

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
        CloseLandInspector();
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
