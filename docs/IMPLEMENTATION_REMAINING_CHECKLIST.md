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
- [ ] Add Cup/championship scaffolding: stable Cup IDs, stable NPC casts, progression/unlock hooks and authorable content. **Implementation is on `agent/cup-scaffolding`; awaiting CI/merge.**
- [ ] Continue production Voidling art ingestion through the centralized visual pipeline as new art revisions arrive.

## Product decisions required before implementation

- [ ] Lock the stat-driven morphology/evolution mapping (for example when/how `normal` changes toward water/fly/power forms).
- [ ] Lock remaining appearance-inheritance probabilities, dominance, rare-trait depth and stacking rules.
- [ ] Lock the final trophy/reincarnation transformation recipe.
- [ ] Lock Cup entry-fee/refund/reward economy details. Cup scaffolding must keep these values/rules out until decided.
- [ ] Decide whether active-computer-use income should exist; do not implement activity monitoring until its privacy/platform/UX requirements are approved.

## Execution order

1. Keep the integration/release baseline green before merging larger feature slices.
2. Finish and verify Cup/championship scaffolding without forking the deterministic race simulator.
3. Ingest production art only through the centralized visual catalog/factory as authored revisions arrive.
4. Stop at unresolved product-decision items and request design input rather than choosing rules implicitly.
