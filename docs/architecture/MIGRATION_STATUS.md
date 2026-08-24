# Architecture migration status

**Branch:** `architecture/futureproof-foundation`  
**Baseline:** post-demo `main`  
**Purpose:** current truth for what has actually moved behind the architecture boundaries.

`ARCHITECTURE.md` defines the durable target and rules. `RESTRUCTURING_PLAN.md` records the research and migration strategy. This file records implementation status so humans and coding agents do not need to infer it from old checklist boxes.

## Completed foundation

### Domain — pure C#

`Scripts/Domain/**` is Godot-free and CI-enforced.

Implemented seams:

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
- pure race performance formulas for ground/swim/glide, stamina, cheering, glide endurance and obstacles.

The old `GeneticsService` and `GameRules` remain compatibility facades for presentation code. New deterministic rules should go into Domain rather than expanding those facades.

### Application — Godot-free use cases

`Scripts/Application/**` is Godot-free and CI-enforced. It also may not reach back through `GameSession.Instance`.

Implemented use cases/services:

- `BreedVoidlingsUseCase`;
- `TrainingUseCase`;
- `ShopUseCase`;
- `AdvanceSimulationUseCase` for lifecycle/incubation/hatching;
- `SettingsUseCase`;
- `VoidlingRosterUseCase` for identity, world-position persistence, departure and failed-egg removal;
- `RaceParticipantSnapshotFactory`;
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

The Resource is loaded only by Bootstrap and converted before entering Application/Domain. Product invariants such as stable stat IDs, inbreeding tiers and fixed identity catalogs are intentionally not exposed as casual tuning knobs yet.

### Bootstrap

`Scripts/Bootstrap/GameBootstrap.cs` is the explicit composition root. It is the one place that intentionally sees concrete Infrastructure plus Application/Domain implementations.

No DI container has been introduced. Manual composition remains small and readable.

### Presentation reference architecture

`Scripts/Presentation/UI/Settings/SettingsScreen.cs` is the first standalone screen component:

- receives immutable/current UI state;
- renders Godot controls;
- emits player intent through C# events;
- does not know about `GameSession`, persistence or `AudioServer`.

Use it as the reference pattern when Shop, Breeding, Race Picker, Details and other screens are migrated out of `MainController`.

### Localization

Godot's native translation pipeline is registered through `Localization/strings.csv`.

Currently migrated representative UI:

- global top navigation/currency label;
- Settings screen and settings tooltips.

Use semantic keys (`UI_*`) for new player-facing presentation text. User-created Voidling names remain literal.

### Tests and CI

The architecture branch has fast xUnit coverage for genetics, inbreeding, mutations, migration, simulation, shop transactions, settings, roster operations and race performance.

CI currently performs:

1. game restore;
2. test restore;
3. Release build;
4. Domain/Application boundary enforcement;
5. domain/application tests;
6. Godot headless project/scene parse.

CI rejects `Godot` references in Domain/Application and rejects `GameSession.Instance` from those inward layers.

## Intentionally transitional code

These files are **not** examples for new feature architecture:

### `GameSession`

`Scripts/Services/GameSession*.cs` remains a Godot-owned compatibility/lifetime facade because existing presentation code already calls it. Most mutable gameplay operations now delegate to Application use cases, but it still owns compatibility responsibilities such as:

- static `Instance` access for legacy presentation;
- save timing/event forwarding;
- initial demo state/spawn placement;
- presentation toast wording;
- compatibility seed/ID allocation.

Do not add new feature rules here. New features should enter through Application/Domain and be adapted by the facade only while legacy presentation still requires it.

### `GameRules`

`Scripts/Core/GameRules.cs` still combines presentation labels/colors with compatibility accessors. Domain formulas and balance data have already moved inward.

Do not add new deterministic formulas to `GameRules`. New presentation catalogs can gradually replace its label/color responsibilities as screens migrate.

### `MainController`

The partial files improve navigation but the object is still a legacy UI coordinator. `SettingsScreen` demonstrates the target component pattern.

Migrate screens one at a time when they are next modified; do not perform a risky visual rewrite solely to eliminate partial classes.

### `RaceController`

This is the largest remaining architecture hotspot. The branch has extracted immutable entry snapshots and the pure numeric performance model, but `Scripts/Race/RaceController.cs` still owns the current frame-by-frame race state, CPU progression, finish ordering and Godot presentation together.

**Rule for incoming race features:** Power sections, route forks, shortcuts, personality decisions, deterministic replay, batch simulation and course graphs should be implemented in the planned pure `RaceSimulation` / course model first. Do not grow another large switch inside `RaceController`.

The eventual direction remains:

```text
Race entry snapshot(s)
  → pure RaceSimulation + authored course data
  → RaceState / RaceEvents
  → Godot RacePresentationController
```

The current extracted `RacePerformanceModel` is the compatibility seam for that migration; it is not intended to become a second competing simulator.

### Garden

`GardenController` is currently presentation-heavy but valid for the demo. Keep lifecycle/genetics/breeding outcomes out of it. Camera, pickup/drop and VFX can become composed presentation components when those areas are next extended.

## Rules for the next implementation-plan features

When adding a feature, choose the layer by responsibility:

- inherited trait, stat, lifecycle, race decision or deterministic rule → **Domain**;
- player action/use-case sequencing → **Application**;
- filesystem/audio/Steam/Godot Resource/platform integration → **Infrastructure**;
- input, camera, sprite, UI, VFX, animation → **Presentation**;
- concrete object graph wiring → **Bootstrap**.

Prefer extending an existing focused service before inventing another abstraction. Introduce a factory, builder, strategy or state machine only when creation/variation/transition complexity actually exists.

## Next architecture migrations

These are future slices, not blockers for ordinary non-race feature development:

1. Convert the next substantially modified modal (Shop or Breeding) to a standalone Presentation screen following `SettingsScreen`.
2. When race gameplay expands, create the pure deterministic `RaceSimulation` and make the Godot controller consume it rather than duplicating rules.
3. Move presentation-only stat labels/colors out of the `GameRules` compatibility facade as related screens migrate.
4. Replace feature-level `GameSession.Instance` access incrementally with explicit setup dependencies as scene/controllers are touched.
5. Add additional custom Godot Resources only for rules/content that designers actually need to author; do not pre-create every roadmap type.

## Merge invariant

This architecture work must not intentionally alter current demo gameplay or reroll existing persisted genetics. Before merge, the exact PR head must pass the full CI sequence above and the PR description must document any remaining transitional boundary explicitly.
