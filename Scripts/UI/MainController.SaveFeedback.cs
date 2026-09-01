using Godot;

namespace VoidlingGame;

public partial class MainController
{
    private Label _saveStatusLabel = null!;
    private Timer _saveStatusTimer = null!;

    private void BuildSaveFeedbackIndicator()
    {
        _saveStatusLabel = UiFactory.CreateLabel(string.Empty, 6);
        _saveStatusLabel.Position = new Vector2(526, 340);
        _saveStatusLabel.Size = new Vector2(104, 14);
        _saveStatusLabel.HorizontalAlignment = HorizontalAlignment.Right;
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
            Color.FromHtml(succeeded ? "#6F8068" : "#9C514B"));
        _saveStatusLabel.Visible = true;
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
        if (GodotObject.IsInstanceValid(_gardenEventLog))
            _gardenEventLog.Append(message);
    }

    private void HideSaveFeedback()
    {
        if (GodotObject.IsInstanceValid(_saveStatusLabel))
            _saveStatusLabel.Visible = false;
    }

    private void DetachSaveFeedbackIndicator()
    {
        if (GodotObject.IsInstanceValid(_session))
            _session.SaveFeedbackRequested -= ShowSaveFeedback;

        if (GodotObject.IsInstanceValid(_saveStatusTimer))
            _saveStatusTimer.Timeout -= HideSaveFeedback;
    }
}
