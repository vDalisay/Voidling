using Godot;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Presentation.UI.Multiplayer;

namespace VoidlingGame;

public partial class MainController
{
    private ChallengePresentationBridge? _challengeBridge;
    private ChallengeHubPanel? _challengeHubPanel;
    private bool _challengeBridgeSubscribed;

    private ChallengePresentationBridge ChallengeBridge
    {
        get
        {
            _challengeBridge ??= GetNode<ChallengePresentationBridge>(
                "/root/GameBootstrap/ChallengePresentationBridge");
            if (!_challengeBridgeSubscribed)
            {
                _challengeBridge.StateChanged += OnChallengeHubStateChanged;
                _challengeBridgeSubscribed = true;
            }
            return _challengeBridge;
        }
    }

    private void ShowChallenges()
    {
        var box = OpenModal(Tr("UI_CHALLENGE_TITLE"), new Vector2(552, 330));
        var panel = new ChallengeHubPanel();
        panel.Configure(ChallengeBridge.Current);
        panel.OfferRaceRequested += OfferRaceChallenge;
        panel.JoinRequested += JoinChallenge;
        panel.LeaveRequested += LeaveChallenge;
        panel.CancelRequested += CancelChallenge;
        _challengeHubPanel = panel;
        box.AddChild(panel);
    }

    private void OfferRaceChallenge(int maxParticipants)
        => ApplyChallengeOperation(ChallengeBridge.OfferRace(maxParticipants));

    private void JoinChallenge(string challengeId)
        => ApplyChallengeOperation(ChallengeBridge.Join(challengeId));

    private void LeaveChallenge(string challengeId)
        => ApplyChallengeOperation(ChallengeBridge.Leave(challengeId));

    private void CancelChallenge(string challengeId)
        => ApplyChallengeOperation(ChallengeBridge.Cancel(challengeId));

    private void ApplyChallengeOperation(ChallengeOperationResult result)
    {
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_CHALLENGE_ACTION_FAILED"),
                result.Error ?? "unknown challenge error"));
        }

        RefreshChallengeHub();
    }

    private void OnChallengeHubStateChanged(ChallengeHubViewState state)
    {
        if (_challengeHubPanel == null || !GodotObject.IsInstanceValid(_challengeHubPanel))
            return;

        _challengeHubPanel.Render(state);
    }

    private void RefreshChallengeHub()
    {
        if (_challengeHubPanel == null || !GodotObject.IsInstanceValid(_challengeHubPanel))
            return;

        _challengeHubPanel.Render(ChallengeBridge.Current);
    }
}
