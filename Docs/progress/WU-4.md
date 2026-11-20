# WU-4: the council, world first

20 Nov 2026, branch `godboard/wu4-council`. Scope: WU-4 row of `Docs/WORLD_UI_PLAN.md`. No rule or sim file changed.

## Done
- Proposal preview and confirm on the shared WorldAction machine (`CouncilAction`): first tap on a note shows ghost catspaws at both places, listeners turn, both places framed, ledger card; another note moves the preview; second tap commits once; a double shows one ghost with two steps; socket notes preview and commit only their own place. `TapTargetNow` keeps the old one-tap path for tests.
- Recorder with stone table, clay tablet and seal (CNC-U, CNC-STATUS): bank needs preview then confirm (belt seal and leads use the same action); the tablet is an inspect `LedgerCard` with exact per-candidate standings (claims of 3, settled N of M per place, listening now, petition), words and supporter counts only.
- Tower ladder (CNC-EXIT): preview then confirm leaves; resume restores.
- Q-7: coloured ground rings (disc for settled, open ring for listeners, frame round places in play or claimed) and a closer camera on phone-height screens (camera moves off the tower towards the worked places, never into the hearth).
- `WorldActionController` no longer cancels previews while the council encounter runs (menu or map still cancel).
- AutoPlay council scripts use preview then confirm. Capture commands added to `WorldUiCaptures` (council, ctap, ctapnow, cadv, cbank, cbust, cinfo, ccloser, cleave).

## Tests
- 20 Nov, final code: `CouncilTests` full run (40 tests, PlayMode): **40 / 40 passed** (about 20 minutes, includes the AutoPlay fixtures). The first full run (before the fixes) was 39 / 40: `Wu4_SocketNotes_NeverCommitHalfATray` failed twice over, both test bugs (it tapped a note before the notes were on screen, and counted the two runners the test itself had staged as "moved"); fixed in the test, no rule or sim change; it passes alone.
- `WorldUiTests` 6 / 6, `HubTests` 9 / 9 (after the card and totem changes).
- Targeted `CouncilTests.Wu4_*` 10 / 10.
- Packaged player rebuilt (`BuildWindows`, 22 s, 0 errors) and run at 1280 x 720: `council_fixture` OK, `council_decline` OK, `council_full` OK, `board_first_round` OK.

## What changed after review
- Pole: the tall banded pole in the middle of every council view was the camp's own totem (a 7.8 m prop at the hearth, found with a new `cpole` capture command that lists what a screen ray crosses). While the council camera is up `CouncilEncounter` hides every renderer named `Totem` taller than 3 m within 60 m of the camera, and shows them again in `End`.
- Cards: the shared `LedgerCard` got a `DockTop` switch (and `LedgerLayout.Compute(..., dockTop)`); the council sets it for the encounter. The card is then a wide strip along the top edge of the safe area (sky and tree line), clear of the places (middle), the notes, recorder, tablet and ladder (foot). Default layout of every other screen is unchanged.

## Captures (local, `Evidence/worldui/`, PNG sizes checked OK)
`c_{choose_preview,tablet,bank_preview,after_bank,after_bust}_{1920x1080,1024x768,844x390}.png`, retaken on the final code, night, closer camera automatic (on at 844 x 390). Capture sequence: `council; ctap note 0; ctap tablet; ctapnow note 0; ctap recorder (twice); cleave; council; cbust`.

## Open
- The two framed places are often at opposite edges of the fan, so at 844 x 390 both are small (ring and ghost visible, not large).
- Notes are small stones at the foot of the picture on phone size.
- Windowed AutoPlay runs leave the Unity window-size registry key (`HKCU\Software\Joe Salmon\WorldMVP`); not deleted here (the shell refused). Delete before the owner double-clicks the exe.
