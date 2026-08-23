# Voidling architecture

**Status:** target architecture for incremental migration. Current gameplay remains the compatibility baseline while legacy controllers are moved behind these boundaries.

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

Compile-time dependencies point inward. The Domain knows nothing about Godot. Infrastructure implements narrow ports defined by Application/Domain. The composition root selects concrete implementations.

We intentionally keep one Godot `.csproj` during this migration. Folder/namespace boundaries provide the architecture first. A separate domain assembly is only justified if boundary violations or test/build performance become a real problem.

## Planned folders and namespaces

```text
Scripts/
├─ Bootstrap/
│  └─ GameCompositionRoot.cs
├─ Domain/
│  ├─ Creatures/
│  ├─ Genetics/
│  ├─ Breeding/
│  ├─ Hatching/
│  ├─ Stats/
│  ├─ Evolution/
│  ├─ Lifecycle/
│  ├─ Racing/
│  ├─ Inventory/
│  └─ Shared/
├─ Application/
│  ├─ Game/
│  ├─ Breeding/
│  ├─ Hatching/
│  ├─ Racing/
│  ├─ Shop/
│  ├─ Settings/
│  └─ Ports/
├─ Infrastructure/
│  ├─ Persistence/
│  ├─ Audio/
│  ├─ Localization/
│  └─ Resources/
├─ Presentation/
│  ├─ Garden/
│  ├─ Voidlings/
│  ├─ Racing/
│  ├─ FamilyTree/
│  └─ UI/
└─ Legacy/ (temporary only if a move cannot be completed safely in one slice)

Resources/
├─ Genetics/
├─ Growth/
├─ Breeding/
├─ Hatching/
├─ Lifecycle/
├─ Racing/
└─ Courses/

Localization/
└─ strings.csv

Tests/
├─ Domain/
├─ Application/
└─ Characterization/
```

Do not create empty folders or placeholder interfaces just to match this tree. A directory appears when its first real feature is migrated.

## Layer responsibilities

### Domain

Pure C# model and game rules.

Good examples:

- stable deterministic RNG/substream derivation;
- genome inheritance and phenotype expression;
- relatedness/inbreeding calculation;
- stat progression;
- lifecycle transitions;
- race participant snapshots and race simulation;
- typed rule/config records consumed by those systems.

Forbidden:

- `using Godot;`
- Nodes, Resources or scene paths;
- `FileAccess`, `AudioServer`, `TranslationServer`;
- UI strings/toasts;
- static mutable singleton state;
- reading wall-clock time as an implicit dependency.

### Application

Coordinates a user/game use case. It may mutate the current save/runtime model through explicit collaborators.

Examples:

- buy a training item;
- breed two selected Voidlings;
- advance incubation/lifecycle by a supplied simulation duration;
- say goodbye;
- register a race and award its result;
- change settings.

Application code returns typed results/events. Presentation decides how those results are worded and displayed.

### Infrastructure

Godot/platform implementation details:

- JSON persistence via `Godot.FileAccess`;
- audio bus application;
- loading/validating custom Resources;
- localization locale switching;
- future Steam/platform services.

Infrastructure does not contain breeding/racing rules.

### Presentation

Godot Nodes/scenes:

- world actors and animation;
- input, camera and drag behavior;
- screen/modal composition;
- race rendering/interpolation;
- VFX and audio triggers;
- localization presentation.

Presentation may map domain state to colors/sprites/text, but it must not invent domain outcomes.

### Bootstrap

The only layer that intentionally sees all concrete layers. It constructs shared services and injects them into the root scene/controllers.

Voidling will use **manual composition first**, not a dependency-injection container. The object graph is currently small and Godot already owns much of the Node lifecycle. If manual wiring becomes genuinely complex later, the decision can be revisited with an ADR.

## Composition over inheritance

Engine inheritance is unavoidable and useful (`Node2D`, `Control`, `Resource`). Game behavior should not form deep class trees.

Prefer:

```text
VoidlingActor
├─ AnimatedSprite2D
├─ SelectionIndicator
├─ ShadowRenderer
├─ MutationAdornmentRenderer
└─ InteractionArea
```

over:

```text
BaseCreature
  → InteractiveCreature
    → MutatedInteractiveCreature
      → AngelMutatedInteractiveCreature
```

Plain C# behaviors can also be composed into a Node when they do not need scene-tree lifecycle.

## Pattern policy

Patterns are vocabulary, not goals.

### Factory

Use when creation itself has rules or multiple collaborators. Planned examples:

- `GenomeFactory` / `StoreEggFactory`;
- `BredEggFactory` or an `EggGenerationService`;
- `RaceParticipantFactory`;
- visual actor factory when spawning requires consistent components.

A factory must own meaningful creation invariants. Do not wrap `new Foo()` merely to claim a factory exists.

### Builder

Use only when construction has many optional/ordered pieces:

- authored race course graph;
- debug/test genome scenarios;
- complex result/pipeline accumulation.

Do not add builders for ordinary save DTOs.

### Strategy / policy

Use where product rules intentionally vary:

- allele expression policy;
- mutation policy;
- race segment behavior;
- ruleset profiles.

If there is one stable rule, keep it direct until a second implementation exists or is already committed in the product plan.

### State

Use explicit enums and switch logic for simple lifecycle states. Extract a state machine only when transitions/behavior become difficult to reason about. Avoid a node-per-state hierarchy by default.

