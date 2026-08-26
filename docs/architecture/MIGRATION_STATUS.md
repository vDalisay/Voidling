# Architecture migration status

**Branch:** `agent/implementation-plan` over current `main`  
**Baseline:** `main` at `940546fc0db82c76d538995bd577c124cf50f1eb`  
**Purpose:** current truth for what has actually moved behind the architecture boundaries.

`ARCHITECTURE.md` defines the durable target and rules. `RESTRUCTURING_PLAN.md` records the original research and migration strategy. `docs/AGENT_HANDOFF_IMPLEMENTATION_PLAN.md` is the current execution contract for remaining product work. This file records implementation status so humans and coding agents do not need to infer it from old checklist boxes.

## Completed foundation

### Domain — pure C#

`Scripts/Domain/**` is Godot-free and CI-enforced.

Implemented seams include:

- deterministic named RNG streams (`StableRandom`);
- typed immutable `GameBalanceRules`;
- ability-gene expression;
- genome creation and parent-only inheritance;
- bounded rare +1 rank breakthrough behavior;
- appearance/color resolution;
- rare mutation inheritance;
- relationship traversal and inbreeding burden;
- hatch viability;
- store-egg creation;
- stat calculation/progression;
- immutable race participant snapshots;
- race performance formulas;
- authored `RaceCourseCatalog` definitions with stable semantic course IDs/versions;
- deterministic fixed-step `RaceSimulation` owning movement, stamina, cheer, glide endurance/failure, obstacle outcomes and finish order.

Race simulation is frame-chunk independent and supports headless fast-forward through the exact same fixed-step path used during normal progression. The current authored catalog contains the original demo course plus a longer standard course using only existing deterministic mechanics.

The old `GeneticsService` and `GameRules` remain compatibility facades for legacy callers. New deterministic rules belong in Domain rather than expanding those facades.

### Application — Godot-free use cases and read models

`Scripts/Application/**` is Godot-free and CI-enforced.

Implemented use cases/services include:

- `BreedVoidlingsUseCase`;
- `TrainingUseCase`;
- `ShopUseCase`;
- `AdvanceSimulationUseCase` for lifecycle/incubation/hatching;
- `SettingsUseCase`;
- `VoidlingRosterUseCase` for identity, world-position persistence, departure and failed-egg removal;
- `LineageTreeProjectionService` for immutable pedigree/family-tree UI data;
- `VoidlingProfileProjectionService` for immutable DNA/training/lineage/player-information data;
- `RaceParticipantSnapshotFactory`;
- `RaceEntryFactory` for immutable player/CPU race entry creation and frozen course identity;
- `RaceResultUseCase`;
- `GameStateMigrationService`.

Application returns data/results and mutates the supplied runtime aggregate where a use case owns a state transition. It does not render, persist files, play audio or call Godot APIs.

### Infrastructure

Implemented adapters include:

- `IGameStateRepository` → `GodotJsonGameStateRepository`;
- `IAudioSettingsAdapter` → `GodotAudioSettingsAdapter`;
- `GameBalanceResource` → immutable `GameBalanceRules` conversion.

The existing save path remains `user://voidling_mvp_save.json`.

Designer-facing active balance data lives in `Resources/Balance/demo_balance.tres`. The Resource is loaded only by Bootstrap and converted before entering Application/Domain. Product invariants such as stable stat IDs, inbreeding tiers and fixed identity catalogs are intentionally not exposed as casual tuning knobs.

### Bootstrap / dependency composition

`Scripts/Bootstrap/GameBootstrap.cs` is the explicit composition root. It is the one place that intentionally sees concrete Infrastructure plus Application/Domain implementations.

It loads the designer-authored balance Resource once, converts it to immutable domain rules, and supplies that same rules object to the Application services. The transitional `GameRules` facade is configured from the same object so legacy callers cannot drift onto a second ruleset.

`VoidlingProfileProjectionService` and the lineage projection service are composed explicitly and exposed through narrow `GameSession` compatibility methods for scene-owned callers. No DI container has been introduced. Manual composition remains small and readable.

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
- `StatPresentationCatalog` for player-facing stat labels/colors/rank labels;
- `VoidlingGroundVisualMetrics` for shared sprite-ground pivot/shadow proportions across world/challenge presentation;
- `VoidlingVisualDefinition` + `VoidlingVisualFactory` for the canonical base Voidling art contract.

These components receive presentation-ready snapshots and emit intent where interaction is needed. They do not reach through `GameSession` or the `GameRules` compatibility facade; CI enforces that boundary for `Scripts/Presentation/**`.

#### Centralized Voidling visual/art pipeline

WP0 is implemented on the unified implementation branch.

`Resources/Presentation/Voidlings/DefaultVoidlingVisual.tres` is the single authoring source for base Voidling presentation. It owns the base/swim atlas references plus art-dependent layout and geometry: frame dimensions and rows, animation rates, portrait crop, adult/child/race scale, feet offsets, hitboxes, held offset, shadow geometry and mutation anchors.

