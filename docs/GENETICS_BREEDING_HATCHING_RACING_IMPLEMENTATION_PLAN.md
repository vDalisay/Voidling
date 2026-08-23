# Voidling Genetics, Breeding, Hatching & Automated Racing Implementation Plan

**Status:** Research-backed architecture plan, revised with current Voidling product requirements  
**Target:** Godot 4.x + C#  
**Presentation:** Top-down 2D pixel art  
**Mechanical references:** Sonic Adventure 2 Battle Chao Garden, public Chao reverse-engineering/modding work including Chao World Extended, with Pokéathlon Speed Course inspiration for race presentation.

> The goal is to reproduce the useful *system relationships* with original Voidling code, names, tuning and assets—not port original or mod source code.

### Current Voidling product decisions

These decisions override reference-game fidelity throughout this document:

- core race stats are **Run, Swim, Fly, Power and Stamina only**; there is no Intelligence or Luck stat;
- breeding is **always player-initiated**;
- eggs cannot be force-hatched;
- store eggs have their complete stat/genetic roll locked when the individual egg enters store inventory;
- bred eggs randomize inheritance from the selected parents when the egg is created, then remain fixed;
- repeated inbreeding creates a persistent 20% → 50% → 80% → 100% hatch-failure burden that is cleansed one level per unrelated, burden-0 outcross generation;
- family trees retain historical inbreeding marks even after the active burden is cleansed;
- extremely rare appearance traits can found short prestige bloodlines: the founder and generation-1 child can transmit them, while generation-2+ carriers cannot.

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

**Voidling does not use Intelligence or Luck as creature stats.** Decisions, route choice, hazard outcomes and behavioral variance must be modeled through course rules, personality, condition and deterministic race randomness instead of hidden substitute Intelligence/Luck values.

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

### Rare appearance traits ("shiny-level" traits)

Voidling should support **very rare appearance traits** that sit above ordinary color/pattern inheritance. Examples:

- extra-shiny rendering;
- an unusual palette/color mutation;
- rare glow/highlight treatment;
- special markings;
- other future prestige cosmetics.

These traits are intentionally rare enough to create memorable bloodlines. Their initial acquisition chance must be data-driven and extremely low.

A rare appearance trait has explicit provenance:

```text
TraitId
FounderCreatureId
GenerationFromFounder
TransmissionEligible
```

Inheritance rule:

```text
Founder (generation 0)       → may transmit
Founder child (generation 1) → may transmit
Generation 2+ descendant     → may express/carry, but may NOT transmit
```

So a founder can pass the trait to a child; that first-generation child can pass it one more time; any second-generation-or-later recipient is a terminal carrier for that special trait. It remains visible/in DNA for history and phenotype purposes, but the breeding service excludes it from further transmission.

The per-breeding transmission probability for an eligible carrier is configurable (prototype default: normal single-allele 50% transmission when only one transmissible copy is present).

**Important:** generation depth belongs to the rare trait instance/provenance, not to the creature's ordinary family-tree generation. A descendant can carry multiple rare traits with different founders and independent transmission depths.

### Special coat

A special coat or rare appearance trait can visually override base color/tone **without deleting the underlying ordinary appearance alleles**. This is important for later generations.

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
life.longevity
personality.energy
personality.skillful
appearance.base_color
appearance.shiny
appearance.rare_trait.*
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

## 10. Breeding eligibility, player control and inbreeding

Breeding is **player-initiated only**. Voidlings must never autonomously choose a mate or create an egg in the garden.

A Voidling can be selected for breeding when:

- alive;
- adult/evolved;
- not in an incompatible transition;
- not on breeding cooldown;
- otherwise allowed by current breeding rules.

The player selects both parents through the breeding interaction/facility. There is no autonomous garden pairing and `MateDesire` is not required for the core system.

A breeding item may still modify fertility/cooldown/eligibility through the normal effect system, but it must not trigger autonomous mating.

### Inbreeding detection and lineage burden

The family tree is mechanically relevant. `RelationshipService` determines whether the selected parents count as related under a configurable ancestry rule. At minimum, the implementation must correctly detect direct ancestor/descendant and sibling relationships; deeper shared-ancestor depth should be configurable.

Every creature stores:

```text
InbreedingBurdenLevel: 0..4
InbreedingHistoryFlag: bool
```

