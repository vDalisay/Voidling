using System;
using System.Collections.Generic;
using Godot;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Owns modal overlay/window lifetime for the root UI. Screens receive only the returned
/// content container; navigation/application state remains outside this presentation host.
///
/// Motion: a fresh window fades its shade in, pops the panel and staggers its buttons and cards
/// in; closing folds the window away on a ghost copy, so input returns to the Garden at once; going
/// from one screen to another swaps panels under a steady shade; and drawing the same screen again
/// (after a purchase or a pick) keeps the window and header, so nothing jumps and the wallet rolls.
/// Structure stays <c>ModalHost &gt; CenterContainer &gt; PanelContainer</c> with a full-screen shade.
/// </summary>
public partial class ModalHost : Control
{
    private static readonly Color ShadeColor = new(0.16f, 0.2f, 0.14f, 0.5f);
    private const int TitleFontSize = 12;

    public bool IsOpen { get; private set; }
    private ColorRect? _shade;
    private Control? _blocker;
    private CenterContainer? _contentRegion;
    private PanelContainer? _panel;
    private VBoxContainer? _box;
    private HBoxContainer? _heading;
    private WalletChip? _wallet;
    private Vector2 _requestedPanelSize;
    private string _signature = string.Empty;
    private Action? _closeRequested;
    private Action? _backRequested;
    private long? _lastWallet;

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
        Color? panelTint = null,
        Texture2D? icon = null)
    {
        _closeRequested = closeRequested ?? throw new ArgumentNullException(nameof(closeRequested));
        _backRequested = backRequested;
        var signature = $"{title}|{size}|{leftInset}|{eyebrow}|{backRequested != null}";

        // The same screen drawn again in place: keep the window and its header, swap the body.
        if (IsOpen && signature == _signature && _box != null && GodotObject.IsInstanceValid(_box))
        {
            ClearBody();
            return _box;
        }

        var swapping = IsOpen;
        if (swapping)
            Exit(keepShade: true);

        ClearContent();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        IsOpen = true;
        Visible = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _signature = signature;

        if (_shade == null || !GodotObject.IsInstanceValid(_shade) || _shade.GetParent() != this)
        {
            _shade = new ColorRect { Name = "Shade", Color = ShadeColor, MouseFilter = MouseFilterEnum.Ignore };
            _shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(_shade);
            if (!swapping)
            {
                var fade = UiMotion.Start(_shade, "fade");
                if (fade != null)
                {
                    _shade.Modulate = new Color(1, 1, 1, 0);
                    fade.TweenProperty(_shade, "modulate", Colors.White, UiMotion.Normal);
                }
            }
        }

        _blocker = new ColorRect { Name = "Blocker", Color = Colors.Transparent, MouseFilter = MouseFilterEnum.Stop };
        _blocker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _blocker.GuiInput += inputEvent =>
        {
            if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                _closeRequested?.Invoke();
        };
        AddChild(_blocker);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _contentRegion = center;
        AddChild(center);
        SetLeftInset(leftInset);

        var panel = UiFactory.CreateWindowPanel(size);
        _panel = panel;
        _requestedPanelSize = size;
        panel.MouseFilter = MouseFilterEnum.Stop;
        center.AddChild(panel);

        var box = new VBoxContainer { Name = "ModalBody" };
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);
        _box = box;

        var heading = new HBoxContainer { Name = "ModalHeading" };
        heading.AddThemeConstantOverride("separation", 6);
        _heading = heading;
        if (backRequested != null)
        {
            var back = UiFactory.CreateButton(string.Empty);
            back.Name = "ModalBack";
            UiSkin.ApplyIconButton(back, UiSkin.IconGlyph.Back);
            back.TooltipText = Tr("UI_COMMON_BACK");
            back.Pressed += () => _backRequested?.Invoke();
            heading.AddChild(back);
        }
        heading.AddChild(TitleTag(title, eyebrow, icon, Mathf.Max(80, size.X - 150)));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        var close = UiFactory.CreateButton(string.Empty);
        close.Name = "ModalClose";
        UiSkin.ApplyIconButton(close, UiSkin.IconGlyph.Close);
        close.TooltipText = Tr("UI_COMMON_CLOSE");
        close.Pressed += () => _closeRequested?.Invoke();
        heading.AddChild(close);
        box.AddChild(heading);

        // Pop the whole window from its centre; the CenterContainer is not itself laid out by a
        // container, so its scale is never reset mid-animation.
        UiMotion.Appear(center, 0.0, UiMotion.Quick, swapping ? 0.05f : 0.1f);
        Callable.From(() => StaggerBody(box)).CallDeferred();
        return box;
    }

    /// <summary>
    /// Shows (or updates) the sprout purse in this window's header. The last value shown carries
    /// over between screens and redraws, so a purchase rolls the number instead of replacing it.
    /// </summary>
    public WalletChip ShowWallet(long coins)
    {
        if (_wallet != null && GodotObject.IsInstanceValid(_wallet) && _wallet.GetParent() == _heading)
        {
            _wallet.SetValue(coins);
            _lastWallet = coins;
            return _wallet;
        }

        var start = _lastWallet ?? coins;
        _wallet = WalletChip.Create(start);
        _heading!.AddChild(_wallet);
        _heading.MoveChild(_wallet, _heading.GetChildCount() - 2);
        if (start != coins) _wallet.SetValue(coins);
        _lastWallet = coins;
        return _wallet;
    }

    public void SetLeftInset(float leftInset)
    {
        if (_contentRegion != null) _contentRegion.OffsetLeft = leftInset;
        if (_panel != null)
            _panel.CustomMinimumSize = new Vector2(Mathf.Min(_requestedPanelSize.X, Size.X - leftInset - 8), _requestedPanelSize.Y);
        if (_contentRegion != null) UiMotion.CenterPivot(_contentRegion);
    }

    public void Close()
    {
        if (IsOpen) Exit(keepShade: false);
        IsOpen = false;
        ClearContent();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _blocker = null;
        _contentRegion = null;
        _panel = null;
        _box = null;
        _heading = null;
        _wallet = null;
        _shade = null;
        _signature = string.Empty;
    }

    /// <summary>A screen's body, below the header, emptied for a redraw in place.</summary>
    private void ClearBody()
    {
        foreach (var child in _box!.GetChildren())
        {
            if (child == _heading) continue;
            if (child is CanvasItem item) item.Visible = false;
            _box.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>
    /// Moves the current window onto a ghost beside this host and folds it away there, so the
    /// host is free for the next screen (or the Garden) this very frame. The ghost takes no input.
    /// </summary>
    private void Exit(bool keepShade)
    {
        if (UiMotion.Reduced || _contentRegion == null || !GodotObject.IsInstanceValid(_contentRegion) ||
            GetParent() is not Control parent)
            return;

        var ghost = new Control { Name = "ModalExit", MouseFilter = MouseFilterEnum.Ignore, ZIndex = ZIndex };
        ghost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        parent.AddChild(ghost);
        parent.MoveChild(ghost, GetIndex() + 1);

        var focus = GetViewport().GuiGetFocusOwner();
        if (focus != null && _contentRegion.IsAncestorOf(focus)) focus.ReleaseFocus();
        var moving = new List<Control> { _contentRegion };
        if (!keepShade && _shade != null && GodotObject.IsInstanceValid(_shade)) moving.Insert(0, _shade);
        foreach (var item in moving)
        {
            UiMotion.Rest(item);
            RemoveChild(item);
            ghost.AddChild(item);
            IgnoreInput(item);
        }
        if (!keepShade) _shade = null;

        var region = _contentRegion;
        UiMotion.CenterPivot(region);
        var tween = ghost.CreateTween().SetParallel();
        tween.TweenProperty(ghost, "modulate", new Color(1, 1, 1, 0), keepShade ? UiMotion.Snap : UiMotion.Quick)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenProperty(region, "scale", Vector2.One * 0.94f, UiMotion.Quick)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(ghost.QueueFree));
        _contentRegion = null;
    }

    private static void IgnoreInput(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
            control.FocusMode = FocusModeEnum.None;
        }
        foreach (var child in node.GetChildren())
            IgnoreInput(child);
    }

    /// <summary>Buttons and button-free cards cascade in, capped so the last is never late.</summary>
    private void StaggerBody(VBoxContainer box)
    {
        if (UiMotion.Reduced || !GodotObject.IsInstanceValid(box) || !box.IsInsideTree()) return;
        var items = new List<CanvasItem>();
        foreach (var child in box.GetChildren())
            if (child != _heading) Collect(child, items);
        if (items.Count > 48) items.RemoveRange(48, items.Count - 48);
        UiMotion.StaggerIn(items, 0.03f, 0.32f, 0.08f);
    }

    private static bool Collect(Node node, List<CanvasItem> items)
    {
        if (node is not Control { Visible: true } control) return false;
        if (control is BaseButton)
        {
            items.Add(control);
            return true;
        }
        var containsButtons = false;
        foreach (var child in control.GetChildren())
            containsButtons |= Collect(child, items);
        if (!containsButtons && control is PanelContainer)
            items.Add(control);
        return containsButtons;
    }

    private static Control TitleTag(string title, string eyebrow, Texture2D? icon, float maxWidth)
    {
        var tag = new PanelContainer
        {
            Name = "TitleTag", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, UiSkin.TitleTagHeight)
        };
        tag.AddThemeStyleboxOverride("panel", UiSkin.TitleTag());
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 4);
        tag.AddChild(row);
        // The icon rides on the tag like a sticker, at the pack's own 32px size.
        if (icon != null)
            row.AddChild(new Sticker { Name = "TitleIcon", Texture = icon, CustomMinimumSize = new Vector2(20, 14), Overhang = new Vector2(8, 9) });

        var titles = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        titles.AddThemeConstantOverride("separation", -2);
        if (eyebrow.Length > 0)
            titles.AddChild(UiFactory.CreateLabel(eyebrow, 7));
        var titleLabel = UiFactory.CreateLabel(title, TitleFontSize);
        titleLabel.AddThemeColorOverride("font_color", UiSkin.Ink);
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        titleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        // An ellipsised label has no minimum width of its own; give the tag its text's width,
        // capped so a very long name trims instead of pushing the window wider.
        var width = UiFactory.InterfaceFont.GetStringSize(title, HorizontalAlignment.Left, -1, TitleFontSize).X;
        titleLabel.CustomMinimumSize = new Vector2(Mathf.Ceil(Mathf.Min(width + 2, maxWidth)), 0);
        titles.AddChild(titleLabel);
        row.AddChild(titles);
        return tag;
    }

    private void ClearContent()
    {
        foreach (var child in GetChildren())
        {
            if (child == _shade && IsOpen) continue;
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
