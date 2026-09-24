# Lifecycle, biomes & encyclopedia implementation plan

**Status:** Draft for review. The direction comes from two design meetings in September 2026. Anything marked **To confirm** is a stop condition.  
**Baseline:** `main` at `5a09bcd` (2026-09-09)  
**Prepared:** 2026-09-24  
**Source:** design meeting notes. Part A translates them; Appendix A keeps the original Dutch text.

---

## 1. How to read this document

The document has two parts.

- **Part A: product direction.** What the meetings decided, translated into English and mapped onto how the game works today. It supplements `GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md` in the same way `PRODUCTION_VOIDLING_APPEARANCE_RULES.md` does. Where a rule here is marked **Decided**, it replaces the matching "unresolved" statement in the gameplay context. Rules marked **To confirm** stay unresolved. §11 lists them, each with a recommended default.
- **Part B: implementation plan.** Work packages, order, save and network impact, art needs and tests. It works inside the execution contract in `AGENT_HANDOFF_IMPLEMENTATION_PLAN.md` and the boundaries in `ARCHITECTURE.md`, and changes neither.

Agents must not answer a **To confirm** item themselves. Build the data-driven seam. Use a recommended default only after this document marks it **Decided**. Otherwise stop.

## 2. Summary

The meetings name four core pillars: **breeding, training, racing and collecting**. Collecting means filling the encyclopedia. Racing stays as it is. One loop connects the other three:

```text
place a baby on a biome hex ──> it slowly trains that biome's stat
        │
        ▼
adulthood: the highest stat picks the adult form ──> encyclopedia entry
        │
        ▼
upgrade biome hexes to 4★ ──> special environment (Swamp)
        │
        ▼
breed two Water adults with a Swamp in play ──> one-time special variant (Swamp guy)
        │
        ▼
end of life: happiness ≥ 70 reincarnates (baby again, new form possible), otherwise death
```

| Area | Today on `main` | Meeting direction | Status |
|---|---|---|---|
| Life stages | Child → adult after 45 s; the adult lives 6 h of open-game time | Baby → adult → end of life, measured in hours of open-game time | Decided; exact hours are tuning |
| Adult form | Hidden Swim/Fly and Run/Power influence picks a specialization; the look stays `normal` | Highest stat level picks the form; minimum level 10 on a 0–99 scale; stamina highest or below the minimum → neutral | Decided; needs the stat-level rework (WP-K) |
| Reincarnation | Happiness ≥ 10 and stress ≤ 70 | Happiness ≥ 70 out of 100 | 70 decided; stress gate **to confirm** |
| Stat breeding | Two alleles per stat, Chao Garden style | Same | No change |
| Color | Continuous hue color DNA on every Voidling | A set of colors plus **neutral**; only neutral Voidlings can become special variants | Details **to confirm** |
| Garden hexes | Training ground per stat; levels 1–3 bought with coins | Biomes (mountain = Fly, water = Swim, plains = Run, dry = Power); merging duplicate tiles raises the star, up to 4★; 4★ = special environment | Decided; what happens to the used-up tile **to confirm** |
| Special variant | None | Swamp guy: one-time spawn, guaranteed S/S Swim, respawn item after death | Shape decided; details **to confirm** |
| Hatching | Every egg incubates 22 s | Hatch time depends on rarity and stats | **To confirm** |
| Encyclopedia | None | Journal tab; "???" and a silhouette until discovered; shows how you got each one | Shape decided; entry granularity **to confirm** |
| Racing and multiplayer | Deterministic simulation, LAN/Steam multiplayer, trading | Keep as is | No change |

---

# Part A: Product direction

## 3. Core pillars

**Decided.** Breeding, training, racing and collecting are the core pillars. Collecting is new as a named pillar. It gives the self-directed projects in gameplay context §1.2 a visible checklist.

## 4. Life stages

### 4.1 Stages and durations

**Decided.** A Voidling hatches as a **baby**. After a number of hours it becomes an **adult**. Later its life ends, and it either reincarnates or dies.

The game already runs this loop: `LifeStage.Child` → `LifeStage.Adult` → cocoon → reincarnate or die, in `AdvanceSimulationUseCase`. Two things change:

- player-facing text says "baby". The saved enum stays `Child` for save compatibility.
- the durations move from demo values (45 s as a child, 6 h as an adult) to hours.

**Decided (Q1): open-game time only.** Voidling is an idle game that runs while the PC is on, so life stages count only time with the game open, as all progress already does (gameplay context §1.3 and §5.7). Durations are hours of open-game time, not weeks. They are data (`ChildToAdultSeconds` and `AdultLifespanSeconds` in `Resources/Balance/demo_balance.tres`). A test already requires an adult to live at least six open-game hours (`Tests/Application/SimulationArchitectureTests.cs`).