The family tree must visibly mark creatures/eggs that were produced through an inbreeding event and retain that historical mark even if later generations cleanse the active burden.

When two related Voidlings breed, calculate the offspring burden before hatch resolution:

```text
NewBurden = clamp(max(ParentA.Burden, ParentB.Burden) + 1, 1, 4)
```

This produces the intended escalating hatch-failure ladder:

| Offspring burden | Hatch failure chance |
|---|---:|
| 0 | 0% |
| 1 | 20% |
| 2 | 50% |
| 3 | 80% |
| 4 | 100% |

Examples:

```text
clean related pairing              → burden 1 → 20%
burden-1 lineage inbred again      → burden 2 → 50%
burden-2 lineage inbred again      → burden 3 → 80%
burden-3 lineage inbred again      → burden 4 → 100%
```

An egg that fails this check becomes permanently non-viable and never hatches. The failure is resolved once and persisted; reloading may not reroll it.

### Cleansing the inbreeding burden

A burdened Voidling can reduce the active penalty by breeding with an **unrelated, burden-0 Voidling**:

```text
NewBurden = max(BurdenedParent.Burden - 1, 0)
```

Examples:

```text
burden 2 × clean unrelated → child burden 1 (20%)
burden 1 × clean unrelated → child burden 0 (no active penalty)
```

This takes one clean outcross generation per burden level. Historical inbreeding marks remain in the family tree even after active burden reaches 0.

If both selected parents carry active burden, or the partner is related, no cleansing step occurs. A related pairing always uses the escalation rule above.

Keep burden/failure rules in `BreedingRulesResource` so percentages and relatedness depth are balance data rather than hard-coded values.

### Breeding pipeline

```text
1. Validate player-selected parents
2. Snapshot parent genomes/relevant statuses
3. Determine relatedness and resulting `InbreedingBurdenLevel`
4. Generate EggId + EggSeed
5. Inherit one allele from each parent for ordinary loci
6. Resolve rare-trait transmission eligibility/generation-from-founder
7. Run pre-expression breeding modifiers
8. Validate resulting genome
9. Resolve phenotype once
10. Resolve and persist inbreeding hatch-failure outcome
11. Create lineage record and historical inbreeding mark when applicable
12. Create persistent EggData
13. Apply parent breeding cooldown changes
14. Emit EggCreated
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
EggSource (Store / Bred / Other)
CreatedAtGameTime
GeneticsRolledAt
Genome
PhenotypeSnapshot
ParentAId
ParentBId
Generation
InbreedingBurdenLevel
InbreedingFailureResolved
IsViable
IncubationProgress
HatchReadiness
HatchInteractionHistory
ShellCosmetic
RulesVersion
```

Suggested states:

```text
Fresh → Incubating → Ready → Hatching → Hatched
                          ↘ Failed / NonViable
```

### Store-bought egg generation

**Every store egg must have its stats/genetics predetermined the moment that exact egg enters store inventory.**

When a store restock creates an egg listing:

1. create the EggId and EggSeed;
2. roll its complete genome;
3. resolve its phenotype/stat grades;
4. roll any eligible extremely-rare founder appearance traits;
5. persist that exact egg as store inventory.

Previewing the egg, leaving the shop, saving/loading, or purchasing it may never reroll its stats. The player is buying a specific generated egg, not a template that rolls on purchase/hatch.

The UI may hide some or all of these predetermined values; hidden information does not mean ungenerated information.

### Bred egg generation

A bred egg is generated from the two player-selected parents. Its inheritance may be randomized according to those parents and the configured breeding rules.

The random result is resolved when the breeding event creates the egg and is then persisted. Hatching does **not** reroll the child. This preserves deterministic saves while still giving every breeding event a randomized offspring distribution.

### Natural hatch

Incubation reaches a configured threshold.

### Gentle/rock hatch

Rocking accelerates hatching.

Optional reference-style ruleset behavior: gentle handling can mutate kindness/aggression-type **heritable values after phenotype resolution**. The newborn phenotype remains unchanged, but descendants can inherit the adjusted genome.

That creates a subtle multi-generation consequence from egg care.

### Hatching constraints

There is **no force-hatch action** in the core design. Player interaction may accelerate incubation (for example rocking), but an egg can only hatch after reaching its allowed readiness state.

