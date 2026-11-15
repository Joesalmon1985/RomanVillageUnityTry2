# Council UI (the village election and active listening, Unity half)

Namespace `World.Game`, code in `Assets/World/Game/Council/`. The rules are in `World.Sim.Encounters.Council` (see `COUNCIL_API.md`); this layer draws, animates, takes input and sends commands. It never writes `WorldState.Council` in normal play. Content text comes from `council_content.json` (any version of the file in the same format works). The owner's brief is not copied here.

## How it works

- **Gathering ground** (`CouncilSite.Find`): full-size open ground near the home camp (about 34 m from the fire on the default board). Chosen by `CouncilScene.Blocked`: off plots, corners, the home camp's paths, camp props and clearings, and off hills and mountains. Unbuilt road lines and hex stock patches are not avoided (full-size ground cannot fit between them): `LandViews.AddClearing` clears trees, crops and rocks under the stage and the bench set, and the stage floor covers the ground. A road built later across the stage would be hidden by the floor.
- **Scene** (`CouncilScene`, `CouncilBench`, `CouncilShapes`): flat-shaded primitives only. Built once an election is in progress and kept (a passer-by sees it, including a declined election playing on). It is driven by `WorldState.Council` each frame plus the council events; `ViewMismatch` compares the view with the state (used by tests).
- **Layout** (`CouncilLayout`): every position is placed from the brief's safe-area percentages by unprojecting screen points with a virtual 16:9 camera (40 degree vertical field, 34 degree pitch) onto the floor, so figures land in their bands. The real camera uses the same projection; the 16:9 safe area is fitted and centred at any aspect ratio. Hit targets are the normalised rectangles `ForwardTargets` and `LeftTargets`.
- **Forward view**: back beam with four emblem bays (circle, triangle, square, diamond; the player's bay has a gold frame, the active bay glows, three hooks per bay take seals), eleven leads each with a large prop (the same mesh in carved wood is the carving on the trays), eleven rows of residents (3,5,7,9,11,13,11,9,7,5,3, last place by the lead), the table with three trays of two sockets, your candidate and the open hand at the left, the heavy seal and the petition at the clerk's end, the bench lantern at the bottom left. Banked progress is a post with the emblem at a place (several candidates share a place by side-by-side lanes); the temporary position is a catspaw wearing the active emblem; secured disputes get a banner at the lead and a seal on a hook.
- **Play** (`CouncilEncounter`): tap the hand to push; usable trays rise onto the table; hover or press shows the affected leads (prop lifts, ring on the floor); tapping a tray (both advances) or one socket (a choice of one) commits once and the catspaws walk; tap the seal to bank, or in a bust that waits for a held petition tap the petition (spend) or the seal (accept). After each accepted command the village clock is stepped one tick (it stays paused for the encounter). Taps are swallowed for 0.45 s at the start, 0.3 s after a command, during camera turns and while the scene is still moving; the engine refuses stale pushes anyway, so a held or repeated tap cannot replay a move. Rival turns are paced by `CouncilAiStep` once per action (`CouncilEncounter.AiDelay`, 0.75 s).
- **Left view**: turn left with A, Left (and the numpad keys), or tap the bench lantern, on a rival's turn (or with a burst open). The aide, one speaker waist-up with a bubble, the next speaker behind, two slabs (Same: two leaf seals; Different: a leaf and a gate), the candidate's lantern, firelight beyond. The reference line is shown, then each line is judged against the one before; response time runs from when the line is readable (0.45 s after it appears); feedback is the aide's reaction and a synthesised cue. The burst continues if the next sitting becomes ready; control returns when it finishes. Turning back keeps an open burst on a rival's turn; on your own turn it is abandoned. An earned petition is carried to the table by the aide when you are back at the table. Right: key D or tap the lantern at the right of the left view.
- **Approach** (`CouncilDirector`): when `CouncilApi.CanOffer` holds for the home village for `ApproachDelay` seconds, the director submits `CouncilOffer`; the asking candidate walks to the player and asks (replies: accept, decline, ask what it is). Accept leads the player to the gathering and opens the encounter; a candidate stays there so a player who left can sit down again ("Sit with the council"). Decline: the four play in the background (`CouncilAiStep`, near the stage every 0.7 s, far away every tick). **The approach never competes with the prince**: it waits until the prince's opening is done, his phase is Waiting or Resigned and no speech bubble at all is open. While the council is open `CouncilBubbles.Sweep` closes every bubble and edge indicator that is not the council's.
- **Leaving**: the pause menu's "Leave encounter" aborts the encounter; `End` sends `CouncilLeave`; state, banked marks and a pending push stay. Sitting down again sends `CouncilResume`. Saving and loading resume exactly because everything is in `WorldState`.

## Launch steps

- Integration entry point: `CouncilDirector.Install(this)` in `WorldBootstrap.Awake` (one line). The encounter id is `council` (`EncounterDirectory`), registered by the director.
- Normal game: build up the home village until four residents can stand; a candidate comes.
- F1 tool "Council": makes the home village eligible at once (developer staging that edits the economy), asks now, or stages the example trays and opens the council.
- Fixture for tests: `CouncilFixture.StageExample(state, settlement, seat)` (trays 4+9, 5+8, 6+7), `StageValues`, `StageElection`, `MakeEligible` (all edit state directly; tests, F1 tool and scripts only).
- AutoPlay (`-autoplay NAME`): `council_fixture`, `council_full`, `council_decline`.
- Capture driver: `AgentScripts/CouncilCaptures.cs` (commands in `Temp/c_cmd.txt`), then `unity command capture game_view`.

## Files outside the council folders

- `Assets/World/Game/Boot/WorldBootstrap.cs`: one line, `CouncilDirector.Install(this);`.
- `AgentScripts/CouncilCaptures.cs`: new file, capture and inspection driver.

## Tests

`Assets/World/Tests/PlayMode/Council/CouncilTests.cs`. See the results at the end of this file.

### Results (15 Nov 2026, Unity 6000.3.28f1)

- Full PlayMode run: 105 tests, 103 passed. The two failures were then re-run: `DanceTests.Drums_LightForTheRightPads` passed alone (a frame-timing assertion that failed under load in the long run; unrelated code), and the council layout test passed after the candidate was moved in from the screen edge. The EditMode suite and `dotnet test --filter Council` were not re-run by this package.
- 26 council tests (`CouncilTests`): layout rectangles inside the safe area and not overlapping at 1920x1080 and 844x390; every lead, prop, emblem, tray, hand, seal and candidate inside the safe area; example trays 4+9, 5+8, 6+7; one move under repeated taps; a double advances two places; temporary versus banked marks; bust rollback with the view equal to the state and banked emblems kept; petition spend and accept; listening Same/Different with response times passed through, wrong answers, petition earned and carried; draws unchanged after visiting the bench; leave and rejoin; save and load mid-push; camera, clock and avatar restored; other bubbles swept; no election with fewer than four residents; approach accept, decline (background play) and waiting for an open bubble; the dance and Ward duel still start; the three AutoPlay scripts (`council_full` about 265 s).
- Captures (`Evidence/screenshots/d_*.png`, git-ignored; the capture command writes 1280x720 files from a 1920x1080 or 844x390 game view).

### Readability (honest)

- 1920x1080: the four emblems are told apart by shape on the beam, on the posts and on the catspaws' chests; the carvings on the trays match the leads' props; temporary catspaws (cloaked, eared, in the seat colour) read apart from banked posts (a pole with a big emblem). Which disputes a tray affects is shown by the carvings and, on hover, by the leads' props lifting and a ring on the floor.
- 844x390: the shapes still read, but the 2.5 percent place pitch is only about 10 px, so catspaws and posts overlap their neighbours and a place count cannot be read by counting residents; the hooks and seals in the beam are about 20 px. Leads' props are small but still distinct in silhouette. The rows of residents blur into columns.
- Left view reads well at both sizes; the leaf seals are dark at night.

### Not done or limited

- The stage sits on cleared open ground 34 m from the fire; the unbuilt road lines and stock patches there are covered by the floor and cleared with `AddClearing`, not avoided.
- The left view's lantern for turning right has no separate drawn prop beyond the candidate's lantern.
- Resident support (gaze, turns) is limited to turning towards the candidate's side and a witness walking off; no handshakes beyond a lead's greeting animation.
- `council_content.json` updates on `council/content` were not merged (instructed); the code uses only the existing format.
