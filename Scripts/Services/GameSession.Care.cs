using Voidling.Application.Care;
using Voidling.Domain.Rules;

namespace VoidlingGame;

public partial class GameSession
{
    // Care currently has no save-shape or platform dependency, so its focused Application use case
    // can remain owned by this transitional facade until the broader session facade is retired.
    private readonly CareUseCase _care = new(CareInteractionRules.DemoDefaults);

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
