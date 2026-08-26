# Voidling Contextualized Agent Handoff Implementation Plan

**Status:** READY FOR AGENT HANDOFF after this review  
**Baseline:** `main` at merge commit `940546fc0db82c76d538995bd577c124cf50f1eb`  
**Prepared:** 2026-08-26  
**Companion documents:**

- `ARCHITECTURE.md` — active dependency and pattern policy;
- `docs/GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md` — player-facing intent and open product decisions;
- `docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md` — detailed research/technical reference, but not authoritative where this handoff plan or gameplay context overrides it;
- `docs/MULTIPLAYER_IMPLEMENTATION_PLAN.md` — historical implementation plan for the multiplayer foundation that is now substantially implemented on `main`;
- `docs/architecture/VOIDLING_VISUAL_ASSET_PIPELINE.md` — required architecture for replacing Voidling artwork from one authoritative source.

---

## 1. Why this handoff document exists

The older genetics/breeding/hatching/racing plan is useful research and contains many good technical details, but it is no longer safe to hand directly to an implementation agent as a greenfield plan. The codebase has moved substantially since it was written, multiplayer has been merged, architecture migrations are already in place, and the newer gameplay-context document intentionally leaves several product decisions unresolved.

An agent must therefore treat this document as the **execution contract for remaining work**, not reimplement systems that already exist and not silently convert proposals from older research into product requirements.

### Authority order

When two sources disagree, use this order:

1. **Current `main` behavior and data compatibility** for already-shipped/implemented behavior.
2. **`ARCHITECTURE.md`** for dependency direction, composition, persistence boundaries and pattern policy.
3. **`docs/GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md`** for player-facing intent.
4. **This handoff plan** for sequencing, implementation constraints, integration boundaries and definition of done.
5. Older implementation/research plans for details that do not conflict with the above.

If a remaining product choice is explicitly marked unresolved, the agent must preserve extensibility and stop before inventing the rule.

---

## 2. Review result

### Was the existing plan ready for autonomous agent handoff?

**No.** The main blockers were:

- it still reads partly like a greenfield architecture plan even though Domain/Application extraction, deterministic racing, persistence boundaries and multiplayer are already implemented;
- some product statements are now too concrete relative to the newer gameplay context;
- it allows personality/course behavior to drift toward race influence even though personality is explicitly atmosphere-only for v1;
- it defines ordinary appearance inheritance more precisely than the current product interview supports, while the appearance section is explicitly incomplete;
- it does not establish a production-grade pipeline for replacing Voidling art consistently across Garden, races, portraits, multiplayer and other screens;
- it lacks a clear “do not rebuild” baseline and agent-sized work packages with verification gates.

### Is it ready after this contextualization?

**Yes, with one qualification:** agents may implement the work packages below autonomously, but any item listed under **Open product decisions** is a deliberate stop condition rather than permission to choose a design.

---

## 3. Current-main baseline: do not rebuild these systems

The merged code already has the architectural foundation the older plan was trying to reach. An implementation agent must extend these systems rather than create parallel replacements.

### Architecture already established

- layered dependency direction: Presentation/Infrastructure → Application → Domain;
- Domain and Application are Godot-free;
- `GameBootstrap` is the explicit composition root;
- manual composition is preferred over a DI container;
- deterministic/randomized rules belong in Domain;
- persistent side effects go through Application and repository ports;
- presentation maps state to sprites/UI but does not own gameplay outcomes;
- CI already rejects Godot dependencies in Domain/Application, direct `GameRules` use from standalone Presentation, and `GameSession.Instance` service-location regressions.

### Genetics/breeding/hatching foundation already exists

Do not replace the current factories/policies/use cases merely because an older section describes an ideal version. Extend the existing components and tests. In particular, preserve existing save compatibility and deterministic RNG/substream behavior.

### Racing foundation already exists

- `RacePerformanceModel` owns numeric performance rules;
- `RaceSimulation` owns deterministic fixed-step race outcomes;
- Presentation renders snapshots and events;
- multiplayer racing reuses the same simulation instead of forking race logic;
- current obstacle/jump behavior and multiplayer synchronization are part of the baseline.

