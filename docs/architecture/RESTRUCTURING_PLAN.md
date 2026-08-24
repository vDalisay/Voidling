# Futureproof foundation restructuring plan

**Branch:** `architecture/futureproof-foundation`  
**Baseline:** `main` after the demo MVP merge (`27cee95c…`)  
**Primary constraint:** restructure without intentionally changing the current playable demo.

## Executive summary

The current demo proved the core product loop quickly, but several classes now combine responsibilities that will expand sharply when the implementation plan adds full genetics, personality, lifecycle, evolution, data-driven race courses, persistence migrations and more UI.

The correct next move is **not** to add every design pattern. It is to establish dependency direction, pull deterministic rules out of Godot controllers, make persistence/configuration explicit, and give future contributors/AI agents a reliable map.

Target principle:

> **Functional/deterministic core, Godot presentation shell, explicit application use cases, thin infrastructure adapters.**

The migration is incremental. Existing public behavior and save data are the compatibility contract.

---

## 1. Research conclusions

### 1.1 .NET / C# architecture

Microsoft's current dependency-injection guidance recommends avoiding stateful static/global services, avoiding direct creation of dependencies inside services, keeping services small and testable, and avoiding the service-locator pattern. It also notes that a class requiring many dependencies can indicate too many responsibilities.

Sources:

- https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines
- https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures

Clean Architecture's useful idea for Voidling is **dependency direction**, not enterprise layering ceremony: core business/game logic does not depend on UI/filesystem/framework implementation details; outer adapters depend inward.

Decision for this project:

- use explicit dependency injection and a composition root;
- **do not add Microsoft.Extensions.DependencyInjection yet**;
- manual construction is clearer for the present object graph and fits Godot's scene lifecycle;
- revisit a container only if wiring complexity becomes measurable.

### 1.2 Godot architecture

Godot's own best-practices documentation emphasizes single responsibility and encapsulation, and specifically warns that broad autoload/global manager patterns can make state and bug origins difficult to trace. Godot recommends scenes/nodes/resources/signals as its native composition tools.

Sources:

- https://docs.godotengine.org/en/4.6/tutorials/best_practices/introduction_best_practices.html
- https://docs.godotengine.org/en/4.6/tutorials/best_practices/autoloads_versus_regular_nodes.html
- https://docs.godotengine.org/en/4.6/tutorials/best_practices/what_are_godot_classes.html
- https://docs.godotengine.org/en/4.6/tutorials/scripting/resources.html

Community Godot guidance reaches the same practical conclusion: Godot's node tree already supports composition, signals provide Observer-like communication, and replacing it with a full ECS adds complexity unless scale/performance justifies it.

References:

- https://www.gdquest.com/tutorial/godot/design-patterns/intro-to-design-patterns/
- https://www.gdquest.com/tutorial/godot/design-patterns/entity-component-pattern/

Decision:

- compose presentation features with Nodes/scenes and plain C# collaborators;
- no deep game-specific inheritance trees;
- no ECS;
- no global event bus;
- signals/events stay owned by a scene/feature;
- custom Resources are designer-facing configuration, then converted to validated plain domain rules.

### 1.3 Pattern selection and YAGNI

Patterns selected because the roadmap already creates their problem:

| Pattern | Concrete Voidling use | Introduce now? |
|---|---|---|
| Composition | actor visual/interaction behaviors, screens, domain services | Yes |
| Factory | seeded genome/egg/race participant creation | Yes |
| Strategy / policy | gene expression, mutation, race segment rules | Foundation only; expand when variants exist |
| Builder | future race course graph, complex test fixtures | Only when first complex course graph arrives |
| State enum / FSM | lifecycle, egg, race action states | Enum/simple transition service now; node FSM only if needed |
| Observer/signals | local UI/game-world reactions | Yes, scoped |
| Repository/port | save state boundary | Yes, one concrete repository |
| Adapter | Godot filesystem/audio/Resource APIs | Yes |
| Command | replayable player commands/undo | No current requirement |
| ECS | massive entity throughput | No |
| Generic event bus | cross-project decoupling | No; dependency tracing cost is worse |
| IoC container | large object graph/lifetimes | No, manual composition first |

