using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Persistence;

/// <summary>
/// Owns backward-compatible normalization of the serialized game-state aggregate.
/// Keep migrations explicit and monotonic: loading an old save may fill deterministic
/// defaults, but must never reroll existing genetics, eggs, lineage, or race data.
/// </summary>
public sealed class GameStateMigrationService
{
    public const int CurrentSaveVersion = 4;

    private readonly GameBalanceRules _rules;

    public GameStateMigrationService(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public void Normalize(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var previousVersion = state.SaveVersion;

        state.Voidlings ??= new List<VoidlingData>();
        state.DepartedVoidlings ??= new List<VoidlingData>();
        state.OwnedEggs ??= new List<EggData>();
        state.StoreEggs ??= new List<EggData>();
        state.TrainingItems ??= new Dictionary<string, int>(StringComparer.Ordinal);

        // Version 4 introduced persisted audio and race auto-finish settings.
        if (previousVersion < 4)
        {
            state.MasterVolume = 1.0f;
            state.AutoFinishRaces = true;
        }

        foreach (var statId in _rules.Genetics.StatIds)
        {
            if (!state.TrainingItems.ContainsKey(statId))
                state.TrainingItems[statId] = 0;
        }

        foreach (var creature in state.Voidlings.Concat(state.DepartedVoidlings))
        {
            creature.TrainingPoints ??= new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var statId in _rules.Genetics.StatIds)
            {
                if (!creature.TrainingPoints.ContainsKey(statId))
                    creature.TrainingPoints[statId] = 0;
            }

            creature.RareTraits ??= new List<RareTraitData>();
        }

        foreach (var egg in state.OwnedEggs.Concat(state.StoreEggs))
            egg.RareTraits ??= new List<RareTraitData>();

        state.SaveVersion = CurrentSaveVersion;
    }
}
