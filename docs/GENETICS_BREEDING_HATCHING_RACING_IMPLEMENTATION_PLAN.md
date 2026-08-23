# Voidling Genetics, Breeding, Hatching & Automated Racing Implementation Plan

**Status:** Initial research-backed architecture plan  
**Target:** Godot 4.x + C#  
**Presentation:** Top-down 2D pixel art  
**Mechanical references:** Sonic Adventure 2 Battle Chao Garden, public Chao reverse-engineering/modding work including Chao World Extended, with Pokéathlon Speed Course inspiration for race presentation.

> The goal is to reproduce the useful *system relationships* with original Voidling code, names, tuning and assets—not port original or mod source code.

---

## 1. Core design principle

The most important Chao Garden idea is that **genotype, phenotype and trained performance are different things**.

```text
Parents
  ↓
Genome (two alleles per inherited locus)
  ↓
Phenotype (which inherited values are expressed)
  ↓
Growth / training (levels, points, evolution influence)
  ↓
Current state (energy, fatigue, stress, happiness, etc.)
  ↓
Immutable race-entry snapshot
  ↓
Automated race simulation
```

This separation produces the long-term breeding game:

- a creature can visibly express a mediocre trait while carrying an excellent hidden allele;
- offspring can reveal traits that were hidden in a parent;
- training improves the individual without automatically improving its genes;
- evolution can permanently improve a particular expressed ability allele;
- two visually similar creatures can have very different breeding value;
- family lines matter instead of the player simply maxing one creature forever.

**Race logic must never read hidden alleles directly.** It only reads expressed traits, trained stats and current state.

---

## 2. Research basis and confidence

The original game does not expose a formal design document, so implementation should distinguish verified system structure from community-measured constants.

### High-confidence structural findings

Public SA2 mod-loader structures and Chao World Extended data corroborate separate storage for:

- paired ability genes;
- current stat grades;
- stat levels;
- stat point totals;
- evolution influence axes;
- alignment/evolution state;
- lifespan values;
- Mate Desire and other needs/emotions;
- a broad personality vector;
- parent/lineage data;
- appearance genes.

Chao World Extended also demonstrates that genetics benefits from extension hooks: it augments coat/color/eye behavior and breeding outcomes around the base gene pipeline.

### Values that should remain configurable

Community reverse-engineering is very useful, but sources/version behavior differ on details such as:

- higher-ability heterozygous expression being roughly 70–75%;
- exact lifecycle timing;
- reincarnation Happiness/Stress thresholds by version;
- some personality-expression details;
- exact legacy formulas turning stat points into race animation/movement timings.

These belong in `Ruleset` data rather than hard-coded conditionals.

---

## 3. Core inherited loci

### 3.1 Ability potential

Initial abilities:

- Run
- Swim
- Fly
- Power
- Stamina
- Intelligence
- Luck

Use paired alleles for each.

### 3.2 Ability grades

Use the familiar six-step scale initially:

```text
E = 0
D = 1
C = 2
B = 3
A = 4
S = 5
```

This is compact, readable and produces clear breeding goals.

Reference-style level-up point gain:

```text
PointGain = 13 + (Grade * 3) + RandomInteger(-2, +2)
```

Expected range:

| Grade | Gain per level |
|---|---:|
| E | 11–15 |
| D | 14–18 |
| C | 17–21 |
| B | 20–24 |
| A | 23–27 |
| S | 26–30 |

Put these values in `StatGrowthRules`, not in `CreatureStat`.

### 3.3 Heterozygous ability expression

When the two inherited ability alleles differ, reference research indicates a bias toward the higher grade.

Prototype rule:

```text
HigherAbilityAlleleExpressionChance = 0.70
```

Keep an alternate 0.75 calibration profile.

Example:

```text
Run genes = [C, A]
70% → A expressed
30% → C expressed
```

Homozygous `[S,S]` always expresses S.

This is preferable to “always choose the best” because it preserves recessive/hidden potential and genetic diversity.

---

## 4. Appearance genetics

Implement appearance as independent loci with expression policies rather than special cases in `BreedingService`.

Initial useful concepts:

- base color;
- tone/pattern family;
- shiny flag;
- special/jewel-style coat;
- later: eye traits, horns, ears, tail, glow, body tendency, markings.

Recommended reference-inspired policies:

### Color

- default/base color recessive to a special color;
- same + same → that color;
- default + special → special expresses;
- two different non-default colors → 50/50 expression initially.

### Shiny

Dominant special trait:

