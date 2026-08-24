# ADR 0005: Determinism and save compatibility are architectural invariants

**Status:** Accepted  
**Date:** 2026-08-24

## Context

Voidling's breeding game depends on fixed egg genetics, hidden alleles, mutation provenance and reproducible race simulation. Refactors/new genes must not silently reroll persisted outcomes. Existing demo saves are user data.

## Decision

- Persist stable seeds/IDs for generated entities where needed.
- Derive random substreams from stable, named salts rather than a shared sequential RNG.
- Never use `.GetHashCode()` for persisted/replay-sensitive randomness.
- Persistence lives behind a repository boundary and an ordered migration pipeline.
- Save migrations preserve existing IDs/genomes/eggs/lineage/mutations/settings and use deterministic defaults for new fields.
- Presentation state, frame rate and animation timing never determine domain outcomes.

## Consequences

- deterministic algorithms receive stronger regression tests;
- save-schema changes require migration work rather than ad-hoc normalization scattered through UI/gameplay;
- race rendering can be changed independently of race results;
- additions to the gene catalog can be designed without shifting unrelated old outcomes.