Do not create a second race simulator, presentation-driven outcome logic, physics-based authoritative race, or separate multiplayer race rules.

### Multiplayer foundation is now baseline, not future scope

The merged multiplayer work includes connected Garden state, Pokémon-style mutual-confirmation trading, deterministic multiplayer racing, friends leaderboards/daily race infrastructure, LAN development adapters and CI smoke tests. New work must preserve offline-first behavior and must not bypass the existing Application/adapter boundaries.

---

## 4. Product decisions locked for implementation

These are safe for agents to rely on now.

### Core loop

```text
Raise → train/care → race → earn → breed toward a goal → raise offspring → tackle harder content
```

Breeding, raising and racing are all core pillars. Breeding carries strategic long-term progression weight.

### Genetics and breeding

- exactly two selected adult parents;
- breeding is always player-initiated;
- two visible DNA profiles are the normal source of inherited stat potential;
- ordinary stat inheritance is parent-only in v1; deeper ancestors matter for lineage/inbreeding, not surprise normal-stat reintroduction;
- ranks are E → D → C → B → A → S;
- rare normal breakthrough may exceed expected parental potential by at most +1 rank;
- randomness remains important and there is no exact player-facing offspring probability calculator;
- inbreeding initially affects hatch-failure risk, not disease/deformity/stat/personality penalties;
- historical family-tree information remains important even after active burden is reduced;
- eggs cannot be force-hatched;
- a bred egg's inheritance outcome is fixed when the egg is created;
- store-egg genetic rolls are fixed when the specific egg enters store inventory.

### Personality

**Personality does not affect race outcomes in v1.** It exists for Garden behavior, reactions, preferences, attachment and flavor. Do not route personality values into `RacePerformanceModel`, `RaceSimulation`, obstacle chances, speed, stamina or race RNG.

### Racing

- races are automated;
- race outcomes derive from immutable participant snapshots + explicit rules + deterministic randomness;
- current Run/Swim/Fly/Power/Stamina model remains the race-stat vocabulary;
- hidden genes are not read directly by race simulation;
- multiplayer must continue to reuse the same deterministic simulation;
- visual animation timing must never change authoritative outcome state.

### Offline-first

Steam/GodotSteam/platform failure must never block local startup, saves, Garden simulation, breeding, hatching, training, shop/progression, lifecycle or local racing.

---

## 5. Open product decisions: do not invent these

These are explicit handoff stop conditions. Architecture may prepare extension points, but agents must not select final player-facing rules without a new product decision.

### Appearance inheritance

The current gameplay interview has **not** finalized:

- ordinary color inheritance;
- pattern inheritance;
- shiny/special-coat behavior;
- mutation rates;
- rare-trait transmission depth;
- stacking rules;
- collection/discovery behavior;
- exact trophy-form appearance rules.

The older technical plan contains candidate/reference-inspired rules. Treat those as research proposals, not requirements.

Implementation may create typed appearance loci, semantic visual IDs, policies and tests for already-confirmed behavior, but must not lock unresolved probabilities/dominance rules into saves or public APIs.

### Lifecycle/trophy transformation

The long-term immortal/trophy form is desired, but exact transformation requirements remain unresolved. Do not invent a final recipe.

### Deep balance/economy tuning

Keep tunable constants in rules/resources. Do not treat prototype numbers in old documents as immutable product decisions unless current code or the gameplay context already depends on them.

---

## 6. Architecture contract for all new work

All implementation packages must obey `ARCHITECTURE.md`.

### Dependency rules

```text
Presentation (Godot) ─┐
                      ├──> Application ───> Domain
Infrastructure (Godot)┘
         ↑
      Bootstrap wires concrete implementations
```

- Domain: pure deterministic rules/models; no Godot, paths, UI text, file I/O or hidden wall-clock inputs.
- Application: use cases/orchestration; no Godot; typed inputs/results; persistence through explicit ports.
- Infrastructure: Godot/platform/file/resource adapters only; no gameplay-rule ownership.
- Presentation: rendering, animation, camera, UI, input, VFX; no authoritative outcomes.
- Bootstrap: explicit composition root.

### Pattern policy

Use patterns only where they solve a concrete problem:

- **Factory** when object creation has invariants or shared construction rules.
- **Strategy/policy** when a rule is intentionally variable.
- **Composition/decorator** for visual mutation/adornment layers.
- **Observer/events** for local notification with visible ownership.
- **Adapter** at platform/resource boundaries.

Do not add abstract factories, repositories, event buses, builders, base-class hierarchies or DI frameworks simply for architectural appearance.

### Persistence

- never persist raw Godot resource paths as creature identity;
- add save fields only when they represent stable semantic game state;
- all schema changes require versioning/migration and round-trip tests;
- transaction/idempotency guarantees in multiplayer trading must remain intact.

### Determinism

Any result-affecting randomness must use the existing deterministic RNG policy/substreams and be reproducible from explicit inputs. VFX and presentation randomness must stay isolated from authoritative streams.

---

## 7. Mandatory pre-feature work package: centralized Voidling visual/art pipeline

This is the highest-priority architectural addition before substantial replacement art arrives.

### Problem observed on current `main`

Voidling visuals are currently assembled in several places:

- `VoidlingActor` directly loads the character spritesheet and builds directional frames;
- `RaceScreen` directly loads character/swim sheets and independently builds race frames/scales;
- `UiFactory` directly loads the character sheet and chooses a fixed portrait atlas region;
- mutation adornments and grounding metrics are shared only partially.

That means replacing the creature art can require edits in multiple screens and creates exactly the inconsistency risk already seen between Garden, racing, trade/selection and family/portrait views.

### Required end state

There must be **one authoritative Voidling visual catalog/definition**. Every context asks that presentation service/factory for the appropriate visual representation.

Changing the default Voidling atlas/definition in that single source must propagate to:

- local Garden actors;
- remote connected-Garden actors;
- single-player racing;
- multiplayer racing;
- race results/podium portraits;
- trade negotiation and exchange portraits;
- breeding selection/presentation;
- details/inspection portraits;
- family-tree/lineage portraits;
- roster/race-picker cards;
- future screens that show a Voidling.

See `docs/architecture/VOIDLING_VISUAL_ASSET_PIPELINE.md` for the exact design and migration sequence.

### Architectural rules for the art pipeline

- Domain/Application remain completely art-agnostic.
- Saves/network packets carry semantic creature appearance data, never texture paths or atlas coordinates.
- Presentation owns the visual catalog and construction.
- Prefer one source atlas for every context. Context-specific source art is allowed only when genuinely required (for example a dedicated swimming sheet) and is still referenced through the same visual definition.
- Stage scales, sprite pivot/ground offset, hit bounds, portrait crop and mutation anchors belong with the visual definition/profile so new art cannot silently break interaction or grounding.
- Mutation rendering composes over the resolved base visual rather than duplicating base-art selection.
- No global service locator. Bootstrap/root presentation composition provides the catalog/factory to consumers. If the transitional root UI makes explicit injection temporarily awkward, use one clearly owned presentation composition object rather than static mutable state.

### Required CI gate after migration

CI must reject new direct creature-art loads outside the approved catalog/loader/factory path and must headlessly validate that all required animation/presentation states resolve.

This gate is part of the migration's definition of done, not optional cleanup.

---

## 8. Remaining implementation work packages

Agents should work in this order unless a concrete dependency proves otherwise.

### WP0 — Visual pipeline consolidation

**Goal:** make incoming art replaceable from one authoritative source before more presentation surfaces are added.

Tasks:

1. inventory every current base-Voidling visual consumer;
2. implement the presentation catalog/definition/factory described in the visual-pipeline architecture doc;
3. migrate Garden actor construction first;
4. migrate race run/swim/glide presentation without touching `RaceSimulation`;
5. migrate all `UiFactory.CreatePortrait`/card use to the visual factory;
6. migrate remote connected-Garden rendering;
7. centralize ground/pivot/shadow/hitbox/held-offset metadata;
8. keep mutation adornments compositional;
9. add catalog validation + no-bypass CI checks;
10. run all existing gameplay and multiplayer smoke tests.

**Acceptance:** replacing the catalog's default Voidling atlas/definition changes every base representation without editing any consumer class.

### WP1 — Reconcile genetics data model with confirmed product rules

