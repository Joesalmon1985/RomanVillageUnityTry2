# WU-8: the whole-game pass

20 Nov 2026, branch `godboard/wu8-pass` (from `godboard/main` with every WU package merged). No sim file changed; no rule changed. Editor 6000.3.28f1.

## Done

- **`WorldTour`** (`Assets/World/Game/Boot/WorldTour.cs`): one coroutine that starts from the hub of `dev.godboard` (seed 7) and enters and leaves all six destinations in a row: temple to god board (sacrifice, the board rises, leave), council (staged example stage, steward, abort), rhythm (home dance circle, abort), healer's lodge (inspection), duel ring (Mira walks over, abort, she walks home), Mira's crystal expedition (her "Set out for the crystals" offer starts the campaign; the player goes down the glade's stair, steps into the quarry, "Lead me out" returns to the glade stair, then a walk back to the hub cell). After each: same hub cell, same heading and yaw, on the lattice, walker unlocked, walking view, pointer back. At the end: the god board table is still in setup, paused, with unchanged `ActiveTicks`, the campaign exists, and the wireframe tool type is not in a player.
- **`WorldTourTests`** (PlayMode): `Tour_EntersAndLeavesAllSixDestinations_InARow_ToTheSameHubPose_WithStateKept` (runs `WorldTour`, about 39 s) and `ClearView_EscMenu_OneSetting_ShowsBoardHud_PlagueButtons_AndDuelPanelTogether` (opens the Esc menu, presses the Clear view handler, closes it: the board HUD and the Plague button are drawn and the ledge is down; the duel panel (`LastLayout`) is drawn and the stones are hidden; the menu handler again and the world objects return).
- **AutoPlay `world_ui_tour`** and **`phone_frametime`** (`GodBoardAutoPlay.cs`; rows in `Docs/PLAY_GUIDE.md`). `world_ui_tour -tourshots <prefix> [-notch]` saves local PNGs at each moment. `MeasureFps` now also logs the average ms.
- **Clear view in the Esc menu**: the button already existed (WU-2); it now calls `Hud.ToggleClearView()` (one place, testable). It writes the single `GodBoardSettings.ClearView`.
- **No wireframe label or outline in a build**: new EditMode test `NoWireframeLabelOrOutlineIsDrawnOutsideTheEditor` (no GUI/Handles/Gizmos call outside `#if UNITY_EDITOR` names a zone id, `WireframeOf`, the zone rectangles or the outline tool). The existing `OverlaySourceIsEditorOnly` and `PlayerScriptsHaveNoOverlay` (compiles the player scripts and scans the dll) pass. The packaged tour also checks the tool type is absent (release player; the type is behind `UNITY_EDITOR`, so also absent in a development player).

## Suites (exact)

| Suite | Result |
|---|---|
| Full PlayMode (`test run --mode playmode`, 49 min) | 284 total, 275 passed, 2 failed, 7 skipped (ActorTests: no local character art). Both failures were stale tests, fixed afterwards and re-run alone green (below) |
| EditMode (all editor assemblies incl. World.Game.EditTests, 40 min) | 801 total, 792 passed, 3 failed, 6 skipped (CharacterImportTests: no local art). Failures: `WorldActionTests.OverlaySourceIsEditorOnly` and `PlayerScriptsHaveNoOverlay` (both my first `WorldTour` named the overlay type as a string; fixed, 20/20 green), plus one more failed row of the same two that the status listing did not name separately. The other 3 re-run: `WorldActionTests` 20/20 |
| `dotnet test DotNet` | 769 passed, 0 failed, 0 skipped (16 min 42 s). Tracked proof docs it rewrote were reverted |

Fixes made (clear regressions from the WU merges, tests only):
- `PlayerTests.Pointer_HitsAnInspectable_AndATapShowsWhatItIs`: a ground tap walks since WU-1b (it used to inspect the hex tile); the test now expects `TapOutcome.Walk`.
- `GradeViewTests.Town_ChangesTheCamp_...`: after `RebuildFromState` the rebuilt views get their fire lights at the next decorator scan (13 registered instead of 133+); the test calls `LightDecorator.ScanNow()` first.
- Re-runs after the fixes: `PlayerTests.Pointer...` 1/1, `GradeViewTests.Town...` 1/1, `WorldTourTests` 2/2, `WorldActionTests` 20/20. The full PlayMode suite was not re-run after the two test fixes (no time); no test pollution of `PointerInput.InputEnabled` showed up in the full run.

## Packaged build (`BuildWindows.Run`, 101 MB, 0 errors; windowed 844x390, `-novsync`)

| Script | Result | Seconds |
|---|---|---|
| world_ui_tour | OK | 52 |
| board_first_round | OK | 21 |
| board_trade_discard | OK | 14 |
| plague_epidemic | OK | 15 |
| war_burn | OK | 6 |
| board_to_historic | OK | 10 |
| phone_frametime | OK | 71 |
| first_scene (`-timescale 2`, older script) | OK | not timed |

The tour also passes at 1920x1080 and 1024x768 (with `-notch`). No exception lines checked beyond the OK/exit code 0 of each run.

**Frame time** (`phone_frametime`, 844x390 windowed, `-start dev.godboard -seed 7`, 60 s at the altar, vSync off, the editor open and idle on the same machine): average 527 fps = **1.90 ms**, worst frame **14.9 ms**.

## Captures (local only, `Evidence/worldui/wu8_<W>x<H>_<moment>.png`, not committed)
Tour moments (hub, board, council, rhythm, duel, crystal, and "back" after each destination) at 844x390, 1024x768 and 1920x1080, simulated notch. The lodge moment has no capture (its card shows in a speech bubble that the tour closes in the same frame). The "board" shot is taken as the camera finishes rising (the ledge is not yet up).

## Open problems / decisions for the owner
1. **The expedition does not return to the hub cell.** "Lead me out" ends at the glade stair (existing, WU-1b open problem 1); the tour walks the player home (the walk is automatic and the heading is then re-set with a teleport onto the same cell). Decision: record the hub pose and restore it after the dungeon, or keep the walk.
2. The tour shortens the 50 to 100 m walk to the stair by teleporting to the stair's mouth, and uses `Interiors.FactsOverride` to open the glade stair (the story has not opened it), as the interior tests do.
3. In the first board capture the footprint stones are still drawn while the camera rises to the board (WU-6 hides them in God View only once it is up).
4. Not re-run after the test fixes: the full PlayMode suite; one EditMode failure row was not named in the listing (the three re-run groups are green).
5. The Esc-menu Clear view button is drawn only when a `GodBoardDirector` exists (god board games); in other games the menu has none. Touch on the IMGUI button was not tested.
6. No registry change was made by the runs (window size comes from the command line).
