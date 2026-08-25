using System;
using Godot;
using Voidling.Application.Multiplayer.Challenges;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Godot-facing bridge for the challenge hub. Presentation receives only application view state and
/// typed operation results; Steam/lobby packet details remain below the Application boundary.
/// </summary>
public partial class ChallengePresentationBridge : Node
{
    private ChallengeFacade? _facade;

    public event Action<ChallengeHubViewState>? StateChanged;

    public ChallengeHubViewState Current => RequireFacade().Current;

    public void Configure(ChallengeFacade facade)
    {
        if (_facade != null)
            throw new InvalidOperationException("Challenge presentation bridge is already configured.");

        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        _facade.StateChanged += HandleStateChanged;
    }

    public ChallengeOperationResult OfferRace(int maxParticipants)
        => RequireFacade().OfferRace(maxParticipants);

    public ChallengeOperationResult Join(string challengeId)
        => RequireFacade().Join(challengeId);

    public ChallengeOperationResult Leave(string challengeId)
        => RequireFacade().Leave(challengeId);

    public ChallengeOperationResult Cancel(string challengeId)
        => RequireFacade().Cancel(challengeId);

    public override void _ExitTree()
    {
        if (_facade != null)
            _facade.StateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(ChallengeHubViewState state)
        => StateChanged?.Invoke(state);

    private ChallengeFacade RequireFacade()
        => _facade ?? throw new InvalidOperationException("Challenge presentation bridge is not configured.");
}
