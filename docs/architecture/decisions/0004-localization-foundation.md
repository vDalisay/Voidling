# ADR 0004: Use Godot localization with semantic keys

**Status:** Accepted  
**Date:** 2026-08-24

## Context

Voidling will ship game UI with many compact pixel-art panels. Localization added late commonly exposes clipped layouts and hard-coded prose throughout gameplay code. Godot already provides translation import, runtime locale switching and pseudolocalization.

The first architecture pass used a CSV catalog and committed a `project.godot` reference to its generated `strings.en.translation` output. On a clean checkout Godot reads project settings before that generated artifact exists, which produces missing-resource errors during initial project scanning.

## Decision

- Use Godot's built-in localization/`TranslationServer`.
- Use stable semantic keys rather than English prose as identifiers.
- Keep each locale in a committed UTF-8 gettext `.po` file that Godot can register directly.
- Do not register generated importer artifacts in `project.godot`.
- User-created Voidling names are literal and never translation keys.
- UI uses Containers/wrapping and is checked with pseudolocalization.
- Do not build a parallel custom localization framework.

## Consequences

- new UI has a clear localization path now;
- clean clones do not depend on an importer-generated translation path existing before project initialization;
- gettext files are text/version-control friendly and can later be used directly by translation tooling;
- semantic localization keys remain unchanged if localization workflow/tooling changes later.