### 4.2 Adult form from the highest stat

**Decided.**

- At the baby → adult transition, the stat with the **highest level** decides the adult form.
- That stat must be at least **level 10**, on a 0–99 level scale.
- If **stamina** is the highest stat, or **no stat reaches the minimum**, the Voidling becomes a **neutral adult**.

The forms are Run, Swim (water), Fly, Power and Neutral. The rule is checked once, at the moment of the transition, and uses no randomness. It replaces the hidden influence rule in `EvolutionService.ResolveFirstEvolution`. It is also the first time adulthood changes the look: the form sets `Appearance.VisualTypeId`.

**Decided (Q2): the Chao Garden level model.** Every stat levels from 0 to 99 whatever its rank. The rank decides how much the stat value is worth at each level, so an S-rank stat at level 99 is far stronger than a C-rank stat at level 99. Level 10 is therefore reachable by every rank.

**`main` does not work this way yet.** Today the rank caps the *level* instead of the value per level:

| Rank | Training-point cap | Highest reachable level today |
|---|---|---|
| E | 20 | 2 |
| D | 40 | 4 |
| C | 60 | 6 |
| B | 80 | 7 |
| A | 100 | 9 |
| S | 120 | 11 |

`StatCalculator.GetLevel` computes level = 1 + training points ÷ 12, and `GetTrainingPointCap` stops training at the rank's cap (`RankTrainingCaps` in `demo_balance.tres`). `MaxLevel` is set to 99, but no stat can get past level 11. The race value is `12 + 13 × rank + 0.55 × training points`, capped at 100. Under this model only an S-rank stat could reach level 10. WP-K reworks stats to the decided model before WP-B relies on level 10.

**Kept unless changed (Q4).** Today adulthood also raises the expressed allele of the winning stat by one rank, and a neutral adult raises stamina. The meetings did not mention this. It is the adulthood rank promotion that gameplay context §8.3 describes.

### 4.3 Reincarnation or death

**Decided as the working value.** Happiness runs from 0 to 100. At the end of its life a Voidling reincarnates when happiness is **70 or more**, and dies otherwise. Happiness stays hidden (gameplay context §8.9).

Today the gate is happiness ≥ 10 **and** stress ≤ 70 (`ReincarnationRules`). The new number is a data change. Q6 asks whether stress still counts. Setting the stress limit to 100 in data turns that check off without code.

Balance facts from the current data:

- happiness starts at 0 at hatch and resets to 0 on reincarnation;
- petting and treats add 2, throwing removes 3, and happiness drains by 6 per open-game hour;
- a Voidling at 100 falls below 70 after 5 hours without care, so the threshold rewards care late in life, not only early;
- the Garden log's care-risk warning uses the reincarnation threshold (`AdvanceSimulationUseCase.IsCareLifecycleSafe`). At 70 it would fire every time happiness dips below 70, so it needs its own threshold.

As today, after reincarnation the Voidling is a baby again and gets a form again at its next adulthood (`ReincarnationService` already clears the old specialization). It can take a different form each life. The encyclopedia keeps every form it has discovered.

## 5. Breeding and color

### 5.1 Stat inheritance: no change

"Stats breed like Chao Garden, with alleles and DNA profiles" describes what the game already does. Each stat has two alleles, one from each parent, a chance to express the higher allele, and a rare +1 rank breakthrough (`GenomeInheritanceService`). Nothing to build.

### 5.2 Colored and neutral Voidlings

**Direction (meeting 1).**

- There is a set of colors, plus a **neutral** color.
- Only **neutral** Voidlings can become special variants.
- A **colored** Voidling only gets its form's line art in its own color. The notes' example: a pink egg hatched in the swamp becomes a pink Swim Voidling.

How this maps onto the current code: color DNA is a continuous hue. Each Voidling has two profiles, one wins at 50/50, and the winner drifts a little toward the other (confirmed in `PRODUCTION_VOIDLING_APPEARANCE_RULES.md` §2). The renderer swaps the palette of the authored sprite sheet, and the identity hue shows the sheet exactly as drawn (commit `22e0f6f`). So "neutral" fits naturally as **a Voidling drawn in its form's authored colors, with no palette swap**. The artist then decides what neutral looks like for each form, and the Swamp guy's green is simply its authored color.

**To confirm:** Q7 (continuous hue or a fixed color set), Q8 (what neutral looks like, how it is inherited, how store eggs get it) and Q9 (what "only neutral becomes a variant" means after meeting 2).

## 6. Biomes

### 6.1 A biome on every training hex

**Decided.** Training hexes become biomes, each tied to one stat.

| Biome | Trains | 4★ special environment |
|---|---|---|
| Mountain | Fly | Not named yet |
| Water | Swim | **Swamp** (Swamp guy) |
| Plains | Run | Not named yet |
| Dry (desert) | Power | **Volcano** (no special variant designed yet) |