GDQuest also notes that FSMs solve real complexity but add code/fragmentation; use them when a simple enum/switch stops being clear rather than preemptively making every behavior a State object:
https://www.gdquest.com/tutorial/godot/design-patterns/finite-state-machine/

### 1.4 Localization

Godot 4.6 already supplies translation import, runtime locale changes, pseudolocalization, text direction support and UI translation behavior. Building a custom localization framework would duplicate engine functionality.

Sources:

- https://docs.godotengine.org/en/4.6/tutorials/i18n/internationalizing_games.html
- https://docs.godotengine.org/en/4.6/tutorials/i18n/localization_using_spreadsheets.html
- https://docs.godotengine.org/en/4.6/tutorials/i18n/localization_using_gettext.html

Decision:

- add localization infrastructure now;
- stable semantic message keys;
- start with UTF-8 CSV while the text corpus is small;
- keep UI flexible under pseudolocalization;
- user-created names are never translation keys;
- move to PO/gettext if translator collaboration/string volume later warrants it.

### 1.5 Agent-friendly repository guidance

OpenAI's 2026 harness-engineering guidance recommends a short `AGENTS.md` as a map and structured repository documentation as the source of truth rather than a giant instruction manual:
https://openai.com/index/harness-engineering/

Anthropic's current Claude Code documentation recommends concise project `CLAUDE.md` files (target under ~200 lines), supports `.claude/rules/` path-scoped instructions, and explicitly documents importing an existing `AGENTS.md` from `CLAUDE.md` rather than duplicating it:
https://code.claude.com/docs/en/memory

Decision:

- `AGENTS.md` = short cross-agent repository map;
- `CLAUDE.md` imports it;
- path-scoped Claude rules contain only boundary-specific reminders;
- `ARCHITECTURE.md`, ADRs and this plan remain the durable sources of truth.

---

## 2. Current-code review

### 2.1 What is already good

The MVP has several foundations worth preserving:

- deterministic salted RNG helpers rather than `.GetHashCode()`;
- persisted egg genetics and lineage IDs;
- existing save-version normalization;
- feature-oriented top-level script directories;
- partial extraction of visual mutation adornments;
- CI that builds C# and parses Godot scenes headlessly;
- editable TileMap/Resources rather than flattened content;
- product implementation plan already calls for pure C# racing and data-driven rules.

This refactor should preserve those strengths rather than rewrite for novelty.

### 2.2 `GameSession` — highest application/infrastructure hotspot

Current responsibilities include:

- Godot autoload lifecycle;
- a static `Instance` service-locator entry point;
- mutable runtime/save state;
- FileAccess/JSON persistence;
- save migration/normalization;
- simulation ticking;
- creature aging;
- egg incubation/hatching;
- shop transactions;
- training;
- breeding validation and execution;
- lineage queries;
- race reward/seed issuance;
- settings persistence;
- audio bus changes;
- starter/store-egg factories;
- UI toast wording.

Adding needs, personality, evolution, reincarnation, store restocks, race history and new item effects here would make the class the de facto whole game.

Migration target:

```text
GameSession (temporary Godot façade)
  ↓
GameApplication / use cases
  ├─ BreedingUseCase
  ├─ ShopUseCase
  ├─ TrainingUseCase
  ├─ LifecycleSimulation
  ├─ HatchingUseCase
  └─ RaceRewardUseCase
  ↓
Domain services

IGameStateRepository ← GodotJsonGameStateRepository
IAudioSettingsAdapter ← GodotAudioSettingsAdapter
```

`GameSession` may remain an autoload as a broad-scope **lifetime owner/composition bridge**, but feature code should stop treating `GameSession.Instance` as a global service locator.