A non-viable inbred egg never reaches a successful hatch result. Its failed/non-viable state is persistent and cannot be bypassed through interaction.

### Hatch extension point

```csharp
public interface IHatchEffect
{
    bool CanApply(HatchContext context);
    void Apply(HatchContext context, HatchResultBuilder result);
}
```

Future examples: incubator temperature, biome, moonlight, ritual, seasonal effect. None may bypass a persisted non-viable egg unless a future explicit product requirement adds such a mechanic.

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
10. retain lineage IDs and `InbreedingBurdenLevel`;
11. retain rare-trait founder/generation provenance;
12. remove egg world entity;
13. emit `CreatureHatched`.

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

## Voidling stat roles

| Stat | Race role |
|---|---|
| Run | Ground movement and ordinary footing/trip reduction |
| Swim | Water movement |
| Fly | Aerial speed/distance |
| Power | Climbing/pushing/work obstacles |
| Stamina | Energy reserve and exhaustion |

There are **no Intelligence or Luck stats**. Course decisions and random events use:

- deterministic race RNG;
- course/segment rules;
- personality tendencies;
- current condition;
- explicit strategy settings where applicable.

No hidden replacement "mental" or "fortune" stat should be introduced unless it becomes a later explicit product requirement.

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

A strong Fly value can enable the shortcut. Route choice is handled by deterministic course logic plus personality/strategy rules rather than an Intelligence stat.

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

### Run stability

```text
TripChance = SurfaceBaseTripChance
           * (1 - RunStabilityCurve(RunCompetency))
           * FatigueRiskMultiplier
           * PersonalityRiskMultiplier
```

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
```

---

## 22. Race determinism and replay

Every race receives:

```text
RaceSeed: ulong
```

Derive independent event streams:

```text
Hash(RaceSeed, CreatureId, SegmentId, "trip")
Hash(RaceSeed, CreatureId, SegmentId, "hazard")
Hash(RaceSeed, CreatureId, SegmentId, "strategy")
Hash(RaceSeed, CreatureId, SegmentId, "route")
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
│  │  │  ├─ RelationshipService.cs
│  │  │  ├─ InbreedingBurdenService.cs
│  │  │  ├─ RareTraitInheritanceService.cs
│  │  │  └─ IBreedingModifier.cs
│  │  ├─ Hatching/
│  │  │  ├─ EggData.cs
│  │  │  ├─ EggGenerationService.cs
│  │  │  ├─ StoreEggFactory.cs
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
│  ├─ Breeding/
│  ├─ Store/
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
RareAppearanceTraitCatalogResource
StoreEggGenerationRulesResource
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
- active inbreeding burden + historical inbreeding marker;
- rare appearance trait provenance/transmission generation;
- reincarnation count;
- summarized race history.

`EggSaveDto` should include:

- EggId/EggSeed;
- genome;
- phenotype;
- parent IDs;
- egg source and genetics-roll timestamp/state;
- active inbreeding burden, historical mark and persisted viability result;
- rare appearance trait provenance/transmission generation;
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

It must also show:

- historical inbreeding marks;
- current active inbreeding burden level;
- whether an outcross reduced the burden;
- failed/non-viable eggs where appropriate;
- rare-trait founder and generation-from-founder provenance.

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
- color, shiny, rare-trait and special-coat expression policies are independent;
- rare appearance trait founder can transmit to generation 1;
- generation-1 carrier can transmit to generation 2;
- generation-2+ carrier cannot transmit that rare trait;
- rare-trait provenance survives save/load;
- adding an unrelated gene does not reroll old outcomes for the same EggSeed;
- save/load never rerolls phenotype;
- store egg genetics are generated when the inventory entry is created and never reroll on preview/purchase/hatch;
- bred egg genome is derived from parents when the egg is created and never rerolls on hatch;
- first related pairing produces burden 1 and a persisted 20% failure roll;
- repeated inbreeding escalates burden/failure rules to 50%, 80%, then 100%;
- burden-2 × clean unrelated produces burden 1;
- burden-1 × clean unrelated produces burden 0;
- historical family-tree inbreeding mark remains after burden reaches 0.

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
- rocking accelerates without bypassing readiness;
- no interaction can force hatch before readiness;
- persisted non-viable inbred egg never successfully hatches;
- hatch interaction effects do not reroll genome/phenotype.

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
- higher Fly improves eligible aerial routes;
- higher Stamina delays exhaustion;
- race logic contains no Intelligence or Luck competency;
- personality modifiers never exceed their cap;
- presentation/animations cannot alter headless result.

