# Voidling production art

`Resources/Presentation/Voidlings/DefaultVoidlingVisual.tres` is the single source of truth for the base Voidling presentation.

## Replacing the base art

1. Put the new source PNG(s) under this directory, preferably under `Assets/Voidlings/Base/`.
2. Point `BaseAtlas` (and `SwimAtlas` only if a separate swim sheet is genuinely required) in `DefaultVoidlingVisual.tres` at the new files.
3. If the sheet layout changed, update the frame size, row/frame mappings and portrait coordinates in that same `.tres` resource.
4. Adjust the presentation geometry in that same resource when the silhouette changed: adult/child/race scale, feet offsets, hitboxes, held offset, shadow size and mutation anchors.
5. Run CI. `VoidlingVisualFactory` validates atlas coverage and CI rejects direct base-art loads from consumer C# files.

Do not edit Garden, race, trade, family-tree, breeding, details or card code just to replace the base creature art. Those contexts all resolve through `VoidlingVisualFactory`/`UiFactory`.

## Current legacy source

The initial resource still points at the existing Sprout Lands character/swim atlases so this architecture migration does not intentionally change the game's visuals. The next production-art import can replace those paths without another consumer refactor.

## Future visual families

Do not use a Voidling display name as an art key. If the product later needs multiple inheritable/base visual families, introduce a stable semantic visual-family ID and resolve it through the same presentation catalog. Do not persist texture paths or atlas coordinates.
