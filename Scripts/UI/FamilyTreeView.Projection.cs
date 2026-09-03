using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Breeding;

namespace VoidlingGame;

public partial class FamilyTreeView
{
    /// <summary>
    /// Transitional adapter for the legacy tree layout. The view receives an immutable Application
    /// projection; snapshot DTOs only adapt that projection to the established card/connection code.
    /// Semantic appearance is copied too so archive-only ancestors keep their historical body/layers.
    /// </summary>
    public void Build(LineageTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var activeSnapshots = new List<VoidlingData>();
        var departedSnapshots = new List<VoidlingData>();
        foreach (var member in projection.Members)
        {
            var snapshot = MaterializeSnapshot(member);
            if (member.Presence == LineageMemberPresence.Departed)
                departedSnapshots.Add(snapshot);
            else
                activeSnapshots.Add(snapshot);
        }

        Build(projection.SelectedCreatureId, activeSnapshots, departedSnapshots);
    }

    private static VoidlingData MaterializeSnapshot(LineageMemberProjection member)
    {
        var snapshot = new VoidlingData
        {
            Id = member.CreatureId,
            Name = member.DisplayName,
            ParentAId = member.ParentAId,
            ParentBId = member.ParentBId,
            FamilyGeneration = member.FamilyGeneration,
            TintHex = string.IsNullOrWhiteSpace(member.TintHex) ? "#F6F0C9" : member.TintHex,
            Appearance = new VoidlingAppearanceData
            {
                VisualTypeId = member.VisualTypeId,
                PaletteHue = member.PaletteHue,
                LayerIds = member.LayerIds.ToList()
            },
            InbreedingHistoryFlag = member.InbreedingHistoryFlag,
            InbreedingBurdenLevel = member.ActiveInbreedingBurden ?? 0
        };
        snapshot.Appearance.Normalize();

        foreach (var stat in member.Stats)
        {
            snapshot.Genome.AbilityGenes[stat.StatId] = new GenePairData
            {
                AlleleA = stat.AlleleA,
                AlleleB = stat.AlleleB,
                ExpressedAlleleIndex = stat.ExpressedAllele == stat.AlleleB && stat.AlleleA != stat.AlleleB ? 1 : 0
            };
        }

        snapshot.RareTraits = member.RareTraitIds
            .Select(traitId => new RareTraitData { TraitId = traitId })
            .ToList();
        return snapshot;
    }
}
