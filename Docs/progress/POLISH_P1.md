# Polish P1: people animation and behaviour (branch godboard/polish-people)

Written 20 Nov 2026. View work only (`Assets/World/Game` reads state, never writes it); no rule, council state or sim file changed. Everything works with the placeholder bodies (committed primitives); the local rigged bodies are untouched (`RiggedActor` keeps its own clips; the new one-shot keys simply return false there).

## What changed

**Shared gait layer** `Actors/Gait.cs` (static, no state): the phase advances by ground covered, half a cycle per step, with the step length of a rigid leg swinging +-A (2 L sin A), so a planted foot stays put at any speed. Amplitude grows with speed (16 degrees strolling, 30 at 3 m/s, 44 running from 3.6 to 5.2 m/s); walking dips the hips at double support (inverted pendulum), running lifts in flight and leans forward; arms counter-swing (pumped harder when running); a little hip roll. `Seed01(id)` gives each person a stable phase.

**Placeholder actor** (`PlaceholderActor`, used by every `Npc` and the hub's camp figures, council crowds, dancers):
- Two-segment legs (thigh, and a shin on a knee pivot): the knee bends while its leg swings forward (38 degrees walking, 75 running), lifting the foot clear; seated people fold the shins down.
- Walk and run from that gait, driven by the root's measured motion (teleports ignored); legs come together when the walker stops; turning on the spot shuffles a few small steps; legs are counter-rotated against body pitch, roll and the idle weight shift so the feet stay planted.
- Idle life with a per-person phase: breathing, slow irregular weight shift from foot to foot, glances (the head turns to look somewhere and back), and fidgets every 5 to 13 s (scratch the head, hands on hips, rub the hands, roll the shoulders). Idle is damped to a quarter under held poses, so the council crowds keep their pose language; fidgets never run during a pose, a point, a carry or talk.
- `Talking`: hand beats while speaking (right hand leads, left joins, palms open now and then, head nods). `Npc` sets it while one of its bubbles shows, or while it has the word in a `ConverseGroup`.
- `LookAt(point)`: the head turns (clamped 70 / 25 degrees); the head also leads every turn. `Npc` makes a standing villager look at the player within 9 m (first-person view, no encounter), else at its `LookPartner`.
- Carry poses `SetCarry(Shoulder | Arms, colour)` (alpha 0 = pose only, the caller's prop).
- New one-shots: `strike` (two-handed staff blow), `block` (guard), `gesture` (explaining sweep), `nod`.
- Off-screen actors (no camera sees them, no one-shot running) animate every fourth frame with the summed time (`OffscreenLod`, off in the new tests).

**Jointed mesh figures** `Actors/MeshFigure.cs`: torso, two legs, two arms on pivots from shared meshes, driven by the owner with the ground covered per frame; holds Swing, Load (arms forward round a load) and Work (chopping / gathering swing). Used by:
- collecting villagers on the board (`CollectWalkers`): stride instead of the old bob, gather with a work swing, carry the load in both arms on the way back; meshes now cached per people and good (they were made per walker before and never freed);
- Historic carriers, woodcutters and foresters (`HistoricDirector`): stride, load held in front, chop/dig at the tree; `CarrierLegSwing` probe.

**Soldiers** (`SoldierView`): the walk phase now follows the ground covered (clamped to a sane cadence, a march on the spot keeps stepping); limp, actions and death unchanged.

**Life touches**
- Temple builders: a plank rides on the right shoulder with the arm up holding it; at the scaffold they turn to it and hammer for about 4 s; at the stack they stoop for the next plank.
- Duel ring: the lunger swings the staff (`strike`), the other raises a guard (`block`); the referee turns to the fight and gestures every third exchange; onlookers clap or cheer now and then; referee and onlookers follow the current lunger with their heads.
- `ConverseGroup` (wordless conversations): the turn passes every 2 to 5 s; the speaker gestures and looks at a listener, listeners look at the speaker and now and then nod or react. Used by Mira's two expedition companions and by every pair of the hub's camp figures standing within 4.5 m.
- The runner already ran (8 m/s) and now reads as a run (wide stride, forward lean); Mira, the keeper, steward, healer, assistant, prince and villagers get the idle life, talk gestures and the look at the player automatically.
- Board ledge envoys rock from foot to foot as they walk in, then shift their weight and bow now and then while waiting.

## Frame time (packaged, 844 x 390, `-autoplay phone_frametime`, dev.godboard seed 7, 60 s, vSync off, desktop)

| Build | Runs, alternated with the other build (avg ms) | Median |
|---|---|---|
| Base (`7ab5606`, built from the base sources) | 2.02, 2.73, 1.73, 1.99, 2.00, 2.30 | 2.01 |
| P1 without knees, off-screen LOD | 1.90, 2.35 | |
| P1 final (knees, LOD) | 1.90, 3.00, 2.17 | 2.17 |
| (P1 before the off-screen LOD, not alternated) | 2.33, 3.99 | |

Run to run noise on this machine is about +-0.5 ms (other Unity editors were running for other agents), larger than the difference between the builds: about 2.0 ms before and 2.1 to 2.2 ms after, so at most about 0.15 ms, not separable from the noise. Worst frames 15 to 34 ms in both builds. Logs: `Builds/logs/ft_before.log`, `ab_Base_*.log`, `ab_After_*.log`.

## Tests

New `PeopleAnimationTests` (PlayMode, 6): the walk cycle moves the limbs while walking and they come together when standing; steps follow ground covered (faster = more and longer steps); running strides wider and leans; idle phases differ between people and standing people shift weight at different times; talk lifts the hands, LookAt turns the head, carry load shows and goes; mesh figure strides with distance and stands still.

PlayMode, run one fixture at a time (async) on the final code: `PeopleAnimationTests` 6/6, `HubTests` 18/18, `GodBoardTests` 10/10, `GodBoardVillagerTests` 7/7, `DanceTests` 15/15, `ExpeditionTests` 13/13, `WorldUiTests` 6/6, `HistoricViewTests` 6/6, `WarViewTests` 5/5, `ActorTests` 1 passed, 7 ignored (no local rigged art in this worktree). `CouncilTests` 40/40. After the knees were added: `PeopleAnimationTests` 6/6, `HubTests` 18/18, `DanceTests` 15/15, `WorldUiTests` 6/6, `CouncilTests` 40/40 again. consoleErrors 0 from game code after every compile. Not run: the full PlayMode suite, EditMode (no sim file changed).

## Captures (local, not committed) `Evidence/polish/`

844 x 390 with the simulated notch: `hub_spawn_844.png`, `crowd_844.png`, `walk_0..7.png` (8 frames 0.1 s apart, sheet `walk_sheet.png`), `builders_844_0..2.png` (plank on the shoulder), `ring_844_0..3.png` (strike and guard), `collect_844_0..3.png` and `collect_side_844.png` (collecting villagers striding), `carrier_844_0..7.png` (Historic carrier's legs, sheet `carrier_sheet.png`), `carrier_844.png`. 1920 x 1080: `hub_spawn_1920.png`, `crowd_1920.png`, `builders_1920.png`, `ring_1920.png`. Driver: `AgentScripts/PeopleCaptures.cs` (commands in `Temp/p1_cmd.txt`; note each `script run` is a new assembly, so the capture camera is found by name).

## Open, for P2
- Arms are rigid (no elbows), so carry and hammer poses are approximate; the board and Historic mesh figures have no knees either.
- Historic carriers often run (their jobs move them at 5+ m/s); they still go in straight lines, not along roads. A gait speed cap or a road path would calm them.
- The capture camera views of the duel ring are blocked by the ring's wall; the sparring reads from the player's view but was not captured from it.
- Soldiers' death is still the sink-and-shrink (G3-3); collapse with a fall would read better.
- Healer and assistant could work (grinding, sorting) instead of idling; the steward could address the bench.
- The `ConverseGroup` pairs are by distance only; the hub has few standing pairs besides the camp figures and the companions.
- The rigged bodies (local only) do not know `strike`, `block`, `gesture`, `nod` (they fall back to nothing); map them to clips when that art is used.
