# Handoff: start here

Written 12 Nov 2026 at the end of the planning session. The next session builds the game ("the day run"). Nothing in this file needs the previous conversation.

## State

- **Status: build in progress (started 13 Nov 2026), see `Docs/PROGRESS.md`; contracts for parallel agents in `Docs/CONTRACTS.md`.**
- **Done (planning):** `PRD.md`, `BUILD_PLAN.md`, `TEST_PLAN.md` and `SOURCES.md` are agreed with the owner as the starting point.
- **Done (build):** Milestone 0 skeleton (assemblies, sim core and board, startup scene, test harnesses). Everything after that: see `PROGRESS.md`.
- **Verified:** the project opens in Unity 6000.3.28f1 and compiles with 0 errors and 0 tests. The Windows standalone build module is installed.
- **Inherited and kept:** the Sims 2 animation probe from RomanVillageUnity (`Assets/RomanVillage/Probe`, `Assets/Bedsit`, `Assets/ThirdParty/OpenTS2`, `Tools/sims2`, `Docs/M0_ANIMATION_FINDINGS.md`). It is the optional character layer; do not build on it until milestones 1 to 3 pass.
- **Owner's instruction:** the owner approved starting the build on 13 Nov 2026; do not revise the plan without the owner.

## Read in this order

1. `Docs/PRD.md`: what the game is, requirement IDs, decisions already made, MVP scope.
2. `Docs/BUILD_PLAN.md`: architecture, folder layout, milestones, time budget, cut order, working method.
3. `Docs/TEST_PLAN.md`: gates G1 to G5 with concrete test vectors, and the owner's play script.
4. `Docs/SOURCES.md`: source repos, pinned revisions, what is carried forward, licence rules.

## Decisions that are settled (do not reopen without the owner)

- Placeholder characters by default; Sims 2 bodies optional and local-only behind `ICharacterActor`.
- Fresh C# simulation using source data and test vectors; no parity with the Godot sims.
- If time runs short: walkable village, both encounters and the road economy are finished first. Milestone 4 lands in the order listed in `BUILD_PLAN.md` section 4.
- Where the owner's plan differs from source code, the plan wins. The table is in `PRD.md` section 3.

## This machine

| Thing | Where |
|---|---|
| This project | `C:\Users\joesa\Documents\Cursor\RhytmDrama\WorldMVP` (remote: `Joesalmon1985/RomanVillageUnityTry2`, branch `main`) |
| Unity editor | 6000.3.28f1 under `C:\Users\joesa\UnityEditors` |
| Unity CLI | `C:\Users\joesa\.unity\bin\unity.exe` |
| logistics-rts-sim at `2385672` | `C:\Users\joesa\Documents\Cursor\RhytmDrama\_sources\logistics-rts-sim` |
| Gods-and-Wizards at `d22e32c` | `C:\Users\joesa\Documents\Cursor\RhytmDrama\_sources\gods-and-wizards` |
| RhythmWarfare | `C:\Users\joesa\Documents\Cursor\MusicGame`; read with `git show 8bf2c02a:<path>`; never switch its branch |
| DuelMasterBattle | `C:\Users\joesa\Documents\Cursor\DuelMaster\DuelMasterBattle`; read with `git show 1b9513dd:<path>`; never switch its branch |
| Sims 2 install (optional layer) | path is in the git-ignored `config.json` at the project root |

If `_sources` is missing, recreate it (the logistics repo is private; `gh` is signed in):

```bash
gh repo clone Joesalmon1985/logistics-rts-sim _sources/logistics-rts-sim && git -C _sources/logistics-rts-sim checkout 2385672
gh repo clone Joesalmon1985/Gods-and-Wizards _sources/gods-and-wizards && git -C _sources/gods-and-wizards checkout d22e32c
```

Stale copies to ignore: `Cursor\BoardGame\wide-rts` and `Cursor\BoardGame\gods-and-wizards`.

## Driving Unity

Run these from the project root.

