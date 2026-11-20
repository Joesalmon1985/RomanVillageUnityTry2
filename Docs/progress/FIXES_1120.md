# Fixes 20 Nov 2026 (decisions 11 and 12)

Branch `godboard/fixes-1120`, editor 6000.3.28f1. No sim file changed; no rule changed; nothing written to the registry.

## Decision 11: leaving the dungeon returns to the hub spot

`InteriorDirector` now remembers where the player stood in the hub clearing (within `CampLayout.HubRadius` of the hearth, on the lattice, not auto-walking or locked): cell, heading and view (`HubPose`). When the player goes down from the surface into an interior, that pose is kept as the pose to return to (`HubReturn`). Every way up taken on foot or by "Lead me out" / `Interiors.Exit` (stair step, lead-out, exit-now) puts the player on that cell with that heading and view, then spends the pose. The campaign state is not touched (the director only reads the sim).

- With no hub pose (a dev start straight into an interior, a non-hub board) the old behaviour stays: the player ends at the stair's mouth.
- `Interiors.Enter` (the expedition placing a following player) never redirects.
- The pose is not saved with a save file; a load underground falls back to the stair's mouth.
- Test hooks: `InteriorDirector.SetHubPose`, `ClearHubPose`.
- `WorldTour` no longer walks home and teleports: after "Lead me out" it checks `Back(home, ...)` directly (same cell, heading, yaw, on lattice, unlocked, walking view, pointer back).

Tests added: `InteriorTests.WaysUp_ReturnToTheHubPoseTheyLeftFrom_AndWithNone_ToTheStair`, `ExpeditionTests.LeadMeOut_AfterSettingOutFromTheHub_ReturnsToTheHubCellAndHeading` (the existing dev-start `LeadMeOut_FromACompanionCard...` still ends at the stair); `WorldTourTests` now expects the "Lead me out of the crystal expedition" step instead of the walk home.

## Decision 12: the unexplained EditMode failure

Full EditMode run (all editor assemblies): 801 total, 794 passed, 1 failed, 6 skipped (CharacterImportTests: no local art). The single failure was `World.Sim.Tests.GodBoardVirtualProof.TuningStudy_WriteOptions`: "Timeout value of 180000 ms was exceeded". It is not order-dependent and not a rule: the study plays 9 option sets x 48 seeds plus 5 finalists x 4 virtual players (440 s in the committed report, 716 s here) against the 180 s default timeout. Fix: `[Timeout(1800000)]` on that test. Re-run alone: passed in 716 s. (WU-8's two overlay failures were fixed in WU-8; the "unnamed third row" was this study.) Side effect to know: the suite rewrites tracked proof docs (`Docs/*_PROOF.md`, `Docs/progress/*-proof.md`, `G1-3-tuning.md`); those were reverted, not committed.

## Suites (this branch)

| Suite | Result |
|---|---|
| InteriorTests | 15 / 15 |
| ExpeditionTests | 14 / 14 |
| WorldTourTests | 2 / 2 |
| HubTests | 18 / 18 |
| WorldUiTests | 6 / 6 |
| EditMode, whole suite | 801 total, 794 passed, 1 failed (the timeout, fixed, re-run green), 6 skipped |

Console: consoleErrors 0.

## Packaged build

`BuildWindows.Run`: Succeeded, 101 MB, 0 errors. `WorldMVP.exe -autoplay world_ui_tour -novsync` at 844x390 windowed: `[AutoPlay] world_ui_tour OK`; the log shows the player back on cell (12, -16) heading 0 after every destination, including "Lead me out of the crystal expedition". `ProjectSettings/ProjectSettings.asset` showed a line-ending-only change; reverted.
