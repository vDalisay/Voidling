# Lifecycle, biomes & encyclopedia implementation plan

**Status:** Decided and in implementation. The direction comes from two design meetings in September 2026 and three follow-up question rounds. Only the items in §11 "Later" are still open.  
**Baseline:** `main` at `5a09bcd` (2026-09-09)  
**Prepared:** 2026-09-24  
**Source:** design meeting notes and answers to this plan's questions. Appendix A keeps the original Dutch notes.

---

## 1. How to read this document

- **Part A: product rules.** What was decided, mapped onto how the game works. It supplements `GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md` in the same way `PRODUCTION_VOIDLING_APPEARANCE_RULES.md` does: where a rule here is specific, it replaces the matching "unresolved" statement there.
- **Part B: implementation.** Work packages, the concrete design, save and network impact, and tests. It works inside `AGENT_HANDOFF_IMPLEMENTATION_PLAN.md` and `ARCHITECTURE.md`.

The game is in development. Old saves must keep loading, but they do not need elaborate backfills (Q23a).

## 2. Summary

The core pillars are **breeding, training, racing and collecting** (filling the encyclopedia). Racing stays as it is.

```text
place a baby on a biome tile ──> it slowly levels that biome's stat (Chao-style, 0–99)
        │
        ▼
adulthood: highest stat at level 10+ picks the form (Neutral otherwise) ──> journal entry
        │
        ▼
stack matching biome tiles up to 4★ ──> Water 4★ becomes a Swamp
        │
        ▼
two Water adults breed while you own an unused Swamp ──> Swamp guy egg (once)
        │                                                  hatches only on an unused Swamp
        ▼
end of life: happiness ≥ 70 reincarnates (baby again, new form possible), otherwise death
```

---

# Part A: Product rules

## 3. Core pillars

Breeding, training, racing and collecting are the core pillars.

## 4. Life stages and stats

### 4.1 Stages and durations

A Voidling hatches as a **baby**, becomes an **adult** after some hours, and later reaches the end of its life, where it reincarnates or dies. All durations count **open-game time only**: Voidling is an idle game that runs while the PC is on. Durations are hours, not weeks.

Starting values (tuning): baby 1.5 hours, adult 8 hours. The saved enum stays `Child`; players see "baby".

### 4.2 Stats work exactly like Chao Garden

- Every stat has a **level from 0 to 99**, whatever its rank.
- Training fills a progress bar of 10 steps. A full bar is a level-up and the bar empties.
- Each level-up adds **stat points**: `3 × rank + 11 + random 1–5`, with E = 0 … S = 5. E gains 12–16 points per level, S gains 27–31.
- Stat points are capped at 3266, SA2's normal-play maximum.
- Reincarnation puts every stat back to **level 1** and keeps **10%** of its points, rounded down.
- Racing reads stat points: the race value is `points ÷ 3266 × 100`.

