# Architecture migration status

**Branch:** current `main` plus active architecture/feature branches  
**Baseline:** post-demo `main`  
**Purpose:** current truth for what has actually moved behind the architecture boundaries.

`ARCHITECTURE.md` defines the durable target and rules. `RESTRUCTURING_PLAN.md` records the original research and migration strategy. `docs/AGENT_HANDOFF_IMPLEMENTATION_PLAN.md` is the current execution contract for remaining product work. This file records implementation status so humans and coding agents do not need to infer it from old checklist boxes.

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

`Scripts/Application/**` is Godot-free and CI-enforced.

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

### Bootstrap / dependency composition

`Scripts/Bootstrap/GameBootstrap.cs` is the explicit composition root. It is the one place that intentionally sees concrete Infrastructure plus Application/Domain implementations.

It loads the designer-authored balance Resource once, converts it to immutable domain rules, and supplies that same rules object to the Application services. The transitional `GameRules` facade is configured from the same object so legacy callers cannot drift onto a second ruleset.

No DI container has been introduced. Manual composition remains small and readable.

The old `GameSession.Instance` service locator has been removed entirely. Scene-owned/root presentation code resolves the composed `GameSession` node once and passes or stores that explicit reference. CI rejects any future `GameSession.Instance` usage anywhere under `Scripts/**`.

### Presentation

Standalone Presentation components now include:

- `SettingsScreen`;
- `ShopScreen`;
- `BreedingScreen`;
- `RacePickerScreen`;
- `InventoryScreen`;
- `DetailsScreen` for Stats/DNA/Visual tabs;
- `ModalHost` for overlay/window lifetime;
- `RaceScreen`, a Godot-only shell over the pure `RaceSimulation`;
- `StatPresentationCatalog` for player-facing stat labels/colors;
- `VoidlingGroundVisualMetrics` for shared sprite-ground pivot/shadow proportions across world/challenge presentation;
- `VoidlingVisualDefinition` + `VoidlingVisualFactory` for the canonical base Voidling art contract.

These components receive presentation-ready snapshots and emit intent where interaction is needed. They do not reach through `GameSession` or the `GameRules` compatibility facade; CI enforces that boundary for `Scripts/Presentation/**`.

#### Centralized Voidling visual/art pipeline

WP0 of the contextualized handoff is implemented on the active visual-pipeline branch.

`Resources/Presentation/Voidlings/DefaultVoidlingVisual.tres` is now the single authoring source for base Voidling presentation. It owns the base/swim atlas references plus art-dependent layout and geometry: frame dimensions and rows, animation rates, portrait crop, adult/child/race scale, feet offsets, hitboxes, held offset, shadow geometry and mutation anchors.

`VoidlingVisualFactory` validates that definition and caches the shared world/race `SpriteFrames`. The following consumers no longer load or slice the base creature atlas themselves:

- local Garden `VoidlingActor`;
- connected/remote Garden `RemoteVoidlingActor`;
- single-player and multiplayer `RaceScreen` presentation;
- `UiFactory` portraits/cards, which transitively covers breeding, race picking/results, details/inspection, trade screens, family tree and other portrait consumers.

`VoidlingGroundVisualMetrics` and `VoidlingMutationVisualMetrics` now derive silhouette-dependent geometry/anchors from the same definition rather than preserving a second set of art constants.

The migration intentionally keeps the existing Sprout Lands textures referenced by the default `.tres`, so it changes architecture without changing the currently visible creature art. A production-art replacement can now update the resource (and source PNGs) without editing each consumer. `Assets/Voidlings/README.md` documents the artist workflow.

The old `voidling-cats` branch was reviewed rather than copied wholesale. Its central-catalog idea was useful, but its display-name-keyed Pip/Mallow overrides and hard-coded asset rules are not part of the production architecture. Future multiple visual families require a stable semantic ID, not a creature name or texture path in saves.

