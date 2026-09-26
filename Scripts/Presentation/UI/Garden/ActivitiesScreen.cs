using System;
using System.Collections.Generic;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>What the check-in card needs, already decided by the daily check-in use case.</summary>
public sealed record CheckInViewState(bool CanClaim, int Streak, int ClaimReward, int NextReward, IReadOnlyList<int> Cycle);

/// <summary>
/// Activities: the daily check-in as a card of reward stamps (done days checked, today glowing,
/// later days waiting) with one Claim action, and the way to the daily missions. Emits intent
/// only; the host claims through the session and redraws.
/// </summary>
public partial class ActivitiesScreen : VBoxContainer
{
    private static readonly Texture2D CheckMark = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Other UI sprites/Xs and check marks/1s/check mark.png");

    public event Action? ClaimRequested;
    public event Action? MissionsRequested;

    private CheckInViewState? _state;
    private bool _missionsClaimable;
    private Button _claim = null!;

    public void Configure(CheckInViewState state, bool missionsClaimable)
    {
        if (IsInsideTree()) throw new InvalidOperationException("ActivitiesScreen must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _missionsClaimable = missionsClaimable;
    }

    /// <summary>Where the Claim button sits, for the sprouts that fly from it.</summary>
    public Vector2? ClaimOrigin => _claim.IsVisibleInTree() ? _claim.GetGlobalRect().GetCenter() : null;

    public override void _Ready()
    {
        if (_state == null) throw new InvalidOperationException("ActivitiesScreen must be configured before AddChild.");
        Name = "Activities";
        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var card = UiFactory.CreatePaperPanel(Vector2.Zero);
        card.Name = "CheckIn";
        AddChild(card);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        card.AddChild(column);

        var summary = UiFactory.CreateLabel(string.Format(Tr("UI_GARDEN_CHECK_IN_STREAK"), _state.Streak), 9);
        summary.Name = "Streak";
        column.AddChild(summary);
        column.AddChild(BuildStamps());

        _claim = UiFactory.CreateButton(_state.CanClaim
            ? string.Format(Tr("UI_GARDEN_CHECK_IN_CLAIM"), _state.ClaimReward)
            : Tr("UI_GARDEN_CHECK_IN_CLAIMED"));
        _claim.Name = "Claim";
        _claim.CustomMinimumSize = new Vector2(0, 28);
        if (_state.CanClaim)
        {
            UiFactory.ApplyPrimaryStyle(_claim);
            _claim.Icon = UiFactory.CreateSproutIcon();
            _claim.AddThemeConstantOverride("icon_max_width", 16);
        }
        _claim.Disabled = !_state.CanClaim;
        _claim.Pressed += () => ClaimRequested?.Invoke();
        column.AddChild(_claim);

        var next = UiFactory.CreateLabel(string.Format(Tr("UI_GARDEN_CHECK_IN_NEXT"), _state.NextReward), 8);
        next.AddThemeColorOverride("font_color", UiSkin.InkSoft);
        column.AddChild(next);

        var missions = UiFactory.CreateButton(Tr("UI_GARDEN_MISSIONS"));
        missions.Name = "Missions";
        missions.CustomMinimumSize = new Vector2(0, 26);
        missions.Icon = UiSkin.Emoji(1, 24);
        missions.ExpandIcon = true;
        missions.AddThemeConstantOverride("icon_max_width", 16);
        if (_missionsClaimable) UiFactory.ApplyPrimaryStyle(missions);
        missions.Pressed += () => MissionsRequested?.Invoke();
        AddChild(missions);
        if (_missionsClaimable)
            Callable.From(() => AttentionBadge.Attach(missions).Active = true).CallDeferred();
    }

    /// <summary>One stamp per day of the reward cycle, today's glowing and bobbing.</summary>
    private Control BuildStamps()
    {
        var row = new HBoxContainer { Name = "Stamps", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 3);
        var stamps = CheckInCalendar.Stamps(_state!.CanClaim, _state.Streak, _state.Cycle.Count);
        for (var day = 0; day < stamps.Length; day++)
        {
            var stamp = stamps[day];
            var cell = new PanelContainer { CustomMinimumSize = new Vector2(34, 34), MouseFilter = MouseFilterEnum.Ignore };
            var style = stamp == CheckInStamp.Today ? UiSkin.Paper(new Color(0.8f, 1.05f, 0.72f))
                : stamp == CheckInStamp.Upcoming ? UiSkin.Paper(new Color(0.92f, 0.88f, 0.82f, 0.8f))
                : UiSkin.Well();
            style.ContentMarginLeft = style.ContentMarginRight = 2;
            style.ContentMarginTop = 2;
            style.ContentMarginBottom = 3;
            cell.AddThemeStyleboxOverride("panel", style);
            var stack = new VBoxContainer { Alignment = AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            stack.AddThemeConstantOverride("separation", 0);
            cell.AddChild(stack);

            if (stamp is CheckInStamp.Done or CheckInStamp.TodayClaimed)
            {
                stack.AddChild(new TextureRect
                {
                    Texture = CheckMark,
                    CustomMinimumSize = new Vector2(16, 16),
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    MouseFilter = MouseFilterEnum.Ignore
                });
            }
            else
            {
                stack.AddChild(new TextureRect
                {
                    Texture = UiFactory.CreateSproutIcon(),
                    CustomMinimumSize = new Vector2(16, 16),
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Modulate = stamp == CheckInStamp.Upcoming ? new Color(1, 1, 1, 0.55f) : Colors.White
                });
            }
            var reward = new Label
            {
                Text = "+" + _state.Cycle[day],
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled
            };
            UiSkin.ApplyNumberFont(reward, 1, stamp == CheckInStamp.Upcoming ? UiSkin.InkSoft : UiSkin.Ink);
            stack.AddChild(reward);
            row.AddChild(cell);

            if (stamp == CheckInStamp.Today)
                Callable.From(() => Bob(cell)).CallDeferred();
        }
        return row;
    }

    /// <summary>Today's stamp hops in whole pixels until it is claimed.</summary>
    private static void Bob(Control cell)
    {
        if (!GodotObject.IsInstanceValid(cell)) return;
        var loop = UiMotion.Loop(cell, "bob");
        if (loop == null) return;
        loop.TweenInterval(0.9);
        loop.TweenCallback(Callable.From(() => UiMotion.Pop(cell, 0.12f, UiMotion.Slow)));
    }
}