`VoidlingVisualFactory` validates that definition and caches shared world/race `SpriteFrames`. The following consumers no longer load or slice the base creature atlas themselves:

- local Garden `VoidlingActor`;
- connected/remote Garden `RemoteVoidlingActor`;
- single-player and multiplayer `RaceScreen` presentation;
- `UiFactory` portraits/cards, which transitively covers breeding, race picking/results, details/inspection, trade screens, family tree and other portrait consumers.

`VoidlingGroundVisualMetrics` and `VoidlingMutationVisualMetrics` derive silhouette-dependent geometry/anchors from the same definition rather than preserving a second set of art constants.

The migration intentionally keeps the existing Sprout Lands textures referenced by the default `.tres`, so it changes architecture without changing currently visible creature art. A production-art replacement can update the resource and source PNGs without editing each consumer. `Assets/Voidlings/README.md` documents the artist workflow.

The old `voidling-cats` branch was reviewed rather than copied wholesale. Its central-catalog idea was useful, but its display-name-keyed Pip/Mallow overrides and hard-coded asset rules are not part of the production architecture. Future multiple visual families require a stable semantic ID, not a creature name or texture path in saves.

The dedicated command-line visual smoke probe was removed after the normal build/import/runtime gates proved sufficient and the extra probe was not worth maintaining. CI keeps the cheap, durable no-bypass checks: it requires the canonical `.tres` and rejects direct base/swim atlas paths from C# consumers.

#### Race content presentation

`RaceScreen` receives an immutable `RaceEntry`, reads the frozen `CourseDefinition`, maps simulation snapshots/events to sprites/camera/HUD/minimap/podium, sends cheer through `RaceSimulation.TryCheer`, and uses the simulator's fast-forward path. VFX has a separate non-authoritative RNG, so particle timing cannot perturb race outcomes.

The standard race picker can now select the original `demo` course or `long-standard`. The daily friend race deliberately remains pinned to the original demo course so an already-saved daily attempt recreates the same course identity. Multiplayer also remains on its existing canonical demo-course protocol while using the same result-authoritative `RaceSimulation` and rules model.

Result presentation remains presentation-only: podium pop/tilt animations, confetti and the fourth-place embarrassment drop do not affect simulation state or result RNG.

#### Player-information projections

WP6 introduced `VoidlingProfileProjectionService` so UI does not need to traverse `Genome`, `TrainingPoints` or rare-trait collections to explain a Voidling.

The projection separates:

- DNA profile 1 rank;
- DNA profile 2 rank;
- expressed inherited potential;
- current training points/level/progress;
- current effective stat value;
- active inbreeding burden;
- historical inbreeding mark;
- rare-trait founder/display information;
- current visual/color DNA facts.

The general race picker, daily race picker, multiplayer race setup, breeding parent cards, selected-Voidling HUD and Details screen now consume this projection for player-information rendering. `DetailsScreen` visibly distinguishes inherited DNA ranks from trained progress and shows the historical lineage mark without exposing an offspring probability calculator.

Family Tree already consumes `LineageTreeProjectionService`, including archive-only ancestors, rather than traversing mutable save objects itself.

### Localization

Godot's native translation pipeline is registered through the committed gettext catalog.

`project.godot` registers source translation files directly. It intentionally does **not** reference importer-generated `.translation` files: generated translation artifacts may not exist yet when Godot first reads project settings on a clean checkout.

Migrated representative UI includes global top navigation/currency, Settings, Shop, Breeding validation/risk text, Race Picker and course labels, Inventory, Details Stats/DNA/Visual tabs and lineage-history text, race result/placement messages, and multiplayer/trade presentation.

Use semantic keys (`UI_*`) for new player-facing presentation text. User-created Voidling names remain literal.

### Tests and CI

The project has fast xUnit coverage for genetics, inbreeding, mutations, migration, lifecycle simulation, shop transactions, settings, roster operations, profile/lineage projections, race performance, deterministic race simulation and multiplayer application behavior.

Race tests characterize:

- every authored course's geometry/terrain;
- finish order/event equivalence across different elapsed-time chunk sizes;
- normal progression versus fast-forward equivalence;
- cheer as a simulation command;
- simulation-owned auto-completion/placement;
- immutable course identity in `RaceEntry`.

Profile-projection tests characterize inherited potential versus trained progression, archive-backed mutation founder names, active burden versus historical marks, immutable snapshots, active-roster filtering and the absence of offspring-probability fields.

CI performs the build/test/import/runtime sequence plus focused presentation/network smoke probes. Relevant gates are:

1. game/test restore and Release build;
2. architecture boundary enforcement;
3. Domain/Application tests;
4. Debug build for Godot editor-side C# integration;
5. blocking Godot `--import` resource import;
6. committed localization-source validation;
7. actual headless main-scene runtime smoke test;
8. trade-panel interaction smoke test;
9. two-process LAN connectivity smoke test;
10. two-process durable negotiated trade smoke test.

CI rejects Godot references in Domain/Application, rejects `GameRules` access from standalone `Scripts/Presentation/**`, forbids `GameSession.Instance` throughout `Scripts/**`, rejects direct base/swim Voidling atlas paths from C# consumers and requires the canonical visual definition resource.

