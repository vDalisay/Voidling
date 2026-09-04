# Remaining implementation checklist

This checklist tracks confirmed remaining work against current `main`, `docs/AGENT_HANDOFF_IMPLEMENTATION_PLAN.md`, and `docs/GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md`.

Items under **Product decisions required before implementation** are deliberate stop conditions. They must not be implemented by inventing missing rules.

## Immediate integration / release blockers

- [x] Wire the existing Garden real-time/season environment presentation into the live Garden scene.
- [x] Wire the existing lifecycle cocoon presentation into the live Garden scene.
- [x] Raise Garden event history from 80 to the specified 300 messages.
- [x] Fix the race-completion smoke shutdown regression that caused CI to crash after the success marker.
- [ ] Verify the full GitHub Actions CI suite is green for this cleanup branch.

## Confirmed implementation-ready feature work

- [ ] Add player-placeable Garden decorations, separate from functional training modules.
- [ ] Extend stat-driven ambient Garden behavior using confirmed rules without allowing personality or presentation state to affect race outcomes.
- [ ] Add Cup/championship scaffolding: stable Cup IDs, stable NPC casts, progression/unlock hooks and authorable content. Keep unresolved fee/refund/reward values configurable rather than hard-coded as product decisions.
- [ ] Continue production Voidling art ingestion through the centralized visual pipeline as new art revisions arrive.

## Product decisions required before implementation

- [ ] Lock the stat-driven morphology/evolution mapping (for example when/how `normal` changes toward water/fly/power forms).
- [ ] Lock remaining appearance-inheritance probabilities, dominance, rare-trait depth and stacking rules.
- [ ] Lock the final trophy/reincarnation transformation recipe.
- [ ] Lock Cup entry-fee/refund/reward economy details.
- [ ] Decide whether active-computer-use income should exist; do not implement activity monitoring until its privacy/platform/UX requirements are approved.

## Execution order

1. Keep the integration/release section green before adding larger feature slices.
2. Implement Garden decorations as its own reviewable slice.
3. Extend ambient behavior through Domain/Application-owned rules and typed projections where needed.
4. Add Cup/championship content on top of the existing deterministic race simulator; do not fork race rules.
5. Ingest production art only through the centralized visual catalog/factory.
6. Stop at unresolved product-decision items and request design input rather than choosing rules implicitly.