**Goal:** verify the existing model represents the two visible DNA profiles and parent-only v1 inheritance without duplicating systems.

Tasks:

- map current `Genome`, phenotype and save fields to the confirmed player model;
- identify only missing semantic fields;
- preserve deterministic inheritance and existing save migrations;
- ensure rare +1 rank breakthrough cannot jump more than one rank;
- ensure deeper ancestry is used for pedigree/inbreeding only, not ordinary stat resurrection;
- add focused Domain tests for these invariants.

Do not implement unresolved appearance dominance/probabilities here.

### WP2 — Pedigree and inbreeding correctness

**Goal:** make lineage calculations reliable, inspectable and migration-safe.

Tasks:

- use the existing pedigree risk policy/calculator rather than UI heuristics;
- verify relationship calculation for parent/child, siblings, half-siblings and deeper shared ancestors;
- keep active burden separate from historical family-tree marks;
- enforce the current burden/outcross behavior only where it is already a confirmed product rule;
- expose typed Application projections for UI rather than letting UI traverse mutable save objects;
- add deterministic pedigree fixtures and save round-trip tests.

### WP3 — Breeding outcome orchestration

**Goal:** keep breeding deterministic, player-initiated and transactional.

Tasks:

- extend the existing breeding use case/factories instead of introducing a parallel service;
- validate both parents are eligible adults and cooldown requirements pass;
- create/freeze the child/egg genetic outcome exactly once;
- persist outcome before presentation celebrates success;
- surface typed failure reasons;
- keep presentation animation non-authoritative;
- ensure multiplayer/trading cannot create duplicate ownership or lineage records.

### WP4 — Hatching/lifecycle progression

**Goal:** extend current incubation/lifecycle orchestration without hidden frame-time or wall-clock dependencies.

Tasks:

- Application receives explicit elapsed duration;
- deterministic state transition occurs once;
- presentation reacts to typed state changes;
- hatch failure uses confirmed inbreeding risk only;
- no force-hatch path;
- no final trophy/reincarnation recipe until product requirements are locked.

### WP5 — Race progression/balance extension

**Goal:** add race content/balance by extending the current deterministic simulator.

Tasks:

- author new course/rule data through existing race abstractions;
- keep race entry immutable;
- never let personality affect v1 race outcome;
- never let presentation animation/physics determine result;
- single-player and multiplayer use the same rules/simulation;
- if new segment behavior varies materially, add a small policy/strategy rather than conditionals spread across Presentation;
- add deterministic replay/chunking tests for every new result-affecting mechanic.

### WP6 — Player information and UX projections

**Goal:** show genetics/lineage/race information without leaking domain structure into UI.

Tasks:

- create Application-owned read projections where UI currently needs to understand domain internals;
- show DNA profiles/ranks and relevant lineage risk in understandable terms;
- do not expose an exact offspring probability calculator;
- preserve the distinction between current trained stats and inherited potential;
- use the centralized visual factory for every creature portrait/card.

### WP7 — Production art ingestion

Only start after WP0 is merged.

For each incoming Voidling art revision:

1. add/replace the source image(s) under the canonical Voidling art folder;
2. update one visual definition/catalog entry if dimensions/layout changed;
3. run catalog validation;
4. run generated presentation smoke/contact-sheet checks if available;
5. verify Garden, race, portrait and remote contexts;
6. no consumer-specific texture edits are permitted.

This is deliberately a content operation, not a code refactor each time art changes.

---

## 9. Agent execution rules

Each autonomous implementation PR/branch should satisfy all of the following.

### Before coding

- read `ARCHITECTURE.md`;
- read this handoff plan;
- read only the relevant older plan sections;
- inspect current implementation before creating new abstractions;
- search for existing factories/policies/facades/tests that own the behavior;
- identify save/network compatibility impact.

### While coding

- prefer modifying/extending the existing owner of a rule;
- keep files feature-oriented and reasonably small;
- no UI-owned game rules;
- no platform calls outside adapters;
- no static mutable service locator;
- no duplicate deterministic simulator;
- no direct Voidling art path after WP0 migration;
- no new product rule where this document says unresolved.

### Before handoff/merge

