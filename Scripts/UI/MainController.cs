using System;
using Godot;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Garden;
using Voidling.Presentation.UI.Multiplayer;

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
    private PanelContainer? _detailsPanel;
    private GardenEventLog _gardenEventLog = null!;
    private Label _toastLabel = null!;
    private float _toastSeconds;
    private string _selectedId = "";
    private RaceScreen? _race;

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
        BuildToast();
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
        DetachMultiplayerRacePresentation();
        DetachTradePresentation();
    }

    public override void _Process(double delta)
    {
        if (_toastSeconds <= 0.0f)
            return;

        _toastSeconds -= (float)delta;
        if (_toastSeconds <= 0.0f)
            _toastLabel.Visible = false;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        // A running race owns Escape: it opens its own pause menu so the player can leave mid-race.
        if (_race != null || _multiplayerRaceScreen != null || _tradeExchangeScreen != null ||
            !inputEvent.IsActionPressed("ui_cancel"))
            return;

        if (_modalHost.IsOpen)
            CloseModal();
        else
            ShowSettingsExtended();
        GetViewport().SetInputAsHandled();
    }

    private void BuildTopBar()
    {
        var panel = UiFactory.CreatePanel(new Vector2(236, 44));
        panel.Name = "GardenStatus";
        panel.Position = new Vector2(12, 10);
        panel.Size = new Vector2(236, 44);
        _uiRoot.AddChild(panel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        panel.AddChild(row);

        var title = UiFactory.CreateTitle(Tr("UI_GARDEN_HOME"));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(title);
        _coinsLabel = UiFactory.CreateLabel(string.Format(Tr("UI_TOP_SPROUTS"), 0), 8);
        _coinsLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_coinsLabel);

        var utilities = new HBoxContainer { Name = "GardenUtilities", Position = new Vector2(400, 18) };
        utilities.AddThemeConstantOverride("separation", 6);
        _uiRoot.AddChild(utilities);
        AddTopButton(utilities, Tr("UI_TOP_ONLINE"), ShowConnectedZone, -1, 70);
        AddTopButton(utilities, Tr("UI_TOP_CENTER"), _garden.ResetCamera, -1, 70);
        AddTopButton(utilities, Tr("UI_TOP_SETTINGS"), ShowSettingsExtended, -1, 76);

        var dock = UiFactory.CreatePanel(new Vector2(376, 46));
        dock.Name = "GardenDock";
        dock.Position = new Vector2(12, 302);
        dock.Size = new Vector2(376, 46);
        _uiRoot.AddChild(dock);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        dock.AddChild(actions);
        AddTopButton(actions, Tr("UI_TOP_SHOP"), ShowShop, -1, 82);
        AddTopButton(actions, Tr("UI_TOP_INVENTORY"), ShowInventory, -1, 92);
        AddTopButton(actions, Tr("UI_TOP_BREED"), ShowBreeding, -1, 82);
        AddTopButton(actions, Tr("UI_TOP_RACE"), ShowRacePickerWithCourses, -1, 78);
        var glyphs = new[] { new Vector2I(7, 6), new Vector2I(7, 7), new Vector2I(4, 4), new Vector2I(6, 0) };
        for (var i = 0; i < glyphs.Length; i++)
        {
            actions.GetChild<Button>(i).Icon = UiFactory.CreateGardenIcon(glyphs[i].X, glyphs[i].Y);
            actions.GetChild<Button>(i).ExpandIcon = true;
            actions.GetChild<Button>(i).AddThemeConstantOverride("icon_max_width", 14);
        }
    }

    private static void AddTopButton(HBoxContainer row, string text, Action action, int iconIndex, float width)
    {
        var button = UiFactory.CreateButton(text, iconIndex);
        button.CustomMinimumSize = new Vector2(width, 24);
        UiFactory.ApplyPixelFont(button, 7);
        button.Pressed += action;
        row.AddChild(button);
    }

    private void BuildToast()
    {
        _toastLabel = UiFactory.CreateLabel("", 9);
        _toastLabel.Position = new Vector2(24, 200);
        _toastLabel.Size = new Vector2(352, 28);
        _toastLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _toastLabel.AddThemeColorOverride("font_color", Color.FromHtml("#F9F4D8"));
        _toastLabel.AddThemeColorOverride("font_shadow_color", Color.FromHtml("#465247"));
        _toastLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _toastLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _toastLabel.Visible = false;
        _uiRoot.AddChild(_toastLabel);
    }

    private void BuildGardenEventLog()
    {
        _gardenEventLog = new GardenEventLog
        {
            Position = new Vector2(12, 244),
            Size = new Vector2(280, 50),
            CustomMinimumSize = new Vector2(280, 50),
            ZIndex = 6
        };
        _uiRoot.AddChild(_gardenEventLog);
    }

    private void RefreshUi()
    {
        _coinsLabel.Text = string.Format(Tr("UI_TOP_SPROUTS"), _session.State.Coins);

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
        => OpenModal(title, size, null);

    private VBoxContainer OpenOnlineModal(string title, Vector2 size, Action backRequested)
        => OpenModal(title, size, backRequested);

    private VBoxContainer OpenModal(string title, Vector2 size, Action? backRequested)
    {
        if (_modalHost.IsOpen)
            CloseModal(false);
        HideGardenHudPanels();
        return _modalHost.Open(title, size, CloseModal, backRequested);
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

        if (restoreGardenHud && _race == null && _multiplayerRaceScreen == null && _uiRoot != null && _uiRoot.Visible)
            RefreshUi();
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
    {
        _toastLabel.Text = text;
        _toastLabel.Visible = true;
        _toastSeconds = 3.0f;
    }

    private void AppendGardenEvent(string text)
        => _gardenEventLog.Append(text);

    private static void StyleOption(OptionButton option)
    {
        option.CustomMinimumSize = new Vector2(165, 24);
        UiFactory.ApplyPixelFont(option, 8);
        UiFactory.ApplyButtonChrome(option);
        option.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
    }
}
