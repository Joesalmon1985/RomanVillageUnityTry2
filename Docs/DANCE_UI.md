# Dance circle and camp dressing (package D)

REFACTOR_SPEC 2.E and 8.3. Namespace `World.Game`. Rules stay in `World.Sim.Encounters.Rhythm` (unchanged, see `RHYTHM_API.md`); this package draws, animates, plays the stems and forwards input. Supersedes the "Rhythm party" section of `ENCOUNTERS_UI.md` (the warehouse is gone).

## Files

| File | What |
|---|---|
| `View/Camp/CampLayout.cs` | Where everything stands, from the board alone (`new CampLayout(state.Board)`); dressing list; clearings for the terrain; `Obstruction` clearance check. |
| `View/Camp/CampBuilder.cs` | Builds the dressing: hide tents, a lean-to, drying racks, totems, a wood pile, three villagers (`CampPropInfo` is the pointer's `IInspectable`; `CampLife` plays their idle one-shots). |
| `View/Camp/DanceCircle.cs` | The circle: fire, four drums, Sol's big drum, log seats, six dancers, ember and spark meshes, ending marks and effects. Scenery in normal play; driven by the dance. |
| `View/Camp/CampInstaller.cs` | D's installer: dressing, circle, `WalkGrid` blockers (owner `"dance_circle"`), Mira and Sol. |
| `Encounters/RhythmPartyEncounter.cs` | The dance ("rhythm_party"). |
| `Encounters/EncounterInstaller.cs` | Registers `ward_mira`, `rhythm_party` and the AutoPlay script `duel_dance`. |
| `Tests/PlayMode/DanceTests.cs`, `EncounterTests.cs` (`RhythmPartyTests`) | See Tests. |

## Layout (default board: home corner 70, reserved plot 635)

- Hearth = the home corner (V draws the store pile and hearth there). Spawn: 4 lattice steps along the Forest spoke, facing the prince's start 4 steps further on (on the hex edge, 20 m away).
- Dance circle at the reserved plot centre (Fields sector, slot 2, 45 m from the corner), radius 7 m. `Forward` = from the corner to the fire. Fire at the centre; drums 1-4 on a 4 m circle on the corner side at -67.5, -22.5, 22.5, 67.5 degrees from the dancer's direction (left to right as seen by the dancer); the dancer 6 m from the fire on the corner side facing it; Sol 5.5 m beyond the fire, 14 degrees right of the line (`DrummerAngle`, so the flames do not hide him), with his big drum in front of him; log seats and six dancers on the far half and flanks. The outer drums 1 and 4 carry the kick and snare (most cues) and stand further away, so they are built 1.3 times larger (`DanceCircle.DrumScale`).
- Ending marks: `FireLeapMark` 2.4 m left of the fire, `SpiritCallMark` 2.4 m right (seen from the dancer).
- Mira: 15.5 m from the corner in the open wedge just beyond the prince's hex edge (out of the forest), facing the spawn; at the spawn she is about 25 m away, 20 degrees left of ahead. Her duel (player 5.5 m in front of her, camera 3 m further back) is staged on open ground towards the spawn with a tent, a totem and a drying rack round it.
- Dressing (`CampLayout.Dressing`, `CampProp{Kind, Position, Yaw, Radius}`) stands only in the six wedges between a spoke (local path) and a hex edge (main road), 9-21 m from the corner, plus two totems flanking the way into the circle. `CampLayout.Obstruction(board, p, radius)` is null when a disc keeps clear of every plot (5 + 2 m), stock patch (6 + 2 m), hex-edge line (3 m), home local path (2.5 m), the home corner (7 m), any other corner (4 m) and the dance circle; the circle's own parts use `insideCircle: true`.
- `CampLayout.Clearings`: circles (x, z, radius) the terrain should keep free of trees, crops, rocks and mounds: the dance circle (8.5 m), every prop (+1.5 m), Mira's duel ground, the spawn, the prince's walk and the spawn-to-Mira sightline. Package V's terrain honours them (appendix B.1); until then, crops grow through the circle and a forest tree stands in front of the spawn on the interim terrain.
- `WalkGrid` static blockers: the fire (2.5 m) and each drum (1.3 m); the cell on the way in, 5 m from the fire between drums 2 and 3, stays free.

## The dance (`RhythmPartyEncounter`)

- Start: talk to Sol (`NpcIds.Host`, option "Dance at the fire"); his `Npc.Stage` is the dancer's spot. The viewpoint is placed with `EncounterContext.PlaceAvatar(DancerSpot, DancerYaw, ViewPitch 15)` (restored by the host). If the scene has no circle, the encounter stands a temporary one up from `CampLayout`.
- Same music, chart, `PhraseSession` rules and tuning as the party: stems scheduled on the DSP clock by `StemPlayer` (drums at song time 0 after the 2 s count-in, bass and other on the reveal downbeat, gains ramped from `CurrentGains`); every audio control write still goes through `AudioGuard.CanWriteControls`.
- Cues: a cue appears `FallSeconds` (1.1 s, the lanes' lead) before its time. From then its drum glows (0.25 at appearance rising to full at the cue time), a firelight ring on the skin shrinks from 2.6 times the skin to the rim at the cue time (`DanceCircle.ApproachRings`), and an ember with a short trail flies on a low arc from the fire to the skin, landing on the cue time. A hold keeps the drum fully lit while held, and its tail is a stream of embers still flying in from the fire. Colours: kick orange (drum 1), snare cyan (drum 4), bass purple (drums 1-2) or teal (drums 3-4). `LitAt[cueIndex]` records when each cue's drum first lit (pad, song time).
- Judgements on the drum: Perfect gold flash, big bounce and a wide spark burst; Great green flash and a smaller burst; OK (accepted) a pale blue flash, no burst; Miss and a released hold: the skin goes dark red and grey smoke puffs; a stray press gives a faint white tap. The word rises from the drum too.
- Count-in: "4 3 2 1" with the four scheduled clicks; every drum and the fire pulse on each count, Sol claps.
- Mix level: dancers come up from their seats into the ring as the stage rises (2 at "Drums", 4 at "Bass joins" and "Drums back", all 6 at "Bass up"), hop on the beat higher at higher stages and circle a little round the fire; the fire pumps on the beat. When the bass joins (after a good block, `BassRevealed`), a banner says so and Sol's big drum flashes on every beat.
- Ending choice in the window (beats 48 to 60.5): Left arrow or a tap left of the fire (above the touch band) = "Fire leap" (`Route.Bar`), Right arrow or a tap right of the fire = "Spirit call" (`Route.Balcony`); the chosen mark glows by the fire and the banner shows it. On a successful phrase with the final block passed: two dancers leap the fire with an orange flare, or blue spirit smoke spirals up from the fire (`DanceCircle.LastEffect`); the result panel waits 2.6 s so the ending is seen. Cosmetic, not saved.
- Input: D F J K and touch strips 1-4 (`TouchStrips.Pressed/Held`, P's) go through `HandlePress/HandleRelease`, as do `InjectPress/InjectRelease` and the built-in autoplayer (`AutoPlayEnabled`, `AutoSkipBlock`, `AutoRoute`). `[` / `]` calibration, R dance again, Enter / Esc return.
- HUD: a slim strip (blocks PASS/MISS, mix stage, beat, judgement counts), the count-in number, the ending banner, the result panel, a footer. No lanes.

## Tests (PlayMode)

- `DanceTests`: drums lit every frame exactly when one of their cues is inside [cue time - fall time, judged) or held, each cue's drum first lit within a frame of cue time - fall time on the right pad, autoplayer all perfect; `Approach` and the choice names; ending choice refused before beat 48, accepted and changeable inside the window, refused after 60.5 and after the end, marks and effect follow; every dressing prop (from the layout and from the built meshes) clear of plots, paths, roads, stock patches and corners, circle parts inside the circle; no object named warehouse, cottage, market, bell or square; the fire blocks its cell, two of its neighbour cells are drum cells, the way in stays free; `duel_dance` AutoPlay passes (Mira's duel completes, then a successful dance with Spirit call) and `first_five` is gone.
- `EncounterTests.RhythmPartyTests` (moved onto the circle): DSP sync of the stems and the bass reveal, perfect autoplayer with Fire leap then Spirit call on retry, missed first block holds the stage and still succeeds, failed final block gives no ending, retry, abort stops the audio, synth fallback; viewpoint restored and the village clock held throughout. `WardDuelTests` unchanged.

## AutoPlay `duel_dance`

Look round at the spawn, walk to Mira, accept and finish her duel (scripted player), return; walk round the circle to Sol, dance a successful phrase choosing Spirit call, return; checks the viewpoint is restored and the village clock did not move during either encounter. Logs `[AutoPlay] duel_dance OK`. It uses the interim avatar's `WalkTo` and `Interact` (P replaces both with lattice steps and the pointer).