- Release and Debug builds pass;
- Domain/Application tests pass;
- architecture-boundary checks pass;
- Godot import/runtime smoke passes;
- multiplayer/trade smoke tests remain green where touched or transitively affected;
- new deterministic behavior has direct tests;
- new save data has migration/round-trip coverage;
- visual-pipeline changes pass the catalog/no-bypass validation;
- agent summarizes what changed, what remains unresolved and what it deliberately did not implement.

---

## 10. Required tests and CI evolution

Existing CI is a strong baseline and must be extended rather than replaced.

### Keep

- Release build;
- architecture boundary grep checks;
- Domain/Application tests;
- Debug build for Godot editor/resource loading;
- headless Godot import;
- localization validation;
- runtime smoke;
- trade-panel interaction smoke;
- two-process LAN handshake;
- two-process durable trade smoke and diagnostic artifacts.

### Add during WP0

A headless **Voidling visual catalog validation** step that fails when:

- catalog cannot load;
- required base definition is missing;
- referenced textures are missing;
- frame rectangles exceed atlas bounds;
- required states are missing (`idle`/directional walk or their defined equivalent, race/run, swim where required, portrait);
- required stage/context presentation profiles are missing;
- portrait region is invalid;
- hit/pivot/ground metrics are invalid;
- a known consumer bypasses the catalog with a direct base-Voidling texture load.

Recommended command-line smoke shape:

```text
godot --headless --path . -- --voidling-visual-catalog-smoke
```

The probe should instantiate at least:

- one Garden visual;
- one race visual;
- one portrait/card visual;
- one remote/shared-snapshot visual;

and verify that each resolves from the same catalog definition ID.

### Optional but valuable art-review artifact

A headless presentation probe may render a small contact sheet showing the required states/contexts and upload it as a CI artifact. This is a review aid, not a pixel-perfect golden-image gate; normal art revisions should not require rewriting brittle screenshot hashes.

---

## 11. Definition of done for centralized art replacement

The visual pipeline is complete only when all of these are true:

- there is one canonical default Voidling visual definition/catalog entry;
- Garden does not hardcode the character spritesheet;
- Race does not hardcode the base character spritesheet or independently duplicate its base frame map;
- UI portraits/cards do not hardcode the base character spritesheet or portrait crop;
- remote multiplayer visuals resolve through the same catalog;
- mutations/adornments remain layered and still align after an art swap;
- stage/context scales, pivots, grounding and hit bounds are catalog/profile data rather than scattered magic numbers;
- replacing the canonical atlas with another valid same-layout atlas changes all contexts with no C# edits;
- changing atlas layout requires editing only the authoritative visual definition, not screen/actor code;
- CI prevents future bypasses;
- existing gameplay state, saves, deterministic race results and multiplayer protocol remain unchanged by a purely visual replacement.

---

## 12. Suggested branch/PR slicing for an implementation agent

Do not hand one agent an unlimited “finish the whole design” task. Use reviewable slices:

1. `architecture/voidling-visual-catalog` — catalog/definition/factory + validation, no consumer migration yet;
2. `refactor/voidling-visual-garden` — local + remote Garden consumers;
3. `refactor/voidling-visual-race` — race visual construction only;
4. `refactor/voidling-visual-portraits` — UI cards/portraits/family/trade/breeding/details;
5. `ci/voidling-visual-pipeline-guard` — no-bypass + headless validation/contact sheet;
6. genetics/pedigree/breeding work in separate domain-focused slices;
7. race-content changes in separate deterministic-simulation slices.

Each slice should keep CI green and should not mix gameplay rebalance with presentation refactors.

---

## 13. Handoff summary

The project does **not** need another architecture rewrite. It needs disciplined extension of the architecture now on `main`.

The immediate technical priority is to remove the remaining presentation-level duplication around Voidling artwork before production art churn increases. After the centralized visual pipeline is in place, replacing a Voidling spritesheet should be a one-source content change rather than a Garden/Race/UI/multiplayer code sweep.

For gameplay systems, agents may proceed with confirmed genetics, lineage, breeding, hatching and deterministic-race invariants, while leaving unresolved appearance and trophy/lifecycle specifics explicitly open for the next product-design pass.