```text
[Normal, Shiny] → Shiny
```

### Special coat

A special coat can visually override base color/tone **without deleting the underlying alleles**. This is important for later generations.

Support a `VisualOverridePriority` in phenotype composition.

---

## 5. Personality and preference genetics

The Chao data model contains a broad inherited personality set plus taste/favorite-food concepts. Voidling should reserve a full semantic personality vector even if only a subset affects v1 gameplay.

Suggested stable internal dimensions:

1. Curiosity
2. Energy
3. Naivety
4. Appetite
5. Carefree tendency
6. Kindness
7. Solitude
8. Vitality
9. Recovery temperament
10. Skillfulness
11. Sociability/Charm
12. Chattiness
13. Fickleness

Store stable IDs; UI labels can change without save migration.

Normalize runtime personality to `[-1,+1]` or another documented range.

### Personality in racing

Personality should modify *strategy and variance*, not replace trained ability.

Recommended normal range: ±3–5%, hard cap around ±10%.

| Trait | Possible automated-race effect |
|---|---|
| Energetic | Earlier/more frequent boosts, greater risk of early exhaustion |
| Skillful | Lower execution-time variance and fewer small mistakes |
| Recovery-oriented | Faster stumble/hazard recovery |
| Curious | More likely to consider optional shortcuts |
| Fickle | More likely to reconsider a race plan |
| Carefree | Faster/riskier hazard decisions |
| Vitality | Slight fatigue resistance |
| Kindness | Primarily garden/social/breeding behavior, not raw speed |

---

## 6. Lifespan genetics

Create a paired locus such as:

```text
life.longevity
```

For the first prototype, expression can use the higher allele. Keep the actual game-time mapping in `LifecycleRules` so pacing can change without touching genomes.

---

## 7. Data-driven gene catalog

Avoid a giant class with `RunGeneA`, `RunGeneB`, `ColorGeneA`, etc.

Serialize stable gene IDs:

```text
ability.run
ability.swim
ability.fly
ability.power
ability.stamina
ability.intelligence
ability.luck
life.longevity
personality.energy
personality.skillful
appearance.base_color
appearance.shiny
appearance.special_coat
preference.fruit
```

Conceptual data structures:

```csharp
public sealed record GenomeLocusState(
    string GeneId,
    int AlleleA,
    int AlleleB,
    AlleleOrigin OriginA,
    AlleleOrigin OriginB);

public sealed record GenomeData(
    int SchemaVersion,
    IReadOnlyDictionary<string, GenomeLocusState> Loci);
```

A `GeneDefinition` should contain:

```text
Id
Category
Allele domain/range
InheritancePolicyId
ExpressionPolicyId
MutationPolicyId
DisplayVisibility
VisualOverridePriority
Tags
```

Use a hybrid model:

- stable string IDs in saves;
- typed accessors/enums for core abilities;
- validation against `GeneCatalog` at load time.

This gives extensibility without sacrificing type safety.

---

## 8. Deterministic genetics

Every egg receives a persistent 64-bit seed:

```text
EggSeed: ulong
```

### Do not use one sequential global RNG

If random calls are consumed in order, adding a new gene later could reroll every trait after it for the same seed.

Instead derive stable substreams:

```text
Hash(EggSeed, "inherit", GeneId, ParentAId)
Hash(EggSeed, "inherit", GeneId, ParentBId)
Hash(EggSeed, "express", GeneId)
Hash(EggSeed, "mutation", ModifierId)
```

Use an explicitly stable/versioned hash. Do not use `.GetHashCode()` for persistent determinism.

**Acceptance invariant:** adding an unrelated gene definition may not change existing gene outcomes for an old EggSeed.

---

## 9. Per-locus breeding

For each independently inherited locus:

```text
Parent A = [A1, A2]
Parent B = [B1, B2]

Child allele A = random(A1, A2)
Child allele B = random(B1, B2)
```

Each parent contributes exactly one allele.

Store parent-of-origin metadata. It is valuable for:

- debugging;
- family-tree tooling;
- breeder probability UI;
- future maternal/paternal effects;
- explaining a child's genotype.

Use independent assortment in v1. Chromosomal linkage/crossover can be a future inheritance policy rather than a foundation requirement.

---

## 10. Breeding eligibility and state

A reference-inspired adult can breed when:

- alive;
- adult/evolved;
- not in an incompatible transition;
- `MateDesire` is high enough;
- not on cooldown;
- an eligible partner is available.

Support both:

1. autonomous garden pairing;
2. future player-assisted breeding facility/UI.

