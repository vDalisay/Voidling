using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Owns modal overlay/window lifetime for the root UI. Screens receive only the returned
/// content container; navigation/application state remains outside this presentation host.
/// </summary>
public partial class ModalHost : Control
{
    public bool IsOpen { get; private set; }
    private Control? _blocker;
    private Control? _contentRegion;
    private PanelContainer? _panel;
    private Vector2 _requestedPanelSize;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    public VBoxContainer Open(
        string title,
        Vector2 size,
        Action closeRequested,
        Action? backRequested = null,
        float leftInset = 0,
        string eyebrow = "",
        Color? panelTint = null)
    {
        if (closeRequested == null)
            throw new ArgumentNullException(nameof(closeRequested));

        ClearContent();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        IsOpen = true;
        Visible = true;
        MouseFilter = MouseFilterEnum.Ignore;

        var shade = new ColorRect
        {
            Color = new Color(0.16f, 0.24f, 0.20f, 0.48f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _blocker = new ColorRect { Color = Colors.Transparent, MouseFilter = MouseFilterEnum.Stop };
        _blocker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _blocker.GuiInput += inputEvent =>
        {
            if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                closeRequested();
        };
        AddChild(_blocker);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _contentRegion = center;
        AddChild(center);
        SetLeftInset(leftInset);

        var panel = UiFactory.CreatePanel(size);
        _panel = panel;
        _requestedPanelSize = size;
        panel.CustomMinimumSize = size;
        panel.MouseFilter = MouseFilterEnum.Stop;
        if (panelTint.HasValue)
        {
            var style = (StyleBoxTexture)panel.GetThemeStylebox("panel").Duplicate();
            style.ModulateColor = panelTint.Value;
            panel.AddThemeStyleboxOverride("panel", style);
        }
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 7);
        if (backRequested != null)
        {
            var back = UiFactory.CreateButton(Tr("UI_COMMON_BACK"));
            back.CustomMinimumSize = new Vector2(66, 23);
            back.Pressed += backRequested;
            heading.AddChild(back);
        }
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        if (eyebrow.Length > 0)
            titles.AddChild(UiFactory.CreateLabel(eyebrow, 7));
        var titleLabel = UiFactory.CreateTitle(title);
        titleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        titles.AddChild(titleLabel);
        heading.AddChild(titles);

        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(30, 23);
        close.Pressed += closeRequested;
        heading.AddChild(close);
        box.AddChild(heading);

        return box;
    }

    public void SetLeftInset(float leftInset)
    {
        if (_blocker != null) _blocker.OffsetLeft = leftInset;
        if (_contentRegion != null) _contentRegion.OffsetLeft = leftInset;
        if (_panel != null)
            _panel.CustomMinimumSize = new Vector2(Mathf.Min(_requestedPanelSize.X, Size.X - leftInset - 8), _requestedPanelSize.Y);
    }

    public void Close()
    {
        ClearContent();
        IsOpen = false;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _blocker = null;
        _contentRegion = null;
        _panel = null;
    }

    private void ClearContent()
    {
        foreach (var child in GetChildren())
        {
            if (child is CanvasItem canvasItem)
                canvasItem.Visible = false;
            if (child is Control control)
                control.MouseFilter = MouseFilterEnum.Ignore;

            // Modal close is commonly invoked from a Button.Pressed signal owned by this subtree.
            // Free() would destroy the signal emitter synchronously and Godot explicitly rejects
            // that. QueueFree() keeps the object alive until signal dispatch has completed.
            child.QueueFree();
        }
    }
}
