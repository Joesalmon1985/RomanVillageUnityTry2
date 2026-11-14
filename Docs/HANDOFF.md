# Handoff: start here

Written 12 Nov 2026; rewritten 14 Nov 2026 when the prehistoric refactor was finished (tag `refactor-done` on branch `refactor/prehistoric`). Nothing in this file needs a previous conversation.

## State

- **Status: the prehistoric refactor is built and packaged (14 Nov 2026, branch `refactor/prehistoric`, tag `refactor-done`).** The game is a small prehistoric island: dice on a clock, growing stock, camps with plots, main roads, the prince and spells, Dungeon Master movement, a dance circle, Mira's duel, a day-night cycle that starts at night, save and load. The binding rules are `Docs/REFACTOR_SPEC.md`. What ran and what did not: the last entries of `Docs/PROGRESS.md`.
- **`main` still holds the older MVP** (the village with the warehouse party and the card game, `189466f`). The refactor branch has not been merged into `main`; that is the owner's call.
- **How to play:** `Docs/PLAY_GUIDE.md` (also in the game: Esc).
- **Not done:** nobody has played it by hand or listened to the dance; characters are block figures; the fine graph is still in `BoardTopology` (unused by the rules); the Sims 2 character layer (`Assets/RomanVillage/Probe`, `Assets/Bedsit`, `Assets/ThirdParty/OpenTS2`) is still optional and unused by the game.
- **Next:** `Docs/NEXT_START_HERE.md` (the ages, the crystals campaign, characters). Do not start it without the owner.

## Build and run

```bash
unity command script run --file AgentScripts/BuildWindows.cs --entry BuildWindows.Run --timeout 900
```

Builds `Assets/Scenes/World.unity` to `Builds/Windows/WorldMVP.exe` (release, no development watermark, fullscreen window at the native resolution, Mono, managed stripping off). `Builds/` is git-ignored. The script parks the probe's `Resources` folder (Sims animation names) outside the build and moves it back afterwards; if the editor dies mid-build, move `Assets/RomanVillage/Probe/ProbeListNotInBuild` back to `Resources`.

Packaged smoke runs (each logs `[AutoPlay] <name> OK` and exits 0, or `FAILED` and exits 1; `-novsync` uncaps the frame rate; `-logFile <path>` keeps one log per run; `-screen-fullscreen 0 -screen-width 1280 -screen-height 720` gives a window):

```bash
Builds/Windows/WorldMVP.exe -autoplay walk_tour -novsync -logFile Builds/logs/walk_tour.log
```

| Script | What | Options |
|---|---|---|
| `boot_tour`, `walk_tour` | spawn, lattice steps, map, HUD, Mira's bubble | |
| `first_scene` | the prince's opening to the first load of wood | `-timescale 2` |
| `duel_dance` | Mira's duel, then the dance with the HUD off | |
| `follow_prince` | a whole game answering the prince after a delay | `-princeDelay 3 -timescale 8` |
| `ignore_prince` | does nothing; must end in Defeat by overrun | `-timescale 8` |
| `cast_all` | plays the proposal queue by itself to an ending | `-speed 8`, `-noprince` |
| `save_load` | save with a carrier mid-path, refused bad files, load, delivered once | |
| `capture_tour` | screenshots beside the exe in `Evidence/screenshots` | |

A plain launch check: `WorldMVP.exe -quitAfterReady -logFile <path>` logs `[Boot] playable: tick, hour, night, screen, fps` after two seconds and quits 0.

Unity remembers the last window size and mode per user (registry `HKCU\Software\Joe Salmon\WorldMVP`, `Screenmanager *` values): after windowed smoke runs, delete that key (`reg delete "HKCU\Software\Joe Salmon\WorldMVP" /f`) so the owner's double-click opens full screen at the native resolution again, and delete the `worldmvp_autoplay_save.json` you created. AutoPlay saves use `worldmvp_autoplay_save.json`, never the player's `worldmvp_save.json` (both under `%USERPROFILE%\AppData\LocalLow\Joe Salmon\WorldMVP`, next to `Player.log`).

## Read in this order

1. `Docs/REFACTOR_SPEC.md`: the game as built (sections 1 to 3), the simulation contract (4 to 6), the Unity-side interface and every change made while building (appendices A and B with their change logs).
2. `Docs/PLAY_GUIDE.md`: how it plays.
3. The package notes: `ECONOMY_API.md` and `ECONOMY_PROOF.md` (simulation), `PLAYER_API.md` (walker, pointer, bubbles, casting, map, HUD, F1 tools, AutoPlay), `LAND_UI.md` (world views), `DANCE_UI.md`, `PRINCE_API.md`, `LIGHTING.md`.
4. `Docs/PROGRESS.md`: what ran and what did not, session by session.
5. `Docs/NEXT_START_HERE.md`: what comes next.
6. The older documents (`PRD.md`, `BUILD_PLAN.md`, `TEST_PLAN.md`, `CONTRACTS.md`, `ECONOMY_DESIGN.md`, the old API notes) describe the MVP on `main`; on the refactor branch the spec overrides them.

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

`AgentScripts/BuildWindows.cs` builds the game (see Build and run).

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
