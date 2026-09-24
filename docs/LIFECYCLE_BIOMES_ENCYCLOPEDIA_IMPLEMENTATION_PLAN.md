# Lifecycle, biomes & encyclopedia implementation plan

**Status:** Draft for review. The direction comes from two design meetings in September 2026 and a follow-up question round. Anything marked **To confirm** is a stop condition.  
**Baseline:** `main` at `5a09bcd` (2026-09-09)  
**Prepared:** 2026-09-24  
**Source:** design meeting notes and answers to this plan's questions. Part A translates them; Appendix A keeps the original Dutch notes.

---

## 1. How to read this document

The document has two parts.

- **Part A: product direction.** What was decided, mapped onto how the game works today. It supplements `GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md` in the same way `PRODUCTION_VOIDLING_APPEARANCE_RULES.md` does. Where a rule here is marked **Decided**, it replaces the matching "unresolved" statement in the gameplay context. Rules marked **To confirm** stay unresolved. §11 lists every question and its status.
- **Part B: implementation plan.** Work packages, order, save and network impact, art needs and tests. It works inside the execution contract in `AGENT_HANDOFF_IMPLEMENTATION_PLAN.md` and the boundaries in `ARCHITECTURE.md`.

Agents must not answer a **To confirm** item themselves. Build the data-driven seam and stop.

## 2. Summary

The core pillars are **breeding, training, racing and collecting**. Collecting means filling the encyclopedia. Racing stays as it is. One loop connects the other three:

```text
place a baby on a biome hex ──> it slowly levels that biome's stat (Chao-style, 0–99)
        │
        ▼
adulthood: the highest stat (level 10+) picks the adult form ──> encyclopedia entry
        │
        ▼
merge duplicate biome tiles up to 4★ ──> special environment (Swamp)
        │
        ▼
breed two neutral Water adults, place their egg on a new Swamp ──> Swamp guy (once)
        │
        ▼
end of life: happiness ≥ 70 reincarnates (baby again, new form possible), otherwise death
```

| Area | Today on `main` | Direction | Status |
|---|---|---|---|
| Life stages | Child → adult after 45 s; the adult lives 6 h of open-game time | Baby → adult → end of life, in hours of open-game time | Decided; exact hours are tuning |
| Stats | Rank caps the level (E stops at level 2, S at 11) | Chao Garden: every rank levels 0–99; rank sets points gained per level | Decided (WP-K) |
| Adult form | Hidden Swim/Fly and Run/Power influence; the look stays `normal` | Highest stat at level 10+ picks the form; stamina highest or nothing at 10 → neutral | Decided |
| Reincarnation | Happiness ≥ 10 and stress ≤ 70 | Happiness ≥ 70 out of 100, nothing else | Decided |
| Stat breeding | Two alleles per stat, Chao Garden style | Same | No change |
| Color | Continuous hue color DNA on every Voidling | Continuous hue per color; a **neutral** color shows the artist's colors | Decided; form color ranges and neutral inheritance **to confirm** |
| Garden hexes | Training ground per stat; levels 1–3 bought with coins | Biomes; two duplicate tiles merge into one tile a star higher, up to 4★; 4★ = special environment | Decided; merge details **to confirm** |
| Special variant | None | Swamp guy from two neutral Water parents, egg placed on a new Swamp; respawn item after death | Decided; egg flow **to confirm** |
| Hatching | Every egg incubates 22 s | More S-rank stats and rare eggs take longer | Decided |
| Encyclopedia | None | Journal tab; one entry per form and special variant; "???" and a silhouette until discovered | Decided; old saves **to confirm** |
| Racing and multiplayer | Deterministic simulation, LAN/Steam multiplayer, trading | Keep as is | No change |

---

# Part A: Product direction

## 3. Core pillars

**Decided.** Breeding, training, racing and collecting are the core pillars. Collecting is new as a named pillar.

## 4. Life stages and stats

### 4.1 Stages and durations

**Decided.** A Voidling hatches as a **baby**, becomes an **adult** after some hours, and later reaches the end of its life, where it reincarnates or dies. All durations count **open-game time only** (Q1): Voidling is an idle game that runs while the PC is on, so durations are hours, not weeks.

The loop already runs (`LifeStage.Child` → `LifeStage.Adult` → cocoon → reincarnate or die, in `AdvanceSimulationUseCase`). Player-facing text says "baby"; the saved enum stays `Child`. The durations are data (`ChildToAdultSeconds`, `AdultLifespanSeconds` in `Resources/Balance/demo_balance.tres`). A test requires an adult to live at least six open-game hours (`Tests/Application/SimulationArchitectureTests.cs`).

