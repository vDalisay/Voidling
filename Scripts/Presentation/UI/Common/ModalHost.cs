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
        Action? backRequested = null)
    {
        if (closeRequested == null)
            throw new ArgumentNullException(nameof(closeRequested));

        ClearContent();
        IsOpen = true;
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;

        var shade = new ColorRect
        {
            Color = new Color(0.16f, 0.24f, 0.20f, 0.48f),
            MouseFilter = MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = UiFactory.CreatePanel(size);
        panel.CustomMinimumSize = size;
        panel.MouseFilter = MouseFilterEnum.Stop;
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
        var titleLabel = UiFactory.CreateTitle(title);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(titleLabel);

        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(30, 23);
        close.Pressed += closeRequested;
        heading.AddChild(close);
        box.AddChild(heading);

        return box;
    }

    public void Close()
    {
        ClearContent();
        IsOpen = false;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
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
