# Voidling documentation entry point

For implementation work, start here:

1. `../ARCHITECTURE.md` — active dependency, composition and pattern policy.
2. `AGENT_HANDOFF_IMPLEMENTATION_PLAN.md` — **current contextualized execution plan and agent handoff contract** against the merged `main` baseline.
3. `GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md` — source of truth for current player-facing intent and explicitly unresolved product decisions.
4. `PRODUCTION_VOIDLING_APPEARANCE_RULES.md` — confirmed production supplement for semantic body types, color-DNA palette inheritance and layered sprite composition.
5. `architecture/VOIDLING_VISUAL_ASSET_PIPELINE.md` — centralized implementation pipeline for all incoming/replacement Voidling art.

The older `GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md` and `MULTIPLAYER_IMPLEMENTATION_PLAN.md` remain useful technical/research references, but they were written across earlier implementation states. Do not treat them as greenfield instructions and do not use them to override current `main`, `ARCHITECTURE.md`, the gameplay context, its confirmed production appearance supplement, or the contextualized handoff plan.

If an older document specifies a player-facing rule that the current gameplay documents mark unresolved, **stop at an extensible architecture boundary rather than inventing a product decision**.