This renames and re-skins what exists. Today a placed hex is converted into training ground for one stat and drawn as grass with a stat-colored wash and a signboard (`TrainingUseCase.ConvertHexToTrainingGround`, `Scripts/Garden/GardenController.Land.cs`). A Voidling on a biome hex still raises that stat **very slowly**. That is the existing passive training (`PassiveTrainingService`), retuned to be slow.

Two open points. The notes give stamina no biome (Q10). "Plains" also clashes with "plain ground", today's name for an empty hex, so the player-facing words need to differ.

### 6.2 Star upgrades and special environments

**Decided.** Meeting 2 replaced meeting 1's "four water hexes next to each other" idea. Biome hexes level up with stars, like Cow Evolution or Clash Royale, from 1★ to 4★. **A 4★ hex is a special environment**: Water 4★ is a Swamp and Dry 4★ is a Volcano.

Levels 1–3 already exist, bought with coins (25, then 50). The Build screen already draws the level as a star row (`PaperCard.StarRating`). The new parts are the fourth star, the change into a special environment, and the upgrade mechanic.

**Decided (Q11): merging.** Merging duplicate tiles evolves the tile to the next star, up to 4★.

**To confirm (Q11a): what happens to the used-up tile.** A hex is also land, and removing one could split the island, which the placement rules forbid (`GardenHexLayout.CanPlaceShape`). Recommended: the used-up tile turns back into plain ground, so the land stays. Also to confirm: whether a duplicate means same biome *and* same star, and whether merging costs coins as well.

A special environment trains its stat faster than a 3★ hex ("more stat boost"), and it is where special-variant rules hook in (§7).

## 7. Special variant: the Swamp guy

"Swamp guy" is the working name for the green-ish Water Voidling. It is the special variant from meeting 1; meeting 2 decided how you get it. The mechanism is generic, defined as data per variant, so a Volcano variant and others can follow.

**Decided.**

1. **Spawn.** The Swamp guy belongs to the Swamp, the 4★ Water environment. The first time two Water-form adults breed with a Swamp in play, the game spawns one Swamp guy. This happens once. Whether owning a Swamp is enough or the parents must be on it is Q14.
2. **Guaranteed genes.** Swim is rank S, and **both** Swim alleles are S.
3. **One at a time.** A save has at most one Swamp guy.
4. **Respawn.** If the Swamp guy dies, the player needs a Swamp again and must buy an item. The notes say "for example from the black market", which is Chao Garden's name for its shop; ours is the Sprout Market. The item can only be used on a Swamp, and only after the Swamp guy has died.

The Swamp guy's advantage is genetic: S/S Swim for breeding and racing. He gets no hidden race bonus, because racing reads only stats (gameplay context §3.3).

With this trigger, his encyclopedia record reads something like "bred from two Water Voidlings while you owned a Swamp" rather than meeting 1's "hatched in the swamp".

**To confirm:** Q13–Q19 (what exactly spawns, trigger scope, form lock, his other stats, breeding, trading and Goodbye, respawn details).

## 8. Egg hatching time

**Direction (meeting 2).** "Hatching depends on rarity + stats."

The recommended reading: an egg's incubation time is worked out from its rarity and its stat potential when the egg is created, then fixed, just like its genes. `EggData.RequiredIncubationSeconds` is already stored per egg, so eggs that exist today keep their time.

**To confirm (Q20):** that this means time rather than hatch chance, and what "rarity" is. Eggs have no rarity today. Rare founder traits and S ranks exist, but no rarity tier.

## 9. Encyclopedia

**Decided.**

- A tab opens a journal that shows how many Voidlings exist, as discovered out of total.
- Undiscovered entries show **"???"** and a **silhouette**.
- Discovered entries show the sprite. Clicking one shows **how you got it**, so a player who got something special by accident can find out what they did and do it again.

Recording "how" needs data the game does not keep yet: the biome a Voidling hatched on, the stat that decided its form, and what triggered a special variant. WP-D, WP-G and WP-I add it.

This settles one gameplay-context item, "whether undiscovered combinations are hinted": entries are listed with silhouettes, and names stay hidden.

**To confirm (Q21–Q23):** what one entry is (a form, or a form in one color), what counts as discovered, and how existing saves fill in.

## 10. Racing and multiplayer: no change

The notes: racing stays basically as it is now, and the multiplayer is good too. The deterministic race simulation, multiplayer racing, trading and the connected Garden stay as they are. New creature fields must survive trades and connected-Garden snapshots (§16).

## 11. Decisions to confirm

Each item has a recommended default. To accept one, mark it **Decided** in this document. Work that depends on an open item stops at a data-driven seam.