### 4.2 Stats work exactly like Chao Garden

**Decided (Q2, Q2a).** Stats follow the Sonic Adventure 2 Chao Garden model:

- every stat has a **level from 0 to 99**, whatever its rank;
- training fills a progress bar; when it is full the stat gains a level and the bar empties;
- each level-up adds **stat points**: `3 × rank + 11 + a random 1 to 5`, with E = 0 … S = 5. An E-rank stat gains 12–16 points per level, an S-rank stat 27–31;
- stat points are capped (SA2's normal-play maximum is 3266; the hard cap is 4000);
- on reincarnation every stat goes back to **level 1** and keeps **10%** of its points, rounded down.

So an S-rank stat at level 99 is far stronger than a C-rank stat at level 99, and every rank can reach level 10. Sources: [Chao Island — Stats](https://chao-island.com/info-center/basics/stats.html), the [FelineWasteland stat calculator](https://felinewasteland.com/shrines/chao/statcalculator) (its code uses `3 × grade + 11 + random(1..5)` and the 3266/4000 caps), [All About Chao — Grades](https://chaohelp.weebly.com/grades.html).

**`main` does not work this way yet.** Today the rank caps the level:

| Rank | Training-point cap | Highest reachable level today |
|---|---|---|
| E | 20 | 2 |
| D | 40 | 4 |
| C | 60 | 6 |
| B | 80 | 7 |
| A | 100 | 9 |
| S | 120 | 11 |

`StatCalculator.GetLevel` computes level = 1 + training points ÷ 12, and `GetTrainingPointCap` stops training at the rank cap. The race value is `12 + 13 × rank + 0.55 × training points`, capped at 100. WP-K replaces this with the Chao model.

Consequences:

- level-up gains are random, so they must use `StableRandom` seeded per creature, stat and level, never frame or wall-clock randomness;
- the race simulation needs a new mapping from stat points to speed and stamina. That is a race balance pass, done once, deliberately;
- how much training fills one level (treats and passive rate) is tuning.

**Existing Voidlings (Q2b):** the answer was "don't convert, replace them with new ones". **To confirm** exactly what that means: see Q2b in §11.

### 4.3 Adult form from the highest stat

**Decided.**

- At the baby → adult transition, the stat with the **highest level** decides the adult form.
- That stat must be at least **level 10**.
- If **stamina** is highest, or **no stat reaches level 10**, the Voidling becomes a **neutral adult**.
- If two or more stats tie for highest, the form is an **equal random pick** between them (Q3), seeded per creature so it is reproducible.
- Adulthood still raises the expressed allele of the winning stat by one rank; neutral raises stamina (Q4).

The forms are Run, Swim (water), Fly, Power and Neutral. The form sets `Appearance.VisualTypeId`, so this is the first time adulthood changes the look. It replaces the hidden influence rule in `EvolutionService.ResolveFirstEvolution`.

**Existing Voidlings (Q5):** they stay neutral. Adults already look different (larger), and most current Voidlings are neutral, so no migration assigns forms to them.

### 4.4 Reincarnation or death

**Decided.** Happiness runs from 0 to 100. At the end of its life a Voidling reincarnates when happiness is **70 or more**, and dies otherwise. Happiness is the only condition (Q6): the stress check goes. Happiness stays hidden.

Balance facts from the current data:

- happiness starts at 0 at hatch and resets to 0 on reincarnation;
- petting and treats add 2, throwing removes 3, and happiness drains by 6 per open-game hour;
- a Voidling at 100 falls below 70 after 5 hours without care;
- the Garden care-risk warning uses the reincarnation threshold today (`AdvanceSimulationUseCase.IsCareLifecycleSafe`). At 70 it would fire on every dip, so it needs its own threshold.

After reincarnation the Voidling is a baby again (stats back to level 1, 10% of points kept) and gets a form again at its next adulthood.

## 5. Breeding and color

### 5.1 Stat inheritance: no change

Two alleles per stat, one from each parent, a chance to express the higher one, and a rare +1 rank breakthrough (`GenomeInheritanceService`). Nothing to build.

### 5.2 Colored and neutral Voidlings

**Decided.**

- Color stays a **smooth hue range** (Q7), as today.
- A **neutral** Voidling shows its form's **artist colors**, with no palette swap (Q8). The renderer already shows the sheet exactly as drawn at the identity hue (commit `22e0f6f`).
- A colored Voidling gets its form's line art in its own color.

**To confirm later:**

- **Color ranges per form.** Each form may only vary within its own range, for example Water Voidlings only within blues. How a colored baby's hue maps into its adult form's range is not defined yet (Q7a).
- **How neutral is inherited** and how store eggs get it (Q8a). The Swamp guy depends on this, because he needs two neutral parents.

## 6. Biomes

### 6.1 A biome on every training hex

**Decided.**

| Biome | Trains | 4★ special environment |
|---|---|---|
| Mountain | Fly | not named yet |
| Water | Swim | **Swamp** (Swamp guy) |
| Plains | Run | not named yet |
| Dry (desert) | Power | **Volcano** (no special variant yet) |
| Stamina biome (name to pick) | Stamina | none planned |

Stamina training tiles stay (Q10). Today a hex is converted into training ground for one stat and drawn as tinted grass with a signboard (`TrainingUseCase.ConvertHexToTrainingGround`, `Scripts/Garden/GardenController.Land.cs`). Biomes rename and re-skin that. A Voidling on a biome hex raises its stat very slowly (existing passive training, retuned). "Plains" clashes with "plain ground", today's name for an empty hex, so the player-facing words must differ.

### 6.2 Merging tiles and special environments

**Decided (Q11).** Two duplicate tiles merge into **one** tile one star higher, up to 4★. **Both old tiles disappear**, and the player chooses where to place the new one. A 4★ Water tile is a Swamp; a 4★ Dry tile is a Volcano. Mountain and Plains 4★ stay unnamed for now (Q12).

Levels 1–3 already exist, bought with coins (25, then 50), and the Build screen already draws the level as stars (`PaperCard.StarRating`). Merging replaces the coin upgrade.

**To confirm (Q11a):**

- **The island can split.** Hexes are the island's land. Placement rules keep the island in one piece (`GardenHexLayout.CanPlaceShape`), but removing two hexes can cut it in two, or leave Voidlings, eggs, treats or decorations standing on empty water.
- **What counts as a duplicate.** Presumably same biome and same star.
- **Where the new tile goes.** Presumably into the inventory as a one-hex piece, placed like a bought piece.

## 7. Special variant: the Swamp guy

The Swamp guy is the green-ish Water Voidling. The mechanism is data per variant, so a Volcano variant and others can follow.

**Decided.**

1. **Parents.** Only an egg from a **neutral Water × neutral Water** pair can become the Swamp guy (Q9).
2. **The egg.** The egg those parents lay is the Swamp guy's egg (Q13). It has to be **placed on a Swamp**; where the parents are doesn't matter, and owning a Swamp is enough (Q14).
3. **Once.** Breeding spawns him once per save.
4. **Genes.** Swim is rank S with **both** Swim alleles S. His other stats come from his parents as usual (Q16).
5. **Looks.** He stays a Swamp guy as a baby, an adult and after reincarnating (Q15). He can breed: his offspring inherit his stats normally, but his looks pass on as a **neutral Voidling's** color DNA, not as a Swamp guy (Q17).
6. **One at a time, not tradable.** Special variants cannot be traded (Q18).
7. **Death and respawn.** If he dies, or the player says Goodbye (which counts as death), the player can buy a respawn item in the shop. It creates a **new** Swamp guy (Q19). It only works on a **new** Swamp: one that has never produced a Swamp guy. The player can build as many Swamps as they like; a Swamp is not used up, but each Swamp can produce a Swamp guy only once.
8. **Racing.** His advantage is genetic (S/S Swim). No hidden race bonus.

**To confirm (Q13a): when the egg becomes a Swamp guy egg.** Two things clash:

- today bred eggs appear next to the parents and start incubating straight away (`GameSession.TryBreed`); only store eggs go to the inventory for the player to place (`ShopUseCase.PlaceStoredEgg`);
- the locked breeding rule says an egg's genes are fixed when the egg is created (handoff §4).

Recommended: when two neutral Water parents breed while the player owns an unused Swamp and no Swamp guy is alive, the egg is created as the Swamp guy egg (genes fixed then) and goes to the inventory. It can only be placed on an unused Swamp. Otherwise the egg is ordinary.

## 8. Egg hatching time

**Decided (Q20).** Incubation time depends on the egg's stats and rarity:

- each S-rank stat adds time: the more S ranks, the longer;
- a rare egg adds extra time: a special variant such as the Swamp guy, or an egg with a rare trait (Lustrous, Prismatic, Aurora).

It is worked out when the egg is created and then fixed. `EggData.RequiredIncubationSeconds` is already stored per egg, so eggs that exist today keep their time. Base time, time per S rank and rare bonuses are tuning. Whether A ranks and others also add time is tuning too.

## 9. Encyclopedia

**Decided.**

- A tab opens a journal showing how many Voidlings exist, as discovered out of total.
- **One entry per form and per special variant** (Q22): Baby, Neutral, Run, Swim, Fly, Power, Swamp guy, plus later variants.
- Undiscovered entries show **"???"** and a **silhouette**.
- Discovered entries show the sprite; clicking shows how you got it.
- **Hatching, evolving and spawning** unlock entries (Q23). Trading does not.
- Where an egg hatched plays no part (Q21): it is not recorded and gives no training bonus.

**To confirm (Q23a):** do existing saves unlock the forms they already own?

## 10. Racing and multiplayer: no change

The deterministic race simulation, multiplayer racing, trading and the connected Garden stay. The stat rework (WP-K) changes what the race reads, so single-player and multiplayer must switch together, in the same build.

## 11. Question status

### Decided

| # | Answer |
|---|---|
| Q1 | Open-game time only; durations in hours |
| Q2 / Q2a | Chao Garden stat model (§4.2) |
| Q3 | Tie for highest stat → equal random pick among the tied stats |
| Q4 | Keep the +1 rank at adulthood |
| Q5 | Existing Voidlings stay neutral |
| Q6 | Happiness ≥ 70 is the only condition |
| Q7 | Smooth hue; per-form ranges later |
| Q8 | Neutral shows the artist's colors |
| Q9 | Swamp guy only from neutral Water × neutral Water |
| Q10 | Keep stamina tiles |
| Q11 | Two duplicates merge into one tile a star higher; both disappear; player places the new one |
| Q12 | Keep Swamp and Volcano; others later |
| Q13 | The parents' egg is the Swamp guy egg |
| Q14 | Owning a Swamp is enough; the egg must be placed on the Swamp |
| Q15 | Swamp guy look is locked for life |
| Q16 | Other stats inherited normally |
| Q17 | He breeds; stats pass on, looks pass on as neutral |
| Q18 | Goodbye counts as death; special variants are untradable |
| Q19 | Respawn = a new Swamp guy, only on a Swamp that never produced one |
| Q20 | More S ranks and rarity (special variant, rare trait) mean longer incubation |
| Q21 | Hatch location does nothing |
| Q22 | One entry per form and per special variant |
| Q23 | Hatching, evolving and spawning unlock entries |

### Still open

- **Q2b — Existing Voidlings' stats.** "Replace them with new ones" could mean (a) keep every Voidling, its genes and lineage, but reset its stats to the new system at level 0; or (b) old saves start over. *Recommended:* (a). The save rules in `AGENTS.md` say existing saves are user data and must not be wiped without an explicit decision.
- **Q7a — Color ranges per form.** Which hue range each form allows, and what happens to a colored baby's hue when it becomes an adult of a form with a different range. *Defined later; not blocking.*
- **Q8a — How neutral is inherited.** For example: neutral is one more color profile won 50/50, or neutral only when both profiles are neutral. Also how often store eggs are neutral. *Blocks WP-F and the Swamp guy trigger.*
- **Q11a — Merging and the island.** Removing two hexes can split the island or strand Voidlings, eggs and decorations. Options: block a merge that would split the island; or allow islands to split. What happens to things standing on the removed tiles? Is a duplicate "same biome and same star"? Does the new tile go to the inventory? *Recommended:* block merges that split the island; things on removed tiles move to the nearest remaining hex; duplicate = same biome and same star; the new tile goes to the inventory. *Blocks WP-E.*
- **Q12 — Names** for Mountain 4★ and Plains 4★. *Not blocking.*
- **Q13a — Swamp guy egg flow.** See §7. *Recommended:* decided at breeding, then placed from the inventory onto an unused Swamp. Also: should all bred eggs go to the inventory for the player to place, like store eggs, or only Swamp guy eggs? *Blocks WP-G.*
- **Q23a — Existing saves in the journal.** *Recommended:* unlock the forms the save already owns or owned. *Blocks the WP-I migration only.*

### Numbers to tune (data only, not blocking)

Baby duration; adult lifespan; happiness gains, drain and care-warning threshold; training progress per level; stat-point cap; stat points → race speed mapping; passive training rate per star; respawn item price; neutral chance on store eggs; incubation base time, time per S rank and rare bonuses.

---

# Part B: Implementation plan

## 12. Baseline: extend, don't rebuild

| Need | Already on `main` | Extend here |
|---|---|---|
| Lifecycle loop | `AdvanceSimulationUseCase`, `ReincarnationService`, cocoon presentation | durations, happiness-only gate, form at adulthood |
| Stats | `StatCalculator`, `RankTrainingCaps`, `TrainingUseCase`, `PassiveTrainingService`, `RacePerformanceModel` | Chao levels and points (WP-K) |
| Adult form | `EvolutionService` (+1 rank promotion, never promotes twice) | new selection rule |
| Form to art | `VoidlingAppearanceData.VisualTypeId`, `VoidlingVisualCatalog`, `VoidlingVisualFactory` (unknown IDs fall back to `normal`) | register new definitions as art arrives |
| Genetics | `GenomeInheritanceService`, `GenomeFactory`, `ColorPhenotypeResolver` | neutral color; Swamp guy overrides |
| Hexes | `GardenModuleData`, `GardenHexLayout`, `TrainingUseCase` land methods | biome ID, merge, fourth star |
| Eggs | `EggData.RequiredIncubationSeconds` (per egg), `StoreEggFactory`, `BreedVoidlingsUseCase`, stored-egg placement | incubation policy; Swamp guy egg |
| Shop | `ShopUseCase`, `ShopItemIds` | respawn item |
| Trading | `TradeTransferService` (copies whole `VoidlingData` as JSON) | reject special variants |
| Portraits | `VoidlingPortraitComposer` | silhouette mode |
| Screens | grid plus detail card screens (`UI_UX_REMAINING_MENUS_PLAN.md`), Garden rail in `MainController` | journal screen |

## 13. Where the new rules live

This follows `ARCHITECTURE.md`.

- **Domain** (pure, deterministic): Chao stat levels and points; adult-form selection; biome catalog; merge rule; special-variant rule; incubation policy; encyclopedia catalog.
- **Application**: when rules run. Adulthood and level-ups in `AdvanceSimulationUseCase` and `TrainingUseCase`, merging in `TrainingUseCase` (or a new `GardenLandUseCase` if the file grows too large), the Swamp guy in `BreedVoidlingsUseCase` and egg placement, respawn in `ShopUseCase`, discoveries in a new encyclopedia use case, migrations in `GameStateMigrationService`.
- **Infrastructure**: authored data → Domain rules. Balance numbers join `GameBalanceResource`; biome, special-variant and encyclopedia content get their own Resources only when first read.
- **Presentation**: one biome visual catalog; forms and variants through the Voidling visual catalog; the journal screen; silhouettes through `VoidlingPortraitComposer`; log text through localization keys.

Rules for every package:

- Saves and network packets store semantic IDs (`water`, `swamp`, `swamp-variant`), never paths.
- Every random result (level-up points, tie picks, respawn genes) uses `StableRandom` substreams with explicit seeds.
- No new rules in `GameSession` or `GameRules`; they only forward typed events.
- New enum values are appended; existing values keep their numbers.

## 14. Work packages

### WP-A — Lifecycle tuning and the happiness gate

**Depends on:** nothing. **Open decisions:** none.

1. Set `ReincarnationMinimumHappiness` to 70 and the stress limit to 100 (off). Remove the stress check if nothing else needs it.
2. Give the care-risk warning its own threshold.
3. Set `ChildToAdultSeconds` and `AdultLifespanSeconds` in open-game hours.
4. Say "baby" in player-facing text.
5. Headless balance test: a scripted care routine ends a life at 70 or more; neglect does not.

**Acceptance:** thresholds come from data; tests pin the boundary (69.9 dies, 70 reincarnates).

### WP-K — Chao Garden stat levels

**Depends on:** nothing. **Open decisions:** Q2b.

1. Characterization tests of today's levels, caps and race values, so the change is visible.
2. Domain: each stat stores level (0–99), progress toward the next level, and points. A level-up adds `3 × rank + 11 + random(1..5)` points (constants as data), capped at the point cap. Randomness is seeded per creature, stat and level.
3. Training (treats and passive) fills progress; stats stop at level 99, not at a rank cap. The "training capped" event fires at level 99.
4. Reincarnation: levels back to 1, keep 10% of points (the existing retained fraction is already 10%).
5. Race: `RaceParticipantSnapshotFactory` and `RacePerformanceModel` read stat points through one mapping (data). Single-player and multiplayer use the same code. Retune race balance and update race tests deliberately.
6. Migration per Q2b, with a save round-trip test.
7. UI: stat cards show level 0–99, points and the rank letter, like Chao Garden.

**Acceptance:** an E-rank and an S-rank stat can both reach level 99; at equal level the S-rank stat has more points and races faster; the same seed gives the same points.

### WP-B — Adult form from the highest stat

**Depends on:** WP-K. **Open decisions:** none.

1. Tests first: highest level wins; stamina highest → neutral; nothing at level 10 → neutral; ties pick evenly and reproducibly; +1 promotion capped at S; never promotes twice.
2. Replace the selection in `EvolutionService.ResolveFirstEvolution`. Minimum level (10) is data. Retire `SpecializationThreshold` without breaking older `.tres` files.
3. Map forms to visual type IDs: run → `run`, swim → `water`, fly → `fly`, power → `power`, neutral → `normal`. Set it at adulthood; reincarnation sets it back to `normal`.
4. `EvolutionSpecialization.Generalist` means neutral; the UI says "Neutral".
5. No migration: existing adults stay neutral (Q5).
6. Garden log line per form, with localization keys (today's lifecycle lines in `GameSession.cs` are English literals; convert the ones you touch).

**Acceptance:** a baby trained to level 10+ in one stat becomes that form; forms without art render with `normal` art.

### WP-C — Biomes on training hexes

**Depends on:** nothing. **Open decisions:** none (Q12 names can come later).

1. Domain `BiomeCatalog`: biome ID → stat, 4★ environment ID.
2. `GardenModuleData.BiomeId`, derived by migration from `StatId` (run → plains, swim → water, fly → mountain, power → dry, stamina → the stamina biome). `StatId` stays as the cached stat.
3. Hexes are converted by biome.
4. One biome visual catalog for ground art and signs; until art arrives, keep tinted grass and signs.
5. Localized biome names, distinct from "plain ground".

**Acceptance:** old saves load with the right biomes and unchanged training; no texture paths outside the catalog.

### WP-E — Merging tiles and special environments

**Depends on:** WP-C. **Open decisions:** Q11a.

1. Domain merge rule: two duplicate tiles → one tile one star higher, max 4★; the connectivity check per Q11a.
2. Application: both tiles are removed; the new tile goes where Q11a says; Voidlings training on removed tiles are unassigned; things standing on them are handled per Q11a.
3. At 4★ the biome becomes its special environment (`water` → `swamp`, `dry` → `volcano`), with its own passive rate.
4. Existing coin-bought levels stay; the coin upgrade button is replaced by merging.
5. Presentation: merge flow in Build, four-star row, special environment art.

**Acceptance:** no merge can break the rule chosen in Q11a; no tile passes 4★.

### WP-F — Neutral color

**Depends on:** nothing. **Open decisions:** Q8a (Q7a later).

1. Explicit neutral marker per color profile in `GenomeData` (not a special hue; `-1` already means "legacy").
2. Inheritance per Q8a; store-egg chance as data.
3. `VoidlingAppearanceData` carries the color mode; presentation skips the palette swap for neutral; the connected-Garden snapshot gets the field.
4. Existing Voidlings: Q5 calls most of them neutral in look. Decide with Q8a whether their color DNA becomes neutral in the migration. Nothing is rerolled.
5. Update `PRODUCTION_VOIDLING_APPEARANCE_RULES.md`.

### WP-G — Special variants (Swamp guy)

**Depends on:** WP-B, WP-E, WP-F. **Open decisions:** Q13a.

1. Domain special-variant definition (data): parent form and color (neutral Water ×2), required environment (Swamp), forced alleles (Swim S/S), locked visual type (`swamp-variant`), respawn item ID, neutral looks DNA.
2. Save: `GameStateData.SpecialVariants` (spawned once? alive creature ID? departed?); per Swamp tile, whether it has produced a Swamp guy; `SpecialVariantId` on `VoidlingData` and `EggData`.
3. Egg flow per Q13a: the egg is flagged and fixed at creation, placed from the inventory, only onto an unused Swamp. Placing it marks that Swamp as used.
4. Lifecycle: the look stays locked; evolution and reincarnation leave it alone.
5. Breeding with him: stats inherit normally; his color DNA is neutral; offspring are never variants.
6. Death and Goodbye → departed → the respawn item is buyable; using it on an unused Swamp creates a new Swamp guy egg there (new ID, founder genes except Swim S/S).
7. Trading rejects special variants and their eggs.
8. Presentation: log lines, shop card, "use on Swamp", art via the visual catalog (falls back to `normal` until registered).

**Acceptance:** breeding spawns him at most once per save; never without two neutral Water parents; Swim exactly S/S; at most one alive; each Swamp produces at most one; respawn only after departure; trades rejected.

### WP-H — Incubation from stats and rarity

**Depends on:** nothing (the Swamp guy bonus plugs in with WP-G). **Open decisions:** none.

1. Domain incubation policy (data): base time + time per S-rank stat + bonus per rare trait + bonus for a special variant.
2. Bred, store-bought and Swamp guy eggs set `RequiredIncubationSeconds` when created. Existing eggs keep their time.

**Acceptance:** an egg with more S ranks takes longer; a rare egg takes longer; the same egg always gets the same time.

### WP-I — Encyclopedia

**Depends on:** WP-B. Swamp guy entry with WP-G. **Open decisions:** Q23a.

1. Domain `EncyclopediaCatalog`: one entry per form and special variant, stable IDs, name keys.
2. Save: `GameStateData.Encyclopedia` with discovered entry IDs and how (hatched, evolved, spawned).
3. Application: record from typed hatch, adulthood and spawn events; journal projection with the discovered / total count.
4. Migration per Q23a.
5. Presentation: journal button on the Garden rail; grid plus detail card; "???" and a silhouette from `VoidlingPortraitComposer`; named nodes for the CI smoke; localization keys.

**Acceptance:** undiscovered entries never reveal a name; trading never unlocks an entry; the smoke test finds the screen.

### WP-J — Art and content

Through the existing art pipeline (`Assets/Voidlings/README.md`). Nothing waits on art.

- adult bodies: `water`, `fly`, `power`, `run` (neutral uses `normal` at adult scale);
- the Swamp guy (artist colors), baby and adult;
- biome ground art for five biomes plus Swamp and Volcano, and star indicators;
- respawn item icon; journal UI art.

WP-D (hatch biome) from the first draft is dropped: hatch location does nothing (Q21).

## 15. Order and PR slices

| Package | Needs first | Blocked by |
|---|---|---|
| WP-A | nothing | nothing |
| WP-K | nothing | Q2b |
| WP-B | WP-K | nothing |
| WP-C | nothing | nothing |
| WP-E | WP-C | Q11a |
| WP-F | nothing | Q8a |
| WP-G | WP-B, WP-E, WP-F | Q13a |
| WP-H | nothing | nothing |
| WP-I | WP-B | Q23a (migration only) |
| WP-J | nothing | art being supplied |

Suggested order: WP-A, WP-C and WP-H now; WP-K once Q2b is answered, then WP-B and WP-I; WP-E and WP-F once their questions are answered; WP-G last.

Branches: `feature/lifecycle-happiness-gate`, `feature/garden-biomes`, `feature/incubation-time`, `feature/chao-stat-levels`, `feature/adult-form-highest-stat`, `feature/encyclopedia`, `feature/biome-tile-merge`, `feature/neutral-color`, `feature/special-variant-swamp`. Keep balance changes and presentation refactors in separate PRs.

## 16. Save and network compatibility

The current save version is 22 (`GameStateMigrationService.CurrentSaveVersion`). Each package that adds state bumps it once and adds a round-trip test from a version-22 save.

| Change | Field | Migration |
|---|---|---|
| Chao stats | level, progress and points per stat on `VoidlingData` (new); `TrainingPoints` retired | per Q2b |
| Adult form look | `Appearance.VisualTypeId` (exists) | none (Q5) |
| Biome | `GardenModuleData.BiomeId` (new) | derived from `StatId` |
| Merge / 4★ | `GardenModuleData.Level`, `BiomeId` | none |
| Special variant | `SpecialVariantId` on creatures and eggs; `GameStateData.SpecialVariants`; per-Swamp "used" flag (new) | empty |
| Neutral color | markers in `GenomeData`, color mode in `VoidlingAppearanceData` (new) | with Q8a |
| Incubation | `EggData.RequiredIncubationSeconds` (exists) | none |
| Journal | `GameStateData.Encyclopedia` (new) | per Q23a |

Network: trades copy `VoidlingData` as JSON, so new fields travel; validation must reject special variants. The connected-Garden snapshot gains the neutral color field. The stat rework changes race inputs, so both players in a multiplayer race must run the same build; bump the multiplayer protocol version with WP-K.

## 17. Tests and CI

- **Domain:** Chao level-up points and determinism; adult-form selection and tie pick; biome catalog; merge rule; special-variant rule; incubation policy; encyclopedia catalog.
- **Application:** training to level 99; reincarnation to level 1 with 10% points; happiness boundary; merging and 4★; Swamp guy once, per-Swamp once, respawn gating; trade rejection; migrations; journal unlocks.
- **Race:** updated deterministic race tests after the stat mapping change; multiplayer lockstep tests still pass.
- **CI:** journal screen smoke; the visual smoke covers new art as it is registered.

## 18. Docs to update as work lands

Each package updates the docs it affects: `GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md` (pillars, stats §2.5/§8.3, morphology §2.10, Garden §5.2, happiness §8.9, unresolved list), `PRODUCTION_VOIDLING_APPEARANCE_RULES.md`, `IMPLEMENTATION_REMAINING_CHECKLIST.md`, `docs/architecture/MIGRATION_STATUS.md` if needed, and `Assets/Voidlings/README.md`.

---

## Appendix A — Original meeting notes

Kept word for word so the translation in Part A can be checked.

### Meeting 1

> Mijn idee was dus dat de core gameplay aspects breeden, trainen, racen en dus collecten/je encylopedia vullen zijn. Dus hoe ik nu denk is:
>
> **Life stages**
>
> - Voidling hatched als baby -> x aantal uur -> adult -> x aantal dagen/weken(?) -> reincarnation of death
> - Baby evolved naar adult met de hoogste stat (minimaal level 10?). Als stamina de hoogste stat is of als alle stats onder level 10 zijn (?) wordt het een neutral adult
> - Voidling reincarnate als happiness op een bepaald cijfer is. (Laten we zeggen voor nu max = 100, reincarnate bij 70)
>
> **Breeden**
>
> - Voidling stats breeden net zoals Chao garden (met die alleles dna profiles lala snap je wel)
> - Qua kleuren denk ik dat het leuk is als je een bepaalde set kleuren hebt, en dan de neutral colored void. De neutral colored void is de enige die die variations kan worden, een colored void krijgt alleen de lineart + de kleur die hij is. Bijv roze voidling ei hatched in swamp = roze swim void, neutral die hatched in de swamp = die speciale variant.
>
> **Hexagons**
>
> - Hexagons zoals digimon lijken me nogsteeds leuk, alleen ipv training spaces dan misschien biomes? EG: mountain biome = fly, water biome = swim, plains biome = run, dry biome = power
> - eventuele verschillende soorten "types" voor die biomes? die dan voor verschillende variants kan zorgen en/of meer stat boost. Soort "level up". Misschien als je 4 water biomes naast elkaar plaatst mag je eentje "upgraden" naar een swamp oid. En 4 dry/desert biomes mag je eentje upgraden naar een vulcano oid.
> - Voor de rest doet een void in zo'n biome zetten heel langzaam de stat raisen
>
> **Encyclopedia**
>
> - Een soort tabje die en soort journal opened met het aantal voidlings wat er zijn, en dan met "???" als naam en een silhouet of zo. Zodra je deze hebt vrijgespeeld komt de voidling sprite daar te staan en kan je er op klikken om te zien hoe je die hebt gekregen. EG "hatched in swamp", zodat je er meer kan maken als je perongeluk een speciale hebt gehatched of zo maar je wist van tevoren niet welke precautions je hebt gedaan.
>
> **Racen**
>
> - eigenlijk gwn zoals het nu is lol en die multiplayer en zo is ook leuk

### Meeting 2 (refinement)

Clarification given with these notes: the "swamp guy" is the green-ish Water Voidling and should be treated as a special variant.

> 1. Tiles upgraden zoals cow evolution/clash royale —> 4 ster is special environment tile (swamp guy)
>
> De eerste keer wanneer 2 voidlings van die environment (water voidlings voor bijvoorbeeld swamp) gaan breeden, spawned het eenmalig 1 special swamp guy. Deze heeft guaranteerd S rank swim + allebei S rank alleles.
>
> Als swamp guy doodgaat moet player opnieuw een swamp maken en item kopen van bijv black market om swamp guy te respawnen. Dit kan alleen op swamps na zijn dood.
>
> Hatching is dependent op rarity + stats
