# Handoff: start here

Written 12 Nov 2026; rewritten 14 Nov 2026 when the prehistoric refactor was finished (tag `refactor-done` on branch `refactor/prehistoric`). Nothing in this file needs a previous conversation.

## State

- **Status: the prehistoric refactor is built and packaged (14 Nov 2026, branch `refactor/prehistoric`, tag `refactor-done`).** The game is a small prehistoric island: dice on a clock, growing stock, camps with plots, main roads, the prince and spells, Dungeon Master movement, a dance circle, Mira's duel, a day-night cycle that starts at night, save and load. The binding rules are `Docs/REFACTOR_SPEC.md`. What ran and what did not: the last entries of `Docs/PROGRESS.md`.
- **`main` still holds the older MVP** (the village with the warehouse party and the card game, `189466f`). The refactor branch has not been merged into `main`; that is the owner's call.
- **How to play:** `Docs/PLAY_GUIDE.md` (also in the game: Esc).
- **Not done:** nobody has played it by hand or listened to the dance; characters are block figures; the fine graph is still in `BoardTopology` (unused by the rules); the Sims 2 character layer (`Assets/RomanVillage/Probe`, `Assets/Bedsit`, `Assets/ThirdParty/OpenTS2`) is still optional and unused by the game.
- **Next:** `Docs/NEXT_START_HERE.md` (the ages, the crystals campaign, characters). Do not start it without the owner.

## Session handoff (paused 17 Nov 2026, mid-task — read this first if you're picking up council work)

Owner asked to finish all remaining work, get it onto one branch, and deliver a single exe plus playtest/economy docs. Paused mid-session (owner going to sleep) with real work in flight. Exact state, so the next agent doesn't rediscover any of this:

- **`ages/h1-5` → `historic/main` (in `WorldMVP`, not this worktree): DONE and verified.** Clean merge, commit `b0e58c3`. EditMode 538/538, PlayMode 132/132, both 0 failed. Build to `Builds/Next/WorldMVP.exe` succeeded. 7/8 AutoPlay smoke scripts passed (`council_full` correctly skipped, council not merged yet). `Docs/PROGRESS.md` updated, committed `328c07e`. Not pushed.
- **Council gestures/readability pass (this worktree, `wt-council`, branch `council/main`): IN PROGRESS, paused, NOT finished.** Three agents worked this in sequence (two Sonnet, one Opus); here's what's real:
  - `World.Sim.Tests` (EditMode): 519/519 passing, solid.
  - `World.Game.Tests` (PlayMode) had **11 real failures**, all `CouncilTests`, one connected root cause: rival-turn tap-target/catspaw/lead-state handling going missing. Last known-good AutoPlay check (`council_full`) was observed **passing** again just before work was paused — the fix looks real but the full PlayMode suite had not finished re-running to confirm 0 failures.
  - A second, separate, genuine bug was found and fixed: `DanceCircle.LateUpdate()` threw `NullReferenceException` when the circle's built state (drums/dancers/layout — plain C# fields, not serialized) was lost to a script reload during Play Mode. This crashed the PlayMode test runner's `ExitPlayModeTask` and broke the whole suite/session every time it happened — fix is `StateLost()` guard added in commit `ffa82c5` (see below), plus a new `Assets/World/Editor/PlayModeReloadGuard.cs`.
  - **Current HEAD: `ffa82c5`**, a WIP safety commit ("DanceCircle play-mode-reload guard, council tap-target fix in progress") on top of `f281183`. It is NOT finished: `CouncilScene.cs`/`CouncilEncounter.cs` still carry `[CouncilDiag]`/`[CouncilDiag2]` debug logging added while chasing the 11 failures — **strip this before any final commit**, it's not meant to ship.
  - **Still to do, in order:** (1) re-run PlayMode to a clean 0/109 failures, confirming the tap-target fix is actually complete, not just `council_full`; (2) strip the diagnostic logging; (3) build `Builds/Windows/CouncilPlaytest.exe` (`AgentScripts/BuildWindows.cs` → `RunCouncilPlaytest`); (4) run all 3 AutoPlay smoke scripts (`council_fixture`, `council_full`, `council_decline`) against it; (5) take and review captures at 844x390 and 1920x1080; (6) write the honest readability verdict into `Docs/COUNCIL_UI.md` under "Readability pass (bigger gestures)"; (7) commit for real (don't amend `f281183` or this WIP commit, make a fresh one).
  - **Owner has approved** merging `council/content` (the workbook dispute-text branch, separate worktree) into `council/main` once the above is done — do that next.
  - **Then** merge the combined `council/main` into `historic/main` in `WorldMVP`, re-run the full suite there including `council_full` this time, rebuild.
- **After that**, the owner wants: in-game debug shortcuts for testing each minigame (rhythm party, council election, Ward duel, crystal/earth quest), a single packaged exe, a short Word document with a manual test plan, and a plain-language economy explainer doc. None of this has been started.
- **Explicitly NOT started, and NOT to be started without the owner:** Crystals Slice 2, characters, full faction economy/military, Modern/Future age missions/worlds (modern and futurist ages don't exist yet at all — don't imply otherwise in anything written for the owner).
- **CLI gotchas worth knowing before you re-derive them the hard way:**
  - `unity command test status` always falsely reports `{"status":"no_tests"}` for a `--detach`'d job. Use the top-level `unity job status <job-id>` / `unity job wait <job-id> --timeout <seconds>` instead.
  - PlayMode runs need `--async-tests true`; after the domain reload, the job id can go invalid ("Job Not Found") even with tracking. If so, poll `unity command console get --tail <N>` for the `[TestResultCollector] Run finished: X passed, Y failed` line instead of re-polling the job.
  - A plain `unity command test run --mode editor` with no filter has been observed to actually run the PlayMode/`World.Game.Tests` assembly, not `World.Sim.Tests` — always pass `--filter-type assembly --filter "World.Sim.Tests"` explicitly for a real EditMode run and sanity-check which assembly ran.
  - If a PlayMode run crashes the test runner (as the `DanceCircle` bug did), the editor's test-runner session can stay broken afterward (`Test tree is not available`, `This cannot be used during play mode` errors) — close and reopen the editor rather than trying to push through it.
  - Stray `Assets/InitTestScene*.unity[.meta]` files are harmless Test Runner scratch artifacts from a crashed/interrupted run — safe to delete, never commit.
- **Permission system note:** `reg delete` (clearing the remembered window-size registry key) and killing/deleting stuck Unity processes/files directly have both been denied by the permission classifier in this project before. Don't fight it; leave it and tell the owner.

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
