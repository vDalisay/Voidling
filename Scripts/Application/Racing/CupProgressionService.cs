using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Racing;
using VoidlingGame;

namespace Voidling.Application.Racing;

public enum CupProgressionFailure
{
    None = 0,
    UnknownCup,
    Locked
}

public readonly record struct CupProgressionEntry(
    CupDefinition Cup,
    bool IsUnlocked,
    bool IsCompleted);

public readonly record struct CupCompletionResult(
    CupProgressionFailure Failure,
    bool Changed,
    CupDefinition? Cup)
{
    public bool Succeeded => Failure == CupProgressionFailure.None;
}

/// <summary>
/// Deterministic championship progression. Unlocks are derived only from stable Cup prerequisites
/// and completed Cup IDs. This intentionally does not charge fees or award prizes; those economy
/// rules remain product decisions and can be layered on without changing the durable Cup identity.
/// </summary>
public sealed class CupProgressionService
{
    public IReadOnlyList<CupProgressionEntry> Project(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return CupCatalog.All
            .Select(cup => new CupProgressionEntry(
                cup,
                IsUnlocked(state, cup),
                IsCompleted(state, cup.Id)))
            .ToArray();
    }

    public bool IsUnlocked(GameStateData state, CupDefinition cup)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(cup);
        return string.IsNullOrEmpty(cup.PrerequisiteCupId) ||
               IsCompleted(state, cup.PrerequisiteCupId);
    }

    public CupCompletionResult RecordVictory(GameStateData state, string cupId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!CupCatalog.TryGet(cupId, out var cup))
            return new CupCompletionResult(CupProgressionFailure.UnknownCup, false, null);
        if (!IsUnlocked(state, cup))
            return new CupCompletionResult(CupProgressionFailure.Locked, false, cup);
        if (IsCompleted(state, cup.Id))
            return new CupCompletionResult(CupProgressionFailure.None, false, cup);

        state.CompletedCupIds.Add(cup.Id);
        return new CupCompletionResult(CupProgressionFailure.None, true, cup);
    }

    private static bool IsCompleted(GameStateData state, string cupId)
        => state.CompletedCupIds.Contains(cupId, StringComparer.Ordinal);
}
