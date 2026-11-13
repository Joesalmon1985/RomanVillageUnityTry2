# WorldMVP (RomanVillageUnityTry2)

A single-player Unity game (Unity 6000.3.28f1) that joins four of the owner's games in one 3D village: walk around in first person, switch to a top-down God View with Tab, duel the wizard Mira, play a 174 BPM rhythm party in the warehouse, and run one connected road economy that grows into a strategy game with a card draft, cities, demons, a hero and soldiers, up to a win or a loss.

**Status: MVP built (13 Nov 2026).** The whole game plays from the first step to an ending, with save and load (F5 / F9) and an in-game guide (Esc). What ran and what did not: `Docs/PROGRESS.md`.

## How to run

- **Windows exe:** build it (below), then double-click `Builds/Windows/WorldMVP.exe`. It opens full screen; Alt+Enter gives a window. The build is not committed.
- **In the editor:** open the project in Unity 6000.3.28f1, open `Assets/Scenes/World.unity`, press Play.
- How to play, controls and a first-15-minutes walkthrough: `Docs/PLAY_GUIDE.md`. The owner's test script: `Docs/TEST_PLAN.md` Part B.

## How to build and test

From the project root, with the editor open on the project (`unity` is `C:\Users\joesa\.unity\bin\unity.exe`):

```bash
unity command script run --file AgentScripts/BuildWindows.cs --entry BuildWindows.Run --timeout 900
dotnet test DotNet
unity command test run --mode editor
unity command test run --mode playmode --async-tests   # then: unity command test status
```

Packaged smoke runs: `Builds/Windows/WorldMVP.exe -autoplay boot_tour` (also `first_five`, `commission`, `full_game`, `save_load`); each logs `[AutoPlay] <name> OK` and exits 0, or logs `FAILED` and exits 1. Details in `Docs/HANDOFF.md`.

## Docs

Start with `Docs/HANDOFF.md`. Then `Docs/PRD.md` (requirements), `Docs/BUILD_PLAN.md` (architecture), `Docs/TEST_PLAN.md` (gates and the owner playtest), `Docs/SOURCES.md` (source repos and licences), and the API notes (`CONTRACTS.md`, `GAME_API.md`, `ECONOMY_UI.md`, `STRATEGY_UI.md`, `ENCOUNTERS_UI.md`).

## Licensing: read before committing

This repository is public. The game uses only original code, primitive placeholder characters, the owner's own rhythm loops and synthesised audio. No third-party art or audio from the source repos is used.

The project was seeded from a Sims 2 animation probe (`Assets/RomanVillage/Probe`, `Assets/Bedsit`, vendored OpenTS2 under `Assets/ThirdParty/OpenTS2`, MPL-2.0). That layer is optional and local-only: Sims 2 content and anything derived from it (baked meshes, textures, clips, screenshots, `*.package`, `config.json`, `LocalData/`, `Assets/LocalBaked/`) is EA/Maxis material, is git-ignored and must **never** be committed or distributed. Check `git status` before every commit. The Windows build of the game uses placeholder characters only and contains no Sims content (the build script keeps the probe's animation-name list out of the player).

## The Sims 2 animation probe (optional, local only)

1. Copy your own Sims 2 install paths into `config.json` (`game_dir`, `user_dir`; see `Docs/M0_ANIMATION_FINDINGS.md`) and put the Pleasantview `N001/Characters` packages under `user_dir/Neighborhoods/N001/Characters`.
2. `python -I Tools/sims2/anim_inventory.py --game-dir <.../game> --out LocalData/m0` lists every Sims 2 animation name.
3. Copy `Tools/sims2/probe_candidates.txt` to `LocalData/m0/`, open `Assets/Scenes/ClipProbe.unity` and press Play. Results: `LocalData/m0/clip_probe.csv`, frames in `Evidence/screenshots/`.

`Assets/Scenes/ClipViewer.unity` cycles through the candidate animations in the editor. It is no longer built to an exe (`AgentScripts/BuildWindows.cs` now builds the game).
