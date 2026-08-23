using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;

    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    private GardenController _garden = null!;
    private CanvasLayer _uiLayer = null!;
    private Control _uiRoot = null!;
    private Label _coinsLabel = null!;
    private PanelContainer? _detailsPanel;
    private PanelContainer? _eggsPanel;
    private Control? _modal;
    private Label _toastLabel = null!;
    private float _toastSeconds;
    private string _selectedId = "";
    private RaceController? _race;

    public override void _Ready()
    {
        _garden = GetNode<GardenController>("Garden");
        _garden.VoidlingSelected += OnVoidlingSelected;

        _uiLayer = new CanvasLayer { Layer = 10 };
        AddChild(_uiLayer);

        _uiRoot = new Control { MouseFilter = Control.MouseFilterEnum.Pass };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_uiRoot);

        BuildTopBar();
        BuildToast();

        GameSession.Instance.StateChanged += RefreshUi;
        GameSession.Instance.ToastRequested += ShowToast;
        RefreshUi();
    }

    public override void _ExitTree()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.StateChanged -= RefreshUi;
            GameSession.Instance.ToastRequested -= ShowToast;
        }
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
        if (_race != null || !inputEvent.IsActionPressed("ui_cancel"))
            return;

        if (_modal != null)
            CloseModal();
        else
            ShowSettings();
        GetViewport().SetInputAsHandled();
    }

    private void BuildTopBar()
    {
        var panel = UiFactory.CreatePanel(new Vector2(624, 44));
        panel.Position = new Vector2(8, 7);
        panel.Size = new Vector2(624, 44);
        _uiRoot.AddChild(panel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 4);
        panel.AddChild(row);

        _coinsLabel = UiFactory.CreateLabel("Sprouts: 0", 9);
        _coinsLabel.CustomMinimumSize = new Vector2(84, 22);
        row.AddChild(_coinsLabel);

        AddTopButton(row, "Shop", ShowShop, 0, 57);
        AddTopButton(row, "Inventory", ShowInventory, 3, 68);
        AddTopButton(row, "Breed", ShowBreeding, 6, 57);
        AddTopButton(row, "Race", ShowRacePicker, 12, 57);
        AddTopButton(row, "Settings", ShowSettings, -1, 67);
        AddTopButton(row, "Center", _garden.ResetCamera, -1, 57);
        AddTopButton(row, "Reset", ShowResetConfirm, -1, 54);
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
        _toastLabel.Position = new Vector2(18, 330);
        _toastLabel.Size = new Vector2(390, 16);
        _toastLabel.AddThemeColorOverride("font_color", Color.FromHtml("#F9F4D8"));
        _toastLabel.AddThemeColorOverride("font_shadow_color", Color.FromHtml("#465247"));
        _toastLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _toastLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _toastLabel.Visible = false;
        _uiRoot.AddChild(_toastLabel);
    }

    private void RefreshUi()
    {
        _coinsLabel.Text = $"Sprouts: {GameSession.Instance.State.Coins}";

        if (_selectedId.Length > 0 && GameSession.Instance.FindVoidling(_selectedId) == null)
            _selectedId = "";

        _garden.Select(_selectedId);
        RebuildDetailsPanel();
        RebuildEggsPanel();

        // Modal windows own the foreground. Keep the persistent garden HUD from
        // bleeding into or overlapping their content.
        if (_modal != null)
            HideGardenHudPanels();
    }

    private VBoxContainer OpenModal(string title, Vector2 size)
    {
        CloseModal(false);
        HideGardenHudPanels();

        var overlay = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight)
        };
        _uiRoot.AddChild(overlay);
        _modal = overlay;

        var shade = new ColorRect
        {
            Color = new Color(0.16f, 0.24f, 0.20f, 0.48f),
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        overlay.AddChild(shade);

        var panel = UiFactory.CreatePanel(size);
        panel.Position = new Vector2((ScreenWidth - size.X) * 0.5f, (ScreenHeight - size.Y) * 0.5f);
        panel.Size = size;
        overlay.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 7);
        var titleLabel = UiFactory.CreateTitle(title);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(titleLabel);
        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(30, 23);
        close.Pressed += CloseModal;
        heading.AddChild(close);
        box.AddChild(heading);

        return box;
    }

    private void HideGardenHudPanels()
    {
        if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            _detailsPanel.Visible = false;
        if (_eggsPanel != null && GodotObject.IsInstanceValid(_eggsPanel))
            _eggsPanel.Visible = false;
    }

    private void CloseModal() => CloseModal(true);

    private void CloseModal(bool restoreGardenHud)
    {
        if (_modal != null && GodotObject.IsInstanceValid(_modal))
            _modal.QueueFree();
        _modal = null;

        if (restoreGardenHud && _race == null && _uiRoot != null && _uiRoot.Visible)
            RefreshUi();
    }

    private void StartRace(VoidlingData selected)
    {
        _garden.SetGameplayActive(false);
        _garden.Visible = false;
        _uiRoot.Visible = false;

        _race = new RaceController();
        AddChild(_race);
        _race.ReturnRequested += EndRace;
        _race.Setup(selected);
    }

    private void EndRace()
    {
        if (_race != null && GodotObject.IsInstanceValid(_race))
            _race.QueueFree();
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

    private static void StyleOption(OptionButton option)
    {
        option.CustomMinimumSize = new Vector2(165, 24);
        UiFactory.ApplyPixelFont(option, 8);
        UiFactory.ApplyButtonChrome(option);
        option.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
    }
}
