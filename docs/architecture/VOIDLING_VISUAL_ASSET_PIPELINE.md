# Voidling Visual Asset Pipeline Architecture

**Status:** required architecture for production Voidling art ingestion  
**Scope:** Presentation/Infrastructure only; gameplay rules and authoritative simulation remain unchanged.

---

## 1. Objective

Replacing or revising the base Voidling artwork must not require hunting through Garden, race, multiplayer and UI code.

The desired workflow is:

```text
new/updated Voidling art
        ↓
one authoritative visual definition/catalog
        ↓
validated presentation factory/resolver
        ↓
Garden | Remote Garden | Race | Portraits | Trade | Breeding | Family Tree | Details
```

For a same-layout art replacement, changing the source atlas in one place should update every consumer automatically. If a new spritesheet has a different frame layout, only the canonical visual definition should need updating.

This is a presentation architecture concern. Domain, Application, saves and multiplayer gameplay protocol must not depend on texture paths, atlas regions or Godot resources.

---

## 2. Current problem

The current code has several independent creature-art construction paths:

- `Scripts/Actors/VoidlingActor.cs` loads the base character sheet and builds directional `SpriteFrames` itself;
- `Scripts/Presentation/Racing/RaceScreen.cs` loads the base character sheet plus a swimming sheet, chooses its own rows, animation speeds, scale and offsets, and builds a second `SpriteFrames` set;
- `Scripts/UI/UiFactory.cs` loads the base character sheet again and hardcodes a portrait region;
- mutation adornments and ground metrics are partially shared but still depend on context-specific sprite scales/positions.

This makes a new art revision a multi-file code change and permits visual drift between screens.

---

## 3. Non-negotiable boundaries

### Domain/Application

Must not know:

- `Texture2D`;
- sprite sheets;
- frame dimensions;
- atlas coordinates;
- Godot resource paths;
- visual scale/pivot;
- portrait crop;
- animation names used by Godot.

They may carry stable semantic appearance state already meaningful to gameplay, such as tint, mutation IDs, life stage and—only if/when product design needs multiple base visual families—a stable semantic `VisualVariantId`.

Do **not** add `VisualVariantId` to saves merely for this refactor if every current Voidling still uses the same base definition. The catalog can resolve `default` without changing persistent state.

### Presentation

Owns:

- visual catalog/resources;
- frame mapping;
- animation mapping;
- context scale/pivot/hit profile;
- portrait construction;
- mutation/adornment composition;
- sprite/VFX nodes.

### Infrastructure/Bootstrap

Infrastructure may load/validate editor-authored Resources. Bootstrap/root presentation composition wires the concrete catalog/factory into presentation consumers. Do not introduce a global static mutable service locator.

---

## 4. Canonical resource model

Use one authoritative Godot Resource-backed catalog.

Recommended layout:

```text
Assets/
└─ Voidlings/
   ├─ base/
   │  ├─ voidling.png
   │  └─ voidling_swim.png        # only while a genuinely separate swim sheet is required
   ├─ mutations/
   │  └─ ...
   └─ README.md                    # artist-facing layout/export expectations

Resources/
└─ Presentation/
   └─ Voidlings/
      └─ DefaultVoidlingVisual.tres

Scripts/
└─ Presentation/
   └─ Voidlings/
      ├─ VoidlingVisualDefinition.cs
      ├─ VoidlingVisualFactory.cs
      ├─ VoidlingVisualState.cs
      ├─ VoidlingVisualContext.cs
      └─ VoidlingVisualCatalogValidator.cs
```

Do not create every class above blindly. The minimum useful implementation is one typed definition/resource plus one factory/resolver. Split additional types only when the implementation gains real independent responsibilities.

### `VoidlingVisualDefinition`

A Godot custom `Resource` containing presentation-only data such as:

```text
DefinitionId: "default"
BaseAtlas
OptionalSwimAtlas
FrameWidth
FrameHeight
PortraitFrame/region
Animation mappings
Animation speeds
Adult presentation profile
Child presentation profile
Race presentation profile
Held offset/profile
Mutation anchor/profile
```

The exact serialized shape should favor typed exported fields/arrays over magic string dictionaries where practical.

### Animation state vocabulary

Consumers should request semantic visual states rather than knowing atlas rows.

A useful initial enum/value vocabulary is:

```text
Idle
WalkDown
WalkUp
WalkLeft
WalkRight
Run
Swim
Glide
Held
Hatch
```

Not every state requires unique source frames. For example `Glide` may intentionally reuse `Run` frames at a modified speed until dedicated art exists. That fallback belongs in the definition/factory, not in `RaceScreen`.

If the art later supplies dedicated jump/glide/idle cycles, the definition can change without rewriting consumers.

---

## 5. Presentation profiles belong with the art definition