### Lifecycle

- **Q1 — Clock for life stages.** **Decided:** open-game time only; durations in hours.
- **Q2 — Adult-form minimum.** **Decided:** level 10 on a 0–99 scale that every rank can climb; the rank sets the value per level (WP-K).
- **Q2a — Rank value curve.** How much is one level worth at each rank, and what does a level-99 stat look like per rank? *Recommended:* keep the current race-value range (0–100) and spread it over 99 levels per rank, tuned in a balance pass. *Blocks:* WP-K numbers.
- **Q2b — Existing training.** How existing training points convert to the new levels. *Recommended:* keep each Voidling's current race value as close as possible, so old saves don't suddenly race better or worse.
- **Q3 — Ties for the highest stat.** *Recommended:* compare exact training points, and treat an exact tie as neutral.
- **Q4 — Rank promotion at adulthood.** Keep the +1 rank for the winning stat, with neutral promoting stamina? *Recommended:* keep it.
- **Q5 — Adults that already exist.** Give them the form that matches their stored specialization, or keep them `normal` until their next life? *Recommended:* give them the form. The migration is deterministic and rerolls nothing.
- **Q6 — Stress gate.** Keep "stress ≤ 70" alongside "happiness ≥ 70"? *Recommended:* drop it by setting the limit to 100 in data, so the rule is the single number from the meeting.

### Color

- **Q7 — Color set.** Keep continuous hue DNA (the confirmed rule) and name color families for display and collecting, or switch to a fixed set of colors? *Recommended:* keep continuous hue.
- **Q8 — Neutral color.** Look: the authored sheet colors (recommended) or a grey or white palette? Inheritance: one more color profile, won 50/50 like any other, with no hue drift when either side is neutral (recommended)? Source: a small chance on store eggs, number to tune.
- **Q9 — "Only neutral becomes a variant", after meeting 2.** *Recommended:* special variants always carry neutral color and show authored art. Colored Voidlings only ever get their form's line art in their own color. No other neutral-only looks until more variants are designed.

### Biomes

- **Q10 — Stamina hex.** Keep a stamina biome (name and art to decide) or retire it? Existing saves already have stamina training ground. *Recommended:* keep it.
- **Q11 — Upgrade mechanic.** **Decided:** merging duplicate tiles evolves the tile to the next star, up to 4★.
- **Q11a — Merge details.** The used-up tile turns back into plain ground (recommended); a duplicate is same biome and same star (recommended); tiles don't need to touch (recommended); no coin cost (recommended). Existing coin-bought levels stay.
- **Q12 — Other 4★ environments.** Names for Mountain 4★ and Plains 4★, and whether Volcano gets a special variant now. *Recommended:* build it generically and ship the Swamp first.

### Swamp guy

- **Q13 — What spawns.** The bred egg itself becomes the Swamp guy's egg (recommended), an extra egg appears, or an adult appears directly?
- **Q14 — Trigger scope.** Owning at least one Swamp anywhere (recommended), or must the parents stand on the Swamp? "The first time" counts from when the player has a Swamp.
- **Q15 — Form lock.** The Swamp guy stays a Swamp guy as a baby, as an adult and after reincarnation (recommended). This needs baby art, or the same art at baby scale.
- **Q16 — His other stats.** Inherited from the parents as usual, with only Swim forced to S/S (recommended).
- **Q17 — Can he breed?** Yes, as a normal adult, and his offspring are ordinary Voidlings (recommended).
- **Q18 — Trading and Goodbye.** Special variants and their eggs cannot be traded in v1 (recommended; this prevents duplicates). Goodbye counts as a death and opens the respawn (recommended).
- **Q19 — Respawn details.** A new individual with a new ID and a clean lineage (recommended), or the same one revived? The item is listed in the shop whenever a respawn is possible (recommended), or only in the rare rotation? For "make a Swamp again": owning a Swamp is enough (recommended), or is a Swamp used up?

### Hatching and encyclopedia

- **Q20 — "Hatching depends on rarity + stats."** Incubation time (recommended) or hatch chance? Rarity: a tier derived from the egg's genes (sum of ranks), rare traits and special variant (recommended), or something else?
- **Q21 — What the hatch biome does.** Besides being recorded: a newborn that hatches on a training hex starts training there if there is room (recommended). "A pink egg in the swamp becomes a pink Swim Voidling" then follows from the highest-stat rule, with no separate rule.
- **Q22 — What one entry is.** One entry per form and special variant, with the discovered color families shown inside the entry (recommended), or one entry per form in each color?
- **Q23 — What counts as discovered, and existing saves.** Hatching, evolving, spawning or receiving one in a trade unlocks it; seeing one in someone else's Garden does not (recommended). Existing saves unlock what they own or have owned, recorded as "before the journal" (recommended).

