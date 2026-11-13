# Resume here: prehistoric refactor, paused 13 Nov 2026 (late evening)

The owner paused the run mid-flight. Three agents were stopped while working. Their unfinished edits are saved as commits marked `WIP (paused, untested)`. Nothing below has been played by hand.

Read `CLAUDE.md`, then `Docs/REFACTOR_SPEC.md` (binding rules for this branch), then this file.

## What the refactor is

Owner-agreed change from the packaged MVP on `main` (`189466f`, still intact, exe at `Builds/Windows/WorldMVP.exe` on the owner's machine):

- Dice roll on a clock (shuffled deck of 36). A hit adds young stock to a hex; it matures in about 75 s; only a collecting building harvests it. Five raw goods: Wood, Clay, Grain, Sheep, Stone.
- One camp per hex corner with its own storehouse and plots. Local paths are automatic. Main roads run along whole hex edges and carry shipments between camps.
- The player is a viewpoint. The prince follows and pesters; the faction brain picks what and where; the player starts every construction by pressing the ground there (a spell, free). Yes, no (next-best offer) or walk away. Ignore him and the Ash Clan overruns you.
- Dungeon Master movement: 60 degree turns, 5 m glided steps. Mouse is a pointer. No on-screen buttons: Tab map, H HUD, F1 developer tools.
- Prehistoric camp; the rhythm game is a dance circle with four drums (same music). Mira's duel is unchanged.
- Added late by the owner: visual polish with light, and the game starts at night.
- Cards, wonder, ages and the fuller military are archived under `Archive/` for a later pass.

## Branches (all pushed)

| Branch | Folder on the owner's machine | State |
|---|---|---|
| `refactor/prehistoric` | `RhytmDrama\WorldMVP` | Integration branch. Sim, player, views and dance circle are merged (four merge commits up to `15d1a22`). The commit after that is the integrator's WIP: joins half fixed, NOT known to compile or pass tests. |
| `refactor/sim` | `RhytmDrama\wt-sim` | Finished. Full simulation and proof, `dotnet test DotNet` 201 passing at `4868ca0`. Already merged. |
| `pre/p` | `RhytmDrama\wt-p` | Finished: walker, pointer, speech bubbles, casting, map, HUD, F1 tools. Already merged. |
| `pre/v` | `RhytmDrama\wt-v` | Finished: world views. Already merged. |
| `pre/d` | `RhytmDrama\wt-d` | Finished: camp and dance circle. Already merged. |
| `pre/n` | `RhytmDrama\wt-n` | Prince, sawyer, merchant, first scene, endings: main commit `b83cd9c`, then a WIP commit. The agent was about to run its `follow_prince` script; tests and pace numbers not reported. NOT merged. |
| `pre/l` | `RhytmDrama\wt-l` | Night start, day-night cycle, fire lights: two commits, then a WIP commit (DayNight edits, new LightingTests not run). NOT merged. Built on `pre/v` + `pre/d`. |

Tag `pre-F0` marks the skeleton all packages started from.

## What to do next, in order

1. **Integration branch (`WorldMVP`).** Open Unity on it, get it compiling, run `dotnet test DotNet`, EditMode and PlayMode. Finish the join list:
   - carrier and shipment views against the real sim (they were only tested with a fake feed);
   - terrain honours `CampLayout.Clearings` (crops and trees through the dance circle, tents, Mira's ground, spawn view);
   - pointer ground hits ignore view trigger colliders; casting works end to end on real views;
   - cast-mark light pillar slim and soft, not a white-out;
   - no HUD in normal play: hide the dance score bar, key hints and the top-left debug text unless H or F1 is on;
   - dance input from touch strips; `duel_dance` and encounter tests on the lattice walker and pointer;
   - `cast_all` AutoPlay script (was being written: `Assets/World/Game/Boot/CastAllScript.cs`, `IntegrationTests.cs`).
2. **Merge `pre/n`** (finish its WIP first in `wt-n`: tests green, `first_scene`, `follow_prince`, `ignore_prince` scripts, report real walking pace per cast).
3. **Merge `pre/l`** (finish its WIP first in `wt-l`: LightingTests, captures; see `Docs/LIGHTING.md` on that branch if present).
4. **Finish (package F-1 in the spec):** save and load on the new state (save version 4), pause guide and `Docs/PLAY_GUIDE.md` for the new game, remove the unused fine graph from the board, Windows build, packaged AutoPlay runs. Then hand the owner the exe.

## Findings the next session should know

- Headless pace (11 seeds): a player taking 45 s per cast wins 11 of 11 in about 16 minutes; 60 s mostly wins on the clock; 90 s is a cliff (4 of 11 overrun); ignoring the prince loses 11 of 11. Walking between casts is only 50 to 90 m. See `Docs/ECONOMY_PROOF.md`.
- Still weak in the sim: the rival is soft against an active player; prompt games often end at 9.5 minutes when the 8-minute opening grace runs out; demons make results swingy.
- Dance readability is unproven: the two outer drums carry most notes and there are no lanes. Needs the owner's hands.
- Each package has its own doc: `PLAYER_API.md`, `LAND_UI.md`, `DANCE_UI.md`, `ECONOMY_API.md`.
- One Unity editor per folder. Several editors were left open on the owner's machine when the run was paused (WorldMVP, wt-n, wt-l); close or reuse them.
- Owner's working preferences: delegate build work to parallel agents on the strongest model; headless proof before graphics; ask before pushing; the repo is public (no Sims content, no third-party art or audio).
