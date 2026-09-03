using System.Collections.Generic;
using Voidling.Application.Breeding;
using Voidling.Application.Roster;

namespace VoidlingGame;

public partial class GameSession
{
    public VoidlingData? FindVoidling(string id)
        => _roster!.FindActive(State, id);

    public VoidlingData? FindLineageVoidling(string id)
        => _roster!.FindLineage(State, id);

    public IReadOnlyList<VoidlingData> GetLineageVoidlings()
        => _roster!.GetLineage(State);

    public bool IsDeparted(string id)
        => _roster!.IsDeparted(State, id);

    public LineageTreeProjection CreateLineageTreeProjection(string selectedCreatureId)
        => _lineageTreeProjection!.Create(State, selectedCreatureId);

    public CreatureProfileProjection? CreateCreatureProfileProjection(string creatureId)
        => _lineageTreeProjection!.CreateCreatureProfile(State, creatureId);

    public void DiscardFailedEgg(string eggId)
    {
        if (!_roster!.DiscardFailedEgg(State, eggId))
            return;

        SaveAndNotify("Removed the failed egg.");
        RaiseGardenEvent("A failed egg was removed from the garden.");
    }

    public bool SayGoodbye(string creatureId)
    {
        var result = _roster!.SayGoodbye(State, creatureId);
        if (!result.Succeeded)
            return false;

        SaveAndNotify($"{result.Name} left the farm forever. Their family record remains.");
        RaiseGardenEvent($"{result.Name} left the garden. Their family record remains.");
        return true;
    }

    public string NameFor(string id)
        => FindLineageVoidling(id)?.Name ?? "Unknown";
}