### Numbers to tune (data only, not blocking)

Baby duration; adult lifespan; happiness gains, drain and care-warning threshold; adult-form minimum; passive training rate per star; copies and coins per star; Swamp respawn item price; neutral chance on store eggs; incubation time per rarity tier.

---

# Part B: Implementation plan

## 12. Baseline: extend, don't rebuild

| Need | Already on `main` | Extend here |
|---|---|---|
| Lifecycle loop | `AdvanceSimulationUseCase`, `ReincarnationService`, cocoon presentation | durations, thresholds, form at adulthood |
| Adult form | `EvolutionService` (influence axes, +1 rank promotion, never promotes twice) | replace the selection rule; keep promotion and the guard |
| Form to art | `VoidlingAppearanceData.VisualTypeId`, `VoidlingVisualCatalog`, `VoidlingVisualFactory` (unknown IDs fall back to `normal`) | register new definitions as art arrives |
| Genetics | `GenomeInheritanceService`, `GenomeFactory`, `ColorPhenotypeResolver` | neutral color; special-variant gene overrides |
| Hexes | `GardenModuleData`, `GardenHexLayout`, `TrainingUseCase` land and upgrade methods, `PassiveTrainingService` | biome ID, fourth star, merge upgrade |
| Eggs | `EggData.RequiredIncubationSeconds` (per egg), `StoreEggFactory`, `BreedVoidlingsUseCase` | incubation policy; special egg |
| Shop | `ShopUseCase`, `ShopItemIds`, `RareShopOfferResolver` | respawn item |
| Trading | `TradeTransferService` (copies the whole `VoidlingData` as JSON) | validate new fields; special-variant rule |
| Portraits | `VoidlingPortraitComposer` | silhouette mode for the journal |
| Screens | `SettingsScreen`, the grid plus detail card screens (`UI_UX_REMAINING_MENUS_PLAN.md`), the Garden rail in `MainController` | journal screen and rail button |

## 13. Where the new rules live

This follows `ARCHITECTURE.md`.

- **Domain** (pure and deterministic): adult-form selection; the biome catalog (biome → stat, 4★ environment); the star upgrade rule; special-variant trigger and gene overrides; incubation policy; the encyclopedia catalog and the "which entry is this creature" lookup.
- **Application**: when those rules run and what they change. Adulthood runs in `AdvanceSimulationUseCase`, the special spawn in `BreedVoidlingsUseCase`, hex conversion and merging in `TrainingUseCase` (or a new `GardenLandUseCase` if that file gets too large), respawn purchase and use in `ShopUseCase`, and discovery recording plus the journal projection in a new encyclopedia use case. Migrations go in `GameStateMigrationService`.
- **Infrastructure**: turns authored data into Domain rules. Balance numbers join `GameBalanceResource`. Biome, special-variant and encyclopedia content get their own focused Resources only when a feature first reads them (the Phase G rule in `MIGRATION_STATUS.md`).
- **Presentation**: biome ground art through one biome visual catalog, so texture paths are not spread through `GardenController.Land.cs`; form and variant art through the existing Voidling visual catalog; the journal screen; silhouettes through `VoidlingPortraitComposer`; log text through localization keys.

Rules for every package:

- Saves and network packets store semantic IDs (`water`, `swamp`, `swamp-variant`), never paths.
- The special trigger is a condition and uses no randomness. Anything rolled, such as a respawn egg's genes, uses `StableRandom` substreams seeded from `SeedCounter`.
- No new rules go into `GameSession` or `GameRules`. They only forward typed events.
- New enum values are appended; existing values keep their numbers.
- Old fields (`SwimFlyInfluence`, `RunPowerInfluence`, `EvolutionMagnitude`) stay in the save. The new rule just stops reading them.

## 14. Work packages

Each package is one or two PRs and lists what it depends on and which decisions block it.

### WP-A — Lifecycle tuning and the happiness gate

**Depends on:** nothing. **Decisions:** Q1 (durations only), Q6.

1. Set `ReincarnationMinimumHappiness` to 70, and set stress according to Q6.
2. Give the Garden care-risk warning its own threshold (a new rule value) so it does not fire on every dip below 70.
3. Set `ChildToAdultSeconds` and `AdultLifespanSeconds` to hours of open-game time (Q1). Keep the six-hour floor test, or update it on purpose.
4. Use "baby" in player-facing text.
5. Add a headless balance test: a scripted care routine over a full life ends at 70 or more, and a neglected Voidling does not.

**Acceptance:** the thresholds come from data; the care warning no longer uses the reincarnation threshold; tests pin the boundary (69.9 dies, 70 reincarnates).

### WP-K — Chao-style stat levels

**Depends on:** nothing. **Decisions:** Q2a, Q2b. Must land before WP-B uses level 10.