Both must call the same `BreedingService.CreateEgg(...)`.

A breeding fruit/item should modify `MateDesire` or a mating-season status through the normal effect system; it should not be hard-coded inside genetics.

### Breeding pipeline

```text
1. Validate parents
2. Snapshot parent genomes/relevant statuses
3. Generate EggId + EggSeed
4. Inherit one allele from each parent for every locus
5. Run pre-expression breeding modifiers
6. Validate resulting genome
7. Resolve phenotype once
8. Run post-expression phenotype modifiers
9. Create lineage record
10. Create persistent EggData
11. Apply parent cooldown/MateDesire changes
12. Emit EggCreated
```

Extension contracts:

```csharp
public interface IBreedingModifier
{
    void Modify(BreedingContext context, MutableGenomeBuilder genome);
}

public interface IPhenotypeModifier
{
    void Modify(PhenotypeContext context, MutablePhenotypeBuilder phenotype);
}
```

Future uses:

- rare mutations;
- race medals/trophies;
- breeding consumables;
- seasonal events;
- biome effects;
- special species;
- unlockable breeding technology.

---

## 11. Phenotype snapshot

Resolve phenotype once and persist it.

```csharp
public sealed record ExpressedLocus(
    string GeneId,
    byte ExpressedAlleleIndex,
    int ExpressedValue);
```

The expressed index matters because evolution may improve only that allele.

### Evolution example

Before:

```text
Run genes = [B, A]
Expressed index = 1
Displayed = A
```

After Run evolution:

```text
Run genes = [B, S]
Displayed = S
```

The hidden B remains B.

This is one of the most important unit tests in the project.

---

## 12. Egg data and hatching

Persistent `EggData`:

```text
EggId
EggSeed
CreatedAtGameTime
Genome
PhenotypeSnapshot
ParentAId
ParentBId
Generation
IncubationProgress
HatchReadiness
HatchInteractionHistory
ShellCosmetic
RulesVersion
```

Suggested states:

```text
Fresh → Incubating → Ready → Hatching → Hatched
```

### Natural hatch

Incubation reaches a configured threshold.

### Gentle/rock hatch

Rocking accelerates hatching.

Optional reference-style ruleset behavior: gentle handling can mutate kindness/aggression-type **heritable values after phenotype resolution**. The newborn phenotype remains unchanged, but descendants can inherit the adjusted genome.

That creates a subtle multi-generation consequence from egg care.

### Rough/impact hatch

A strong impact can force hatching and apply separate effects such as:

- relationship penalty toward responsible actor;
- short daze;
- optional opposite genetic imprint in the reference profile.

Do not conflate relationship effects with DNA changes.

### Hatch extension point

```csharp
public interface IHatchEffect
{
    bool CanApply(HatchContext context);
    void Apply(HatchContext context, HatchResultBuilder result);
}
```

Future examples: incubator temperature, biome, moonlight, ritual, seasonal effect.

### Newborn initialization

At final hatch:

1. create stable `CreatureId`;
2. transfer genome;
3. transfer persisted phenotype;
4. initialize stats from expressed grades;
5. initialize runtime personality;
6. initialize needs/emotions;
7. initialize child-age timer;
8. initialize evolution influence to neutral;
9. initialize relationship to hatcher;
10. retain lineage IDs;
11. remove egg world entity;
12. emit `CreatureHatched`.

---

## 13. Stats and training

Recommended persistent/current stat:

```text
CreatureStat
- AbilityId
- Level
- Points
- ProgressToNextLevel
- ExpressedGrade
```

Normal training changes:

- progress;
- level;
- points.

It does **not** mutate inherited grade alleles.

Race code should convert point totals through designer curves:

```text
normalized = clamp(points / ReferencePointMax, 0, 1)
competency = StatCurve(normalized)
```

Never use raw point totals directly as pixel speed.

---

## 14. Evolution

Preserve the Chao-like distinction between trained points and hidden raising influence.

Suggested axes:

```text
SwimFlyInfluence
-1.0 = Swim
 0.0 = neutral
+1.0 = Fly

RunPowerInfluence
-1.0 = Run
 0.0 = neutral
+1.0 = Power
```

Prototype specialization threshold:

```text
0.50
```

At first evolution:

1. inspect influence axes;
2. choose strongest specialization above threshold or Generalist;
3. determine adult visual form;
4. increase the relevant **expressed** grade allele by one, capped at S;
5. Generalist/reference profile increases Stamina;
6. transition lifecycle state.

