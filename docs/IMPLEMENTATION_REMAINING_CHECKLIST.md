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
  - [ ] Blocked on authored art rather than on the rule: the mapping stays empty until the body/wing/crown forms are supplied.
  - [x] Decision-neutral implementation seam added on `feature/remaining-systems-2026-09-09`: a pure resolver can translate an already-resolved evolution specialization through an explicitly supplied semantic visual mapping while blank mappings preserve the current visual type. The production Godot visual catalog remains unchanged until morphology rules and authored forms are approved.
- [ ] Lock remaining appearance-inheritance probabilities, dominance, rare-trait depth and stacking rules.
  - [x] Rare-trait transmission depth is now authored balance (`GeneticsRules.RareTraitMaxTransmittedGenerations`, exported on `GameBalanceResource`) instead of a hardcoded `< 2` in `RareTraitInheritanceService`. The default keeps current behavior; probabilities, dominance and stacking remain undecided.
  - [x] Stacking is locked: a child carries each rare trait once. When both parents transmit the same trait the copy closest to its founder survives, so the child keeps the longest remaining transmission depth. Traits with different IDs still stack. This only applies at inheritance time; creatures already saved with duplicate traits are left untouched rather than rewritten.
- [ ] Lock the final trophy/reincarnation transformation recipe.
  - [x] Decision-neutral gate added: `TrophyTransformation` / `TrophyRequirements` answer whether an explicitly authored requirement set is met, and `TrophyRequirements.Undecided` (the default) never qualifies. Only the multi-lifecycle dimension named in the design context is represented; the confirmed trophy effects (immortal, retains appearance, cannot breed, can race, no hidden race power) stay unwired until the recipe is approved.
- [ ] Lock Cup entry-fee/refund/reward economy details. Deliberately parked: Cups stay free so test runs are not gated by currency, and the numbers are revisited during the balancing pass.
  - [x] Decision-neutral arithmetic added: `CupEconomy` / `CupEconomyRules` price entry per stable Cup ID and express refunds as a fraction per finishing placement, which covers both the winner-only and placement-based directions without choosing between them. `CupEconomyRules.Free` (the default) charges nothing and refunds nothing, and no Cup is priced. Prizes stay absent: the design direction is item/medal/trophy/unlock rather than currency.
- [ ] Decide whether active-computer-use income should exist; do not implement activity monitoring until its privacy/platform/UX requirements are approved.

## Current stopping point

All currently confirmed implementation-ready gameplay/system work is implemented and merged. Production-art ingestion remains an ongoing pipeline task rather than a missing gameplay system: the latest authored outline revision is already integrated, and the next implementation step requires new authored art. The remaining gameplay/system items are explicit product-decision stop conditions and should not be implemented by inventing rules.

The remaining-systems branch may prepare narrowly scoped, behavior-neutral extension points for those blocked systems, but must keep them inactive until the corresponding player-facing decisions are locked.
