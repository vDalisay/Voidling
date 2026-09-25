using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;

namespace VoidlingGame;

/// <summary>
/// The Garden UI's shared motion wiring: the root theme (pack chrome for tooltips and stray
/// controls), tooltip pops, the effect layer, toasts over menus, attention badges on things to
/// claim, and the celebrations that follow a successful purchase or claim. Every effect here is
/// triggered after the session has already applied the action; none of it decides anything.
/// </summary>
public partial class MainController
{
    private ToastStack _toasts = null!;
    private RollingCounter _walletCounter = null!;
    private AttentionBadge? _activitiesBadge;
    private bool _reduceMotionApplied;

    private void ComposeUiMotion()
    {
        ApplyReduceMotion();
        _uiRoot.Theme = UiFactory.CreateRootTheme();
        AddChild(new TooltipJuice { Name = "TooltipJuice" });
        UiFxLayer.For(this);

        _toasts = new ToastStack
        {
            Position = new Vector2(170, 200),
            Size = new Vector2(300, 118),
            ZIndex = 150
        };
        _uiRoot.AddChild(_toasts);
    }

    private void ApplyReduceMotion()
    {
        var reduced = _session.State.ReduceMotion;
        if (_reduceMotionApplied && UiMotion.Reduced == reduced) return;
        UiMotion.Reduced = reduced;
        _reduceMotionApplied = true;
    }

    /// <summary>
    /// News the player cannot see in the log (a menu covers it, or it is folded to one line) also
    /// pops up as a toast above everything.
    /// </summary>
    private void ToastIfLogHidden(string message)
    {
        if (_modalHost.IsOpen || _gardenEventLog.IsCompact || !_gardenEventLog.IsVisibleInTree())
            _toasts.Post(message);
    }

    /// <summary>The claimable-reward badge on the log's Activities button.</summary>
    private void RefreshAttention()
    {
        if (_activitiesBadge == null || !GodotObject.IsInstanceValid(_activitiesBadge)) return;
        var login = _session.GetDailyLoginStatus();
        var missions = _session.GetDailyMissionStatus();
        _activitiesBadge.Active = login.CanClaim || missions.Missions.Any(mission => mission.CanClaim);
    }

    /// <summary>The global centre of a live modal control, captured before a redraw replaces it.</summary>
    private Vector2? ModalPoint(string name)
        => _modalHost.FindChildren(name, string.Empty, true, false).OfType<Control>()
            .FirstOrDefault(control => !control.IsQueuedForDeletion() && control.IsVisibleInTree())
            ?.GetGlobalRect().GetCenter();

    /// <summary>A burst where the confirming button was; purchases and claims only.</summary>
    private void Celebrate(Vector2? origin, PixelBurst.Palette palette = PixelBurst.Palette.Leafy)
    {
        if (origin.HasValue) PixelBurst.Spawn(this, origin.Value, palette);
    }

    /// <summary>
    /// Sprouts earned in a window fly from the button to the window's purse, which rolls up when
    /// the first one lands. The session already holds the new balance; the purse was redrawn still
    /// showing <paramref name="before"/>.
    /// </summary>
    private void FlyRewardToWallet(Vector2? origin, long before, int reward)
    {
        if (!_modalHost.IsOpen) return;
        var wallet = _modalHost.ShowWallet(before);
        var coins = _session.State.Coins;
        Celebrate(origin);
        if (!origin.HasValue)
        {
            wallet.SetValue(coins);
            return;
        }
        RewardFlight.Launch(this, origin.Value, wallet, UiFactory.CreateSproutIcon(),
            Math.Clamp(reward / 2, 3, 6), () => wallet.SetValue(coins));
    }

    /// <summary>
    /// While a claim redraws its window, the purse keeps showing the balance from before the claim
    /// so the flying sprouts can deliver the difference.
    /// </summary>
    private long? _walletShownBeforeReward;

    private long WalletToShow => _walletShownBeforeReward ?? _session.State.Coins;
}