Retain `EvolutionMagnitude` and influence values even before continuous adult morphing exists so later visual evolution does not require a save redesign.

---

## 15. Lifecycle, death and reincarnation

```csharp
public enum LifeStage
{
    Egg,
    Child,
    Adult,
    Reincarnating,
    Dead
}
```

Reincarnation is useful because it lets a player keep an individual while restarting growth.

Reference-inspired Voidling policy:

- eligibility based on Happiness and Stress rules;
- levels reset;
- retain a fraction of points;
- genome remains;
- evolution influences reset;
- runtime needs/personality reinitialize;
- `CreatureId` remains stable;
- `ReincarnationCount++`;
- parent IDs remain stable;
- race history/trophies may remain as biography.

Prototype retained-points rule:

```text
RetainedPoints = floor(PreviousPoints * 0.10)
```

A reincarnation is **not** a new lineage generation.

---

## 16. Needs / emotions required initially

Reserve at least:

```text
Hunger
Energy
Fatigue
Stress
Boredom
Loneliness
MateDesire
Nourishment
Condition
Happiness
```

Race entry captures a snapshot of relevant condition. Garden simulation should not nondeterministically change an already-running race.

---

# 17. Automated race model

## Goals

1. Raised stats are visibly meaningful.
2. Different courses favor different bloodlines/builds.
3. Personality makes racers feel individual without overriding training.
4. Races are fun to watch in top-down pixel art.
5. Simulation is deterministic and can run headless.

The presentation can have the quick, kinetic feel of Pokéathlon while the underlying progression remains Chao-like.

## Reference-inspired stat roles

| Stat | Race role |
|---|---|
| Run | Ground movement and ordinary footing/trip reduction |
| Swim | Water movement |
| Fly | Aerial speed/distance |
| Power | Climbing/pushing/work obstacles |
| Stamina | Energy reserve and exhaustion |
| Intelligence | Puzzle/decision execution |
| Luck | In modern SA2B/HD, should not be treated as the ordinary anti-trip stat; that role belongs to Run |

### Luck rulesets

`Sa2bFidelity`:

```text
Luck race coefficient = 0
Run controls ordinary tripping
```

`VoidlingModern`:

Luck affects only tagged external uncertainty:

- hazard avoidance;
- beneficial random opportunities;
- rare mishaps;
- event/route tie breaks.

Semantic separation:

```text
Run = physical stability
Luck = external fortune
```

Luck never directly adds running speed.

---

## 18. Pure C# race simulation

`Voidling.Domain.Racing` should have **no dependency on**:

- `Node2D`;
- `AnimationPlayer`;
- `Sprite2D`;
- TileMap collision;
- physics callbacks.

Godot presentation consumes simulation state/events.

This enables:

- deterministic replays;
- unit tests;
- batch balancing;
- stable behavior across framerates;
- animation/VFX changes without balance changes.

### Race-entry snapshot

```text
RaceParticipantSnapshot
- CreatureId
- appearance/display reference
- Run competency
- Swim competency
- Fly competency
- Power competency
- Stamina competency
- Intelligence competency
- Luck competency
- race-relevant personality vector
- condition modifiers
- strategy profile
- deterministic seed salt
```

The simulator never reads the live garden creature after the race begins.

---

## 19. Data-driven course graph

Use a directed graph of race segments rather than one linear progress value.

Segment types:

- GroundSprint
- Water
- FlightGap
- Climb
- PushObject
- Balance/Hazard
- PuzzleGate
- RecoveryStretch
- ChoiceFork
- Shortcut
- BurstPad
- Pack/Crowd section
- FinishSprint

Example:

```text
               ┌─ Flight shortcut ───┐
Ground Sprint ─┤                     ├─ Climb ─ Finish
               └─ Safe bridge route ─┘
```

A strong Fly value can enable the shortcut; Intelligence can improve route judgment; Luck only influences explicitly random/fortune-tagged events.

---

## 20. Race performance formulas

All numeric relationships should be designer curves/resources.

### Ground movement

```text
GroundSpeed = BaseGroundSpeed
            * RunSpeedCurve(RunCompetency)
            * FatigueMultiplier
            * BoostMultiplier
            * BoundedPersonalityModifier
```

### Swimming

```text
SwimSpeed = BaseSwimSpeed * SwimCurve(SwimCompetency)
```

Low Swim can also cause inefficient animation and greater energy drain.

### Flight

Separate:

- flight speed;
- sustainable distance/control.

This allows a moderate flyer to clear a short gap while an elite flyer can use a longer shortcut.

