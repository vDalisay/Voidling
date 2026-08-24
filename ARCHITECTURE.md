# Voidling architecture

**Status:** active architecture for incremental feature development. Current implementation status is tracked in `docs/architecture/MIGRATION_STATUS.md`.

## Goals

Voidling is a simulation-heavy Godot game. Its long-lived complexity is expected to come from genetics, lifecycle, breeding, items, evolution, racing, persistence and content—not from rendering itself.

The architecture therefore optimizes for:

1. deterministic game rules that can run without Godot;
2. designer-editable Godot scenes/resources at the outer edge;
3. composition over game-specific inheritance;
4. stable save migrations;
5. small feature-oriented files that are easy for humans and coding agents to navigate;
6. fast tests for genetics/racing/lifecycle without launching a scene;
7. YAGNI: no framework or abstraction without a concrete problem.

## Dependency direction

```text
                         ┌───────────────────────────────┐
                         │ Bootstrap / composition root  │
                         └───────────────┬───────────────┘
                                         │ wires
                    ┌────────────────────┴────────────────────┐
                    │                                         │
          Presentation (Godot)                      Infrastructure (Godot)
     scenes, Nodes, UI, actors, VFX             persistence, audio, Resources
                    │                                         │
                    └────────────────┬────────────────────────┘
                                     ↓
                              Application
                       use cases / runtime orchestration
                                     ↓
                                  Domain
                   deterministic rules / simulation / models
```

Compile-time dependencies point inward. Domain knows nothing about Godot. Infrastructure implements narrow ports defined inward. `GameBootstrap` is the explicit composition root and selects concrete implementations.

We intentionally keep one Godot `.csproj`. Folder/namespace boundaries plus CI checks provide the architecture. A separate domain assembly is only justified if boundary violations or test/build performance become a demonstrated problem.

## Current folders and intended growth

```text
Scripts/
├─ Bootstrap/
│  └─ GameBootstrap.cs
├─ Domain/
│  ├─ Creatures/
│  ├─ Genetics/
│  ├─ Breeding/
│  ├─ Hatching/
│  ├─ Stats/
│  ├─ Racing/
│  ├─ Rules/
│  └─ Shared/
├─ Application/
│  ├─ Game/
│  ├─ Breeding/
│  ├─ Persistence/
│  ├─ Racing/
│  ├─ Roster/
│  ├─ Settings/
│  ├─ Shop/
│  ├─ Simulation/
│  ├─ Training/
│  └─ Ports/
├─ Infrastructure/
│  ├─ Persistence/
│  ├─ Audio/
│  └─ Resources/
├─ Presentation/
│  ├─ Racing/
│  ├─ Voidlings/
│  └─ UI/
├─ Actors/          # transitional garden presentation
├─ Garden/          # transitional garden coordinator
├─ Services/        # transitional GameSession lifetime facade
└─ UI/              # transitional root/navigation UI

Resources/
└─ Balance/

Localization/
└─ en.po

Tests/
├─ Domain/
└─ Application/
```

Do not create empty folders or placeholder interfaces just to match a future ideal tree. A directory appears when its first real feature needs it.

## Layer responsibilities

### Domain

Pure C# model and game rules.

Good examples:

- stable deterministic RNG/substream derivation;
- genome inheritance and phenotype expression;
- relatedness/inbreeding calculation;
- stat progression;
- lifecycle transitions;
- race participant snapshots, course data and race simulation;
- typed rule/config records consumed by those systems.

Forbidden:

- `using Godot;`
- Nodes, Resources or scene paths;
- `FileAccess`, `AudioServer`, `TranslationServer`;
- UI strings/toasts;
- static mutable singleton state;
- wall-clock/frame time as a hidden deterministic input.

### Application

Coordinates player/game use cases through explicit inputs and focused collaborators.

Examples:

- buy/use a training item;
- breed two selected Voidlings;
- advance incubation/lifecycle by a supplied duration;
- say goodbye/rename/move;
- create an immutable race entry and award its result;
- change settings;
- normalize an older save after deserialization.

Application returns typed results/events. Presentation decides how those results are worded and displayed. Application must remain Godot-free.

### Infrastructure

Godot/platform implementation details:

- JSON persistence via `Godot.FileAccess`;
- audio bus application;
- loading/validating custom Resources;
- future Steam/platform services.