---

## 29. Initial vertical slice

Do not build a full garden first. Prove the lineage→raising→race loop.

### Content

- two hand-authored adult parents;
- five ability genes;
- one color gene;
- ordinary shiny gene plus one rare generational appearance-trait definition;
- three initially race-relevant personality genes;
- longevity gene;
- player-initiated breeding;
- relatedness detection + inbreeding burden;
- one clean-outcross burden-reduction path;
- one store egg whose roll is fixed at inventory generation;
- one bred egg generated from parents;
- egg;
- natural/rock hatching with no force-hatch path;
- newborn initialization;
- debug training;
- first evolution;
- one automated race course.

### Test course

```text
1. Ground sprint          → Run
2. Water strip            → Swim
3. Climb wall             → Power
4. Flight shortcut fork   → Fly + personality/strategy route rule
5. Hazard                 → Run footing + deterministic segment RNG
6. Finish sprint          → Run + Stamina
```

### Vertical-slice success condition

Breed several generations and observe reproducible differences in:

- genetic potential;
- expressed phenotype;
- training outcome;
- evolution;
- race behavior;
- offspring distributions;
- store-egg fixed rolls;
- escalating/cleansed inbreeding burden;
- rare appearance trait transmission limits.

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
- rare appearance trait provenance/transmission model;
- personality/lifespan definitions;
- save/load round-trip;
- statistical tests.

**Acceptance:** same parents + same EggSeed produce the exact same child forever.

### Phase 2 — Breeding + egg + hatching

Implement:

- player-controlled breeding eligibility;
- BreedingService;
- ancestry/relatedness detection;
- inbreeding burden escalation, cleansing and persisted failure roll;
- family-tree inbreeding marks;
- rare appearance-trait provenance/transmission limits;
- store egg generation at inventory-entry time;
- bred egg generation at breeding-event time;
- lineage;
- EggData/incubation;
- natural and rock hatching only;
- hatch effects;
- relationship hook;
- egg persistence.

**Acceptance:** create store and bred eggs, save/load without rerolls, verify the inbreeding failure ladder/outcross cleansing, and hatch the exact same viable child for a fixed seed.

### Phase 3 — Stats + evolution

Implement:

- five stats;
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
- sprint/swim/flight/climb/hazard/route-choice logic;
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

- player-initiated breeding integration;
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
8. appearance + rare-trait provenance policies;
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
Ordinary shiny: dominant
Rare appearance founder roll: extremely low, data-driven
Rare appearance trait transmissible generations: founder + generation 1 only
Inbreeding failure by burden: 0%=L0, 20%=L1, 50%=L2, 80%=L3, 100%=L4
Clean unrelated outcross: reduce burden by 1 generation
Store egg genetics: lock when inventory entry is generated
Bred egg genetics: roll from parents when egg is created
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

- gradually expose breeding information to the player;
- make store eggs stable inventory entities rather than rerolling templates;
- make lineage risk visible through inbreeding marks and burden levels;
- use generation-limited rare cosmetic inheritance to create prestigious but non-permanent bloodlines;
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
- [ ] Store egg genetics are locked when the egg enters store inventory.
- [ ] Bred egg genetics are rolled from selected parents at egg creation.
- [ ] Inbreeding burden escalates 20% → 50% → 80% → 100% failure and persists its roll.
- [ ] A clean unrelated outcross reduces burden exactly one level per generation.
- [ ] Family tree retains historical inbreeding marks after active burden is cleansed.
- [ ] Rare appearance traits follow founder/G1 transmission eligibility and stop transmitting from G2 onward.
- [ ] Hatch interactions use explicit, testable effects and cannot force a hatch.
- [ ] Reincarnation preserves genome and lineage identity.
- [ ] Race entry uses immutable snapshots.
- [ ] Race simulation runs without Godot presentation.
- [ ] Race outcome is deterministic from seed/config/participants.
- [ ] Run/Swim/Fly/Power/Stamina have visibly different race roles.
- [ ] Race domain contains no Intelligence or Luck stat.
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
