# POLISH P2b: duel, healer's lodge, council

20 Nov 2026, branch `godboard/polish-games-b` (from `e71e3d0`). Presentation only: no rule or sim file changed. `Actors/*`, hub people, `Board/GodBoard/*`, `Historic/*`, `War/*` untouched.

## Duel
- The practice duel now uses the same wide over-the-shoulder camera as the campaign duels (`WardDuelEncounter.FrameDuelCamera`, one routine for both): caster, opponent, posts and the stones row in frame, the whole caster body above the stones (`CameraBack` 4.6 m, `StageLiftDegrees` 4) so the stage sits below the card strip.
- The referee's card is docked along the top edge (`LedgerCard.DockTop` is set for the duel's life and cleared in `End`), so it no longer covers the stage.
- Referee gestures (`DuelWorldProps.RefereeGestures`): raises a hand at the start, a hand again after a rematch, points at the winner at the result (uses `RiggedActor.PointAt` when the body is rigged, else a cheer one-shot). At the result he stands further to the side (1.1 m) so Mira is not hidden.
- Opponent beads and tally: unchanged (tap to read).

## Healer's lodge
- Map table is square (2.9 x 2.9 m) and the island fills it (margin 0.2 m); walk-solid radius 1.85 m.
- Lean view (`LodgeTable.BeginLean / EndLean / LeanEye`): a tap on the table or a pebble puts the camera above the table (pitch 70), the island fits the left 56 % of the safe area, the card is a right-hand column (`LedgerCard.DockRight`, new). Walking, auto-walk or Esc stands up (the walker returns to its cell).
- While leaning, the 48 pebbles are registered as enlarged targets (`WorldHitTarget.MinSide`); `WorldActions.PickEnlarged` now breaks ties by the nearest centre; `PointerInput` lets the enlarged pick override the table collider only while leaning.
- Mixed figurines stand on a wider ring (0.068 m for three); the tunnel is a pale-violet-lipped black groove with a glowing seam.

## Council
- `CouncilScene.Frame`: when the two framed places are far apart the closer camera now pulls back until their span fits the picture (`SpanOf`), wider vertical cap (`MaxVfov` 58), more margin round the places. Far pair (47 m) now holds both places at 844 x 390.
- Each previewed place gets a pale pillar (`SyncPlaceBeacons`) and the ghost catspaw scales with the picture (12 % of its height). Notes are cream, unlit and about 10 % of the picture's height at the foot, spaced wider. The tower platform renderer is hidden in the forward view (its edge showed at the foot of the wide picture). `DockTop` card kept.
- Side effect: the closer camera may now stay at the tower (push 0) for far-apart places; `Wu4_GroundRings...` was changed to assert that the worked places are in the picture instead of push > 1.

## Tests (editor 6000.3.28f1, PlayMode)
New: `WardDuelTests.P2b_RefereeCard_DoesNotOverlapTheStage_At844x390...`, `PlagueViewTests.P2b_LodgeTable_LeanView_PebblesAreHitTargets_At844x390...` (all 48 pebbles at least the minimum, a tap at each centre picks it, island inside the safe area and clear of the card, figurines apart), `CouncilTests.P2b_PreviewFraming_HoldsBothPlaces_At844x390_AndNotesAreBig`.

| Suite | Result |
|---|---|
| WardDuelTests | 6 / 6 |
| CampaignDuelTests | 13 / 13 |
| PlagueViewTests | 10 / 10 |
| WorldUiTests | 6 / 6 |
| HubTests | 18 / 18 |
| WorldTourTests | 2 / 2 |
| CouncilTests (full, 41) | 40 / 41 on the first run (`Wu4_GroundRings...`, the push assertion above); that test then 1 / 1 after the change; the other 40 were not re-run after that test-only edit |

## AutoPlay (rebuilt player, 0 errors, 844 x 390 windowed)
`world_ui_tour`, `plague_epidemic`, `council_fixture`, `council_decline`, `council_full`, `duel_friendly`, `duel_keeper_win`, `duel_keeper_loss`, `duel_dance`: all exit 0, no exception in any log (`Builds/logs/p2b_*.log`, not committed). The ProjectSettings.asset line-ending change from the build was reverted. The Unity window-size registry key was not touched (never written or deleted here); delete it before the owner double-clicks the exe.

## Captures (local, never committed): `Evidence/worldui/`
`p2b_duel_before_844x390` (and `..\wt-wu7\Evidence\worldui\wu7_*` as the old set), `p2b_duel_after_setup_*`, `p2b_duel_after_active_*`; `p2b_lodge_lean_*`, `p2b_lodge_node_*`, `p2b_lodge_outside_*` (before: `..\wt-wu3\Evidence\worldui\lodge_*`); `p2b_council_before_*`, `p2b_council_after_*` (before also `..\wt-wu4\Evidence\worldui\c_*`). Each at `844x390` (simulated notch), `1024x768`, `1920x1080`; sizes checked with the driver `check`. Drivers: `WorldUiCaptures` (`cfar` new), `WU3Drive` (`taptable`, `tapnode N`, `standup`, `leanstate` new).

## Open
- At 4:3 the lodge map is smaller (the 56 % share minus the notch inset); the card just clears it.
- Far council places are still small people; the pillars and rings carry them.
- Opponent beads and tally remain small on a phone.
- The friendly and lethal duels use the new 4.6 m camera too; only checked through tests and AutoPlay, not by capture.
