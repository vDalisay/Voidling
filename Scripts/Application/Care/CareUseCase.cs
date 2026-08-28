using System;
using System.Linq;
using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Care;

public enum CareInteractionFailure
{
    None,
    CreatureNotFound
}

public readonly record struct CareInteractionResult(CareInteractionFailure Failure, bool Changed)
{
    public bool Succeeded => Failure == CareInteractionFailure.None;
}

/// <summary>
/// Coordinates explicit player care actions against active owned Voidlings. The use case owns no
/// presentation, persistence, clock, or race behavior.
/// </summary>
public sealed class CareUseCase
{
    private readonly CareInteractionRules _rules;
    private readonly CareInteractionService _care = new();

    public CareUseCase(CareInteractionRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public CareInteractionResult Pet(GameStateData state, string creatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var creature = state.Voidlings.FirstOrDefault(candidate => candidate.Id == creatureId);
        if (creature == null)
            return new CareInteractionResult(CareInteractionFailure.CreatureNotFound, false);

        var changed = _care.Pet(creature.Needs, _rules);
        return new CareInteractionResult(CareInteractionFailure.None, changed);
    }
}
