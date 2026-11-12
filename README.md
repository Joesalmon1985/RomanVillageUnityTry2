# WorldMVP (RomanVillageUnityTry2)

Unity 6000.3.28f1 project. The goal is one single-player 3D village that joins four games (RhythmWarfare, DuelMasterBattle, logistics-rts-sim, Gods-and-Wizards): walk around in first person, switch to a top-down God View, duel Mira, play a rhythm set in the warehouse, and run one connected economy.

**Status: planning complete, build not started.** Start with `Docs/HANDOFF.md`, then `Docs/PRD.md`, `Docs/BUILD_PLAN.md`, `Docs/TEST_PLAN.md` and `Docs/SOURCES.md`.

The rest of this file describes the Sims 2 animation probe this project was seeded from. It remains as the optional, local-only character layer.

---

# RomanVillageUnity

Proof of concept (Unity 6000.3.28f1): a small Roman-themed village (woodcutter, forester, sawmill) whose workers are animated like Sims,
using the original Sims 2 skeleton and animations through [OpenTS2](https://github.com/LazyDuchess/OpenTS2) (MPL-2.0, vendored under `Assets/ThirdParty/OpenTS2`).
The economy is intended to come from the original deterministic sim in the private `logistics-rts-sim` repo (ported to C#).

Plan and status: `Docs/M0_ANIMATION_FINDINGS.md` (M0 / M0b animation spike done; economy port and village scene not started).

## Licensing: read before committing
Sims 2 content and anything derived from it (baked meshes, textures, clips, screenshots, `*.package`, `config.json`, `LocalData/`) is EA/Maxis material.
It is git-ignored and must **never** be committed or distributed. This repository is public, so check `git status` before every commit. Only code, names of animations and docs are committed.

## Running the animation probe
1. Copy your own Sims 2 install paths into `config.json` (`game_dir`, `user_dir`; see `Docs/M0_ANIMATION_FINDINGS.md`) and put the Pleasantview `N001/Characters` packages under `user_dir/Neighborhoods/N001/Characters`.
2. `python -I Tools/sims2/anim_inventory.py --game-dir <.../game> --out LocalData/m0` lists every Sims 2 animation name.
3. Copy `Tools/sims2/probe_candidates.txt` to `LocalData/m0/`, open `Assets/Scenes/ClipProbe.unity` and press Play. Results: `LocalData/m0/clip_probe.csv`, frames in `Evidence/screenshots/`.

## Viewer exe (Windows)
`Assets/Scenes/ClipViewer.unity` + `Assets/RomanVillage/Probe/ClipViewer.cs` cycle through the 56 candidate animations with on-screen labels
(Left/Right, Space auto-advance, C camera, F12 screenshot, Esc quit; `-selftest` plays 14 clips and quits).
Build: open the project in Unity 6000.3.28f1 and run `AgentScripts/BuildWindows.cs` (`BuildWindows.Run`) via `unity command script run`, or Build Settings with the ClipViewer scene.
The exe contains no Sims 2 content: it needs a `config.json` beside it pointing at your own install (see the README bundled with the build).
Prebuilt zips are attached to private GitHub Releases, never committed.
