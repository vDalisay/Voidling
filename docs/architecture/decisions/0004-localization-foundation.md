# ADR 0004: Use Godot localization with semantic keys

**Status:** Accepted  
**Date:** 2026-08-24

## Context

Voidling will ship game UI with many compact pixel-art panels. Localization added late commonly exposes clipped layouts and hard-coded prose throughout gameplay code. Godot already provides translation import, runtime locale switching and pseudolocalization.

## Decision

- Use Godot's built-in localization/`TranslationServer`.
- Use stable semantic keys rather than English prose as identifiers.
- Start with UTF-8 CSV while the catalog is small.
- User-created Voidling names are literal and never translation keys.
- UI uses Containers/wrapping and is checked with pseudolocalization.
- Move to gettext PO if translator collaboration or catalog scale warrants it.
- Do not build a parallel custom localization framework.

## Consequences

- new UI has a clear localization path now;
- pixel-layout failures can be caught early with pseudolocalization;
- CSV remains easy to edit during early development;
- switching source format later does not require changing semantic keys in code.
