using Godot;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// The sprout purse: a small paper chip with the sprout and a rolling pixel-font count. Anything
/// that spends or earns sprouts shows one, so a purchase or a claim is seen leaving or arriving.
/// </summary>
public partial class WalletChip : PanelContainer
{
    private RollingCounter _counter = null!;
    private Label _amount = null!;
    private long _initial;

    public static WalletChip Create(long coins)
    {
        var chip = new WalletChip { Name = "WalletChip", _initial = coins, MouseFilter = MouseFilterEnum.Ignore };
        return chip;
    }

    public long Value => _counter?.Value ?? _initial;

    public override void _Ready()
    {
        var style = UiSkin.Paper();
        style.ContentMarginLeft = 5;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 3;
        style.ContentMarginBottom = 4;
        AddThemeStyleboxOverride("panel", style);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 3);
        AddChild(row);
        var icon = new TextureRect
        {
            Texture = UiFactory.CreateSproutIcon(),
            CustomMinimumSize = new Vector2(16, 16),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(icon);
        _amount = new Label
        {
            Name = "WalletAmount",
            // Wide enough for six digits, so rolling never reflows the header it sits in.
            CustomMinimumSize = new Vector2(38, 16),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiSkin.ApplyNumberFont(_amount);
        row.AddChild(_amount);
        _counter = RollingCounter.Attach(_amount, _initial);
        _counter.PopTarget = this;
        Resized += () => UiMotion.CenterPivot(this);
    }

    public void SetValue(long coins, bool animate = true) => _counter.SetValue(coins, animate);
}