Sources: [Chao Island — Stats](https://chao-island.com/info-center/basics/stats.html), the [FelineWasteland stat calculator](https://felinewasteland.com/shrines/chao/statcalculator) (its code uses `3 × grade + 11 + random(1..5)` and the 3266 cap), [All About Chao — Grades](https://chaohelp.weebly.com/grades.html).

**Existing Voidlings keep their genes, lineage and everything else; their stats reset to level 0 under the new system (Q2b).**

### 4.3 Adult form from the highest stat

- At the baby → adult transition, the stat with the **highest level** decides the form.
- It must be at least **level 10**.
- **Stamina** highest, or **no stat at level 10**, makes a **Neutral** adult.
- Stats tied for highest are an **equal random pick** (reproducible per Voidling).
- Adulthood still raises the expressed allele of the winning stat by one rank; Neutral raises stamina.

Forms and their visual type IDs: baby `normal`; Neutral `neutral`; Run `run`; Swim `water`; Fly `fly`; Power `power`.

**Existing adults become Neutral** (Q5): they are bigger than babies and most current Voidlings are neutral.

### 4.4 Reincarnation or death

Happiness runs from 0 to 100. At the end of its life a Voidling reincarnates when happiness is **70 or more** and dies otherwise. Happiness is the only condition. It stays hidden.

The Garden's care warning gets its own threshold (happiness below 30), because at 70 it would fire on every dip.

## 5. Breeding and color

### 5.1 Stat inheritance: no change

Two alleles per stat, one from each parent, a higher-allele expression chance and a rare +1 rank breakthrough.

### 5.2 Neutral is a type; colors

- **Neutral is a type**, like Swim, not a color.
- **Neutral adults show the artist's colors.** Babies and Run/Swim/Fly/Power adults show their own color DNA.
- Special variants such as the Swamp guy also show their artist's colors.
- Color DNA stays a smooth hue range and is inherited as today.
- **Later:** each type may only vary within its own hue range (for example Water in blues).

## 6. Biomes and tiles

### 6.1 Biomes

| Biome | Trains | 4★ |
|---|---|---|
| Plains | Run | 4★ Plains (special name later) |
| Water | Swim | **Swamp** |
| Mountain | Fly | 4★ Mountain (special name later) |
| Dry | Power | **Volcano** |
| Grove | Stamina | 4★ Grove |

A Voidling on a biome tile raises that stat very slowly. Active training (treats) is faster.

### 6.2 Tiles, stacking and picking up

- The shop sells **1★ biome tiles**. They go to the inventory.
- Placing a tile on **plain ground** turns that hex into the biome at the tile's stars.
- Placing a tile **on top of a matching tile** (same biome, same stars) merges both into **one tile one star higher**, up to 4★.
- A placed tile below 4★ can be **picked up** back into the inventory with its stars; the hex returns to plain ground. This is how a player collects a second 2★ or 3★ tile to stack.
- A **4★ tile is permanent**. Water 4★ is a **Swamp** and Dry 4★ is a **Volcano**.
- Coin upgrades are gone; stacking replaces them. Existing tiles keep their stars.

## 7. Special variant: the Swamp guy

The Swamp guy is the green-ish Water Voidling.

1. **Egg.** When two **Water adults** breed while the player owns an **unused Swamp**, their egg is the Swamp guy's egg. This happens **once per save** through breeding.
2. **Hatching.** The egg only incubates while it sits **on an unused Swamp**. Players pick up and move eggs freely, like Voidlings. When he hatches, that Swamp is used.
3. **Genes.** Swim is rank S with **both** Swim alleles S. His other stats come from his parents as usual.
4. **Looks.** He stays a Swamp guy as a baby, an adult and after reincarnating, and shows his artist's colors. His own color DNA passes on normally; his offspring are ordinary Voidlings.
5. **One at a time, untradable.** Special variants and their eggs cannot be traded.
6. **Death and respawn.** If he dies, or the player says Goodbye (which counts as death), the shop sells a **Swamp guy egg**. It hatches only on a Swamp that has never hatched one. The player can build as many Swamps as they like; each hatches at most one Swamp guy.
7. **Racing.** His advantage is genetic. No hidden race bonus.

## 8. Egg hatching time

Incubation time is worked out when the egg is created, then fixed:

- base time;
- plus time for **each S-rank stat** (expressed rank);
- plus time for each **rare trait** (Lustrous, Prismatic, Aurora);
- plus time for a **special variant** egg.

Starting values (tuning): base 22 s, +45 s per S stat, +120 s per rare trait, +300 s for a special variant.

## 9. Encyclopedia

- A journal tab shows how many Voidlings exist, as discovered out of total.
- **One entry per form and special variant:** Baby, Neutral, Run, Swim, Fly, Power, Swamp guy.
- Undiscovered entries show **"???"** and a **silhouette**; discovered ones show the sprite, how you get it, and who found it first.
- **Hatching, evolving and spawning** unlock entries. Trading does not.
- Where an egg hatched plays no role.

## 10. Racing and multiplayer

The race simulation, multiplayer racing, trading and the connected Garden stay. The stat rework changes race inputs, so the multiplayer protocol version goes up and both players need the same build.

## 11. Decision record

| # | Decision |
|---|---|
| Q1 | Open-game time only; durations in hours |
| Q2 | Chao Garden stat model (§4.2) |
| Q2b | Keep Voidlings, genes and lineage; reset stats to level 0 |
| Q3 | Tie for highest stat → equal random pick |
| Q4 | Keep the +1 rank at adulthood |
| Q5 | Existing adults become Neutral |
| Q6 | Happiness ≥ 70 is the only condition |
| Q7 | Smooth hue range |
| Q8 | Neutral is a type; Neutral adults show the artist's colors; babies and typed adults show color DNA |
| Q9 | Swamp guy egg comes from two Water adults |
| Q10 | Keep stamina tiles (Grove) |
| Q11 | Stack a matching tile on top to merge; placed tiles can be picked up; the shop sells 1★ tiles |
| Q12 | Swamp and Volcano; other 4★ names later |
| Q13 | The parents' egg is the Swamp guy egg; players place eggs by hand |
| Q14 | Owning an unused Swamp is enough; the egg must sit on the Swamp to hatch |
| Q15 | Swamp guy look is locked for life |
| Q16 | Other stats inherited normally |
| Q17 | He breeds; stats pass on, looks pass on like an ordinary Voidling's |
| Q18 | Goodbye counts as death; special variants are untradable |
| Q19 | Respawn is a new Swamp guy from a Swamp that never hatched one |
| Q20 | More S ranks and rarity mean longer incubation |
| Q21 | Hatch location does nothing (apart from the Swamp rule) |
| Q22 | One journal entry per form and special variant |
| Q23 | Hatching, evolving and spawning unlock entries; no backfill for old saves |

**Later (not blocking):** hue ranges per type (Q7a); names for the other 4★ tiles (Q12); a Volcano special variant.

---

# Part B: Implementation

## 12. Work packages

Each package is one commit on this branch.

| Package | Content |
|---|---|
| WP-A | Lifecycle hours, happiness-only reincarnation, separate care warning |
| WP-K | Chao stat levels, points and reincarnation; race mapping; UI; stat reset migration |
| WP-B | Adult form from the highest stat; `neutral` artist colors; existing adults → Neutral |
| WP-H | Incubation from S ranks and rarity |
| WP-C | Biome catalog and biome IDs on hexes |
| WP-E | Biome tile inventory, shop tiles, place, stack, pick up, 4★ environments |
| WP-L | Pick up and move eggs in the Garden |
| WP-G | Swamp guy: egg rule, Swamp-only incubation, used Swamps, respawn egg, trade block |
| WP-I | Encyclopedia: catalog, discoveries, journal screen with silhouettes |
| WP-J | Art: runs through the art pipeline as art arrives; unknown types fall back to `normal` |

## 13. Design

### Domain

- `StatProgressData { Level, Progress, Points }` per stat on `VoidlingData.Stats`. `StatGrowthRules` holds the level cap, progress per level, point cap and level-up formula constants. `StatProgressionService` applies progress, level-ups (seeded by creature, stat, reincarnation count and level) and reincarnation resets. `StatCalculator` reads levels, progress, points and the race value.
- `EvolutionService` picks the form by level with a seeded tie-break and returns the form's visual type ID.
- `BiomeCatalog` maps biome ID → stat, 4★ environment ID, and each environment back to its base biome.
- `IncubationPolicy` computes an egg's incubation time.
- `SpecialVariantCatalog` defines the Swamp guy: parent form, required environment, forced alleles, visual type.
- `EncyclopediaCatalog` lists entries by visual type ID.

### Application

- `AdvanceSimulationUseCase`: lifecycle, passive training, adulthood, Swamp-only incubation, hatch effects (used Swamp, special status, discoveries).
- `TrainingUseCase`: treats add progress; biome tiles: buy, place, stack, pick up.
- `BreedVoidlingsUseCase`: Swamp guy egg rule.
- `ShopUseCase`: biome tiles and the Swamp guy egg.
- `EggPlacementUseCase` (or the shop's egg methods): move a placed egg.
- `EncyclopediaUseCase`: record discoveries and build the journal projection.
- `GameStateMigrationService`: version 23 migration.

### Presentation

- The Voidling visual catalog gets `AuthoredColorVisualTypeIds` (`neutral`, `swamp-variant`): those types render the artist's colors. Unknown types fall back to the `normal` art.
- Stat UI shows level 0–99, the progress bar and points.
- Hex menu: place a biome tile, stack a matching tile, pick up a tile. Shop: biome tiles and the Swamp guy egg. Inventory: tile stacks.
- Garden: eggs can be dragged like Voidlings.
- Journal: a rail button and a grid plus detail card screen; silhouettes come from the portrait composer.

## 14. Save and network

The save version goes from 22 to 23.

| Change | Field | Migration |
|---|---|---|
| Chao stats | `VoidlingData.Stats` (new); `TrainingPoints` removed | every stat reset to level 0 |
| Forms | `Appearance.VisualTypeId` | adults → `neutral` |
| Biomes | `GardenModuleData.BiomeId` (new) | derived from `StatId` |
| Tile stacks | `GameStateData.BiomeTiles` (new) | empty |
| Used Swamps | `GardenModuleData.SpecialVariantHatched` (new) | false |
| Special variants | `SpecialVariantId` on creatures and eggs; `GameStateData.SpecialVariants` (new) | empty |
| Journal | `GameStateData.Encyclopedia` (new) | empty |

Network: the multiplayer protocol version goes up because race inputs change. Trades copy `VoidlingData` as JSON; validation rejects special variants and their eggs.

## 15. Tests

- **Domain:** level-up points and determinism; reincarnation reset; form selection and ties; biome catalog; incubation policy; special-variant rule; encyclopedia catalog.
- **Application:** training to level 99; happiness boundary; stacking and picking up tiles; Swamp guy egg once, Swamp-only incubation, used Swamps, respawn; trade rejection; migration; journal unlocks.
- **Godot smokes:** Garden UI smoke covers the journal screen; race smokes still pass.

## 16. Docs to update

`GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md`, `PRODUCTION_VOIDLING_APPEARANCE_RULES.md`, `IMPLEMENTATION_REMAINING_CHECKLIST.md` and `Assets/Voidlings/README.md` as the packages land.

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
