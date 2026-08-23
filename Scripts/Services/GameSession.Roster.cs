using System.Collections.Generic;
using System.Linq;

namespace VoidlingGame;

public partial class GameSession
{
    public VoidlingData? FindVoidling(string id)
        => State.Voidlings.FirstOrDefault(v => v.Id == id);

    public VoidlingData? FindLineageVoidling(string id)
        => State.Voidlings.FirstOrDefault(v => v.Id == id)
           ?? State.DepartedVoidlings.FirstOrDefault(v => v.Id == id);

    public IReadOnlyList<VoidlingData> GetLineageVoidlings()
        => State.Voidlings.Concat(State.DepartedVoidlings).ToList();

    public bool IsDeparted(string id)
        => State.DepartedVoidlings.Any(v => v.Id == id);

    public void DiscardFailedEgg(string eggId)
    {
        var egg = State.OwnedEggs.FirstOrDefault(e => e.Id == eggId && e.State == EggState.Failed);
        if (egg == null)
            return;

        State.OwnedEggs.Remove(egg);
        SaveAndNotify("Removed the failed egg.");
    }

    public bool SayGoodbye(string creatureId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return false;

        State.Voidlings.Remove(creature);
        State.DepartedVoidlings.Add(creature);
        SaveAndNotify($"{creature.Name} left the farm forever. Their family record remains.");
        return true;
    }

    public string NameFor(string id)
        => FindLineageVoidling(id)?.Name ?? "Unknown";
}