### Observer / signals

Godot signals and C# events are appropriate for local notifications. Keep ownership visible. Avoid a global message bus because it makes dependencies difficult to trace.

### Components

Use scene/node components for reusable presentation behaviors. Do not introduce a parallel ECS: Godot's node tree already provides compositional structure, and Voidling does not currently have an ECS-scale performance problem.

## Domain events vs UI events

Domain/application operations can produce small typed result events, for example:

```text
EggCreated
CreatureHatched
CreatureBecameAdult
TrainingApplied
RaceCompleted
```

These are data describing what happened. They are not a global publish/subscribe framework. The application owner forwards relevant events to presentation.

UI signals such as `Pressed`, `MemberSelected` and `DragStarted` remain local presentation concerns.

## Rules and designer data

Balance values currently concentrated in `GameRules` will migrate into typed rule records such as:

```text
GeneticsRules
BreedingRules
HatchingRules
StatGrowthRules
LifecycleRules
RaceRules
ShopRules
```

Godot custom `Resource` assets are the editor-facing authoring format. At startup they are validated and converted into immutable/plain C# rule values used by Domain. This keeps the Inspector useful without making domain algorithms depend on `Resource`.

Presentation-only values (colors, fonts, sprite paths) remain presentation catalogs/resources and are not mixed into domain rule objects.

## Persistence

Persistence is a boundary, not part of the simulation.

Application depends on:

```csharp
public interface IGameStateRepository
{
    GameStateData? Load();
    void Save(GameStateData state);
}
```

Godot infrastructure implements it with `FileAccess` and JSON.

Migration is explicit:

```text
serialized save
  → deserialize tolerant DTO
  → ordered migration pipeline
  → normalize/validate
  → current runtime model
```

Rules:

- preserve the existing `user://voidling_mvp_save.json` path during restructuring;
- never reroll existing genomes/eggs as a side effect of migration;
- deterministic defaults for newly introduced data;
- preserve lineage and stable IDs;
- future unknown-content preservation should be preferred over silent deletion when practical.

## Determinism

Persisted or replay-sensitive random decisions use explicit stable seeds and named substreams.

Never use:

- `.GetHashCode()`;
- frame rate;
- current animation state;
- current wall-clock time;
- global sequential RNG consumption

as hidden inputs to a persisted deterministic result.

Race simulation eventually follows the same rule: simulation state and seed decide results; presentation only displays/interpolates them.

## Localization

Localization is a foundation concern because retro pixel UI has tight layout constraints.

Initial policy:

- stable semantic keys such as `UI_SHOP_TITLE`, not English prose as identifiers;
- Godot's built-in translation pipeline and `TranslationServer`;
- start with UTF-8 CSV while the string set is small;
- switch to/gettext PO when collaboration tooling or string volume makes it worthwhile;
- use Containers, wrapping and sensible minimum sizes;
- pseudolocalization is a required UI QA mode before release;
- user-generated Voidling names are literal and must not auto-translate;
- formatted messages keep values as placeholders rather than string-concatenating sentence fragments.

Do not build a second custom localization framework over Godot.

## UI architecture

The current `MainController` partial class is a transitional monolith. Target shape:

```text
MainScreen / ModalHost
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

Each screen owns its controls and emits user intent. It receives a narrow application facade/use case rather than reaching into `GameSession.Instance`.

`UiFactory` should shrink toward reusable styling/widget construction. It must not become another controller or service locator.

## Garden architecture

`GardenController` should become the coordinator for a garden scene, with composed responsibilities for:

- camera navigation;
- creature spawn/visual synchronization;
- pickup/drop interaction;
- breeding/hatching presentation;
- environment/TileMap ownership.

Domain age/incubation/breeding state does not live in the Garden node.

## Race architecture

The current `RaceController` is explicitly temporary because it mixes simulation and presentation.

Target:

```text
RaceEntryUseCase
  → RaceParticipantSnapshot[]
  → RaceSimulation (pure C#)
      → RaceState / RaceEvents
          → RacePresentationController (Godot)
              → sprites / camera / HUD / VFX
```

Course definitions are data-driven segments. Adding Power sections, route forks or new challenges should add domain course behavior and presentation mappings without expanding one giant controller switch indefinitely.

## Testing strategy

Three categories:

1. **Domain tests** — fast deterministic tests for genetics, inbreeding, lifecycle, stats and race simulation.
2. **Application tests** — use-case sequencing with in-memory repositories/adapters.
3. **Godot smoke/scene tests** — project loads, scenes parse, critical presentation integration.

Characterization tests protect existing demo behavior during refactors before rules are moved.

High-value invariants include:

- one allele inherited from each parent;
- same seed + same inputs = same genome;
- adding an unrelated gene does not change old gene outcomes;
- mutation transmission uses configured probability and depth;
- inbreeding burden escalation/cleansing;
- save/load does not reroll eggs;
- race sim determinism;
- animation changes do not change race results.

## Current migration status

See `docs/architecture/RESTRUCTURING_PLAN.md`. Until a subsystem is migrated, current `Scripts/Core`, `Scripts/Services`, `Scripts/UI`, `Scripts/Garden` and `Scripts/Race` code is compatibility code. New features should prefer the target layers rather than increasing those monoliths.