1. Characterization tests first: current levels, caps and race values for each rank.
2. Change `StatCalculator` so every stat levels 0–99 regardless of rank, and the rank sets the value gained per level. Remove the rank training cap (`RankTrainingCaps`) as a level limit.
3. Race values (`GetEffectiveStat`, `RacePerformanceModel` inputs) come from level + rank. Multiplayer races keep using one shared calculation.
4. Update passive and active training so they stop at level 99, not at a rank cap. The "training capped" log event fires at level 99.
5. Migration per Q2b, with a save round-trip test. Race balance tests are updated deliberately, not silently.
6. UI: stat cards show level 0–99 plus the rank letter.

**Acceptance:** an E-rank and an S-rank stat can both reach level 99; at equal levels the S-rank stat has the higher race value; existing saves load without unintended race changes.

### WP-B — Adult form from the highest stat

**Depends on:** WP-K. **Decisions:** Q3–Q5.

1. Write tests for the new rule first: the highest stat wins; stamina highest → neutral; nothing at the minimum → neutral; the tie rule; +1 promotion capped at S; promotion never runs twice.
2. Replace the selection in `EvolutionService.ResolveFirstEvolution` with the level rule. Add `EvolutionRules` fields for the minimum and the tie rule, and retire `SpecializationThreshold` from the resource without breaking older `.tres` files.
3. Map forms to stable visual type IDs: run → `run`, swim → `water`, fly → `fly`, power → `power`, neutral → `normal`. Set `Appearance.VisualTypeId` at adulthood. `ReincarnationService.ApplyReincarnation` sets it back to `normal`.
4. Treat `EvolutionSpecialization.Generalist` as neutral so the enum does not change. The UI says "Neutral".
5. Migration (per Q5): existing adults get a `VisualTypeId` from their stored specialization. Nothing is re-evaluated or rerolled.
6. Garden log: "{name} grew into a Swim adult" and so on, from `CreatureBecameAdultEvent.Specialization`. Use localization keys. The current lifecycle log lines in `Scripts/Services/GameSession.cs` are English literals, so convert the ones you touch.

**Acceptance:** a baby trained on one stat becomes that form; an untrained or stamina-heavy baby becomes neutral; forms without art yet render with `normal` art; a save round-trip test covers the migration.

### WP-C — Biomes on training hexes

**Depends on:** nothing. **Decisions:** Q10.

1. Add a Domain `BiomeCatalog`: biome ID → stat, maximum stars, 4★ environment ID. It is authored data.
2. Add `GardenModuleData.BiomeId`. The migration derives it from `StatId` (run → plains, swim → water, fly → mountain, power → dry, stamina per Q10). `StatId` stays as the cached stat, so passive training keeps working.
3. Hexes are converted by biome rather than by stat. Passive rates come from biome and star.
4. Presentation: one biome visual catalog holds the ground art and sign style per biome and star. Until art arrives, keep today's tinted grass and sign. The Build screen and the hex menu show biomes.
5. Localize biome names and keep "plains" distinct from "plain ground".

**Acceptance:** old saves load with the right biomes and unchanged training; new hexes are converted by biome; no texture paths outside the catalog.

### WP-D — Hatch biome and newborn placement

**Depends on:** WP-C. **Decisions:** Q21.

1. Add `VoidlingData.HatchBiomeId`. Empty means unknown, including every Voidling hatched before this change.
2. At hatch (`AdvanceSimulationUseCase.Hatch`), find the hex under the egg with `GardenHexLayout.At(egg.WorldX, egg.WorldY)` and store its biome, or plain ground, or none.
3. Per Q21: if the hex is training ground with room, assign the newborn to it, using the same rules as `TrainingUseCase.SetPassiveTrainingLand`.

**Acceptance:** the result depends only on the egg position and the Garden state; tests cover a biome hex, plain ground, off the island and a full hex.

### WP-E — Star upgrades and special environments

**Depends on:** WP-C. **Decisions:** Q11, Q12.

1. Add a Domain merge rule, as data: what counts as a duplicate, and any cost (Q11a). The maximum is 4★. `GameBalanceResource` has fixed level-2 and level-3 fields today, so add level 4 (or switch to a list).
2. Application merge: the target gains one star; the donor becomes plain ground (biome and stat cleared, level 1); Voidlings assigned to the donor are unassigned and keep their points.
3. At 4★ the biome turns into its special environment (`water` → `swamp`), with its own passive rate.
4. Existing coin-bought levels stay. The coin upgrade button is replaced by merging.
5. Presentation: a merge flow in Build and the hex menu, a four-star row, and special environment art through the biome catalog.

**Acceptance:** the island stays connected after any merge; no hex goes past 4★; tests cover each star step and the donor.