`RaceScreen` receives an immutable `RaceEntry`, maps simulation snapshots/events to sprites/camera/HUD/minimap/podium, sends cheer through `RaceSimulation.TryCheer`, and uses the simulator's fast-forward path. VFX has a separate non-authoritative RNG, so particle timing can no longer perturb race outcomes.

Result presentation is also presentation-only: podium pop/tilt animations, confetti and the fourth-place embarrassment drop do not affect simulation state or result RNG.

`ModalHost` uses deferred `QueueFree()` disposal for modal subtrees. This is required because close/navigation can be requested from a button signal owned by the subtree itself; synchronous `Free()` during that signal emission is unsafe in Godot.

The legacy result-owning `Scripts/Race/RaceController*.cs` implementation has been removed.

The root `MainController` still coordinates navigation and legacy HUD/family-tree/goodbye flows, but it receives/resolves the composed `GameSession` once rather than using global static access. Modal lifetime is delegated to `ModalHost`.

`GardenController` likewise holds one explicit session reference. `FamilyTreeView` receives edge-panning as view state rather than querying global settings.

Stat display names/colors have moved out of `GameRules` into `StatPresentationCatalog`. Stable stat IDs and gameplay formulas remain outside Presentation.

### Localization

Godot's native translation pipeline is registered through the committed gettext catalogs.

`project.godot` registers source translation files directly. It intentionally does **not** reference importer-generated `.translation` files: generated translation artifacts may not exist yet when Godot first reads project settings on a clean checkout.

Migrated representative UI includes:

- global top navigation/currency label;
- Settings;
- Shop;
- Breeding and breeding validation/risk text;
- Race Picker;
- Inventory;
- Details Stats/DNA/Visual tabs and explanatory labels;
- race result/placement messages introduced by the presentation polish pass;
- multiplayer/trade presentation introduced by the later multiplayer foundation.

Use semantic keys (`UI_*`) for new player-facing presentation text. User-created Voidling names remain literal.

The direct Windows launcher still performs a blocking Godot import phase for ordinary imported assets before launch, but localization no longer depends on a generated CSV artifact or pre-existing `.godot` cache.

### Tests and CI

The project has fast xUnit coverage for genetics, inbreeding, mutations, migration, lifecycle simulation, shop transactions, settings, roster operations, race performance, deterministic race simulation and multiplayer application behavior.

Race tests characterize:

- current demo course geometry/terrain;
- finish order/event equivalence across different elapsed-time chunk sizes;
- normal progression versus fast-forward equivalence;
- cheer as a simulation command;
- simulation-owned auto-completion/placement.

CI performs the build/test/import/runtime sequence plus focused presentation/network smoke probes. Relevant gates include:

1. game/test restore and Release build;
2. architecture boundary enforcement;
3. domain/application tests;
4. Debug build for Godot editor-side C# integration;
5. blocking Godot `--import` resource import;
6. committed localization-source validation;
7. actual headless main-scene runtime smoke test;
8. canonical Voidling visual pipeline smoke test;
9. trade-panel interaction smoke test;
10. two-process LAN connectivity and negotiated trade smoke tests.

CI rejects Godot references in Domain/Application, rejects `GameRules` access from standalone `Scripts/Presentation/**`, forbids `GameSession.Instance` throughout `Scripts/**`, and rejects direct base/swim Voidling atlas paths from C# consumers. The visual smoke resolves world directions, race run/swim states, shared UI portrait construction, geometry and mutation anchors through `VoidlingVisualFactory`.

## Intentionally transitional code

These files are **not** examples for new feature architecture.

### `GameSession`

`Scripts/Services/GameSession*.cs` remains a Godot-owned compatibility/lifetime facade because legacy world/UI code already calls its public API. Most mutable gameplay operations delegate to Application use cases, but it still owns compatibility responsibilities such as:

- save timing/event forwarding;
- initial demo state/spawn placement;
- presentation toast wording;
- compatibility seed/ID allocation.

It no longer provides global/static access. Do not add new feature rules here; new deterministic behavior belongs in Domain and player-action sequencing belongs in Application.

### `GameRules`