### Power

Model strength interactions as work:

```text
CompletionTime = RequiredWork / PowerWorkRate
```

Useful for climb, push, shake/drop, break barrier.

### Intelligence

```text
DecisionDelay = Lerp(MaxDelay, MinDelay, IntelligenceCurve(Intelligence))
MistakeChance = BaseMistakeChance * (1 - IntelligenceSafetyCurve(Intelligence))
```

Keep a minimum animation/action delay even for elite competitors so actions remain readable.

### Run stability

```text
TripChance = SurfaceBaseTripChance
           * (1 - RunStabilityCurve(RunCompetency))
           * FatigueRiskMultiplier
           * PersonalityRiskMultiplier
```

### Modern Luck

```text
FortuneFailureChance = BaseFortuneChance
                     * (1 - LuckSafetyCurve(LuckCompetency))
```

Only apply this on explicitly tagged `Fortune` events.

---

## 21. Stamina and automatic strategy

```text
MaxRaceEnergy = BaseEnergy + StaminaCapacityCurve(StaminaCompetency)
CurrentEnergy = EntryCondition * MaxRaceEnergy
```

Energy drains from:

- movement;
- boosts;
- difficult terrain;
- swimming/flying/climbing;
- recovering from mistakes.

Exhaustion should cause a strong speed penalty, configurable by ruleset.

### Automated boost AI

Inputs:

- remaining distance;
- current rank;
- remaining energy;
- upcoming segment;
- own strengths/weaknesses;
- personality strategy modifiers.

Examples:

```text
Energetic → lower threshold to spend stamina
Careful → saves energy for clear opportunities/finish
Fickle → occasionally changes plan
Intelligent → better estimates whether upcoming segment warrants conservation
```

Intelligence may use known course information; it must not peek at future random outcomes.

---

## 22. Race determinism and replay

Every race receives:

```text
RaceSeed: ulong
```

Derive independent event streams:

```text
Hash(RaceSeed, CreatureId, SegmentId, "trip")
Hash(RaceSeed, CreatureId, SegmentId, "puzzle")
Hash(RaceSeed, CreatureId, SegmentId, "luck")
Hash(RaceSeed, CreatureId, SegmentId, "strategy")
```

Useful domain events:

```text
RaceStarted
SegmentEntered
SegmentCompleted
LaneChanged
BoostStarted
BoostEnded
Stumbled
Recovered
HazardSucceeded
HazardFailed
PuzzleStarted
PuzzleSolved
ShortcutChosen
Overtake
Finished
```

A debug `RaceEventLog` makes bug reports exactly reproducible.

---

## 23. Top-down pixel-art presentation

Expose simulation state:

```text
TrackProgress
LaneId
LateralOffset
CurrentSegmentId
ActionState
AnimationTag
Facing
SpeedNormalized
EnergyNormalized
StatusEffects
```

Presentation interpolates visual position and plays sprint/swim/fly/climb/stumble/burst animations.

**Critical invariant:** animation length does not determine race results. If the simulator says the climb ends on tick 315, visual animation fits that interval.

---

## 24. Project architecture

Recommended initial structure:

```text
Voidling/
├─ project.godot
├─ Voidling.csproj
├─ Scripts/
│  ├─ Domain/
│  │  ├─ Creatures/
│  │  │  ├─ CreatureId.cs
│  │  │  ├─ CreatureData.cs
│  │  │  ├─ LifeStage.cs
│  │  │  ├─ LineageRecord.cs
│  │  │  └─ NeedsState.cs
│  │  ├─ Genetics/
│  │  │  ├─ GeneDefinition.cs
│  │  │  ├─ GeneCatalog.cs
│  │  │  ├─ GenomeLocusState.cs
│  │  │  ├─ GenomeData.cs
│  │  │  ├─ PhenotypeSnapshot.cs
│  │  │  ├─ GenomeInheritanceService.cs
│  │  │  ├─ PhenotypeExpressionService.cs
│  │  │  ├─ ExpressionPolicies/
│  │  │  └─ MutationPolicies/
│  │  ├─ Breeding/
│  │  │  ├─ BreedingService.cs
│  │  │  ├─ BreedingContext.cs
│  │  │  ├─ BreedingEligibilityService.cs
│  │  │  └─ IBreedingModifier.cs
│  │  ├─ Hatching/
│  │  │  ├─ EggData.cs
│  │  │  ├─ EggIncubationService.cs
│  │  │  ├─ HatchingService.cs
│  │  │  └─ IHatchEffect.cs
│  │  ├─ Stats/
│  │  ├─ Evolution/
│  │  ├─ Lifecycle/
│  │  └─ Racing/
│  ├─ Application/
│  ├─ Presentation/
│  ├─ Persistence/
│  └─ DebugTools/
├─ Resources/
│  ├─ Genetics/
│  ├─ Growth/
│  ├─ Lifecycle/
│  ├─ Racing/
│  └─ Courses/
├─ Scenes/
├─ Tests/
└─ docs/
```

