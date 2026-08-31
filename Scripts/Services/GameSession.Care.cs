using Voidling.Application.Care;
using Voidling.Domain.Rules;

namespace VoidlingGame;

public partial class GameSession
{
    // GameRules is configured from the exact same immutable balance resource before GameSession is
    // constructed, so care interactions cannot silently drift to a separate DemoDefaults ruleset.
    private readonly CareUseCase _care = new(GameRules.CareInteractionRules);

    public bool PetVoidling(string creatureId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return false;

        var result = _care.Pet(State, creatureId);
        if (!result.Succeeded)
            return false;

        var missionChanged = RecordDailyMissionEvent(DailyMissionEventKind.PetVoidling);
        var message = $"{creature.Name} enjoyed some attention.";
        if (result.Changed || missionChanged)
        {
            Save(showFeedback: true);
            StateChanged?.Invoke();
            if (result.Changed)
                RaiseGardenEvent(message);
        }

        ToastRequested?.Invoke(message);
        return true;
    }

    public bool MistreatVoidling(string creatureId)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return false;

        var result = _care.Mistreat(State, creatureId);
        if (!result.Succeeded)
            return false;

        if (!result.Changed)
            return true;

        Save(showFeedback: true);
        StateChanged?.Invoke();
        RaiseGardenEvent($"{creature.Name} disliked being thrown around.");
        return true;
    }
}