### 2.3 `GameRules` — mixed domain and presentation concerns

It currently combines:

- gameplay balance numbers;
- stat identity strings;
- Godot `Color` values;
- gene/stat access helpers;
- stat progression formulas;
- mutation lookup;
- tint conversion.

This prevents domain code from becoming Godot-free.

Target:

```text
Domain rules:
- GeneticsRules
- BreedingRules
- HatchingRules
- StatGrowthRules
- LifecycleRules
- RaceRules
- ShopRules

Presentation:
- StatPresentationCatalog (labels/colors)
- PalettePresentationCatalog
```

`GameRules` can temporarily remain as a compatibility facade while callers migrate.

### 2.4 `GeneticsService` — good algorithms, too many concepts

The implementation is already mostly pure C#, but one static class owns:

- random founder generation;
- child inheritance;
- expression;
- appearance color inheritance;
- rare mutation inheritance;
- relationship traversal;
- inbreeding burden;
- hatch viability;
- stable random derivation.

These concepts are independently expected to grow in the roadmap.

Target stateless collaborators:

- `StableRandom` / deterministic seed derivation;
- `GenomeFactory`;
- `GenomeInheritanceService`;
- `PhenotypeExpressionService` later;
- `RareTraitInheritanceService`;
- `RelationshipService`;
- `InbreedingBurdenService`;
- `HatchViabilityService`.

Keep a compatibility `GeneticsService` during migration so behavior can be characterized and moved one concern at a time.

### 2.5 `RaceController` — highest domain/presentation hotspot

The ~34 KB controller currently owns:

- participants/CPU generation;
- deterministic random state;
- movement formulas;
- run/swim/fly segment rules;
- stamina and cheer;
- obstacle outcomes;
- finish ordering/auto-finish;
- animation selection;
- sprite placement/elevation;
- camera;
- HUD/minimap;
- course rendering;
- results/podium.

This directly blocks the roadmap goal of headless deterministic racing and future Power/route/shortcut segments.

Target split:

```text
RaceParticipantFactory
  ↓
RaceParticipantSnapshot[]
  ↓
RaceSimulation (pure C#)
  ├─ course segment policies
  ├─ stamina/boost strategy
  ├─ deterministic obstacle events
  └─ finish order
  ↓
RaceState + RaceEvents
  ↓
RacePresentationController (Godot)
  ├─ sprites/animations
  ├─ camera
  ├─ HUD/minimap
  ├─ VFX
  └─ podium
```

This is a key deliverable of the architecture branch, but it should be migrated behind characterization tests rather than rewritten blindly.

### 2.6 `MainController` partials — file split without object split

Partial classes improve file length but do not reduce coupling. `MainController` still owns every modal/screen and reaches through the global session.

Target UI composition:

- `ModalHost` owns overlay/window lifetime;
- `TopBarView` emits navigation intent;
- each modal becomes a dedicated screen/control or presenter;
- screens receive narrow application operations/state;
- `MainController` becomes navigation/root coordination.

Migration can happen screen-by-screen. No need to convert every panel in one commit.

### 2.7 `GardenController`

The garden is currently responsible for many valid presentation tasks, but camera, spawn synchronization, pickup/drop and breeding/hatching animation can become composed controllers/components as they grow.

Immediate architecture goal is narrower: ensure no additional domain progression logic enters the Garden controller.

### 2.8 persistence

Existing JSON persistence is adequate for the prototype, but migration logic is embedded inside `GameSession` and there is no boundary to test it independently.

Target:

- `IGameStateRepository`;
- `GodotJsonGameStateRepository` preserving the current save path;
- ordered migration pipeline;
- normalization/validation independent of UI;
- future save DTO/domain mapping can evolve without scene coupling.

---

## 3. Proposed dependency-safe migration

### Phase A — repository map and guardrails

Deliverables:

- [x] `AGENTS.md`
- [x] `CLAUDE.md`
- [x] `.claude/rules/`
- [x] `ARCHITECTURE.md`
- [x] this research/restructuring plan
- [ ] ADRs
- [ ] expanded `.editorconfig`
- [ ] architecture validation notes in CI/docs

No gameplay changes.

### Phase B — pure domain seams

Deliverables:

- `StableRandom` extracted from `GeneticsService`;
- typed domain rules with current constants as defaults;
- split relationship/inbreeding/rare-trait services;
- `GeneticsService` compatibility facade delegates to new services;
- presentation stat colors removed from domain rule dependencies;
- characterization tests for current deterministic outputs.

Acceptance:

- existing demo produces equivalent outcomes for existing input seeds;
- Domain files compile without `Godot` imports;
- current save remains readable.

### Phase C — persistence and application seams

Deliverables:

- `IGameStateRepository`;
- `GodotJsonGameStateRepository`;
- `SaveMigrationPipeline`;
- application use cases for the highest-churn operations (breeding, shop/training, simulation tick);
- `GameSession` reduced toward lifetime/event adapter.

Acceptance:

- same save file path;
- no loss/reroll of current data;
- use cases test against in-memory repository without Godot scene nodes.

### Phase D — composition root and service-locator removal

Deliverables:

- explicit composition root/bootstrap wiring;
- presentation controllers receive required service references;
- replace feature-level `GameSession.Instance` access;
- autoload can remain as game-lifetime root if useful, but static global access is no longer the normal dependency mechanism.

Acceptance:

- dependencies visible from constructors/setup methods;
- individual screens/controllers can be instantiated with test doubles or in isolation.

### Phase E — race simulation extraction

Deliverables:

- immutable participant snapshot;
- deterministic pure C# simulation state;
- current demo course represented as data/segments;
- run/swim/fly/hurdle/stamina/cheer behavior migrated without intentional tuning change;
- presentation maps simulation states to existing visuals;
- CPU fast-forward uses the same simulation, not a separate result path.

Acceptance:

- same seed/snapshots produce the same finish order/events independent of frame rate;
- headless batch simulation possible;
- animation/camera edits cannot alter result.

### Phase F — UI decomposition + localization foundation

Deliverables:

- ModalHost/navigation root;
- migrate Shop and one other screen as reference feature modules;
- localization CSV + project registration;
- stable keys for global navigation/settings/common actions and migrated screens;
- locale setting hook;
- pseudolocalization instructions/check.

Acceptance:

- user-generated creature names never translate;
- UI remains functional in pseudolocalization;
- adding a new screen does not require another partial of a god controller.

### Phase G — Godot Resources for balance/content

Deliverables only for rules actively used by the next roadmap phase:

- custom Resource definitions;
- startup validation/conversion;
- default `.tres` ruleset reproducing current constants.

Do not create every future Resource type at once. First candidates: genetics/breeding/stat/race rules because they already exist.

---

## 4. File and namespace policy

Use feature folders inside layers rather than generic dumping grounds.

Good:

```text
Scripts/Domain/Breeding/RelationshipService.cs
Scripts/Application/Breeding/BreedVoidlingsUseCase.cs
Scripts/Infrastructure/Persistence/GodotJsonGameStateRepository.cs
Scripts/Presentation/UI/Shop/ShopScreen.cs
```

Avoid:

```text
Helpers/
Managers/
Utils/
Common/
Misc/
```

A shared helper belongs in `Domain/Shared` only if it is truly domain-generic and has a concrete caller.

Namespaces mirror folders after migration, for example:

```csharp
namespace Voidling.Domain.Breeding;
namespace Voidling.Application.Breeding;
namespace Voidling.Infrastructure.Persistence;
namespace Voidling.Presentation.UI.Shop;
```

Legacy `VoidlingGame` namespace remains temporarily for files not yet migrated. Avoid namespace-only mass edits that add risk but no boundary value.

