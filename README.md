# Voidling

Godot 4.6 C# prototype for a creature-raising, breeding and automated racing game.

## Demo MVP

The current MVP lives on `feature/demo-mvp` while it is being validated. It contains:

- an editor-editable Sprout Lands `TileMapLayer` garden;
- randomly wandering, color-tinted Voidlings using the same base character sprite;
- five stats: Run, Swim, Fly, Power and Stamina;
- two-allele genetics and parent-to-child inheritance;
- player-initiated breeding, incubation and hatching;
- escalating inbreeding hatch-failure risk and one-generation-at-a-time outcross cleansing;
- rare appearance-trait provenance with founder → G1 → terminal G2 transmission;
- a UI-only shop for stat treats and pre-rolled mystery eggs;
- training items that add small stat gains;
- an automated left-to-right obstacle race where Run affects speed and avoidance;
- persistent local save data at `user://voidling_mvp_save.json`.

Open the project with the **Godot 4.6 .NET** editor and press **F6/F5**.

See [`docs/DEMO_MVP.md`](docs/DEMO_MVP.md) for controls and editing notes, and
[`docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md`](docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md)
for the larger system plan.
