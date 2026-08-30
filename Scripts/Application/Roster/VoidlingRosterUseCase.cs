using System;
using System.Collections.Generic;
using System.Linq;
using VoidlingGame;

namespace Voidling.Application.Roster;

public enum RenameFailure
{
    None,
    CreatureNotFound,
    EmptyName
}

public readonly record struct RenameVoidlingResult(RenameFailure Failure, string Name, bool Changed)
{
    public bool Succeeded => Failure == RenameFailure.None;
}

public readonly record struct GoodbyeResult(bool Succeeded, string Name);

/// <summary>
/// Owns persistent roster mutations and lookup rules without Godot, UI wording, or persistence.
/// Keeping these rules here prevents scene controllers from becoming the authority for identity
/// and lineage state as more creature-management screens are added.
/// </summary>
public sealed class VoidlingRosterUseCase
{
    public const int MaxNameLength = 18;

    public VoidlingData? FindActive(GameStateData state, string id)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Voidlings.FirstOrDefault(v => v.Id == id);
    }

    public VoidlingData? FindLineage(GameStateData state, string id)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Voidlings.FirstOrDefault(v => v.Id == id)
               ?? state.DepartedVoidlings.FirstOrDefault(v => v.Id == id);
    }

    public IReadOnlyList<VoidlingData> GetLineage(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Voidlings.Concat(state.DepartedVoidlings).ToArray();
    }

    public bool IsDeparted(GameStateData state, string id)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.DepartedVoidlings.Any(v => v.Id == id);
    }

    public bool Move(GameStateData state, string creatureId, float worldX, float worldY)
    {
        var creature = FindActive(state, creatureId);
        if (creature == null)
            return false;

        creature.WorldX = worldX;
        creature.WorldY = worldY;
        return true;
    }

    public RenameVoidlingResult Rename(GameStateData state, string creatureId, string? requestedName)
    {
        var creature = FindActive(state, creatureId);
        if (creature == null)
            return new RenameVoidlingResult(RenameFailure.CreatureNotFound, string.Empty, false);

        var cleaned = (requestedName ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        if (cleaned.Length == 0)
            return new RenameVoidlingResult(RenameFailure.EmptyName, creature.Name, false);

        if (cleaned.Length > MaxNameLength)
            cleaned = cleaned[..MaxNameLength].TrimEnd();

        if (string.Equals(creature.Name, cleaned, StringComparison.Ordinal))
            return new RenameVoidlingResult(RenameFailure.None, cleaned, false);

        creature.Name = cleaned;
        return new RenameVoidlingResult(RenameFailure.None, cleaned, true);
    }

    public bool DiscardFailedEgg(GameStateData state, string eggId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var egg = state.OwnedEggs.FirstOrDefault(e => e.Id == eggId && e.State == EggState.Failed);
        return egg != null && state.OwnedEggs.Remove(egg);
    }

    public GoodbyeResult SayGoodbye(GameStateData state, string creatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var creature = FindActive(state, creatureId);
        if (creature == null)
            return new GoodbyeResult(false, string.Empty);

        creature.DepartureReason = CreatureDepartureReason.Goodbye;
        state.Voidlings.Remove(creature);
        state.DepartedVoidlings.Add(creature);
        return new GoodbyeResult(true, creature.Name);
    }
}
