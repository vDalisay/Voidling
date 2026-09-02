using Godot;
using Voidling.Application.Breeding;

namespace VoidlingGame;

public partial class GameSession
{
    private static readonly BreedingPairInfoProjectionService BreedingPairInfo = new();

    public BreedingPreview GetBreedingPreviewData(string parentAId, string parentBId)
        => _breeding!.Preview(State, parentAId, parentBId);

    public BreedingPairInfoProjection GetBreedingPairInfo(string parentAId, string parentBId)
        => BreedingPairInfo.Create(GetBreedingPreviewData(parentAId, parentBId));

    // Transitional compatibility text for legacy callers. New presentation should consume the
    // qualitative BreedingPairInfoProjection and own its localized wording.
    public string GetBreedingPreview(string parentAId, string parentBId)
    {
        var preview = GetBreedingPairInfo(parentAId, parentBId);
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

        var risk = DescribeLineageRisk(preview.LineageRisk);
        if (preview.Related)
            return $"Related pairing • {risk} lineage risk.";
        if (preview.IsCleanOutcross)
            return $"Clean outcross • lineage risk improves to {risk}.";
        return preview.ChildBurden > 0
            ? $"Unrelated pairing • lineage risk remains {risk}."
            : "Unrelated pairing • no inbreeding penalty.";
    }

    public bool TryBreed(string parentAId, string parentBId, Vector2 eggWorldPosition)
    {
        var parentAName = NameFor(parentAId);
        var parentBName = NameFor(parentBId);
        var previousSeedCounter = State.SeedCounter;
        var result = _breeding!.ExecuteAndPersist(
            State,
            parentAId,
            parentBId,
            NextSeed(),
            NewId(),
            eggWorldPosition.X,
            eggWorldPosition.Y,
            _stateRepository!);

        if (!result.Succeeded)
        {
            // Seed allocation is part of the durable breeding transaction. Failed validation or a
            // failed save must not consume an authoritative RNG seed that never produced an egg.
            State.SeedCounter = previousSeedCounter;
            ToastRequested?.Invoke(result.Failure switch
            {
                BreedingFailure.SameParent => "Choose two different Voidlings.",
                BreedingFailure.ParentNotAdult => "Both parents must be adults.",
                BreedingFailure.ParentOnCooldown => "A parent is still on breeding cooldown.",
                BreedingFailure.PersistenceFailed => "Could not save the new egg. Breeding was rolled back.",
                BreedingFailure.DuplicateAssetId => "Could not create a unique egg. Please try again.",
                BreedingFailure.InvalidEggId => "Could not create a valid egg. Please try again.",
                _ => "Choose two adults."
            });
            return false;
        }

        // ExecuteAndPersist has already durably written this exact state. Only now may presentation
        // observe/celebrate the new egg; animation remains downstream of authoritative state.
        StateChanged?.Invoke();
        var warning = result.Related
            ? $" Egg carries {DescribeLineageRisk(LineageRiskProjection.FromBurden(result.ChildBurden))} lineage risk."
            : "";
        ToastRequested?.Invoke($"Breeding produced an egg.{warning}");
        RaiseGardenEvent($"{parentAName} and {parentBName} produced a new egg.");
        return true;
    }

    private static string DescribeLineageRisk(LineageRiskBand risk)
        => risk switch
        {
            LineageRiskBand.None => "no",
            LineageRiskBand.Low => "low",
            LineageRiskBand.Moderate => "moderate",
            LineageRiskBand.High => "high",
            _ => "critical"
        };
}
