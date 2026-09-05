using System;
using Godot;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Garden;
using Voidling.Presentation.UI.Multiplayer;

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
    private PanelContainer? _detailsPanel;
    private GardenEventLog _gardenEventLog = null!;
    private LineEdit _gardenNameField = null!;
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
        var panel = UiFactory.CreatePanel(new Vector2(120, 50));
        panel.Name = "GardenStatus";
        panel.Position = new Vector2(12, 10);
        panel.Size = new Vector2(120, 50);
        _uiRoot.AddChild(panel);

        // Name over sprouts rather than side by side: the island is the player's to name, so the
        // name gets the top line and the wallet reads underneath it.
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 1);
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(column);

        _gardenNameField = BuildGardenNameField();
        column.AddChild(_gardenNameField);

        _coinsLabel = UiFactory.CreateLabel(string.Format(Tr("UI_TOP_SPROUTS"), 0), 8);
        _coinsLabel.VerticalAlignment = VerticalAlignment.Center;
        column.AddChild(_coinsLabel);

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

        // The premium glyphs stay (the Garden UI smoke gate requires them), but they no longer
        // stretch: an expanding icon ate the button and shoved the label off to one side.
        var glyphs = new[] { new Vector2I(7, 6), new Vector2I(7, 7), new Vector2I(4, 4), new Vector2I(6, 0) };
        for (var i = 0; i < glyphs.Length; i++)
        {
            var button = actions.GetChild<Button>(i);
            button.Icon = UiFactory.CreateGardenIcon(glyphs[i].X, glyphs[i].Y);
            button.ExpandIcon = false;
            button.IconAlignment = HorizontalAlignment.Left;
            button.AddThemeConstantOverride("icon_max_width", 14);
        }
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
        // The label sits in the middle of the button; the dock reads as four even tiles.
        button.Alignment = HorizontalAlignment.Center;
        button.CustomMinimumSize = new Vector2(width, 24);
        UiFactory.ApplyPixelFont(button, 7);
        button.Pressed += action;
        row.AddChild(button);
    }

    private void BuildToast()
    {
        _toastLabel = UiFactory.CreateLabel("", 9);
        _toastLabel.Position = new Vector2(24, 164);
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
        // Wide and tall enough to read the last handful of entries without scrolling.
        _gardenEventLog = new GardenEventLog
        {
            Position = new Vector2(12, 214),
            Size = new Vector2(320, 82),
            CustomMinimumSize = new Vector2(320, 82),
            ZIndex = 6
        };
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