Infrastructure does not contain genetics, breeding or race outcome rules.

### Presentation

Godot Nodes/scenes:

- world actors and animation;
- input, camera and drag behavior;
- screen/modal composition;
- race rendering/interpolation;
- VFX and audio triggers;
- localization presentation.

Presentation may map immutable/application state to colors/sprites/text, but it must not invent domain outcomes.

### Bootstrap

The one layer that intentionally sees concrete types from every layer. `Scripts/Bootstrap/GameBootstrap.cs` constructs shared services and the configured `GameSession` lifetime facade.

Voidling uses **manual composition**, not a dependency-injection container. The object graph remains small and Godot already owns Node lifetime. Revisit only if manual wiring becomes genuinely difficult.

## Composition over inheritance

Engine inheritance is normal and useful (`Node2D`, `Control`, `Resource`). Game behavior should not form deep class trees.

Prefer composed presentation responsibilities and shared helpers/components over:

```text
BaseCreature
  → InteractiveCreature
    → MutatedInteractiveCreature
      → AngelMutatedInteractiveCreature
```

A current example is `VoidlingGroundVisualMetrics`: the garden actor and race presentation use one shared definition for sprite-ground pivot and shadow proportions rather than tuning separate challenge-specific footprints.

Plain C# behaviors can also be composed into a Node when they do not need scene-tree lifecycle.

## Pattern policy

Patterns are vocabulary, not goals.

### Factory

Use when creation itself has rules or invariants. Existing examples include:

- `GenomeFactory`;
- `StoreEggFactory`;
- `RaceEntryFactory` / `RaceParticipantSnapshotFactory`.

Do not wrap `new Foo()` merely to claim a factory exists.

### Builder

Use only when construction has many optional/ordered pieces, for example a future authored race-course graph or complex test scenarios. Do not add builders for ordinary DTOs.

### Strategy / policy

Use where product rules intentionally vary, such as future allele-expression policies, mutation policies or race segment behavior. If there is one stable rule, keep it direct until variation is real.

### State

Use enums and straightforward transition logic for simple lifecycle states. Extract a state machine only when transition behavior becomes hard to reason about.

### Observer / signals

Godot signals and C# events are appropriate for local notifications. Keep ownership visible. Avoid a global message bus because it obscures dependencies.

Nodes being removed from inside their own/descendant signal callbacks must use deferred disposal (`QueueFree`) rather than synchronous `Free`.

### Components

Use scene/node components for reusable presentation behavior. Do not introduce a parallel ECS; Godot's node tree already provides composition and the project does not currently have an ECS-scale performance requirement.

## Domain events vs UI events

Domain/application operations can produce small typed result events, for example:

```text
CreatureHatched
CreatureBecameAdult
EggFailed
RaceParticipantFinished
```

These are data describing what happened, not a global pub/sub framework. UI signals such as `Pressed`, `PairChanged` and `RaceRequested` remain local Presentation concerns.

## Rules and designer data

Balance is represented as immutable typed records such as:

```text
GeneticsRules
BreedingRules
HatchingRules
StatGrowthRules
LifecycleRules
RaceRules
ShopRules
```

Godot custom `Resource` assets are the editor-facing authoring format where designer editing is genuinely needed. Bootstrap validates/converts them to plain domain rules once. Do not expose an Inspector field until gameplay actually consumes that authored value; otherwise two sources of truth are created.

Presentation-only values (colors, fonts, sprite paths) belong to Presentation catalogs/resources, not Domain rule objects.

## Persistence

Persistence is an external boundary, not part of simulation.

Application defines the narrow repository port:

```csharp
public interface IGameStateRepository
{
    GameStateData? Load();
    void Save(GameStateData state);
}
```

Godot Infrastructure implements it with `FileAccess` and JSON.

Migration flow:

```text
serialized save
  → deserialize tolerant DTO
  → deterministic migration/normalization
  → current runtime model
```

Rules:

- preserve `user://voidling_mvp_save.json` during this restructuring;
- never reroll existing genomes/eggs as a migration side effect;
- use deterministic defaults for newly introduced data;
- preserve lineage and stable IDs;
- prefer unknown-content preservation over silent deletion when practical.

## Determinism

Persisted or replay-sensitive random decisions use explicit stable seeds and semantic substreams.

