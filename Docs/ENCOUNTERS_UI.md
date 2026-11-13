# Encounters UI (Milestone 2)

The two opening encounters as playable scenes. Rules stay in `Assets/World/Sim/Encounters` (see `WARD_API.md`, `RHYTHM_API.md`); everything here draws, animates, plays audio and forwards input. Namespace `World.Game`.

## Wiring

- `Encounters/EncounterInstaller.cs`: `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` registers `ward_mira` -> `WardDuelEncounter`, `rhythm_party` -> `RhythmPartyEncounter`, and the AutoPlay script `first_five`. No edit to `WorldBootstrap`.
- Both encounters run under the existing `EncounterHost` (clock paused with `PauseReasons.Encounter`, exact avatar restore). Tab only switches the camera; the encounter HUD draws and reads keys in both views. Esc opens the existing pause menu; its "Leave encounter" aborts (duel = forfeit, nothing recorded; party = stems stopped). Esc on a result screen means Return; `EncounterKeys.ConsumeEscape` + `EncounterEscGuard` close the menu that Hud opens on the same key press.
- While the pause menu is open the duel clock stands still; the rhythm phrase keeps playing (the audio clock cannot pause) but pads are ignored.
- Shared IMGUI pieces: `EncounterUi` (large styles authored at 1080p, panels, essence chips, `Fit` to keep panels on screen at 1280x720).

## Ward duel (`WardDuelEncounter`, `WardDuelStage`)