An art swap can change apparent feet position, body bounds and required scale even when gameplay is unchanged. Therefore the visual definition should also be the authoritative source for presentation geometry that currently appears as scattered constants.

At minimum model:

- world adult scale;
- world child scale;
- race scale;
- sprite center/feet offset;
- held/lifted sprite offset;
- shadow radii/offset;
- pointer hitbox size/offset;
- portrait frame or crop;
- mutation halo/adornment anchor or scale reference.

These values are **presentation geometry**, not Domain stats.

`VoidlingGroundVisualMetrics` can either become a calculation helper over the profile or be folded into the new profile implementation. Do not maintain two independent sources of truth.

---

## 6. Factory/resolver responsibilities

A concrete `VoidlingVisualFactory` is justified because visual construction currently has repeated invariants across multiple call sites.

It should provide a small set of operations, conceptually similar to:

```text
CreateWorldVisual(semanticAppearance, lifeStage)
CreateRaceVisual(semanticAppearance)
CreatePortrait(semanticAppearance, requestedSize)
ApplyState(animatedSprite, VoidlingVisualState)
UpdateAppearance(existingVisual, semanticAppearance)
```

The exact signatures should follow the existing code and should avoid unnecessary interfaces.

The factory should:

- resolve the one current definition;
- build/copy the necessary `SpriteFrames` from that definition;
- apply tint/semantic appearance data;
- apply the correct profile;
- expose semantic state changes without callers knowing animation clip names or frame rows;
- provide portrait texture/crop consistently;
- compose mutation adornments using the same resolved presentation profile.

### Cache immutable frame resources

Do not rebuild identical `SpriteFrames` for every actor if profiling or simple inspection shows they can safely be shared. Cache definition-derived immutable frame resources inside the presentation factory/catalog where appropriate.

Do not share mutable per-creature state that would make animation speed/selection on one actor alter another unexpectedly.

---

## 7. Mutation/adornment composition

Mutation rendering should remain compositional rather than create a hierarchy of special Voidling actor subclasses.

Current `MutationAdornment2D` is already directionally correct: it decorates a target sprite. The pipeline should improve its source of geometry:

```text
resolved base visual
    + semantic mutation IDs
    + definition mutation anchor/profile
        ↓
mutation/adornment renderer(s)
```

A mutation may eventually use:

- pixel halo;
- accessory sprite;
- overlay material;
- particles;
- alternate palette;
- body-part overlay.

Those visual strategies remain presentation-only. Any mutation gameplay effect stays in Domain and is referenced by the same semantic mutation ID rather than by art files.

---

## 8. Context adapters without duplicate art ownership

Garden, race and UI have different node/layout needs, but they must not become different sources of art truth.

### Garden

The actor owns movement/input behavior but receives/creates its sprite through the central factory. It requests directional semantic states.

`VoidlingActor` must no longer:

- call `GD.Load` for the base Voidling sheet;
- know atlas row numbers;
- own a private `BuildSpriteFrames` implementation;
- hardcode art-dependent scale/pivot/hitbox values.

### Remote connected Garden

Remote visuals use the same definition and appearance resolver as local Garden actors. Network snapshots carry semantic appearance state only. The client resolves local art.

This prevents a protocol version from being tied to a local file path and guarantees local/remote creatures use identical base art.

### Race

`RaceScreen` remains responsible for race presentation timing/position/VFX, but not frame source/layout.

It requests states such as `Run`, `Swim`, `Glide` from the factory. A separate swimming source sheet, if still required, is referenced by the same visual definition.

Changing art must not touch `RaceSimulation`, course rules or result state.

### UI portraits/cards

`UiFactory` should remain a general UI chrome helper, not the owner of Voidling art.

Move creature-specific portrait creation to the Voidling visual factory (or a focused `VoidlingPortraitFactory` only if separation becomes useful). `UiFactory.CreateVoidlingCard` can receive the constructed portrait/appearance presenter or delegate to the visual factory through an explicit dependency at the calling layer.

The important invariant is that `UiFactory` no longer chooses a base spritesheet or atlas region itself.

---

## 9. Incoming-art workflow

### Case A: replacement uses the same standardized layout

The ideal steady-state artist workflow:

1. export the new atlas to the canonical asset location;
2. replace the existing source image;
3. let Godot import it;
4. run visual catalog validation;
5. all contexts update automatically.

No C# or scene edits should be required.

### Case B: new art uses a different frame layout

1. add the new source image(s);
2. update **only** `DefaultVoidlingVisual.tres` frame/layout/profile metadata;
3. run validation;
4. all consumers continue requesting the same semantic states.

No Garden/Race/UI/multiplayer consumer code changes should be required.

### Case C: truly new visual family/variant

Only after product design requires multiple persistent base visual families:

1. add a new definition to the catalog with a stable semantic ID;
2. add/migrate the semantic variant ID in creature state if persistence is required;
3. update save migration and multiplayer snapshot projections;
4. never store file paths as the ID.

This is intentionally distinct from a simple art replacement.

---

## 10. Validation rules

Implement a headless validator as part of the migration.

### Definition validation

Fail when:

- canonical definition cannot load;
- required texture is null/missing;
- frame width/height is non-positive;
- any declared frame rectangle falls outside texture bounds;
- a required semantic state cannot resolve and has no declared fallback;
- portrait frame/crop is invalid;
- adult/child/race scales are non-positive;
- hitbox dimensions are invalid;
- ground/held/mutation metrics contain invalid numeric values;
- duplicate definition IDs exist once variants are supported.

### Consumer smoke validation

A headless probe should instantiate representative visuals using the production path:

- local Garden visual;
- remote/shared-snapshot Garden visual;
- race visual cycling Run → Swim → Glide;
- UI portrait/card.

It should assert all resolve from the expected canonical definition ID and emit a clear marker such as:

```text
VOIDLING_VISUAL_CATALOG_SMOKE_SUCCESS
```

### No-bypass architecture guard

After migration, CI should prevent reintroduction of direct base-art ownership.

A pragmatic guard can grep for the legacy base asset filenames or disallow `GD.Load`/`ResourceLoader.Load` against `Assets/Voidlings/` outside the approved catalog/resource-loading files.

Keep the guard specific enough not to block unrelated environment/UI assets.

---

## 11. Art review artifact

Optional but useful: a headless visual probe can render a contact sheet and upload it as a workflow artifact.

Suggested contents:

```text
Adult: idle / down / up / left / right
Child: idle / movement
Race: run / swim / glide
Portrait: small / medium / trade-card
Mutation: base / angel / multi-trait example
```

This should be a human-review aid, not a brittle image hash test. Art changes are expected and should not fail merely because pixels changed.

---

## 12. Migration sequence

### Phase 1 — Inventory and canonical definition

- identify every direct base-Voidling texture load/frame builder;
- introduce the single default visual definition;
- write validator tests/probe;
- no gameplay behavior change.

### Phase 2 — Garden

- migrate local Garden actor;
- migrate remote connected-Garden visual;
- preserve selection, hold/drag/drop, grounding, mutation and movement behavior;
- verify child/adult profiles.

### Phase 3 — Race

- migrate base run frames;
- migrate swim source through the same definition;
- preserve jump/glide/failed-jump presentation and deterministic simulation behavior;
- keep existing race outcome tests unchanged.

### Phase 4 — Portrait surfaces

Migrate every creature portrait/card surface, including at minimum:

- roster/race picker;
- details/inspection;
- breeding;
- family tree;
- trade room/exchange;
- race result podium;
- multiplayer-related cards.

Delete the base character texture and portrait-region ownership from `UiFactory`.

### Phase 5 — CI guard and cleanup

- add no-bypass check;
- add headless catalog smoke;
- optionally upload contact sheet;
- remove obsolete duplicate frame builders/constants;
- document artist workflow under `Assets/Voidlings/README.md` when the canonical production art format is settled.

---

## 13. Acceptance criteria

The migration is complete when:

1. one canonical visual definition controls base Voidling art;
2. no Garden/Race/UI/remote consumer loads the base Voidling art directly;
3. consumers use semantic visual states rather than atlas rows;
4. one replacement atlas propagates everywhere for same-layout art;
5. one definition edit propagates everywhere for different-layout art;
6. portraits and world sprites come from the same semantic definition;
7. stage scale, feet/pivot, shadow, hitbox and held offset are not duplicated across contexts;
8. mutation overlays align through definition/profile metadata;
9. a pure visual replacement changes no save data, race result, genetics state or multiplayer protocol;
10. CI validates the catalog and prevents new bypasses;
11. existing Release/Debug builds, Domain/Application tests, Godot runtime smoke and multiplayer/trade smoke remain green.

---

## 14. Design rationale

This solution deliberately does **not** introduce a large asset framework or ECS.

The concrete problem is repeated source-art/frame/profile knowledge. A Resource-backed catalog plus a focused factory resolves that problem while following the existing architecture:

- designer-editable Godot Resources stay at the outer edge;
- Domain/Application remain pure;
- creation invariants are centralized with a Factory;
- variant/fallback animation mapping can use a small Strategy/policy only where variation exists;
- mutations remain composed/decorated rather than inherited through actor subclasses;
- Bootstrap/presentation composition keeps dependencies explicit;
- CI makes the architectural rule enforceable rather than relying on memory.

The result is the desired production workflow: **replace Voidling art once, validate once, and have every representation update through the same source of truth.**