# Voidling agent guide

This file is the repository map for coding agents. Keep it short. Detailed architecture and product rules live in the linked documents.

## Start here

- Architecture: `ARCHITECTURE.md`
- Current architecture migration status: `docs/architecture/MIGRATION_STATUS.md`
- Architecture research/restructuring plan: `docs/architecture/RESTRUCTURING_PLAN.md`
- Architecture decisions: `docs/architecture/decisions/`
- Product implementation plan: `docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md`
- Demo behavior: `docs/DEMO_MVP.md`

## Build and verify

Windows local workflow:

```bat
build.bat
playgame.bat
```

Portable compile check:

```sh
dotnet restore Voidling.csproj
dotnet build Voidling.csproj --no-restore
```

Before finishing a code change, run the same Godot/.NET checks as `.github/workflows/ci.yml`. If tests exist for the touched subsystem, run them too.

## Architecture boundaries

Dependencies point inward:

```text
Presentation (Godot Nodes / scenes)
        ↓
Application (use cases / orchestration)
        ↓
Domain (game rules / deterministic simulation)
        ↑
Infrastructure (Godot filesystem, audio, Resources, localization adapters)
```

`Bootstrap` is the composition root and is the only place allowed to know concrete implementations from every layer.

### Domain

- `Scripts/Domain/**` must not reference `Godot`.
- No scene nodes, file I/O, audio, localization, UI strings, or static mutable global state.
- Deterministic systems take seeds/inputs explicitly.
- Prefer small stateless services and immutable inputs/results.
- Preserve stable IDs used by saves.

### Application

- `Scripts/Application/**` must not reference `Godot`.
- Coordinates domain services and repository ports.
- Owns use-case sequencing, not rendering.
- Infrastructure is accessed through narrow interfaces defined inward of the implementation.
- Do not use `GameSession.Instance` or other service-locator patterns.

### Presentation

- Godot Nodes render state, collect input, and emit intent.
- Do not put breeding/genetics/race-balance rules in UI or actors.
- Prefer scene/node composition over inheritance hierarchies.
- Use signals/events for local scene reactions; avoid a global event bus.
- Player-facing text must use localization keys unless it is user-generated content.
- `Scripts/Presentation/UI/Settings/SettingsScreen.cs` is the current reference screen pattern.

### Infrastructure

- Contains Godot-specific adapters: `FileAccess`, `AudioServer`, Resources, platform APIs.
- Serialization migrations must preserve existing saves.
- Never let infrastructure types leak into pure domain APIs.
- Designer-authored balance Resources are converted to immutable domain rules in Bootstrap.

## Pattern policy

Use a pattern only when the problem exists:

- Factory: object creation with invariants, seeded generation, or multiple implementations.
- Builder: complex optional/ordered construction (for example authored race courses or tests), not trivial DTOs.
- Strategy/policy: genuinely variable rules such as gene expression or race segments.
- State machine: lifecycle/action flows with meaningful mutually exclusive states; prefer a simple enum/switch until complexity warrants more.
- Observer/signals: events with one-to-many local reactions.
- Components: reusable Godot behaviors attached to scenes.

Avoid speculative abstractions, `BaseManager` hierarchies, generic repositories for everything, global service locators, and replacing Godot's node tree with an ECS.

## C# conventions

- Nullable reference types stay enabled.
- PascalCase: types, methods, properties, public members.
- camelCase: parameters and locals.
- `_camelCase`: private fields.
- One primary responsibility per type.
- Prefer explicit domain names over abbreviations.
- Keep pure logic easy to instantiate in tests.
- Do not use `.GetHashCode()` for persisted determinism.

## Godot conventions

- Engine inheritance is fine (`Node`, `Control`, `Resource`); game behavior should be composed beneath it.
- Configure nodes before `AddChild` when possible.
- Use custom `Resource` assets for designer-authored data, then validate/convert them to plain domain rules.
- Autoloads are only for genuinely game-wide lifetime owners; do not make them service locators.
- User-created names/text must not be auto-translated.

## Save compatibility

Existing demo saves are user data. Any schema change must:

1. increment/track a schema version when needed;
2. migrate deterministically;
3. preserve IDs, lineage, genomes, mutations, inventory and settings;
4. never reroll existing eggs/genes because unrelated content was added.

## Change discipline

- Preserve current gameplay while restructuring unless the task explicitly changes behavior.
- Prefer small, compilable migrations over a big-bang rewrite.
- Add characterization tests before moving fragile deterministic rules.
- Update `ARCHITECTURE.md` or an ADR when a dependency direction or durable design rule changes.
- Update `docs/architecture/MIGRATION_STATUS.md` when a subsystem crosses a boundary or a new transitional hotspot is introduced.
- Do not duplicate architecture rules across many docs; link to the source of truth.
