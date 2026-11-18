# Council UI (the village election and active listening, Unity half)

Namespace `World.Game`, code in `Assets/World/Game/Council/`. The rules are in `World.Sim.Encounters.Council` (see `COUNCIL_API.md`); this layer draws, animates, takes input and sends commands. It never writes `WorldState.Council` in normal play. Content text comes from `council_content.json` (opening, progress, setback, revisit, agreement, acknowledgement lines, used sparingly). The owner's brief is not copied here.

## What the player sees

The election is a village, not a board. The player accepts and climbs a lookout tower near the home fire with their candidate; the camera is a perspective camera on the tower top (5.4 m above the ground), looking out over eleven dispute places at normal world size. The candidate stands at the foot of the tower at the lower left of the picture, the aide (with the bench lantern) at the lower right on a rival's turn.

**During a candidate's sitting the whole village shows only that candidate's standing.** At each place, of its people (3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3 including the lead):

Each place has three zones, so position and body language alone tell the three states apart (bigger gestures, no text, no markers):

- **Settled** (as many as the candidate's banked progress there): a tight block, shoulder to shoulder, on a low stepped mound on the near-left of the prop (tower side), all facing the lookout, both arms overhead waving slowly and bouncing (`ActorPose.Cheer`), now and then clapping overhead (`ClapHigh`). They stand in rows of up to 5, front row first, each row filled from the middle out.
- **Listening** (won this sitting, not banked): a half-circle (rows of 5, 8, 11) in a clear patch of lighter ground on the near-right of the prop, round the catspaw, bodies leaning well forward (26 degrees) towards it, one hand cupped to the ear or at the chin, shifting weight, heads glancing back now and then (`ActorPose.Listen`). The catspaw stands on a low plinth with both arms spread, addressing them (`Address`).
- **Undecided** (the rest): spread in two loose rows behind the prop, side-on or turned away, arms folded or hands on hips, weight back, some shaking the head now and then (`Doubt`); one in five sits.

Moments: **bank**: the village keeps showing the banker for 5 s after the bank; the lead holds out a small folded note (`Offer`), the first banked listener jogs over waving (`Joining`), takes it (a hand-sized note stays in their hand) and joins the block throwing both arms up; the rest follow. **Bust**: the listeners straighten, throw a dismissive arm, turn their backs and walk behind the prop with drooped shoulders (`Dismiss`, `Droop`) while the block keeps cheering (shown for 4.5 s before the turn passes). **Secured dispute**: everyone at the place is in the block, cheering and facing the lookout. **Turn passing**: people re-form for the next candidate (settled ones jog). Poses are an additive API on `PlaceholderActor` (`Pose`, `PoseSeed`, `Arm(right)`), default `None`.

A catspaw is a person in the candidate's colour with a coloured head band; it runs to the places being worked this sitting. A dispute secured by someone has all its people with the winner. When the turn passes, the village re-forms for the next candidate: people turn, shift and cross the ground. Rival turns play from the wide view with the same re-forming.

Nothing floats and there are no counters, markers or text: only speech bubbles (the lines of `council_content.json` and a three-line onboarding).

## How it works

- **Site** (`CouncilSite.Find`): tries 12 directions for a fan of places; the lookout tower stands 18 m behind the home fire, the grain dispute's storehouse place 10 m beyond the fire, the others at 23 to 56 m (closer than before) on the clearest open ground (camp layout obstruction test, no plots, no hills or mountains). `LandViews.AddClearing` clears trees, crops and rocks under every place, the tower foot and the bench set; solid props are registered with `WalkGrid.AddSolid` (owner "council").
- **Places** (`CouncilScenePlaces`): flat-shaded primitives only: water's edge with a landing and paddle (2), boundary stakes and rope (3), forest edge with trees and a sapling (4), builder's workyard (5), healer's shelter with pots (6), storehouse with baskets and sacks (7), quarry face (8), mill stone (9), stone-ringed spring (10), the track with a log roller (11), keeper's post with notches (12). Each place has a fire basket so it reads at night.
- **People** (`CouncilScene`): 83 `PlaceholderActor`s (`Person`), scale 0.93 to 1.06, varied clothing, a golden-spiral home each, pairs facing each other, every fourth sitting (the actor's new `Seated` pose). The last person of a place is its lead (the one who speaks). The state of every person (`PState`: undecided, listening, settled, away) is computed each frame from the engine state for the shown candidate: settled = banked place, listening = the catspaw's place this sitting minus banked, the lead last. They walk (about 3 m/s) with solid-prop push-out and turn on arrival. After a bust the listeners walk back and away for 3 s, then home; the settled stay.
- **Camera** (`CouncilEncounter`): one tower camera; `CouncilScene.Frame` gives yaw, pitch and vertical field of view that fit the places in play (the settled block on the left through the catspaw's patch on the right) at the screen's aspect ratio; the pitch keeps the candidate (up to 20 m out, bottom of the picture) in frame and, in the wide view, pushes the camp's fire ring to the bottom edge. While pressing on, the frame holds every place with a catspaw, which is the risk. Wide (all eleven) when nothing is in play, the places of the proposals while choosing, the catspaws' places while pressing on, the place where something just changed for 3 s after a bank, bust or secured dispute. Smoothed over about half a second. The candidate and the aide stand where the camera's lower-left and lower-right screen anchors meet the ground, so they stay in view.
- **Choosing**: after a push the candidate holds out a small folded note for each legal proposal (one per tray that commits both advances, one per socket of a tray that offers a choice of one), each with a tiny stamp (the carved miniature of the place) per place it touches; the candidate points at the places of each proposal in turn (arm pointing, the lead raises a hand, the gathering turns to the tower) and the camera frames them. Tap a note, or the place of a proposal when exactly one proposal reaches that place (when several do, for example a double and two single advances at one place, only the notes decide).
- **Press on / bank**: tap the candidate's raised hand or a catspaw to press on; tap the seal at the candidate's belt or the lead of a place with listeners to bank (the same seal accepts a bust; the petition roll at the other hip spends the petition).
- **Taps**: `CouncilScene.Targets(camera)` gives every tappable thing as a screen circle (finger radius at least 6 percent of the screen height); `Pick` takes the circle the point is deepest in. Taps are swallowed for 0.45 s at the start, 0.3 s after a command, during camera turns and while the scene is still moving; the engine refuses stale pushes anyway, so a held or repeated tap cannot replay a move. After each accepted command the village clock is stepped one tick. Rival turns are paced by `CouncilAiStep` once per action (`CouncilEncounter.AiDelay`, 0.75 s), waiting at most 2.5 s for the crowd to settle.
- **Left view (the listening bench)**: tap the aide's lantern on a rival's turn (or with a burst open), or press A or Left. The aide, one speaker waist-up with a bubble, the next speaker behind, two slabs (Same: two leaf seals; Different: a leaf and a gate), the candidate's lantern, firelight beyond. The leaf seals are now plainly leaf-shaped with a stem and lighter so they read at night. Gate and burst rules are unchanged. The bench set stands 40 to 120 m behind the tower on its own open ground.
- **Approach** (`CouncilDirector`): unchanged: the asking candidate walks up and asks; accept leads the player to the foot of the tower and opens the encounter; decline lets the four play in the background. The approach never competes with the prince. While the council is open `CouncilBubbles.Sweep` closes every bubble that is not the council's.
- **Leaving**: the pause menu's "Leave encounter" aborts the encounter; `End` sends `CouncilLeave`; state, banked support and a pending push stay. Sitting down again sends `CouncilResume`. Saving and loading resume exactly because everything is in `WorldState`.

## Launch steps

- Integration entry point: `CouncilDirector.Install(this)` in `WorldBootstrap.Awake` (one line). The encounter id is `council`.
- F1 tool "Council": makes the home village eligible at once (developer staging), asks now, or stages the example proposals (4+9, 5+8, 6+7) and opens the council.
- Fixture for tests: `CouncilFixture.StageExample`, `StageValues`, `StageElection`, `MakeEligible` (all edit state directly; tests, F1 tool and scripts only).
- AutoPlay (`-autoplay NAME`): `council_fixture`, `council_full`, `council_decline`; they press on and bank by tapping the candidate's hand, catspaws, seal and leads, and choose by tapping notes.
- Capture driver: `AgentScripts/CouncilCaptures.cs` (commands in `Temp/c_cmd.txt`: `size W H`, `stage`, `tap NAME`, `focus T1,T2`, `info`, `targets`, `hour H`), then `unity command capture.game_view`.

## Files outside the council folders

- `Assets/World/Game/Boot/WorldBootstrap.cs`: one line, `CouncilDirector.Install(this);`.
- `Assets/World/Game/Actors/PlaceholderActor.cs`: a `Seated` flag (legs forward, body lowered).
- `AgentScripts/CouncilCaptures.cs`: capture and inspection driver.

## Tests

`Assets/World/Tests/PlayMode/Council/CouncilTests.cs`. See the results at the end of this file.

### Results (15 Nov 2026, Unity 6000.3.28f1)

- Full PlayMode run: 108 tests, 108 passed (about 52 minutes; the council's 29 tests take about half of that). EditMode was not re-run by this package (no change to `Assets/World/Sim`).
- 29 council tests: left-view targets inside the safe area; village targets on screen and distinct at 1920x1080 and 844x390 (choosing and ready phases, measured with render-texture cameras); exact crowd counts 3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3 (83 people, one lead each, normal size); nothing floating or screen-space; example proposals 4+9, 5+8, 6+7 held out as notes; one move per tap under repeated taps; a double advances one catspaw two places; listeners and settled counts match the engine after the advance and after the bank; banked support re-forms when the turn returns to the player; a bust returns only the unsettled; petition spend and accept; listening Same/Different with response times passed through, wrong answers, petition earned; draws unchanged after visiting the bench; leave and rejoin; save and load mid-push; camera, clock and avatar restored; other bubbles swept; no election with fewer than four residents; approach accept, decline and waiting for an open bubble; the dance and Ward duel still start; the three AutoPlay scripts (`council_full`: 37 banks, 6 busts, one listening burst with the petition earned and spent, 129 pushes, 43 sittings, a winner).
- Captures at 844x390 and 1920x1080 at night (`Evidence/screenshots/c*.png`, `p*.png`, git-ignored).

### Readability (honest)

- Wide view (all eleven places, nothing in play): it reads as a believable settlement: the fire, tents, eleven gatherings with fire baskets, props that tell the places apart at a glance (pond, stakes, trees, workyard logs, shelter, quarry blocks, mill stone, spring ring, track, post). At 844x390 the near places (20 to 30 m) show people about 40 px tall, the far ones (50 to 62 m) 15 to 20 px: a group's presence and spread read, individual posture does not.
- Choosing: the camera follows the proposal the candidate is pointing at (two places, 30 to 45 m, people 25 to 35 px). The pointing arm and the three held notes read; the lead's raised hand and the gathering turning to the tower are small at that distance. Which dispute is being offered is told by the camera framing and the stamp-sized carved miniatures on the notes (readable only at 1920x1080).
- Standing: with several people at one place, the settled huddle (close, turned to the tower, clapping now and then) and the leaning arc round a catspaw are different shapes, and the catspaw is in the candidate's colour with a coloured band. At 844x390 at 40 m the two groups are distinguishable by where they stand, but "relaxed" versus "leaning in" is a posture difference too fine to see at that size (the actor has no lean pose; the arc is only a position and facing).
- Bust: the unsettled listeners walk back and away over about 3 s; the settled stay. This reads as movement, less so as a still picture.
- Rival turns: the rival's candidate (own cap colour) stands at the tower foot and the village re-forms to the rival's standing; people cross the ground. Readable from the wide view as groups changing places; the count at each place is only readable by looking close.

### Not done or limited

- Rival delegates with cords or notched sticks and the spoken risk and bank-offer lines were dropped on the owner's later instruction; only one candidate's standing is shown at a time.
- The listening arc has no lean-in pose, and a bust shows no one handing back a note (there is no note handover at a bank either: banking shows the listeners joining the settled huddle).
- Places are placed by a search that can fall back to relaxed ground (reported in the log as "N places on relaxed ground"); on the default seed two places sit on ground the search rated relaxed. The fan direction (12 tried) is chosen per board.
- The tower base is 18 m behind the home fire and the village shares ground with the camp's dressing; places partly under the camp's paths are cleared with `AddClearing` rather than avoided.
- Candidate and aide stand where the camera's lower-left and lower-right screen anchors meet the ground, so they walk when the camera turns; at the widest framing they are 10 to 14 m from the camera.



## Readability pass (bigger gestures)

What changed: `PlaceholderActor` gained held body-language poses (cheer, listen, doubt, address, dismiss, droop, offer, joining) and an overhead clap (`cffb07e`), and the council scene uses them for the candidate, the catspaws, listeners, the settled and the undecided (`7432525`). Fixes made while finishing it (18 Nov 2026): the camera tilts down until the candidate's and aide's lower-corner anchors fall within reach, so their tap targets stay on screen at wide and phone aspects; crowd homes are pushed out of the places' scenery so every figure can reach its spot and the scene settles; a figure the scenery holds back no longer counts as walking.

### Results (18 Nov 2026, Unity 6000.3.28f1)

- PlayMode `World.Game.Tests`: 109 total, 109 passed, 0 failed (one full run, about 28 minutes). EditMode `World.Sim.Tests`: 519 total, 519 passed, 0 failed.
- One test was corrected, not the view: `BankedSupport_ShowsAgain_WhenTheTurnComesBackToThePlayer` asserted while the village was still holding a rival's standing for 5 s after that rival's bank (`HoldShown`), so it compared the rival's settled people with the player's banked places. It now waits for the player's seat to be the one shown, and leaves claimed (secured) places out of the sum.
- Packaged AutoPlay: `council_fixture` OK on `CouncilPlaytest.exe`. `council_full` and `council_decline` cannot pass on that exe by design: `COUNCIL_PLAYTEST_BUILD` always drops straight into the election, so the approach they wait for never happens ("the candidate did not come and ask"). They were run on the normal `WorldMVP.exe` build instead: `council_full` OK (36 banks, 6 busts, one listening burst with the petition earned and spent, 133 pushes, 42 sittings, a winner) and `council_decline` OK, both exit 0. Inside the PlayMode suite all three AutoPlay council tests pass.
- Captures (git-ignored, `Evidence/screenshots/rp_*.png` and `zoom_*.png`): example election staged at night, choosing and ready (two catspaws, at 4 and 9) at 844x390; ready and the moment after a bank at 1920x1080; enlarged crops of the candidate corner and both worked places.

### Verdict (honest)

- **1920x1080: the bigger gestures help, but only up close.** The candidate (about 120 px tall in the lower-left corner) reads well: the outstretched pointing arm is an unmistakable "address" gesture. At the nearer places (figures 40 to 60 px) the held poses give distinct silhouettes (arms forward, arms down, hands on hips), so a crowd looks alive rather than a block of identical figures. They do not, however, make supporters and undecided distinguishable at a glance: many people at every place have an arm out, so "leaning towards the candidate" does not stand out from the general busy-ness. What carries the information is still colour (the catspaws' green caps) and grouping. The bank moment reads as movement (the catspaws walking back towards the tower with arms out); in a still, the one or two newly settled people at a place are not picked out, and no overhead clap or cheer was identifiable in the captures.
- **844x390: no real change.** Figures at the places are 15 to 25 px tall; poses are not legible even in 4x crops (a limb is one or two pixels). The candidate (about 50 px) still reads as a figure holding something out; the catspaws read only by their green caps and the cluster around them. On a phone the gestures are decoration; colour, position and the camera framing do all the work.
- **What would actually help** (not done, owner's call): an outline or ground ring in the candidate's colour under settled and listening people, larger or brighter cap colours, a closer camera on the worked places during the player's turn, or a held cheer that lasts longer than the bank hold.
