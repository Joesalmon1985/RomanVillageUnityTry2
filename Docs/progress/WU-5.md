# WU-5: rhythm, world first

20 Nov 2026, branch `godboard/wu5-rhythm` (from `ba2f82a`, merged with `godboard/main` at `34afb86` for WU-0). Scope: the WU-5 row of `Docs/WORLD_UI_PLAN.md` section 5 except Sol as the hub's guide to the spires (hub package). No sim file changed; beat maps, note types, scoring and windows untouched. The audio DSP clock is still the only timing authority.

## Done

- **Drum-shaped hit areas** (`TouchStrips.ShapeResolver`, `DanceStageProps.DrumAt`): while a dance runs the encounter registers each drum's projected shape (enlarged skin ellipse plus the body down to the ground, never smaller than 10 % of the screen height across). `TouchStrips.PadAt` = shape first, else the strip, so a tap on a drum and a tap on its strip both press that pad, simultaneous touches work, a finger resting on a drum holds the pad. The mouse stands in for a finger while the dance runs. On foot nothing changes (resolver null). Virtual fingers for tests: `TouchStrips.SimulateTouchBegin/End`.
- **Performers by cue family** (`View/Camp/DanceStageProps.cs`, built by the encounter and removed with it, `DanceCircle` not touched): a primitive performer behind each drum; kick = foot braced and crouch, snare = stick raised, bass = lean in, rest otherwise; the pose follows the family of the drum's next cue from the moment it appears until it is judged; a stick dip on each hit.
- **Block torches**: four torches at the stage edge; the playing block flickers, a kept block burns gold, a missed one is a dull ember. Tap one: Sol says, and a ledger card gives track, tempo, block of N, mix stage.
- **Sol's tally**: tap Sol (any time, also after the result) for a ledger card with exact Perfect / Great / OK / Miss, strays, holds kept of total, blocks kept. Counts come from every `JudgementEvent` and equal `PhraseResult` at the end (tested). Uses WU-0 `LedgerCard`.
- **Tuning bell** (`TapAlongCalibration`, pure): tap the bell between phrases (result screen) for eight clicks half a second apart; tap along on any drum, key or the ground; the median offset in ms is applied as `CalibrationMs`, saved in PlayerPrefs (`World.Rhythm.CalibrationMs`, the same key as `[` and `]`) and read by every later dance. Refused mid-phrase (Sol says so).
- **Log seat** (left of the far arc): during a phrase a tap pauses; paused or between phrases a tap previews "Leave the dance?" on the ledger and the second tap confirms (stops all audio at once, ends the dance). Uses the WU-0 `WorldActionMachine` and `IWorldAction`, but a **private machine instance**: the shared `WorldActionController` cancels any open preview while an encounter runs, so the seat cannot use `WorldActions` yet (see next steps).
- **Mid-song pause (owner Q-6)**: `StemPlayer.Pause/Resume`: stops every stem, freezes song time (the audio clock's song time and the session both hold), resumes on a four-click count one beat apart and restarts every stem on one dspTime at the paused position; the song clock stays at the paused time until the count ends, so no judgement window moves. Pads are ignored while paused and during the count; any drum tap (shape) or D F J K, P or Space resumes. Keys: P pauses. Scoring unchanged (autoplay through a mid-hold pause stays all Perfect, tested).

## Tests run (20 Nov, editor 6000.3.28f1)

- PlayMode `DanceTests`: see the end of this file for the final count (new in this package: window edges, two pads at once with a hold and a tap, calibration shifts judgement, tap-along median, real strip / drum-shape / key inputs, performers + torches + tally equal the session, mid-song pause, log seat pause then leave with confirm, tuning bell saved and surviving a reload).
- `RhythmPartyTests` 4/4, `SpireTests` 7/7 (one new: the dance at a spire has props, drum shapes and the seat exit), `WorldUiTests` 6/6.
- Not run: `dotnet test DotNet` (no sim change), the full PlayMode suite, a player build.

## Captures (local, not committed): `Evidence/worldui/`

See the list at the end of this file.

## Not done / open

- Tuning bell, torches, seat and performers exist only while a dance runs (the encounter builds them). The bell cannot be tapped from the hub, and "outside a phrase" means the result screen. Putting them in the standing camp and spire stages needs `DanceCircle` / `CampBuilder` (the hub package's files).
- The log seat uses a private `WorldActionMachine`; once `WorldActionController` stops cancelling during encounters it should become a registered `WorldAction` (one place to change: `TapLogSeat`, `LogSeatAction`).
- Props are unlit primitives (readable at night, plain looking). Poses are rigid body parts, not the Sims-style actors the dancers use.
- A pause inside a hold keeps the hold; a finger lifted while paused ends it as a partial hold when play resumes (a player lifting a finger mid-hold is a real release).
- The hit shapes were tuned for the 844 x 390 view only.

## Final test counts and captures

- PlayMode `DanceTests` **15/15** (10 new, the old five unchanged including `DuelDance_AutoPlay_Passes`), `RhythmPartyTests` 4/4, `SpireTests` 7/7 (1 new), `WorldUiTests` 6/6. consoleErrors 0 from game code at every compile.
- Captures in `Evidence/worldui/` (local only; every PNG header checked against its name): at 844 x 390 `count_in`, `mid_phrase`, `a_miss` (two torches lit, block 2 left alone), `pause`, `pause_tally`, `resume_count`, `ending` (Fire leap), `tally` (after the result); at 1920 x 1080 and 1024 x 768 `count_in`, `mid_phrase`, `pause_tally`.
- Seen in the captures: at 844 x 390 the Sol card sits over the "Paused" label while both are open (the pause label is behind the ledger card).

## Next steps

1. Move the log seat to a registered `WorldAction` when `WorldActionController` stops cancelling during encounters.
2. Put bell, torches and seat into the standing camp / spire stages (hub package) so the bell works outside a dance.
3. Tune hit shapes and prop placement at 1920 x 1080 and 1024 x 768 (positions were checked at 844 x 390).
4. Optional: Sims-style performers instead of primitives; a setting to show strips as footprint stones is WU-6.