Keep pure domain services free of Godot nodes. Use Godot `Resource` assets for designer configuration, validate/convert them into immutable domain rules at startup.

Suggested resources:

```text
GeneCatalogResource
BreedingRulesResource
HatchingRulesResource
StatGrowthRulesResource
EvolutionRulesResource
LifecycleRulesResource
RaceRulesResource
RaceCourseResource
PersonalityRaceRulesResource
```

---

## 25. Ruleset profiles

Maintain at least:

```text
Sa2bReference
VoidlingModern
```

The reference profile is useful for validating research relationships.

The modern profile can intentionally change:

- lifecycle pacing;
- Luck behavior;
- stat caps;
- breeding information visibility;
- mutation content;
- race segment types;
- stamina punishment;
- personality influence.

This avoids undocumented magic constants accumulating over time.

---

## 26. Persistence

Save domain DTOs rather than scene nodes.

```text
SaveGame
- SaveVersion
- RulesVersion
- WorldTime
- Creatures[]
- Eggs[]
- RaceHistory[]
- Unlocks[]
```

`CreatureSaveDto` should include:

- CreatureId/name;
- genome;
- phenotype snapshot;
- stats;
- runtime personality;
- needs;
- lifecycle/evolution;
- parent IDs/generation;
- reincarnation count;
- summarized race history.

`EggSaveDto` should include:

- EggId/EggSeed;
- genome;
- phenotype;
- parent IDs;
- incubation;
- hatch interactions;
- shell cosmetic.

### Save migration rules

- version all serialized domain models;
- adding a new gene may not reroll old genes;
- derive deterministic defaults for newly introduced loci if needed;
- warn on unknown gene IDs;
- preserve unknown records when practical instead of silently deleting them.

---

## 27. Debug/breeder tooling

Build early.

### Genome inspector

Show:

```text
Gene | Allele A | Allele B | Expressed | Origin
Run  | B        | S        | S         | Parent A / Parent B
```

Also show EggSeed, expression stream, applied modifiers, mutations and evolution promotions.

### Family tree

Clickable ancestry/offspring graph with genome comparison.

### Offspring probability preview

Debug-only first. Given two parents, enumerate possible allele pairs and phenotype probabilities.

### Race inspector

Show effective competency, energy, current segment, multipliers, next decision, trip/hazard probability and deterministic stream/event seed.

### Batch simulator

Run 10,000+ races without rendering and export CSV/JSON:

- win rates;
- average finish time;
- energy remaining;
- trips;
- hazard failures;
- segment times;
- route choices.

This should be a primary balance tool.

---

## 28. Required automated tests

### Genetics

- child allele A always originates from Parent A's pair;
- child allele B always originates from Parent B's pair;
- heterozygous parent transmission approaches 50/50 across a large sample;
- `[S,S]` always expresses S;
- heterozygous ability expression matches configured 70/30 within tolerance;
- color, shiny and special-coat expression policies are independent;
- adding an unrelated gene does not reroll old outcomes for the same EggSeed;
- save/load never rerolls phenotype.

### Evolution

Given:

```text
Run genes [B,A]
Expressed index = 1
```

Run evolution must result in:

```text
[B,S]
```

and hidden B must remain unchanged.

Also test S cap and Generalist→Stamina promotion.

### Hatching

- natural hatch at configured incubation;
- rocking accelerates;
- impact can force hatch;
- optional hatch genetic imprint modifies genome after phenotype lock without rerolling the current phenotype;
- impact relationship penalty applies to responsible actor.

### Reincarnation

- levels reset;
- retained points are floored 10%;
- genome stays;
- evolution influences reset;
- CreatureId remains;
- parent IDs remain;
- reincarnation count increments.

### Racing

- same participants/course/seed = identical event log and finish order;
- higher Run improves expected ground time and lowers normal trip rate;
- higher Swim improves water time;
- higher Power reduces work/climb time;
- higher Intelligence reduces puzzle delay/errors;
- higher Stamina delays exhaustion;
- Luck has zero race effect under reference profile;
- modern Luck affects only Fortune-tagged events, not base ground speed;
- personality modifiers never exceed their cap;
- presentation/animations cannot alter headless result.