```bash
unity open .                                   # if no editor is open on the project
unity status --until-ready
unity command editor set_auto_tick --enabled true
unity command script recompile                 # then poll: unity command script recompile_status
unity command test run --mode editor           # add --filter <Fixture> to keep it fast
unity command console status                   # consoleErrors must be 0
unity command editor.playmode start            # and: stop
unity command capture game_view --output-path Evidence/<name>.png
unity command script run --file AgentScripts/<File>.cs --entry <Class>.<Method>
```

`AgentScripts/BuildWindows.cs` is the existing Windows build entry; it still points at the ClipViewer scene and must be repointed at the new startup scene in milestone 5.

## Where the exact source rules are

These were verified at the pinned revisions and are the fastest way back to the details behind the PRD tables.

| Topic | File (in the source repo) |
|---|---|
| Ward scoring | `godot_project/sim/feedback.gd`; `WizardRTSHybrid/wizard_rts/duel/ward.py` |
| Mira's cadence and guessing | `WizardRTSHybrid/wizard_rts/net/ai_wizard.py`, `data/ai_wizards.json`, `net/pvp.py` |
| Duel test vectors | `shared_fixtures/*.json`; `WizardRTSHybrid/tests/test_pvp_realtime.py`, `test_mortality.py` |
| Rhythm tuning | `content/run/run.json` keys `timing`, `judgement`, `stages`, `mix`; node `warehouse` |
| Rhythm engine and judge | `src/run/run_engine.gd`, `src/input/pad_judge.gd`, `src/run/run_audio.gd`, `src/core/song_clock.gd` |
| Rhythm test vectors | `tests/test_pad_judge.gd`, `tests/test_run_engine.gd` |
| Owner's warehouse loops and chart | `songs/packs/lv1_warehouse/pack.json` (stems under `songs/packs/_shared/`, 48 kHz, 32-beat loop, cues as `[tick, dur, pad, family]` at 960 ticks per beat) |
| Economy systems | `godot_project/sim/economy/*.gd`; data in `godot_project/sim/data/config/*.json` |
| Settler commission | `sim/data/scenarios/economy/settler_mvp.json`, `config/settler_mvp_objective.json`, `sim/economy/settler_objective.gd` |
| Stall and block reasons | `sim/economy/economy_constants.gd`, `economy_diagnostics_system.gd` |
| Hybrid military | `godot_project/sim/hybrid/*.gd`, `config/hybrid.json`, `military_units.json`, `military_power.json` |
| Board topology | `godot_game/core/types/hex_coord.gd`, `board_node.gd`, `edge_coord.gd`, `core/board/hex_board.gd` |
| Placement, draft, cards, demons | `godot_game/core/rules/build_rules.gd`, `draft_rules.gd`, `development_effect_engine.gd`, `spread_rules.gd`, `contact_resolution_rules.gd` |
| Card data | `godot_game/data/development/development_cards_v1.json` |
| 3D world scale and camera | `godot_game/integration/world_presentation_scale.gd`, `embodied/player/wizard_camera_rig.gd` |

Known defects in the sources that must not be copied: Gods-and-Wizards grants end-game card points on every evaluation, its cost discount never applies, and its vertex positions collapse at the board edge; the logistics hybrid sim charges unit costs to headquarters stock instead of delivering them.

## Hard rules

- This repository is public. Never commit `config.json`, `LocalData/`, `Assets/LocalBaked/`, `Evidence/screenshots/`, `*.package`, or anything else read, baked or captured from Sims 2. Check `git status` before every commit.
- Do not import third-party art or audio from the source repos: Red Alert sound effects, OpenRA sprites, Widelands or Mindustry art, commercial music packs. Owner's own loops or synthesised audio only.
- Mechanics from OpenRA, Widelands, Catan and 7 Wonders are inspiration. Use original names, data and balance.
- Do not modify or switch branches in any source repository.
- Commit after each gate. Keep `Docs/PROGRESS.md` honest about which tests and playthroughs actually ran.

## First steps for the build session

1. Confirm with the owner that the run should start.
2. `git pull`, open the project, confirm 0 console errors.
3. Milestone 0 from `BUILD_PLAN.md` section 4, then proceed gate by gate.