`Scripts/Core/GameRules.cs` is now narrower. Player-facing stat labels/colors have moved to Presentation. It still exposes compatibility accessors for existing gameplay values/formulas, stable IDs, tint conversion and mutation lookup.

Do not add new deterministic formulas or presentation catalogs to `GameRules`. Move remaining responsibilities inward/outward as their callers migrate.

### `MainController`

The root object is now primarily navigation, persistent garden HUD coordination, race handoff and a few legacy modal flows. Settings, Shop, Breeding, Race Picker, Inventory and Details no longer own their rendering inside the controller.

Family Tree, goodbye/reset confirmation and the persistent selected-Voidling HUD remain legacy code. Extract them when those features are next substantially modified rather than performing a visual rewrite solely for symmetry.

### Garden

`GardenController` is presentation-heavy but acceptable for the current demo. Keep lifecycle/genetics/breeding outcomes out of it. Camera, pickup/drop and breeding/hatching animation can become composed presentation components when those areas are next extended.

Its session dependency is explicit; there is no static service-locator access.

## Completed major migration phases

### Phase B — deterministic domain seams

Completed for the current demo feature set. Genetics, breeding-related rules, stats and racing now have focused pure-C# collaborators/characterization tests.

### Phase C — persistence/application seams

Completed for the current demo's high-churn operations. Persistence is behind a repository port and migrations/use cases are testable without scenes.

### Phase D — composition/service-locator removal

Completed for the current demo. Bootstrap is explicit, `GameSession.Instance` has been deleted, and CI prevents it from being reintroduced. Current scene/root controllers use explicit node-owned references while standalone Presentation components receive view state and emit intent.

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

Completed to the intended foundation level:

- shared `ModalHost` with signal-safe deferred disposal;
- standalone Settings, Shop, Breeding, Race Picker, Inventory and Details screens;
- committed gettext localization/project registration with clean-clone validation;
- Presentation boundary enforcement;
- stat presentation catalog separated from gameplay rules;
- shared Voidling ground/shadow visual metrics across garden and race presentation.

Phase F deliberately does **not** require every legacy panel to be rewritten before feature work can continue. Future screens should follow these reference components.

### Phase G — active designer Resources

Completed for rules/content that currently have a real designer-authored source. `GameBalanceResource`/`Resources/Balance/demo_balance.tres` provide active gameplay tuning, while `VoidlingVisualDefinition`/`Resources/Presentation/Voidlings/DefaultVoidlingVisual.tres` provide presentation-only art authoring. Gameplay Resources are converted before entering Domain/Application; presentation Resources remain on the Godot presentation edge.

Do not create speculative Resource classes for roadmap systems before those systems consume authored data. Add focused Resources alongside the first concrete feature that requires them.

### WP0 — centralized Voidling visual pipeline

Implemented on `agent/contextualized-implementation-plan`, pending exact-head CI before merge. The required consumer migrations, validation, no-bypass CI rule and dedicated headless visual smoke are present. No save/network schema change was needed because all current Voidlings still resolve the same semantic default visual definition.

## Next architecture migrations

These are incremental follow-ups, not reasons to rewrite stable demo code:

1. When Family Tree, the persistent selected-Voidling HUD, goodbye/reset flows or another legacy UI area is next substantially changed, move it behind presentation-ready view state + intent events.
2. Move remaining presentation-only tint/mutation formatting out of `GameRules` as related legacy callers migrate; do not duplicate domain identity/rules in Presentation.
3. Introduce additional Godot Resources only when the next roadmap feature has concrete designer-authored content/tuning that consumes them.
4. Add ADRs for durable architecture decisions when a decision first needs history/trade-off context; do not create ceremonial ADRs for obvious local refactors.
5. If multiple base Voidling visual families become a confirmed product feature, introduce a stable semantic visual-family ID and extend the existing visual catalog; do not key art selection to display names or persisted resource paths.

## Merge invariant

Architecture changes must not intentionally alter current demo gameplay, race balance or persisted genetics. Before merge, the exact PR head must pass the full CI sequence above, and the PR description must reflect the actual remaining transitional boundaries.