- Staging: the player's wizard body (ActorFactory) stands 5.5 m in front of Mira, facing her; the first-person camera moves over that wizard's shoulder so both wizards and both Wards are in frame (restored by the host).
- Screens: Setup (keys 1-4 add the pool's essences, Backspace removes, Enter locks; buttons too) -> Active (same weave builder, Enter/Space casts) -> Result (outcome, both Wards revealed, Rematch R, Return Enter/Esc).
- HUD: casts left on both sides, a 2 s recovery bar, "Mira is weaving...", both histories as rows of weave + fractures/echoes/fades totals (never positions), the idle auto-cast countdown from 15 s, refusal toasts. A three-line "how it works" strip shows during setup and the duel while no duel has been completed.
- Visuals: a shimmering hexagon Ward in front of each wizard; crack lines for the best fracture count so far (generic, never tied to a locus); it shatters on a break. Each cast sends a bolt of three orbs coloured by the weave; Mira plays `cast` one-shots; cheer/surprise at the end.
- Results go through `WardProgressApi.RecordDuel(state, outcome)` (in `Sim/Encounters/Ward`), the one sanctioned game-layer write: it touches only `WorldState.Ward`, is deterministic and is saved with `WorldState`. A command was not used because `WorldSimFactory` registers no encounter handlers and queued commands apply only on the next tick, while the clock stays paused for the whole encounter (a rematch must already see the new count, e.g. Light on duel 4).
- Tuning: `WardTuning.Practice()` + `StreamingAssets/world/ward.json` (re-read for every duel). Shipped: Mira's first cast 3-6 s, think 6-10 s, mistake rate 0. Measured (`WardBalanceTests`, 300 seeds, scripted first-time player = candidate filter with 15% random casts, a cast every 5-7 s): wins 54% with three essences, 60% with four. With the PRD cadence (2.5-6 s) the same player wins under 25%. Mira's dialogue already frames it as a practice duel.
- Seeds: `Rng(state.Seed).Fork("ward_duel_<completed>_<n>")`. Test hooks: `WardDuelEncounter.TuningOverride`, `SeedOverride`, `Current`; scripted player `WardBot` (AutoPlay, captures).

## Rhythm party (`RhythmPartyEncounter`, `Audio/StemPlayer`, `Audio/SynthAudio`)

- Staging: the avatar stands on the dance floor facing the decks, turned 24 degrees so DJ Sol is visible left of the highway. Six placeholder dancers, three beat lights; DJ and dancers squash on the beat, cheer on passed blocks. Door music is switched off during the phrase and restored after.
- Screens: count-in (2 s, "4 3 2 1" on the beat grid, four scheduled clicks) -> phrase (4-lane highway D F J K, fall time 1.1 s, holds as bars, hit line, Perfect/Great/OK/Miss pops and sparks, lane flash, block list with PASS/MISS, mix stage "Drums / Bass in / Drums back / Bass up", beat counter and judgement counts) -> during beats 48-60.5 the banner "Choose: <- Bar   Balcony ->" (Left/Right; default bar) with the picked doorway's lights blinking -> result (blocks passed, judgement counts, "Party success!" at 3+ passes, the opened route or "stay on the floor and try again", Retry R, Return Enter/Esc).
- R9 as built (rule change in `PhraseSession`): `Success` = 3+ passing blocks; `RouteOpened` = the chosen route iff the final block passed; `CanRetry` always. An opened route stays open for the session (`WarehouseSet.SetRouteOpen`).
- Audio: `StemPlayer` schedules drums with `PlayScheduled` at `dspTime + 2 s + 0.15 s` (song time 0); bass and other start together, from their loop start, on the bass-reveal downbeat (the chart's bass origin; scheduled from the `BassRevealed` event 3.5 beats ahead). All loop (32-beat clips, 64-beat phrase) and stop with `SetScheduledEndTime` at beat 64. Gains follow `PhraseSession.CurrentGains` with 0.35 s linear ramps. Song time = dspTime - start, extrapolated with real time between DSP updates, monotonic; if the DSP clock does not advance (no audio device) it follows real time so the phrase still plays.
- Stems: `Resources/Rhythm/lv1_warehouse/{drums,bass,other}` (owner's loops; 529655 frames, 48 kHz stereo; import set to PCM, Decompress On Load, Preload, Load In Background off, no Force To Mono via `AgentScripts/EncounterAudioImport.cs`). Missing clips -> `SynthAudio` loops generated from the chart (kick on pad-1 cues, snare on pad-4 cues, bass notes on bass cues, a pad for "other"); missing pack -> `FallbackChart`.
- Input: D/F/J/K KeyDown/KeyUp read in Tick with the sampled song time -> `HandlePress/HandleRelease` -> `PhraseSession.Press/Release`. Calibration `[`/`]` +-10 ms (shown in the footer, saved to PlayerPrefs `World.Rhythm.CalibrationMs`). `GodCam.KeyboardPan` is off during the encounter.
- Test hooks: `InjectPress/InjectRelease(pad, songTime)` (queued, handled through the same path when the song reaches that time), built-in autoplayer (`AutoPlayEnabled`, `AutoSkipBlock`, `AutoRoute`; presses at cue time + calibration), `TuningOverride`, `StemDirOverride`, `Current`.

## Audio safety: the "ADTM" fatal error

Seen once in the editor during a duel (13 Nov): "Fatal Error! ADTM: Attempting to update control while a mix is ongoing, but realtime thread is disabled will lead to a deadlock". The crash logs (this one and an earlier one on 9 Nov in another project) show the cause: Windows' audio device changed or failed, FMOD fell back ("FMOD failed to switch back to normal output, audio output stays suspended until the device can be re-initialized"), Unity then mixed without its realtime audio thread, and the next audio control update (the crash stack is `AudioManager::Update -> DualThreadManager::ControlUpdate`) raised the fatal error. It was not reproduced on demand (it needs the device failure); duel + Tab + pause-menu cycles ran without it afterwards.

How it is avoided now:
- `Audio/AudioGuard.cs` samples `AudioSettings.dspTime` every frame and listens to `AudioSettings.OnAudioConfigurationChanged`. `AudioGuard.CanWriteControls` is false while the DSP clock is stalled, during a device change (until the clock runs steadily for 1.5 s) or while `AudioListener.pause` is set.
- World.Game writes audio controls only when `CanWriteControls` and only when the value changes: stem volume ramps, `PlayScheduled`, one-shots, Stop, door music on/off in encounters.
- `WarehouseSet` (minimal edit outside the encounter folders): the door music's low-pass cutoff used to be written every frame; it is now smoothed in C# and written to the filter only in coarse steps (about every 400 Hz, then once at the target), never while `CanWriteControls` is false.
- No `OnAudioFilterRead`, no `AudioSettings.Reset` or configuration changes at runtime.

## Tests and tools

- PlayMode `Assets/World/Tests/PlayMode/EncounterTests.cs`: `WardDuelTests` (win, rematch, loss, return; Light on duel 4 and abort records nothing, pause menu freezes the duel; Tab mid-duel) and `RhythmPartyTests` (DSP sync + perfect autoplayer -> bar, retry -> balcony, Tab mid-phrase; miss block 0 holds the stage and still succeeds; failed final block -> no route, retry, abort stops audio; synth fallback).
- EditMode: `WardBalanceTests`, updated `RhythmTests` (R9), JSON tuning and progress API tests.
- `AgentScripts/EncounterCaptures.cs` puts Play Mode into each capture state; `AgentScripts/EncounterAudioImport.cs` applies and reports the stem import settings.
- AutoPlay `first_five`: boot tour, accept and finish a duel (scripted player), walk to the decks, perfect phrase choosing the balcony, walk onto the balcony and back to the square; logs `[AutoPlay] first_five OK`.
