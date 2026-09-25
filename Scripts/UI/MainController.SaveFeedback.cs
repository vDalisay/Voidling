using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;

namespace VoidlingGame;

public partial class MainController
{
    private Label _saveStatusLabel = null!;
    private Timer _saveStatusTimer = null!;

    private void BuildSaveFeedbackIndicator()
    {
        // A small paper slip that pops up above the rail's utility buttons, never on top of them.
        _saveStatusLabel = UiFactory.CreateLabel(string.Empty, 6);
        _saveStatusLabel.Name = "GardenSaveStatus";
        var slip = UiSkin.Paper();
        slip.ContentMarginTop = 2;
        slip.ContentMarginBottom = 4;
        slip.ContentMarginLeft = slip.ContentMarginRight = 6;
        _saveStatusLabel.AddThemeStyleboxOverride("normal", slip);
        _saveStatusLabel.Position = new Vector2(14, 302);
        _saveStatusLabel.Size = new Vector2(68, 18);
        _saveStatusLabel.VerticalAlignment = VerticalAlignment.Center;
        _saveStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _saveStatusLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _saveStatusLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _saveStatusLabel.ZIndex = 120;
        _saveStatusLabel.Visible = false;
        _uiLayer.AddChild(_saveStatusLabel);

        _saveStatusTimer = new Timer { OneShot = true };
        _saveStatusTimer.Timeout += HideSaveFeedback;
        AddChild(_saveStatusTimer);

        _session.SaveFeedbackRequested += ShowSaveFeedback;
        Callable.From(ShowStartupPersistenceNoticeIfNeeded).CallDeferred();
    }

    private void ShowSaveFeedback(bool succeeded)
    {
        if (!GodotObject.IsInstanceValid(_saveStatusLabel) || !GodotObject.IsInstanceValid(_saveStatusTimer))
            return;

        _saveStatusLabel.Text = Tr(succeeded ? "UI_SAVE_STATUS_SAVED" : "UI_SAVE_STATUS_FAILED");
        _saveStatusLabel.AddThemeColorOverride(
            "font_color",
            Color.FromHtml(succeeded ? "#36533D" : "#9C514B"));
        _saveStatusLabel.TooltipText = _saveStatusLabel.Text;
        var wasVisible = _saveStatusLabel.Visible;
        _saveStatusLabel.Visible = true;
        UiMotion.Kill(_saveStatusLabel, "fade");
        _saveStatusLabel.Modulate = Colors.White;
        if (!wasVisible) UiMotion.Appear(_saveStatusLabel, 0.0, UiMotion.Quick, 0.2f);
        _saveStatusTimer.Start(succeeded ? 1.25 : 4.0);
    }

    private void ShowStartupPersistenceNoticeIfNeeded()
    {
        var key = _session.StartupNotice switch
        {
            GameSessionStartupNotice.SaveRecoveredFromBackup => "UI_SAVE_RECOVERED_BACKUP",
            GameSessionStartupNotice.SaveLoadFailed => "UI_SAVE_LOAD_FAILED",
            GameSessionStartupNotice.SaveUnavailable => "UI_SAVE_UNAVAILABLE",
            _ => string.Empty
        };
        if (key.Length == 0)
            return;

        var message = Tr(key);
        ShowToast(message);
    }

    private void HideSaveFeedback()
    {
        if (!GodotObject.IsInstanceValid(_saveStatusLabel)) return;
        var fade = UiMotion.Start(_saveStatusLabel, "fade");
        if (fade == null)
        {
            _saveStatusLabel.Visible = false;
            return;
        }
        fade.TweenProperty(_saveStatusLabel, "modulate", new Color(1, 1, 1, 0), UiMotion.Normal);
        fade.TweenCallback(Callable.From(() => _saveStatusLabel.Visible = false));
    }

    private void DetachSaveFeedbackIndicator()
    {
        if (GodotObject.IsInstanceValid(_session))
            _session.SaveFeedbackRequested -= ShowSaveFeedback;

        if (GodotObject.IsInstanceValid(_saveStatusTimer))
            _saveStatusTimer.Timeout -= HideSaveFeedback;
    }
}
