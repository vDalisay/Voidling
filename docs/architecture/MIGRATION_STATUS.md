# Architecture migration status

**Branch:** `architecture/futureproof-foundation`  
**Baseline:** post-demo `main`  
**Purpose:** current truth for what has actually moved behind the architecture boundaries.

`ARCHITECTURE.md` defines the durable target and rules. `RESTRUCTURING_PLAN.md` records the research and migration strategy. This file records implementation status so humans and coding agents do not need to infer it from old checklist boxes.

## Completed foundation

### Domain — pure C#

`Scripts/Domain/**` is Godot-free and CI-enforced.

Implemented seams include:

- deterministic named RNG streams (`StableRandom`);
- typed immutable `GameBalanceRules`;
- ability-gene expression;
- genome creation and inheritance;
- appearance/color resolution;
- rare mutation inheritance;
- relationship traversal and inbreeding burden;
- hatch viability;
- store-egg creation;
- stat calculation/progression;
- immutable race participant snapshots;
- race performance formulas;
- data-described demo race course/segments;
- deterministic fixed-step `RaceSimulation` owning movement, stamina, cheer, glide endurance/failure, obstacle outcomes and finish order.

Race simulation is frame-chunk independent and supports headless fast-forward through the exact same fixed-step path used during normal progression.

The old `GeneticsService` and `GameRules` remain compatibility facades for legacy callers. New deterministic rules belong in Domain rather than expanding those facades.

### Application — Godot-free use cases

`Scripts/Application/**` is Godot-free and CI-enforced. It may not reach back through `GameSession.Instance`.

Implemented use cases/services include:

- `BreedVoidlingsUseCase`;
- `TrainingUseCase`;
- `ShopUseCase`;
- `AdvanceSimulationUseCase` for lifecycle/incubation/hatching;
- `SettingsUseCase`;
- `VoidlingRosterUseCase` for identity, world-position persistence, departure and failed-egg removal;
- `RaceParticipantSnapshotFactory`;
- `RaceEntryFactory` for immutable player/CPU race entry creation;
- `RaceResultUseCase`;
- `GameStateMigrationService`.

Application returns data/results and mutates the supplied runtime aggregate. It does not render, persist files, play audio or call Godot APIs.

### Infrastructure

Implemented adapters:

- `IGameStateRepository` → `GodotJsonGameStateRepository`;
- `IAudioSettingsAdapter` → `GodotAudioSettingsAdapter`;
- `GameBalanceResource` → immutable `GameBalanceRules` conversion.

The existing save path remains `user://voidling_mvp_save.json`.

Designer-facing active balance data lives in:

- `Resources/Balance/demo_balance.tres`

The Resource is loaded only by Bootstrap and converted before entering Application/Domain. Product invariants such as stable stat IDs, inbreeding tiers and fixed identity catalogs are intentionally not exposed as casual tuning knobs. Race tuning is not duplicated in the Inspector while the authored resource does not yet control those values.

### Bootstrap

`Scripts/Bootstrap/GameBootstrap.cs` is the explicit composition root. It is the one place that intentionally sees concrete Infrastructure plus Application/Domain implementations.

It loads the designer-authored balance Resource once, converts it to immutable domain rules, and supplies that same rules object to the Application services. The transitional `GameRules` facade is configured from the same object so legacy callers cannot drift onto a second ruleset.

No DI container has been introduced. Manual composition remains small and readable.

### Presentation

Standalone Presentation components now include:

- `SettingsScreen`;
- `ShopScreen`;
- `BreedingScreen`;
- `ModalHost` for overlay/window lifetime;
- `RaceScreen`, a Godot-only shell over the pure `RaceSimulation`;
- `StatPresentationCatalog` for player-facing stat labels/colors.

These components do not reach through `GameSession.Instance` or the `GameRules` compatibility facade; CI enforces that boundary for `Scripts/Presentation/**`.

`RaceScreen` receives an immutable `RaceEntry`, maps simulation snapshots/events to sprites/camera/HUD/minimap/podium, sends cheer through `RaceSimulation.TryCheer`, and uses the simulator's fast-forward path. VFX has a separate non-authoritative RNG, so particle timing can no longer perturb race outcomes.

The legacy result-owning `Scripts/Race/RaceController*.cs` implementation has been removed.

The root `MainController` still coordinates legacy UI partials, but it no longer repeatedly reaches through the static service locator. It resolves the composed `GameSession` once and its partials use that explicit reference. Modal lifetime is delegated to `ModalHost`.

Stat display names/colors have moved out of `GameRules` into `StatPresentationCatalog`. Stable stat IDs and gameplay formulas remain outside Presentation.

### Localization

Godot's native translation pipeline is registered through `Localization/strings.csv`.

Migrated representative UI includes:

- global top navigation/currency label;
- Settings;
- Shop;
- Breeding and breeding validation/risk text.

Use semantic keys (`UI_*`) for new player-facing presentation text. User-created Voidling names remain literal.

The direct Windows launcher performs Godot's import phase before starting the game so a clean clone generates CSV translation resources instead of relying on an existing `.godot` cache.

### Tests and CI

