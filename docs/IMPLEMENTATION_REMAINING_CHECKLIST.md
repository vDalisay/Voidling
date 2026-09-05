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

## Confirmed implementation-ready feature work

- [x] Add player-placeable Garden decorations, separate from functional training modules. Merged in PR #28.
- [x] Extend stat-driven ambient Garden behavior with confirmed Run/Stamina behavior and Swim shoreline affinity. Merged in PR #29.
- [x] Add Cup/championship scaffolding: stable Cup IDs, stable NPC casts, progression/unlock hooks and authorable content. Merged in PR #31 with full CI green.
- [ ] Continue production Voidling art ingestion through the centralized visual pipeline as new authored art revisions arrive.
  - [x] Latest authored Normal-body outline palette revision is integrated on `main` (`9f971202` / merge `71b4c3a3`) and keeps the centralized palette/resource path intact.
  - [ ] Ingest the next authored body/wing/crown/form revision when artwork is supplied; do not synthesize new production art or fork the visual pipeline.

## Product decisions required before implementation

- [ ] Lock the stat-driven morphology/evolution mapping (for example when/how `normal` changes toward water/fly/power forms).
- [ ] Lock remaining appearance-inheritance probabilities, dominance, rare-trait depth and stacking rules.
- [ ] Lock the final trophy/reincarnation transformation recipe.
- [ ] Lock Cup entry-fee/refund/reward economy details. Cup scaffolding keeps these values/rules out until decided.
- [ ] Decide whether active-computer-use income should exist; do not implement activity monitoring until its privacy/platform/UX requirements are approved.

## Current stopping point

All currently confirmed implementation-ready gameplay/system work is implemented and merged. Production-art ingestion remains an ongoing pipeline task rather than a missing gameplay system: the latest authored outline revision is already integrated, and the next implementation step requires new authored art. The remaining gameplay/system items are explicit product-decision stop conditions and should not be implemented by inventing rules.
