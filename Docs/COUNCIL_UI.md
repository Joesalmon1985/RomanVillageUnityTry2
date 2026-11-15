# Council UI (the village election and active listening, Unity half)

Namespace `World.Game`, code in `Assets/World/Game/Council/`. The rules are in `World.Sim.Encounters.Council` (see `COUNCIL_API.md`); this layer draws, animates, takes input and sends commands. It never writes `WorldState.Council` in normal play. Content text comes from `council_content.json` (opening, progress, setback, revisit, agreement, acknowledgement lines, used sparingly). The owner's brief is not copied here.

## What the player sees

The election is a village, not a board. The player accepts and climbs a lookout tower near the home fire with their candidate; the camera is a perspective camera on the tower top (5.4 m above the ground), looking out over eleven dispute places at normal world size. The candidate stands at the foot of the tower at the lower left of the picture, the aide (with the bench lantern) at the lower right on a rival's turn.

**During a candidate's sitting the whole village shows only that candidate's standing.** At each place, of its people (3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3 including the lead):

- the number who stand **with** the candidate equals the candidate's banked progress there: they huddle close, on the tower side of the place, turned to the candidate, relaxed, and now and then clap or nod (pleased);
- the people a catspaw has **won this sitting but not banked** stand in a leaning arc round the catspaw's spot, facing it, in the open (not at ease): what a bust would lose is visible from bodies alone;
- the rest are **undecided**: scattered round the objective and at the edge, in pairs talking, some sitting, varied facing, not turned to the candidate.

A catspaw is a person in the candidate's colour with a coloured head band; it runs to the places being worked this sitting. A dispute secured by someone has all its people with the winner. When the turn passes, the village re-forms for the next candidate: people turn, shift and cross the ground. Rival turns play from the wide view with the same re-forming.

Nothing floats and there are no counters, markers or text: only speech bubbles (the lines of `council_content.json` and a three-line onboarding).

## How it works

- **Site** (`CouncilSite.Find`): tries 12 directions for a fan of places; the lookout tower stands 18 m behind the home fire, the grain dispute's storehouse place 10 m beyond the fire, the others at 26 to 62 m on the clearest open ground (camp layout obstruction test, no plots, no hills or mountains). `LandViews.AddClearing` clears trees, crops and rocks under every place, the tower foot and the bench set; solid props are registered with `WalkGrid.AddSolid` (owner "council").
- **Places** (`CouncilScenePlaces`): flat-shaded primitives only: water's edge with a landing and paddle (2), boundary stakes and rope (3), forest edge with trees and a sapling (4), builder's workyard (5), healer's shelter with pots (6), storehouse with baskets and sacks (7), quarry face (8), mill stone (9), stone-ringed spring (10), the track with a log roller (11), keeper's post with notches (12). Each place has a fire basket so it reads at night.
- **People** (`CouncilScene`): 83 `PlaceholderActor`s (`Person`), scale 0.93 to 1.06, varied clothing, a golden-spiral home each, pairs facing each other, every fourth sitting (the actor's new `Seated` pose). The last person of a place is its lead (the one who speaks). The state of every person (`PState`: undecided, listening, settled, away) is computed each frame from the engine state for the shown candidate: settled = banked place, listening = the catspaw's place this sitting minus banked, the lead last. They walk (about 3 m/s) with solid-prop push-out and turn on arrival. After a bust the listeners walk back and away for 3 s, then home; the settled stay.
- **Camera** (`CouncilEncounter`): one tower camera; `CouncilScene.Frame` gives yaw, pitch and vertical field of view that fit the places in play at the screen's aspect ratio. Wide (all eleven) when nothing is in play, the places of the proposals while choosing, the catspaws' places while pressing on, the place where something just changed for 3 s after a bank, bust or secured dispute. Smoothed over about half a second. The candidate and the aide stand where the camera's lower-left and lower-right screen anchors meet the ground, so they stay in view.
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

RESULTS_PLACEHOLDER
