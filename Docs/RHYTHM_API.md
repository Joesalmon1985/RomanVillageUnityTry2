# Rhythm API (namespace `World.Sim.Encounters.Rhythm`, pure C#)

Rules live in `PhraseSession`; the Unity side only plays audio, draws cues and forwards input.

## Use
```
var chart = PackJson.Parse(Resources.Load<TextAsset>("Rhythm/lv1_warehouse/pack").text); // or FallbackChart.Create()
var s = new PhraseSession(new RhythmTuning { CalibrationMs = saved }, chart);
// every frame, songTime = audio clock seconds, beat 0 = 0.0, count-in = [-LeadInSeconds, 0)
s.Advance(songTime); s.Press(pad1to4, songTime); s.Release(pad, songTime); s.ChooseRoute(Route.Bar|Balcony, songTime) // bool
foreach (var e in s.DrainEvents()) ...
```
- `Press/Release` return the `Judgement` (Kind Perfect/Great/Ok/Miss/Stray/HoldComplete/HoldPartial, Pad, Time, CueIndex, Credit, DeltaMs) or null when ignored. Misses/hold ends arrive via events.
- Events (`PhraseEvent.SongTime`): `JudgementEvent`, `BlockDecided{Block,Credit,Passed,Stage,NextStage}`, `BassRevealed{Beat}`, `RouteWindowOpened`, `RouteLocked{Route}`, `PhraseEnded{Result}`.
- State: `CurrentBlock`, `StageOfBlock(b)` (-1 until decided), `FamilyOfBlock(b)`, `BlockResultOf(b)`, `BassRevealBeat`/`HasBassReveal`, `CurrentGains` (Drums/Bass/Other targets for the current block; ramp ~0.35 s yourself), `RouteWindow` (NotYet/Open/Locked), `CurrentRoute` (default Bar), `IsEnded`, `Result`.
- `UpcomingCues()`: cues of the current and next block with `Time`/`EndTime` (song seconds), `Pad`, `Family` (kick/snare/bass), `IsHold`, `State`, `Credit`. Block b+1 is charted at block b's decision (beat 12.5), 3.5 beats (about 1.2 s) before it starts, so fall time must be at most 1.2 s.
- `PhraseResult`: `PassedBlocks`, `FinalBlockPassed`, `Success` (>= 3 passing blocks and a passing final block), `RouteOpened` (None/Bar/Balcony; None unless Success), `CanRetry` (final block failed), counts per judgement.
- Route window: opens at beat 48, locks at beat 60.5 (a choice at exactly 60.5 is refused); the route opens at beat 64 when `PhraseEnded` arrives.
- `CalibrationMs`: a press at song time t is judged as t - CalibrationMs/1000 (positive if inputs arrive late). Cue times are never shifted.

## Resources (Assets/World/Game/Resources/Rhythm/lv1_warehouse/)
- `pack.json` (TextAsset "Rhythm/lv1_warehouse/pack"), `drums.wav`, `bass.wav`, `other.wav` (AudioClip, 48 kHz stereo 16-bit, 529655 frames = 32 beats at 174 BPM). The stem paths inside pack.json (`../_shared/...`) are source-repo paths; load the three clips by the names above. Owner's own Beat Shaper loops.

## Loop and scheduling contract
- The pack loop is 32 beats; the game phrase is 64 beats, so all three stems loop twice (play them with PlayScheduled and loop, or restart exactly at beat 32). Keep all three sample-aligned from one `dspTime` start.
- Song time 0 = first sample of the stems = beat 0. Count-in occupies negative time (`LeadInSeconds` = 2 s); start the stems at dspTime0 + LeadInSeconds.
- Cue time in seconds = tick * 60 / (bpm * 960) with absolute phrase ticks (the session already gives `Time`). Bass cues play from the reveal beat: position in the loop = (blockStartBeat - origin) mod 32, origin 0 for drums, the reveal beat for bass.
- Stage gains per block come from `CurrentGains`; the stage changes at the block boundary.
- Cues exist only in beats 0-12 of a block; holds are clipped to beat 12; a hold under 480 ticks is a tap.
