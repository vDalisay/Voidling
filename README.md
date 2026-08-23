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

## Windows quick start

You do not need to open the Godot editor to build or play the current demo.

Double-click:

- `build.bat` — restores NuGet packages and builds the Debug C# project.
- `playgame.bat` — builds the project, locates Godot 4.6 .NET, and launches the game directly.
- `openeditor.bat` — locates Godot and the .NET SDK, then opens this project in the editor.

For a faster launch when the C# code has already been built:

```bat
playgame.bat --no-build
```

All three launchers use `godot-env.bat`. It automatically supports both repository machines by checking:

- `%USERPROFILE%\Documents\Godot`;
- the extracted Godot folder under `%USERPROFILE%\Downloads`;
- a user-local .NET installation beside Godot or under `%USERPROFILE%\.dotnet`;
- a system .NET SDK available on `PATH`.

If either tool is installed somewhere else, set an override once with:

```bat
setx GODOT_EXE "C:\path\to\Godot_v4.6.1-stable_mono_win64.exe"
setx DOTNET_ROOT "C:\path\to\dotnet"
```

Open a new terminal after `setx`.

You can still open `project.godot` with the **Godot 4.6 .NET** editor whenever you want to edit scenes, TileMaps, resources or other editor content.

See [`docs/DEMO_MVP.md`](docs/DEMO_MVP.md) for controls and editing notes, and
[`docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md`](docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md)
for the larger system plan.