---

## 5. Testing and CI plan

Current CI already provides a valuable final integration gate:

1. restore;
2. C# build;
3. Godot headless project/scene parse.

Add a standard test step once the first domain characterization tests land.

Priority characterization tests before extraction:

1. random genome equality for fixed seed;
2. one allele from each parent;
3. 70% expression mechanism uses named deterministic stream;
4. rare mutation 10% transmission uses trait-specific stream and terminal depth;
5. relationship traversal/inbreeding burden;
6. viability ladder;
7. store egg remains fixed after purchase/save flow;
8. save migration retains lineage/mutations/settings;
9. race finish order invariant once simulator is extracted.

Tests should assert rules/invariants rather than implementation structure where possible.

---

## 6. Code-quality setup

Expand `.editorconfig` to make style predictable for both humans and agents:

- UTF-8, LF, final newline;
- 4-space C# indentation;
- file-scoped namespace preference for new files;
- conventional C# naming;
- useful analyzer warnings.

Do **not** make every analyzer warning an error during the initial refactor; the MVP contains generated/Godot-facing patterns and enforcing a huge unrelated cleanup would increase risk. Ratchet quality forward in touched/new code.

The `.csproj` should keep nullable enabled and add build-time code-style analysis conservatively. We can raise severity later after the architecture branch is clean.

---

## 7. Localization setup details

Initial file:

```text
Localization/strings.csv
```

Format:

```csv
keys,en
UI_COMMON_CLOSE,Close
UI_TOP_SHOP,Shop
UI_TOP_INVENTORY,Inventory
...
```

New presentation uses semantic keys and `Tr("KEY")` / translated controls.

Rules:

- no sentence assembly from separately translated fragments;
- placeholders for values/names;
- pluralization uses Godot plural APIs when needed;
- creature names are literal values passed into messages;
- layout containers preferred to hand-positioned text;
- pseudolocalization should be checked when a UI feature is considered done.

CSV is intentionally chosen now because the project has a small set of strings. PO/gettext becomes preferable once translators need per-locale version-control workflows, comments, fuzzy-string tracking or collaboration platforms.

---

## 8. AI-agent legibility

The repository itself should answer:

- Where does a rule belong?
- What can depend on Godot?
- How do I build/test?
- What are the non-negotiable product rules?
- Where is the current implementation plan?
- Which documents are durable decisions vs temporary execution plans?

`AGENTS.md` answers those as a map, not a full textbook. Durable architectural choices receive ADRs. Active multi-stage work lives in `docs/architecture/` or future `docs/exec-plans/`.

This follows current OpenAI agent-first repository guidance and Claude Code's scoped-project instruction mechanism without maintaining two conflicting manuals.

---

## 9. Explicit non-goals for this architecture branch

Unless a concrete migration requires it, do not:

- change game balance;
- redesign the UI visually;
- change existing gameplay controls;
- change the save path;
- introduce ECS;
- introduce a generic message/event bus;
- add a DI container;
- build networking/mod/plugin architecture;
- make every planned roadmap feature now;
- build a custom localization engine;
- split into many assemblies/projects only for aesthetic layering;
- create interfaces with exactly one obvious implementation unless they form a real external boundary or enable a needed test seam.

The branch creates a clean runway for the implementation plan; it does not implement the whole plan itself.

---

## 10. Definition of done

This restructuring is ready to merge when:

- current demo still builds and scenes parse;
- core deterministic rules have pure-domain seams and characterization tests;
- persistence is behind an explicit repository/migration boundary;
- new code no longer needs static `GameSession.Instance` access as its default dependency model;
- race simulation has a credible pure-C# boundary or a completed first extraction preserving the current course;
- localization infrastructure is registered and a representative set of screens uses it;
- the root UI is materially less monolithic through at least reference screen components;
- architecture/agent docs match the actual structure;
- CI is green;
- no intentional gameplay regression was introduced.
