---
paths:
  - "Scripts/Domain/**/*.cs"
---

# Domain rules

- This layer is pure C# and may not reference the `Godot` namespace.
- Do not read files, scenes, input, time, localization, audio, or singleton state directly.
- Pass seeds, clocks, rules and external data explicitly.
- Domain services should be deterministic for identical inputs unless randomness is an explicit input.
- Prefer immutable request/result records and small stateless collaborators.
- Stable persisted identifiers must not depend on `.GetHashCode()` or runtime object identity.
- Game balance constants belong in typed rule objects, not scattered conditionals.
- Add strategies/policies only when the rule actually varies; otherwise keep the direct implementation.