Never use the following as hidden inputs to persisted outcomes:

- `.GetHashCode()`;
- frame rate;
- animation state;
- wall-clock time;
- presentation/VFX RNG;
- accidental global sequential RNG consumption.

The live demo race now follows:

```text
immutable RaceEntry
  → pure RaceCourse + RaceSimulation
  → state snapshots / typed events
  → RaceScreen
  → sprites / camera / HUD / VFX
```

`RaceSimulation` is fixed-step and result-authoritative. `RaceScreen` uses separate VFX randomness, so particles/animation cannot change finish order.

## Localization

Localization is a foundation concern because the retro pixel UI has tight layout constraints.

Policy:

- stable semantic keys such as `UI_SHOP_TITLE`, not English prose as identifiers;
- Godot's built-in translation pipeline and `TranslationServer`;
- committed UTF-8 gettext `.po` files as locale source (`Localization/en.po` today);
- register source translation files directly in `project.godot`, never importer-generated `.translation` artifacts;
- use Containers, wrapping and sensible minimum sizes;
- pseudolocalization is required UI QA before release;
- user-generated Voidling names are literal and must not auto-translate;
- formatted messages use placeholders rather than assembling translated sentence fragments.

A generated CSV `.translation` file was intentionally abandoned because a clean checkout can load project settings before that generated artifact exists, producing missing-resource errors. The committed PO source has no such bootstrap dependency.

Do not build a second localization framework over Godot.

## UI architecture

`MainController` is now primarily a transitional navigation/root coordinator. Standalone screens currently include Settings, Shop, Breeding, Race Picker, Inventory and Details, with `ModalHost` owning modal lifetime.

Target direction as remaining areas are next modified:

```text
MainController / ModalHost
├─ TopBar
├─ VoidlingInspector
├─ ShopScreen
├─ InventoryScreen
├─ BreedingScreen
├─ RacePickerScreen
├─ DetailsScreen
├─ FamilyTreeScreen
└─ SettingsScreen
```

Standalone screens receive presentation-ready state and emit intent; they do not reach through a global session locator. `GameSession.Instance` has been removed and CI forbids its return.

`UiFactory` should remain reusable styling/widget construction. It must not become another controller or service locator.

## Garden architecture

`GardenController` coordinates garden presentation:

- camera navigation;
- creature visual synchronization;
- pickup/drop interaction;
- breeding/hatching presentation;
- environment/TileMap ownership.

Domain age/incubation/breeding outcomes do not live in the Garden node. Shared Voidling visual conventions such as ground pivot/shadow metrics belong in reusable Presentation code so challenges and the garden remain visually consistent.

## Race architecture

The old result-owning `RaceController` has been removed. The current architecture is:

```text
RaceEntryFactory
  → immutable RaceEntry / RaceParticipantSnapshot[]
  → RaceSimulation (pure C#)
      → RaceParticipantStateSnapshot / RaceEvents
          → RaceScreen (Godot)
              → sprites / camera / HUD / minimap / podium / VFX
```

Course geometry is represented by pure `RaceCourse`/segment data. Adding Power sections, forks, shortcuts or personality decisions should extend the pure course/simulation model first, then map resulting state/events in Presentation.

## Testing strategy

Three categories:

1. **Domain tests** — deterministic genetics, inbreeding, lifecycle/stat/race logic.
2. **Application tests** — use-case sequencing and aggregate mutation without scenes.
3. **Godot integration checks** — clean import plus actual main-scene headless runtime smoke.

Characterization tests protect existing demo behavior during migration.

High-value invariants include:

- one allele inherited from each parent;
- same seed + same inputs = same genome;
- adding unrelated content does not change old deterministic decisions;
- mutation transmission uses configured probability and depth;
- inbreeding burden escalation/cleansing;
- save/load does not reroll eggs;
- race simulation determinism and frame-chunk independence;
- presentation/VFX changes do not change race results.

CI also enforces the principal compile-time architecture boundaries and rejects runtime Godot errors during the smoke launch.

## Current migration status

`docs/architecture/MIGRATION_STATUS.md` is the current implementation map. `docs/architecture/RESTRUCTURING_PLAN.md` preserves the research/migration reasoning, while ADRs record durable decisions and later corrections.

Do not infer current implementation state from an older execution-plan checklist when `MIGRATION_STATUS.md` says otherwise.