The architecture branch has fast xUnit coverage for genetics, inbreeding, mutations, migration, lifecycle simulation, shop transactions, settings, roster operations, race performance and deterministic race simulation.

Race tests characterize:

- current demo course geometry/terrain;
- finish order/event equivalence across different elapsed-time chunk sizes;
- normal progression versus fast-forward equivalence;
- cheer as a simulation command;
- simulation-owned auto-completion/placement.

CI performs:

1. game restore;
2. test restore;
3. Release build;
4. architecture boundary enforcement;
5. domain/application tests;
6. Debug build for Godot editor-side C# integration;
7. blocking Godot `--import` resource import;
8. verification that the CSV localization resource was generated;
9. actual headless main-scene runtime smoke test with runtime errors treated as failures.

CI rejects Godot references in Domain/Application, rejects `GameSession.Instance` from inward layers, and prevents new `Scripts/Presentation/**` components from reaching through `GameSession.Instance` or `GameRules`.

## Intentionally transitional code

These files are **not** examples for new feature architecture.

### `GameSession`

`Scripts/Services/GameSession*.cs` remains a Godot-owned compatibility/lifetime facade because legacy world/UI code already calls its public API. Most mutable gameplay operations delegate to Application use cases, but it still owns compatibility responsibilities such as:

- a static `Instance` entry point for remaining legacy world code;
- save timing/event forwarding;
- initial demo state/spawn placement;
- presentation toast wording;
- compatibility seed/ID allocation.

The root UI coordinator no longer uses the static entry point, but Garden/other untouched legacy code may still do so. Remove those references incrementally when those features are touched; do not add new feature rules here.

### `GameRules`

`Scripts/Core/GameRules.cs` is now narrower. Player-facing stat labels/colors have moved to Presentation. It still exposes compatibility accessors for existing gameplay values/formulas, stable IDs, tint conversion and mutation lookup.

Do not add new deterministic formulas or presentation catalogs to `GameRules`. Move remaining responsibilities inward/outward as their callers migrate.

### `MainController`

The root object is still a legacy UI/navigation coordinator and several screens (race picker, Details, Inventory, family tree/goodbye/reset flows) are still implemented in partial files.

Its dependency pattern is improved: it holds one explicit session reference instead of using `GameSession.Instance` throughout its partials, and modal lifetime is owned by `ModalHost`. Continue extracting screens when they are substantially modified rather than performing a visual rewrite solely to eliminate partial classes.

### Garden

`GardenController` is presentation-heavy but acceptable for the current demo. Keep lifecycle/genetics/breeding outcomes out of it. Camera, pickup/drop and breeding/hatching animation can become composed presentation components when those areas are next extended.

Remaining static `GameSession.Instance` access in Garden/related legacy presentation is a Phase D cleanup target when those controllers are touched.

## Completed major migration phases

### Phase B — deterministic domain seams

Completed for the current demo feature set. Genetics, breeding-related rules, stats and racing now have focused pure-C# collaborators/characterization tests.

### Phase C — persistence/application seams

Completed for the current demo's high-churn operations. Persistence is behind a repository port and migrations/use cases are testable without scenes.

### Phase D — composition/service-locator removal

Substantially complete, not globally finished. Bootstrap is explicit and new Presentation/Application code does not depend on the static session locator. The root UI has also migrated away from it. Remaining untouched Garden/legacy components can be migrated incrementally.

### Phase E — race simulation extraction

Completed for the demo race:

```text
immutable RaceEntry / participant snapshots
  → pure RaceCourse + RaceSimulation
  → RaceState snapshots / RaceEvents
  → Godot RaceScreen presentation
  → placement event
  → Application race reward handoff
```

There is one result-authoritative simulator. Animation, camera and VFX edits cannot consume its outcome RNG or change finish order. Headless batch/fast-forward simulation is possible.

### Phase F — UI decomposition/localization foundation

The reference foundation is in place:

- shared `ModalHost`;
- Settings, Shop and Breeding standalone screens;
- localization CSV/project registration and clean-clone import verification;
- Presentation boundary enforcement;
- stat presentation catalog separated from gameplay rules.

Phase F does **not** require every legacy panel to be rewritten before feature work can continue. Future screens should follow these reference components.

## Next architecture migrations

These are incremental follow-ups, not reasons to rewrite stable demo code:

1. When Race Picker, Details/Inventory, Family Tree or another legacy modal is next substantially changed, move it into a standalone Presentation component using immutable view state + intent events.
2. Remove remaining `GameSession.Instance` access from Garden/other touched legacy presentation controllers through explicit setup/scene-owned references.
3. Move remaining presentation-only tint/mutation formatting out of `GameRules` as related screens migrate; do not duplicate domain identity/rules in Presentation.
4. Introduce additional Godot Resources only when the next roadmap feature has concrete designer-authored content/tuning that consumes them.
5. Add ADRs for durable architecture decisions when a decision first needs history/trade-off context; do not create ceremonial ADRs for obvious local refactors.

## Merge invariant

This architecture work must not intentionally alter current demo gameplay, race balance or persisted genetics. Before merge, the exact PR head must pass the full CI sequence above, the PR description must reflect the actual remaining transitional boundaries, and PR #2 remains draft until the user decides the architecture work is complete.
