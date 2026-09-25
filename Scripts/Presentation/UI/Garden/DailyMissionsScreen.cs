using System;
using System.Collections.Generic;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>One of today's goals, worded and counted by the host.</summary>
public sealed record MissionViewState(string MissionId, string Text, int Progress, int Target, int Reward, bool CanClaim, bool Claimed);

/// <summary>
/// Today's goals as paper cards: what to do, a pixel progress bar, the reward, and a Claim button
/// that turns green when the goal is met and stamps a check once taken. Emits intent only.
/// </summary>
public partial class DailyMissionsScreen : VBoxContainer
{
    private static readonly Texture2D CheckMark = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Other UI sprites/Xs and check marks/1s/check mark.png");

    public event Action<string>? ClaimRequested;
    public event Action? BackRequested;

    private IReadOnlyList<MissionViewState> _missions = Array.Empty<MissionViewState>();
    private readonly Dictionary<string, Button> _claims = new();

    public void Configure(IReadOnlyList<MissionViewState> missions)
    {
        if (IsInsideTree()) throw new InvalidOperationException("DailyMissionsScreen must be configured before it enters the scene tree.");
        _missions = missions ?? throw new ArgumentNullException(nameof(missions));
    }

    /// <summary>Where a mission's Claim button sits, for the sprouts that fly from it.</summary>
    public Vector2? ClaimOrigin(string missionId)
        => _claims.TryGetValue(missionId, out var button) && button.IsVisibleInTree() ? button.GetGlobalRect().GetCenter() : null;

    public override void _Ready()
    {
        Name = "DailyMissions";
        AddThemeConstantOverride("separation", 5);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var intro = UiFactory.CreateLabel(Tr("UI_MISSIONS_INTRO"), 7);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.CustomMinimumSize = new Vector2(398, 0);
        intro.AddThemeColorOverride("font_color", UiSkin.InkSoft);
        AddChild(intro);

        foreach (var mission in _missions)
            AddChild(BuildRow(mission));

        var back = UiFactory.CreateButton(Tr("UI_GARDEN_ACTIVITIES"));
        back.Name = "MissionsBack";
        back.CustomMinimumSize = new Vector2(112, 24);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        UiFactory.ApplyPixelFont(back, 7);
        back.Pressed += () => BackRequested?.Invoke();
        AddChild(back);
    }

    private Control BuildRow(MissionViewState mission)
    {
        var card = UiFactory.CreatePaperPanel(new Vector2(398, 0));
        card.Name = "Mission_" + mission.MissionId;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        card.AddChild(row);

        var textColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        textColumn.AddThemeConstantOverride("separation", 3);
        row.AddChild(textColumn);
        var description = UiFactory.CreateLabel(mission.Text, 8);
        description.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        textColumn.AddChild(description);

        var progressRow = new HBoxContainer();
        progressRow.AddThemeConstantOverride("separation", 5);
        var bar = new ProgressBar
        {
            Name = "Progress",
            MinValue = 0,
            MaxValue = Math.Max(1, mission.Target),
            Value = Math.Min(mission.Progress, mission.Target),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(150, 6),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        bar.AddThemeStyleboxOverride("background", UiSkin.BarTrack());
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = Color.FromHtml(mission.Claimed ? "#9EB78E" : "#78B85A"), AntiAliasing = false,
            BorderColor = Color.FromHtml("#B8E08F"), BorderWidthTop = 1
        });
        progressRow.AddChild(bar);
        var count = UiFactory.CreateLabel($"{Math.Min(mission.Progress, mission.Target)}/{mission.Target}", 7);
        count.VerticalAlignment = VerticalAlignment.Center;
        count.AddThemeColorOverride("font_color", UiSkin.InkSoft);
        progressRow.AddChild(count);
        var reward = new HBoxContainer();
        reward.AddThemeConstantOverride("separation", 1);
        reward.AddChild(new TextureRect
        {
            Texture = UiFactory.CreateSproutIcon(),
            CustomMinimumSize = new Vector2(16, 16),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            MouseFilter = MouseFilterEnum.Ignore
        });
        var rewardValue = UiFactory.CreateLabel("+" + mission.Reward, 7);
        rewardValue.VerticalAlignment = VerticalAlignment.Center;
        rewardValue.AddThemeColorOverride("font_color", UiSkin.Gain);
        reward.AddChild(rewardValue);
        progressRow.AddChild(reward);
        textColumn.AddChild(progressRow);

        if (mission.Claimed)
        {
            var stamp = new TextureRect
            {
                Name = "ClaimedStamp",
                Texture = CheckMark,
                CustomMinimumSize = new Vector2(94, 24),
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                TooltipText = Tr("UI_MISSION_CLAIMED")
            };
            row.AddChild(stamp);
            return card;
        }

        var claim = UiFactory.CreateButton(mission.CanClaim
            ? string.Format(Tr("UI_MISSION_CLAIM"), mission.Reward)
            : Tr("UI_MISSION_IN_PROGRESS"));
        claim.Name = "ClaimMission_" + mission.MissionId;
        claim.CustomMinimumSize = new Vector2(94, 26);
        claim.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        UiFactory.ApplyPixelFont(claim, 7);
        if (mission.CanClaim) UiFactory.ApplyPrimaryStyle(claim);
        claim.Disabled = !mission.CanClaim;
        claim.Pressed += () => ClaimRequested?.Invoke(mission.MissionId);
        _claims[mission.MissionId] = claim;
        row.AddChild(claim);
        if (mission.CanClaim)
            Callable.From(() => AttentionBadge.Attach(claim).Active = true).CallDeferred();
        return card;
    }
}
