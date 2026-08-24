using Godot;
using Voidling.Application.Breeding;

namespace VoidlingGame;

public partial class GameSession
{
    public BreedingPreview GetBreedingPreviewData(string parentAId, string parentBId)
        => _breeding!.Preview(State, parentAId, parentBId);

    // Transitional compatibility text for legacy callers. New presentation should consume the
    // structured BreedingPreview and own its localized wording.
    public string GetBreedingPreview(string parentAId, string parentBId)
    {
        var preview = GetBreedingPreviewData(parentAId, parentBId);
        if (!preview.CanBreed)
        {
            return preview.Failure switch
            {
                BreedingFailure.SameParent => "Choose two different Voidlings.",
                BreedingFailure.ParentNotAdult => "Both parents must be adults.",
                BreedingFailure.ParentOnCooldown => "One parent is still on breeding cooldown.",
                _ => "Choose two adults."
            };
        }

        if (preview.Related)
            return $"Related pairing • inbreeding level {preview.ChildBurden} • {preview.HatchFailurePercent}% hatch-failure risk.";
        if (preview.IsCleanOutcross)
            return $"Clean outcross • inherited burden falls to level {preview.ChildBurden}.";
        return preview.ChildBurden > 0
            ? $"Unrelated pairing • inherited burden remains level {preview.ChildBurden}."
            : "Unrelated pairing • no inbreeding penalty.";
    }

    public bool TryBreed(string parentAId, string parentBId, Vector2 eggWorldPosition)
    {
        var preview = GetBreedingPreviewData(parentAId, parentBId);
        if (!preview.CanBreed)
        {
            if (preview.Failure == BreedingFailure.ParentNotAdult)
                ToastRequested?.Invoke("Both parents must be adults.");
            else if (preview.Failure == BreedingFailure.ParentOnCooldown)
                ToastRequested?.Invoke("A parent is still on breeding cooldown.");
            return false;
        }

        var parentAName = NameFor(parentAId);
        var parentBName = NameFor(parentBId);
        var result = _breeding!.Execute(
            State,
            parentAId,
            parentBId,
            NextSeed(),
            NewId(),
            eggWorldPosition.X,
            eggWorldPosition.Y);

        if (!result.Succeeded)
            return false;

        var warning = result.Related
            ? $" Egg carries level {result.ChildBurden} inbreeding risk ({result.HatchFailurePercent}%)."
            : "";
        SaveAndNotify($"Breeding produced an egg.{warning}");
        RaiseGardenEvent($"{parentAName} and {parentBName} produced a new egg.");
        return true;
    }
}
