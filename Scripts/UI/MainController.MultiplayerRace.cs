using System;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Multiplayer;

namespace VoidlingGame;

public partial class MainController
{
    private MultiplayerRacePresentationBridge? _multiplayerRaceBridge;
    private MultiplayerRaceSetupPanel? _multiplayerRaceSetupPanel;
    private RaceScreen? _multiplayerRaceScreen;
    private string _multiplayerRaceSetupChallengeId = string.Empty;
    private bool _multiplayerRaceBridgeSubscribed;

    private MultiplayerRacePresentationBridge MultiplayerRaceBridge
    {
        get
        {
            _multiplayerRaceBridge ??= GetNode<MultiplayerRacePresentationBridge>(
                "/root/GameBootstrap/MultiplayerRacePresentationBridge");
            if (!_multiplayerRaceBridgeSubscribed)
            {
                _multiplayerRaceBridge.PreparationChanged += OnMultiplayerRacePreparationChanged;
                _multiplayerRaceBridge.RaceReadyToLaunch += OnMultiplayerRaceReadyToLaunch;
                _multiplayerRaceBridge.RacePreparationFailed += OnMultiplayerRacePreparationFailed;
                _multiplayerRaceBridgeSubscribed = true;
            }
            return _multiplayerRaceBridge;
        }
    }

    private void ShowMultiplayerRaceSetup(string challengeId)
    {
        _multiplayerRaceSetupChallengeId = challengeId;
        var state = BuildMultiplayerRaceSetupState(challengeId);
        var box = OpenOnlineModal(Tr("UI_MP_RACE_SETUP_TITLE"), new Vector2(522, 330), ShowChallenges);
        var panel = new MultiplayerRaceSetupPanel();
        panel.Configure(state);
        panel.SelectionRequested += creatureId => SubmitMultiplayerRaceSelection(challengeId, creatureId);
        panel.StartRequested += () => RequestMultiplayerRaceStart(challengeId);
        _multiplayerRaceSetupPanel = panel;
        box.AddChild(panel);
    }

    private MultiplayerRaceSetupPanelState BuildMultiplayerRaceSetupState(string challengeId)
    {
        var preparation = MultiplayerRaceBridge.GetPreparation(challengeId);
        var voidlings = _session.State.Voidlings
            .Select(creature =>
            {
                var view = CreateRacePickerView(creature);
                return new MultiplayerRaceSetupVoidlingView(
                    view.Id,
                    view.Name,
                    view.Appearance,
                    view.HasAngelMutation,
                    view.OtherMutationCount,
                    view.StatSummary);
            })
            .ToArray();
        return new MultiplayerRaceSetupPanelState(preparation, voidlings);
    }

    private void SubmitMultiplayerRaceSelection(string challengeId, string creatureId)
    {
        var result = MultiplayerRaceBridge.SubmitSelection(challengeId, creatureId);
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_MP_RACE_SELECTION_FAILED"),
                result.Error ?? "unknown race selection error"));
        }
        RefreshMultiplayerRaceSetup(challengeId);
    }

    private void RequestMultiplayerRaceStart(string challengeId)
    {
        var result = MultiplayerRaceBridge.RequestStart(challengeId);
        if (!result.Success)
        {
            ShowToast(string.Format(
                Tr("UI_MP_RACE_START_FAILED"),
                result.Error ?? "unknown race start error"));
        }
        RefreshMultiplayerRaceSetup(challengeId);
    }

    private void OnMultiplayerRacePreparationChanged(string challengeId)
        => RefreshMultiplayerRaceSetup(challengeId);

    private void OnMultiplayerRacePreparationFailed(string challengeId, string error)
    {
        if (string.Equals(challengeId, _multiplayerRaceSetupChallengeId, StringComparison.Ordinal))
            ShowToast(string.Format(Tr("UI_MP_RACE_START_FAILED"), error));
        RefreshMultiplayerRaceSetup(challengeId);
    }

    private void RefreshMultiplayerRaceSetup(string challengeId)
    {
        if (!string.Equals(challengeId, _multiplayerRaceSetupChallengeId, StringComparison.Ordinal) ||
            _multiplayerRaceSetupPanel == null ||
            !GodotObject.IsInstanceValid(_multiplayerRaceSetupPanel))
        {
            return;
        }

        _multiplayerRaceSetupPanel.Render(BuildMultiplayerRaceSetupState(challengeId));
    }

    private void OnMultiplayerRaceReadyToLaunch(ResolvedMultiplayerRace race)
    {
        if (_multiplayerRaceScreen != null && GodotObject.IsInstanceValid(_multiplayerRaceScreen))
            return;

        if (_modalHost.IsOpen)
            CloseModal(false);

        _multiplayerRaceSetupPanel = null;
        _multiplayerRaceSetupChallengeId = string.Empty;
        _garden.SetGameplayActive(false);
        _garden.Visible = false;
        _uiRoot.Visible = false;

        var screen = new RaceScreen();
        screen.ConfigureMultiplayer(race, MultiplayerRaceBridge);
        screen.RaceCompleted += OnMultiplayerRaceCompleted;
        screen.ReturnRequested += EndMultiplayerRace;
        _multiplayerRaceScreen = screen;
        AddChild(screen);
    }

    private void OnMultiplayerRaceCompleted(int placement)
        => _gardenEventLog.Append(string.Format(Tr("UI_GARDEN_LOG_RACE_RESULT"), placement));

    private void EndMultiplayerRace()
    {
        if (_multiplayerRaceScreen != null && GodotObject.IsInstanceValid(_multiplayerRaceScreen))
        {
            _multiplayerRaceScreen.RaceCompleted -= OnMultiplayerRaceCompleted;
            _multiplayerRaceScreen.ReturnRequested -= EndMultiplayerRace;
            _multiplayerRaceScreen.QueueFree();
        }
        _multiplayerRaceScreen = null;

        _garden.Visible = true;
        _garden.SetGameplayActive(true);
        _uiRoot.Visible = true;
        RefreshUi();
    }

    private void DetachMultiplayerRacePresentation()
    {
        if (!_multiplayerRaceBridgeSubscribed || _multiplayerRaceBridge == null)
            return;

        _multiplayerRaceBridge.PreparationChanged -= OnMultiplayerRacePreparationChanged;
        _multiplayerRaceBridge.RaceReadyToLaunch -= OnMultiplayerRaceReadyToLaunch;
        _multiplayerRaceBridge.RacePreparationFailed -= OnMultiplayerRacePreparationFailed;
        _multiplayerRaceBridgeSubscribed = false;
    }
}
