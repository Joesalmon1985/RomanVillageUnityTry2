# WorldMVP (RomanVillageUnityTry2)

A single-player Unity game (Unity 6000.3.28f1) that joins four of the owner's games.

- **Branch `refactor/prehistoric` (tag `refactor-done`, 14 Nov 2026): the prehistoric island.** Two peoples, each with a camp on a hex corner. Dice roll on a clock and grow stock on the hexes; camps gather it, carriers walk it home, main roads join camps. You command nothing: you walk the island step by step (Dungeon Master style, with a free mouse pointer) and the prince and his villagers come to you with what they want built. Nothing is built until you walk to the spot and press the ground, which is a free spell. Ignore the prince and the rival Ash Clan overruns you. Mira's Ward duel and a dance at a fire with four drums (the owner's 174 BPM loops) are part of the camp. The game starts at night and has a day-night cycle, one save slot and an in-game guide (Esc).
- **Branch `main` (13 Nov 2026): the older MVP**, a first-person village with a top-down God View, the Ward duel, a rhythm party in a warehouse, and a road economy that grows into a card and city game.

Nobody has played either by hand yet. What ran and what did not: `Docs/PROGRESS.md`.

## How to run

- **Windows exe:** build it (below), then double-click `Builds/Windows/WorldMVP.exe`. It opens full screen; Alt+Enter gives a window. The build is not committed.
- **In the editor:** open the project in Unity 6000.3.28f1, open `Assets/Scenes/World.unity`, press Play.
- How to play and the controls: `Docs/PLAY_GUIDE.md`.

## How to build and test

From the project root, with the editor open on the project (`unity` is `C:\Users\joesa\.unity\bin\unity.exe`):

```bash
unity command script run --file AgentScripts/BuildWindows.cs --entry BuildWindows.Run --timeout 900
dotnet test DotNet
unity command test run --mode editor --async-tests     # then: unity command test status
unity command test run --mode playmode --async-tests   # then: unity command test status
```

Packaged smoke runs: `Builds/Windows/WorldMVP.exe -autoplay walk_tour` (also `first_scene`, `duel_dance`, `follow_prince`, `ignore_prince`, `cast_all`, `save_load`, `capture_tour`); each logs `[AutoPlay] <name> OK` and exits 0, or logs `FAILED` and exits 1. Options and details in `Docs/HANDOFF.md`.

## Docs

Start with `Docs/HANDOFF.md`. On `refactor/prehistoric`: `Docs/REFACTOR_SPEC.md` (the binding rules and every change made while building), `Docs/PLAY_GUIDE.md`, the package notes (`ECONOMY_API.md`, `ECONOMY_PROOF.md`, `PLAYER_API.md`, `LAND_UI.md`, `DANCE_UI.md`, `PRINCE_API.md`, `LIGHTING.md`) and `Docs/NEXT_START_HERE.md` (what comes next). The older documents (`PRD.md`, `BUILD_PLAN.md`, `TEST_PLAN.md`, `CONTRACTS.md` and the old API notes) describe the MVP on `main`. `Docs/SOURCES.md` lists the source repos and licences.

## Licensing: read before committing

This repository is public. The game uses only original code, primitive placeholder characters, the owner's own rhythm loops and synthesised audio. No third-party art or audio from the source repos is used.

The project was seeded from a Sims 2 animation probe (`Assets/RomanVillage/Probe`, `Assets/Bedsit`, vendored OpenTS2 under `Assets/ThirdParty/OpenTS2`, MPL-2.0). That layer is optional and local-only: Sims 2 content and anything derived from it (baked meshes, textures, clips, screenshots, `*.package`, `config.json`, `LocalData/`, `Assets/LocalBaked/`) is EA/Maxis material, is git-ignored and must **never** be committed or distributed. Check `git status` before every commit. The Windows build of the game uses placeholder characters only and contains no Sims content (the build script keeps the probe's animation-name list out of the player).

## The Sims 2 animation probe (optional, local only)

1. Copy your own Sims 2 install paths into `config.json` (`game_dir`, `user_dir`; see `Docs/M0_ANIMATION_FINDINGS.md`) and put the Pleasantview `N001/Characters` packages under `user_dir/Neighborhoods/N001/Characters`.
2. `python -I Tools/sims2/anim_inventory.py --game-dir <.../game> --out LocalData/m0` lists every Sims 2 animation name.
3. Copy `Tools/sims2/probe_candidates.txt` to `LocalData/m0/`, open `Assets/Scenes/ClipProbe.unity` and press Play. Results: `LocalData/m0/clip_probe.csv`, frames in `Evidence/screenshots/`.

`Assets/Scenes/ClipViewer.unity` cycles through the candidate animations in the editor. It is no longer built to an exe (`AgentScripts/BuildWindows.cs` now builds the game).