The active unified branch is included in the push trigger so exact-head CI can run independently of pull-request event delivery.

## Intentionally transitional code

These files are **not** examples for new feature architecture.

### `GameSession`

`Scripts/Services/GameSession*.cs` remains a Godot-owned compatibility/lifetime facade because legacy world/UI code already calls its public API. Most mutable gameplay operations delegate to Application use cases, but it still owns compatibility responsibilities such as save timing/event forwarding, initial demo state/spawn placement, presentation toast wording and compatibility seed/ID allocation.

It no longer provides global/static access. Do not add new feature rules here; new deterministic behavior belongs in Domain and player-action sequencing belongs in Application.

### `GameRules`

`Scripts/Core/GameRules.cs` is narrower. Player-facing stat labels/colors/rank names used by newly migrated UI have moved to Presentation catalogs/read models. It still exposes compatibility accessors for existing gameplay values/formulas and some legacy callers.

Do not add new deterministic formulas or presentation catalogs to `GameRules`. Move remaining responsibilities inward/outward as their callers migrate.

### `MainController`

The root object is primarily navigation, persistent garden HUD coordination, race handoff and a few legacy modal flows. Settings, Shop, Breeding, Race Picker, Inventory and Details no longer own their rendering inside the controller.

The selected-Voidling HUD still renders inside `MainController`, but its stat/DNA values now come from `VoidlingProfileProjectionService` rather than direct genetics/stat traversal. Family Tree uses the Application lineage projection. Goodbye/reset confirmations remain legacy controller-owned presentation and can be extracted when next substantially modified.

### Garden

`GardenController` is presentation-heavy but acceptable for the current demo. Keep lifecycle/genetics/breeding outcomes out of it. Camera, pickup/drop and breeding/hatching animation can become composed presentation components when those areas are next extended.

Its session dependency is explicit; there is no static service-locator access.

## Completed implementation-plan work packages

### WP0 — centralized Voidling visual pipeline

Implemented on `agent/implementation-plan`. Garden, remote Garden, race and portraits resolve through the canonical visual definition/factory. The static no-bypass CI guard remains; the extra dedicated visual smoke command was removed as unnecessary maintenance overhead.

### WP1 — genetics data model reconciliation

Implemented for confirmed v1 rules: exactly two visible ability-DNA profiles, parent-only ordinary inheritance, stable deterministic substreams and a rare breakthrough capped to one rank above parental potential. Deeper ancestry is not used to resurrect ordinary stat alleles.

### WP2 — pedigree and inbreeding correctness

Implemented with deterministic relationship fixtures, active burden/history separation, archive-safe lineage traversal and typed Application family-tree projections.

### WP3 — breeding outcome orchestration

Implemented with player-initiated eligibility validation, one-time frozen egg/genetic outcome creation, typed failures and rollback if persistence fails before success is presented.

### WP4 — hatching/lifecycle progression

Implemented/verified with explicit elapsed-duration progression, one-time deterministic state transitions, hatch-failure idempotence and regression coverage for different elapsed-time chunking. No force-hatch or unconfirmed trophy/reincarnation recipe was added.

### WP5 — race progression/balance extension

Implemented to the currently safe confirmed scope. `RaceCourseCatalog` owns authored course identity/data, `RaceEntry` freezes the selected definition, the player can choose the original or longer standard course, and deterministic replay/chunking coverage runs across authored courses. No personality modifier or presentation-owned outcome logic was introduced.

### WP6 — player information and UX projections

Implemented to the current confirmed scope. `VoidlingProfileProjectionService` supplies immutable player-information read models, UI distinguishes inherited potential from training, DNA profile ranks and lineage history are readable, and no exact offspring-genetics probability calculator was introduced.

### WP7 — production art ingestion

Not started as code work. WP0 has made this a content operation, but the actual production art revision should only be ingested when the intended source atlas/assets are supplied and this unified implementation branch is ready to merge/review. No consumer-specific texture override should be added.

## Next architecture migrations

These are incremental follow-ups, not reasons to rewrite stable demo code:

1. Extract goodbye/reset or other legacy controller-owned presentation when those flows are next substantially changed.
2. Move remaining presentation-only tint/formatting compatibility helpers out of `GameRules` as their related legacy callers migrate; do not duplicate domain identity/rules in Presentation.
3. Introduce additional Godot Resources only when a roadmap feature has concrete designer-authored content/tuning that consumes them.
4. Add ADRs only when a durable decision needs history/trade-off context; do not create ceremonial ADRs for obvious local refactors.
5. If multiple base Voidling visual families become a confirmed product feature, introduce a stable semantic visual-family ID and extend the existing visual catalog; do not key art selection to display names or persisted resource paths.

## Merge invariant

Architecture changes must not unintentionally alter persisted genetics, existing daily-race identity, multiplayer protocol or deterministic results for an unchanged course/ruleset. Before merge, the exact PR head must pass the full CI sequence above, and the PR description must reflect the actual remaining transitional boundaries.
