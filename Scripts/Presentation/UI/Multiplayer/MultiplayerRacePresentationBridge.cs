using System;
using Godot;
using Voidling.Application.Multiplayer.Racing;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Godot-facing race bridge. It exposes race preparation and deterministic lockstep operations while
/// keeping transport, Steam callbacks, and application service composition out of presentation code.
/// </summary>
public partial class MultiplayerRacePresentationBridge : Node
{
    private MultiplayerRaceFacade? _facade;

    public event Action<string>? PreparationChanged;
    public event Action<ResolvedMultiplayerRace>? RaceReadyToLaunch;
    public event Action<string, string>? RacePreparationFailed;

    public void Configure(MultiplayerRaceFacade facade)
    {
        if (_facade != null)
            throw new InvalidOperationException("Multiplayer race presentation bridge is already configured.");
        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        _facade.PreparationChanged += HandlePreparationChanged;
        _facade.RaceReadyToLaunch += HandleRaceReady;
        _facade.RacePreparationFailed += HandlePreparationFailed;
    }

    public MultiplayerRacePreparationView GetPreparation(string challengeId)
        => RequireFacade().GetPreparation(challengeId);

    public MultiplayerRaceOperationResult SubmitSelection(string challengeId, string creatureId)
        => RequireFacade().SubmitSelection(challengeId, creatureId);

    public MultiplayerRaceOperationResult RequestStart(string challengeId)
        => RequireFacade().RequestStart(challengeId);

    public MultiplayerRaceOperationResult RequestCheer(string challengeId)
        => RequireFacade().RequestCheer(challengeId);

    public MultiplayerRaceOperationResult AdvanceFixedSteps(string challengeId, int stepCount)
        => RequireFacade().AdvanceFixedSteps(challengeId, stepCount);

    public bool TryGetFrame(string challengeId, out MultiplayerRaceFrameView frame)
        => RequireFacade().TryGetFrame(challengeId, out frame);

    public override void _ExitTree()
    {
        if (_facade == null)
            return;
        _facade.PreparationChanged -= HandlePreparationChanged;
        _facade.RaceReadyToLaunch -= HandleRaceReady;
        _facade.RacePreparationFailed -= HandlePreparationFailed;
    }

    private void HandlePreparationChanged(string challengeId)
        => PreparationChanged?.Invoke(challengeId);

    private void HandleRaceReady(ResolvedMultiplayerRace race)
        => RaceReadyToLaunch?.Invoke(race);

    private void HandlePreparationFailed(string challengeId, string error)
        => RacePreparationFailed?.Invoke(challengeId, error);

    private MultiplayerRaceFacade RequireFacade()
        => _facade ?? throw new InvalidOperationException("Multiplayer race presentation bridge is not configured.");
}
