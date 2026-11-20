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
- New `CouncilTests.Wu4_*` (10): 10/10 passed on the last targeted run (after fixing the test staging for socket notes and claimed place).
- NOT confirmed: the full `CouncilTests` run (about 40 tests, includes AutoPlay) was started on the final commit and had not finished when this note was written. `HubTests`, `WorldUiTests`, the rebuilt-player AutoPlay run were NOT run.

## Captures (local, `Evidence/worldui/`, PNG sizes checked)
`c_{choose_preview,tablet,bank_preview,after_bank,after_bust}_{1920x1080,1024x768,844x390}.png`. Taken before the last note-scale tweak (notes now 1.4x). Night only.

## Open
- At phone size the pushed camera shows a tall banded pole prop close up; the places stay small when both are far apart.
- Notes tiny on small screens; tablet and ledger cards cover the middle of the picture.
- Re-capture after the final tweak, run the remaining tests and AutoPlay, revert ProjectSettings line endings if changed.