### WP-F — Neutral color

**Depends on:** nothing. **Decisions:** Q7–Q9. **Stop until they are decided.**

1. Add an explicit neutral marker to each color profile in `GenomeData`. Do not use a special hue value: `-1` already means "legacy".
2. Inheritance and expression per Q8; the store-egg chance is data.
3. `VoidlingAppearanceData` carries the color mode. Presentation skips the palette swap for neutral. The connected-Garden snapshot gets the field, and its validation is updated.
4. Existing Voidlings stay colored. Nothing is rerolled.
5. Update `PRODUCTION_VOIDLING_APPEARANCE_RULES.md`.

### WP-G — Special variants (Swamp guy)

**Depends on:** WP-B (the water form) and WP-E (the Swamp). It uses WP-F if Q9 ties variants to neutral color. **Decisions:** Q13–Q19.

1. Add a Domain special-variant definition, as data: variant ID, required environment, the form both parents need, forced alleles (Swim S/S), locked visual type and respawn item ID. The Swamp guy is the first entry.
2. Save: `GameStateData.SpecialVariants` records each variant's status (not spawned yet, alive with a creature ID, or departed). `VoidlingData` and `EggData` get a `SpecialVariantId`.
3. Breeding: in `BreedVoidlingsUseCase.Execute`, check the trigger after the normal validation. If it fires, the egg becomes the special egg (per Q13): forced alleles, locked visual type, always viable, fixed at creation. The status becomes "alive" as soon as the egg exists, so the trigger cannot fire again while it incubates. The egg ID becomes the creature ID at hatch.
4. Lifecycle: the form stays locked. `EvolutionService` and reincarnation leave the variant's visual type alone.
5. Departure: death, and Goodbye per Q18, set the status to "departed".
6. Respawn: the shop item appears and can be bought only while the status is "departed" and a Swamp exists. Using it on a Swamp hex creates the special egg there (per Q19). If the Garden is full, the egg waits for space as eggs already do (`EggState.WaitingForSpace`).
7. Trading: `TradeTransferService` rejects special variants and their eggs (Q18), and any incoming creature that would make a second one.
8. Presentation: Garden log lines, the shop card and "use on Swamp" in the hex menu. The Swamp guy's art is a `swamp-variant` definition in the Voidling visual catalog. Until it is registered he renders with the default `normal` art, like any unregistered type.

**Acceptance:** the spawn happens once per save; never without a Swamp or with non-Water parents; Swim is exactly S/S; at most one is alive; respawn works only after departure and only on a Swamp; save round-trip and trade rejection tests pass.

### WP-H — Incubation from rarity and stats

**Depends on:** nothing. **Decisions:** Q20. **Stop until it is decided.**

1. Add a Domain egg rarity policy and incubation policy, as data tables.
2. Bred, store-bought and special eggs set `RequiredIncubationSeconds` from the policy when created. Existing eggs keep their stored time.
3. Show the rarity on the egg card if the design wants it.

### WP-I — Encyclopedia

**Depends on:** WP-B (forms). It uses WP-D and WP-G records when they exist; their entries can be added later. **Decisions:** Q22, Q23.

1. Add a Domain `EncyclopediaCatalog` (authored entries with a stable ID, the form or variant they match and a name key) and a lookup from creature to entry.
2. Save: `GameStateData.Encyclopedia` holds discovered entry IDs, each with a first-discovery record: method (hatched, evolved, special spawn, respawn, traded in, before the journal), biome, deciding stat and level, parent forms and creature name. Domain uses no wall-clock time. If the journal shows a date, it is passed in and never affects outcomes.
3. Application: record discoveries from typed events on hatch, adulthood, special spawn and trade-in. Provide a journal projection for the UI: entries, discovered flags, records and the "discovered / total" count.
4. Migration: fill in from `Voidlings`, `DepartedVoidlings` and the lineage archive, per Q23.
5. Presentation: a journal button on the Garden rail (`MainController`), and a grid of entries with a detail card, following the house pattern. Undiscovered entries show "???" and a silhouette. Silhouettes come from `VoidlingPortraitComposer` in a silhouette mode, so they follow art changes. Every node is named for the CI smoke test, and all text uses localization keys.

**Acceptance:** entries and the count come from the catalog; undiscovered entries never reveal a name; records survive save/load; a creature received in a trade unlocks its entry; the smoke test finds the screen's named nodes.

### WP-J — Art and content

This runs alongside the other packages through the existing art pipeline (handoff WP7 and `Assets/Voidlings/README.md`). No package waits for art, because unknown visual types fall back to existing art.

Needed:

- adult bodies: `water`, `fly`, `power`, `run`. The neutral adult uses `normal` at adult scale unless the artist wants a separate one;
- the Swamp guy: a green-ish Water Voidling in authored colors, with baby art or a baby scale per Q15;
- biome ground art: mountain, water, plains, dry, a stamina biome (Q10), swamp and volcano, plus star indicators;
- the respawn item icon, and journal UI art (the Sprout Lands UI packs may already cover it).

## 15. Order and PR slices

| Package | Needs first | Blocked by |
|---|---|---|
| WP-A | nothing | Q6 |
| WP-K | nothing | Q2a, Q2b |
| WP-B | WP-K | Q3–Q5 |
| WP-C | nothing | Q10 |
| WP-D | WP-C | Q21 |
| WP-E | WP-C | Q11a, Q12 |
| WP-F | nothing | Q7–Q9 (hard stop) |
| WP-G | WP-B, WP-E; WP-F if Q9 ties variants to neutral | Q13–Q19 |
| WP-H | nothing | Q20 (hard stop) |
| WP-I | WP-B; WP-D and WP-G add their records when they land | Q22, Q23 |
| WP-J | nothing | art being supplied |

Suggested order: WP-A, WP-K and WP-C in parallel; then WP-B and WP-D; then the first version of the journal (WP-I) with forms and hatch records; then WP-E and WP-G, which add special entries to the journal. WP-F and WP-H start once their decisions are made. WP-J runs throughout.

Suggested branches:

1. `feature/lifecycle-happiness-gate` (WP-A) and `feature/chao-stat-levels` (WP-K)
2. `feature/adult-form-highest-stat` (WP-B)
3. `feature/garden-biomes`: Domain and save first, presentation second (WP-C)
4. `feature/hatch-biome` (WP-D)
5. `feature/encyclopedia`: Domain, save and fill-in first, the screen second (WP-I)
6. `feature/biome-star-merge` (WP-E)
7. `feature/special-variant-swamp` (WP-G)
8. `feature/neutral-color` (WP-F) and `feature/incubation-rarity` (WP-H), once decided

Keep balance changes and presentation refactors in separate PRs (handoff §12).

## 16. Save and network compatibility

The current save version is 22 (`GameStateMigrationService.CurrentSaveVersion`). Each package that adds state bumps it once and adds a round-trip test that loads a version-22 save.

| Change | Field | Migration |
|---|---|---|
| Adult form look | `VoidlingData.Appearance.VisualTypeId` (exists) | per Q5, map the stored specialization |
| Biome | `GardenModuleData.BiomeId` (new) | derived from `StatId` |
| 4★ and environment | `GardenModuleData.Level` (exists), `BiomeId` | none |
| Hatch biome | `VoidlingData.HatchBiomeId` (new) | empty means unknown |
| Special variant | `SpecialVariantId` on creatures and eggs, `GameStateData.SpecialVariants` (new) | empty |
| Neutral color | markers in `GenomeData`, color mode in `VoidlingAppearanceData` (new) | existing Voidlings stay colored |
| Incubation | `EggData.RequiredIncubationSeconds` (exists) | existing eggs unchanged |
| Journal | `GameStateData.Encyclopedia` (new) | filled in per Q23 |

Network:

- trades copy `VoidlingData` as JSON, so new fields travel with the creature. `TradeTransferService` validation must accept and normalize them, and enforce Q18.
- the connected-Garden snapshot already carries `VisualTypeId`. Neutral color (WP-F) adds a field and a validation rule.
- a peer on an older build that does not know a visual type shows `normal` art, which is acceptable. Check that `ConnectedZoneValidation` does not reject packets carrying fields or IDs it does not know.
- races do not change: participant snapshots read stats only.

## 17. Tests and CI

- **Domain (new):** adult-form selection; biome catalog; star upgrade rule; special-variant trigger and overrides; incubation policy; encyclopedia lookup.
- **Application:** adulthood sets the look and reincarnation resets it; the happiness boundary; hatch biome and newborn assignment; merging and 4★; the special spawn happens once and respawn is gated; the shop respawn item; trade rejection; migration round-trips; journal fill-in.
- **CI:** a journal screen smoke test (named nodes). The existing visual smoke already covers every registered body definition, so new art is checked as it lands.

## 18. Docs to update as work lands

Each package updates the docs it affects when it lands. The PR that adds this plan only adds pointers to it.

- `GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md`: pillars (§1.1), stat-driven morphology (§2.10), collection (§3.4), Garden modules (§5.2), happiness threshold (§8.9) and the unresolved list;
- `PRODUCTION_VOIDLING_APPEARANCE_RULES.md`: forms, neutral color, special variants;
- `IMPLEMENTATION_REMAINING_CHECKLIST.md`;
- `docs/architecture/MIGRATION_STATUS.md`, if a package adds a transitional hotspot;
- `Assets/Voidlings/README.md`, when new definitions are registered.

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
