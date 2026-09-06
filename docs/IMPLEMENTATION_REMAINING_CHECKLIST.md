# Remaining implementation checklist

This checklist tracks confirmed remaining work against current `main`, `docs/AGENT_HANDOFF_IMPLEMENTATION_PLAN.md`, and `docs/GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md`.

Items under **Product decisions required before implementation** are deliberate stop conditions. They must not be implemented by inventing missing rules.

## Integration / release baseline

- [x] Wire the existing Garden real-time/season environment presentation into the live Garden scene.
- [x] Wire the existing lifecycle cocoon presentation into the live Garden scene.
- [x] Raise Garden event history from 80 to the specified 300 messages.
- [x] Fix the race-completion smoke shutdown regression.
- [x] Verify the complete GitHub Actions suite is green after the Garden/ambient integration work.
- [x] Stabilize the command-line LAN trade smoke so a completed durable exchange is not reported as failed by the Godot 4.6 Mono ENet teardown crash.
- [x] Re-sync the implementation branch with current `origin/main` after the later Garden/UI/race merges (`main` at `83142362`, merge commit `79c34edf`).

## Confirmed implementation-ready feature work

- [x] Add player-placeable Garden decorations, separate from functional training modules. Merged in PR #28.
- [x] Extend stat-driven ambient Garden behavior with confirmed Run/Stamina behavior and Swim shoreline affinity. Merged in PR #29.
- [x] Add Cup/championship scaffolding: stable Cup IDs, stable NPC casts, progression/unlock hooks and authorable content. Merged in PR #31 with full CI green.
- [ ] Continue production Voidling art ingestion through the centralized visual pipeline as new authored art revisions arrive.
  - [x] Latest authored Normal-body outline palette revision is integrated on `main` (`9f971202` / merge `71b4c3a3`) and keeps the centralized palette/resource path intact.
  - [ ] Ingest the next authored body/wing/crown/form revision when artwork is supplied; do not synthesize new production art or fork the visual pipeline.

## Product decisions required before implementation

- [ ] Lock the stat-driven morphology/evolution visual mapping (for example which authored form corresponds to Run/Swim/Fly/Power/Generalist and when `normal` changes to it). The domain specialization rules already exist; production visual mapping still requires authored form IDs/art and an explicit product rule.
- [ ] Lock remaining appearance-inheritance probabilities, dominance, rare-trait depth and stacking rules.
- [ ] Lock the final trophy/reincarnation transformation recipe.
- [ ] Lock Cup entry-fee/refund/reward economy details. Cup scaffolding keeps these values/rules out until decided.
- [ ] Decide whether active-computer-use income should exist; do not implement activity monitoring until its privacy/platform/UX requirements are approved.

## 2026-09-07 audit

- [x] Rechecked current `main` after the newer Garden/UI/race merges.
- [x] Confirmed Cup scaffolding is now present on `main` (`CupCatalog`, `CupProgressionService`, `GameSession.Cups`, persisted completed-Cup IDs).
- [x] Confirmed evolution specialization logic already exists in `EvolutionService`; the remaining morphology item is specifically the unresolved production visual/form mapping, not missing stat logic.
- [x] Confirmed the visual catalog still contains only the authored `normal` definition, so there is no additional supplied production form to ingest yet.
- [x] Confirmed the handoff/design docs still leave trophy transformation, detailed appearance inheritance, Cup economy, and active-computer-use income unresolved.

## Current stopping point

All currently confirmed implementation-ready gameplay/system work is implemented and merged. Production-art ingestion remains an ongoing pipeline task rather than a missing gameplay system: the latest authored outline revision is already integrated, and the next implementation step requires new authored art. The remaining gameplay/system items are explicit product-decision stop conditions and should not be implemented by inventing rules.

The next actionable implementation step is therefore one of:

1. ingest newly supplied body/wing/crown/form art through the existing centralized visual pipeline; or
2. implement one of the blocked gameplay/system items after its product rule is explicitly locked.