---

## 29. Initial vertical slice

Do not build a full garden first. Prove the lineage→raising→race loop.

### Content

- two hand-authored adult parents;
- seven ability genes;
- one color gene;
- shiny gene;
- three initially race-relevant personality genes;
- longevity gene;
- breeding;
- egg;
- natural/rock/impact hatching;
- newborn initialization;
- debug training;
- first evolution;
- one automated race course.

### Test course

```text
1. Ground sprint          → Run
2. Water strip            → Swim
3. Climb wall             → Power
4. Flight shortcut fork   → Fly + Intelligence decision
5. Hazard                 → Run footing; modern Luck only if Fortune-tagged
6. Finish sprint          → Run + Stamina
```

### Vertical-slice success condition

Breed several generations and observe reproducible differences in:

- genetic potential;
- expressed phenotype;
- training outcome;
- evolution;
- race behavior;
- offspring distributions.

Only broaden garden AI after this loop is fun and understandable.

---

## 30. Implementation phases

### Phase 0 — Bootstrap

- create Godot 4 C# project;
- establish pure domain/test boundary;
- deterministic RNG/hash utility;
- ruleset loader/validation;
- save serialization strategy;
- nullable reference types.

**Acceptance:** domain test runs without launching a Godot scene; fixed seed is stable across repeated runs.

### Phase 1 — Genetics foundation

Implement:

- GeneDefinition/GeneCatalog;
- GenomeLocusState/GenomeData;
- validation;
- deterministic inheritance;
- phenotype snapshots;
- expression policy registry;
- core abilities;
- appearance policies;
- personality/lifespan definitions;
- save/load round-trip;
- statistical tests.

**Acceptance:** same parents + same EggSeed produce the exact same child forever.

### Phase 2 — Breeding + egg + hatching

Implement:

- breeding eligibility/MateDesire;
- partner reservation;
- BreedingService;
- lineage;
- EggData/incubation;
- natural, rock and impact hatching;
- hatch effects;
- relationship hook;
- egg persistence.

**Acceptance:** create egg, save/load, hatch the exact same child.

### Phase 3 — Stats + evolution

Implement:

- seven stats;
- grade-dependent point growth;
- progress/leveling;
- influence axes;
- first evolution;
- expressed-allele promotion;
- debug training controls.

**Acceptance:** same child can be raised toward different evolution paths while hidden genes remain unchanged except explicit promotion.

### Phase 4 — Lifecycle + reincarnation

Implement:

- game-time age;
- lifespan expression;
- adult countdown;
- Happiness/Stress eligibility;
- reset/retention policy;
- death path.

**Acceptance:** hatch → mature → reincarnate → hatch again with stable genome/lineage identity.

### Phase 5 — Headless race simulator

Implement:

- participant snapshot;
- competency curves;
- stamina/energy;
- race-agent state;
- segment graph;
- sprint/swim/flight/climb/puzzle/hazard logic;
- auto boost strategy;
- deterministic event streams/log;
- batch runner.

**Acceptance:** 10,000 deterministic races can run headless for balance analysis.

### Phase 6 — Pixel-art race presentation

Implement:

- top-down track scene;
- presentation controller;
- lane interpolation;
- action animations;
- energy/rank UI;
- finish sequence.

**Acceptance:** changing animation/VFX never changes race results.

### Phase 7 — Garden integration

Implement:

- autonomous mating;
- egg world interaction;
- creature needs;
- training interactions;
- race registration;
- family/breeding UI.

**Acceptance:** complete raise → race → breed → offspring loop without debug controls.

### Phase 8 — Tooling/balance

Implement genome inspector, family tree, probability preview, race inspector, CSV batch analysis and ruleset comparison.

---

## 31. First feature branch after bootstrap

```text
feature/genetics-foundation
```

Recommended commit order:

1. deterministic RNG/hash streams;
2. stable IDs and core enums;
3. GeneCatalog validation;
4. genome DTO/runtime model;
5. one-allele-per-parent inheritance;
6. phenotype snapshot;
7. ability expression policy;
8. appearance policies;
9. personality/lifespan policies;
10. save round trip;
11. statistical tests;
12. debug genome dump.

Do not start mating animations or race visuals before deterministic genetics and persistence pass tests.

---

## 32. Prototype defaults

Lock these initially to avoid design paralysis:

```text
Ability grades: E–S / 0–5
Parental allele selection: 50/50
Higher heterozygous ability expression: 70%
Normal breeding mutation chance: 0%
Shiny: dominant
Default color: recessive
Different special colors: 50/50 expression
Longevity: higher allele expressed
Point gain: 13 + grade*3 + random(-2..2)
Max level: 99
Evolution specialization threshold: 0.50
Specialized first evolution: +1 expressed matching ability allele
Generalist first evolution: +1 expressed Stamina allele
Reincarnation point retention: floor(10%)
Personality race effect target: ±3–5%, hard max ~±10%
```

---

## 33. Intentional modernizations

Do not copy every old limitation just for fidelity.

Recommended modern improvements:

- make Luck useful in the modern profile through Fortune events;
- gradually expose breeding information to the player;
- use deterministic seeds/replays;
- separate simulation from presentation;
- data-drive genes and race courses;
- preserve deep ancestry;
- support explicit modifier pipelines;
- build probability/debug tools early;
- keep reference and modern rulesets side by side.

---

## 34. Future extension backlog

### Genetics

- de novo mutations;
- mutation items;
- linked loci/crossover;
- incomplete dominance;
- codominant patterns;
- size/body genes;
- eye/horn/tail genes;
- disease/resistance;
- biome adaptations;
- player genome research/identification.

### Breeding

- breeding facility;
- compatibility preferences;
- seasonal fertility;
- special combinations;
- lineage achievements;
- optional inbreeding coefficient only if it creates fun choices.

### Hatching

- temperature;
- incubators;
- environmental imprinting;
- rare shell types;
- ritual/timed interactions.

### Lifecycle

- multiple adult evolution stages;
- continuous visual morphing;
- elder visuals;
- inherited/non-genetic legacy behaviors;
- reincarnation memories.

### Racing

- tournaments;
- discipline-specific cups;
- relay/team events;
- drafting;
- body contact/lane control;
- weather/surfaces;
- equipment;
- ghost/replay races;
- asynchronous player bloodlines.

---

## 35. Definition of done for the foundation

- [ ] Every inherited locus contains two alleles.
- [ ] Child receives one allele from each parent per locus.
- [ ] Phenotype is resolved separately and persisted.
- [ ] Hidden alleles do not directly affect race performance.
- [ ] Training changes levels/points, not genes.
- [ ] Evolution promotes only the expressed ability allele.
- [ ] Eggs save/load without rerolling.
- [ ] Hatch interactions use explicit, testable effects.
- [ ] Reincarnation preserves genome and lineage identity.
- [ ] Race entry uses immutable snapshots.
- [ ] Race simulation runs without Godot presentation.
- [ ] Race outcome is deterministic from seed/config/participants.
- [ ] Run/Swim/Fly/Power/Stamina/Intelligence have visibly different race roles.
- [ ] Luck behavior differs explicitly between reference and modern profiles.
- [ ] Personality effects are bounded and inspectable.
- [ ] Adding a new gene does not reroll old genes for the same seed.
- [ ] Batch simulator can run thousands of races.
- [ ] Genome inspector can explain offspring outcomes.

---

## 36. Public research references

Behavior/structure references used for this plan:

### Chao Island Wiki / Chao research

- Genetics
- Stats
- Breeding
- Egg
- Evolution
- Age
- Death
- Personality & Emotion
- Chao Races (SA2)
- Chao World Extended

Primary reference index: https://chao-island.com/wiki/

### Public reverse-engineering/mod repositories

- `Exant64/CWE` — Chao World Extended; notably `CWE/al_gene.cpp` and `CWE/SA2Structs.h`.
- `Exant64/ChaoModding_Docs` — Chao data-structure documentation.
- `X-Hax/sa2-mod-loader` — public SA2 structure documentation.

These sources corroborate the architectural separation of paired genes, current/expressed grades, levels/points, evolution influence, personality/emotions and lineage data.

---

## 37. Architectural rule of thumb

For every future feature, ask:

```text
Inherited?              → Genome
Expression of a gene?   → Phenotype
Learned/trained?        → Growth/current stats
Temporary?              → Needs/emotions/state
Life transition?        → Lifecycle/evolution
Changes conception?     → Breeding modifier
Changes egg handling?   → Hatch effect
Changes competition?    → Race snapshot/rules
Only visual?            → Presentation
```

If these boundaries remain intact, Voidling can grow from a small Chao-inspired breeding prototype into a much larger creature-raising game without genetics, garden AI and racing becoming one tightly coupled system.
