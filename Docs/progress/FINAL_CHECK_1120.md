# Final check, 20 Nov 2026

Worktree `wt-final`, branch `godboard/final-check`, cut from `godboard/main` at b842c87 (after balance F1, dungeon return, Historic clay, P1, P3, P2a, P2b).

## Compile

Clean: `script recompile_status` completed, no errors, `consoleErrors` 0. The hand-merged `LedgerLayout.Compute(..., dockTop, dockRight, floorTop)` and its forwarding overloads compile; the lodge right column, council top dock and ledge-floor cards are covered by the suites below (no failure in them).

## Suites before fixes (b842c87)

| Suite | Result |
|---|---|
| `dotnet test DotNet` | 774 passed, 0 failed, 0 skipped (24 m 49 s) |
| Unity EditMode (full) | 806 total: 800 passed, 0 failed, 6 skipped (no local character art, public repo) |
| Unity PlayMode (full, async, 3019 s) | 309 total: 297 passed, 5 failed, 7 skipped (7 `ActorTests` skipped: no local Sims art) |

PlayMode failures:

1. `LightingTests.LateGame_LightBudget_IsNotExceeded`: 7 per-pixel point lights at camp 0, budget 6. Reproduced alone. Real merge interaction.
2. `ExpeditionTests.Pedestal_OffersMirasAct_AndTheSecondTapSendsOneCommand`: expected 1 command, was 2.
3. `HubTests.Healer_AndAssistant_Work_AndTheHealerTurnsToTheMapTable`: healer faces the map table, angle 55.5 (limit 15).
4. `PlagueViewTests.Shrine_Tap_Previews_ThenBanishesOnce_AndTheCharmCools`: the guard swallows a burst (expected Ignored, was Previewed).
5. `WorldUiTests.Altar_FirstTapPreviews_SecondConfirmsOnce`: double-tap's second half expected Ignored, was Confirm.

Note: during the full PlayMode run the packaged AutoPlay scripts were running in parallel on the same machine, so the tap-debounce timing tests (4, 5) are suspected load flakes; see the status of the reruns below.

## Fix

- `StandingBell` (P2a): its lantern is a real per-pixel point light outside the `WorldLights` budget, always on. It now burns only while the camera is within 30 m of the bell, so a far camp's light budget is not shared with it. `LightingTests` 5/5 after the fix. No rule change; no test changed.

## Reruns after the fix

See the end of this file.

## AutoPlay (packaged player, windowed 844x390, `-novsync`)

Built with `AgentScripts/BuildWindows.cs` (Succeeded, 102 MB, 0 errors, 76 s). All exit 0 and log OK:
`board_first_round`, `board_trade_discard`, `plague_epidemic`, `war_burn`, `board_to_historic`, `world_ui_tour`, and the older `boot_tour`, `walk_tour`, `first_scene`, `duel_dance`, `save_load`, plus `phone_frametime`.

Frame time (`phone_frametime`, 60 s, dev.godboard seed 7, 844x390, vSync off, run with the editor idle): average 543 fps (1.84 ms), worst frame 21.1 ms.

## Reruns after the fix (fixture by fixture, async)

| Fixture | Result alone |
|---|---|
| `LightingTests` | 5/5 passed (the budget test fails before the fix, passes after) |
| `ExpeditionTests` | 6/6 passed (failure 2 was a load flake) |
| `WorldUiTests` | 6/6 passed (failure 5 was a load flake) |
| `PlagueViewTests` | not confirmed: the rerun did not finish inside the time limit (over 10 min); failure 4 (burst guard) is the same debounce kind as failure 5 and is suspected to be the same load flake |
| `HubTests` | not rerun (time limit); failure 3 (healer facing 55 degrees, limit 15) is unexplained and may be a real merge interaction with P3's lodge work or P2b's lodge table; open |

After the fix the suite counts are therefore not fully re-established: PlayMode 309 total, at best 1 real failure fixed and 2 confirmed flakes; 2 failures (Hub healer facing, Plague burst guard) remain unconfirmed. EditMode (806) and dotnet (774) were green before the fix and the fix touches only a PlayMode light toggle.
- Healer turn test (HubTests.Healer_AndAssistant_Work_AndTheHealerTurnsToTheMapTable): cause was the test, not the game; it waited 90 frames but the actor turns at 300 deg/s of game time, so a fast editor (~3 ms frames) covered only ~100 deg; the test now waits up to 2.5 s of game time for the bearing under 15 deg. No game code changed.
