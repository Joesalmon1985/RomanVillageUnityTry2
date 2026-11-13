# Prince API (package N)

The prince, the sawyer and the merchant, the pitch loop, the first scene, the endings, the villagers' walking and N's AutoPlay scripts (REFACTOR_SPEC 2.C.2, 2.C.5, 6.3, 8.5, appendix B). Namespace `World.Game`. Nothing here writes `WorldState`: the director submits only `DeclineProposal`; casts reach the sim through P's `SpellCaster` when the player presses the ground.

## Files

| File | What |
|---|---|
| `Game/Prince/PrinceInstaller.cs` | Entry point (fourth installer): N's placeholder views if V is missing, the director, the AutoPlay scripts. |
| `Game/Prince/PrinceDirector.cs` | The pitch loop, leading, the first scene, reactions, endings, HUD line. |
| `Game/Prince/PrinceLines.cs` | Every line (original text) by speaker and topic; `Voice` picks lines without saying the same sentence twice in a row. |
| `Game/Prince/PrinceVillagers.cs` | Spawning and dressing the villagers (primitives only); idle spots by the home camp. |
| `Game/Prince/PrinceScripts.cs` | AutoPlay `first_scene`, `follow_prince`, `prompt_game`, `ignore_prince`, `prince_captures`. |
| `Game/Prince/Placeholder/NPlaceholderViews.cs` | **Placeholder** land views while package V's are missing (delete the folder when V lands). |
| `Game/Actors/Npc.cs` | Villagers walk on the lattice; directed villagers; tap hook; name plate hidden under a bubble. |
| `Game/Actors/PlaceholderActor.cs` | Pointing pose, `beckon` and `hammer` one-shots, `HeadPivot` / `BodyPivot` for accessories. |
| `Tests/PlayMode/PrinceTests.cs` | Package N's PlayMode tests. |

## PrinceDirector

`PrinceDirector.Instance` (appendix B): `Prince`, `Sawyer`, `Merchant`, `Builder` (the first scene's builder, null before), `FirstSceneStep`, `RestoreFirstScene(step)`.

First scene steps (`scene.first`): `SceneNotStarted` 0, `SceneAsked` 1, `SceneLeading` 2, `SceneAtPlot` 3, `SceneBuilding` 4, `SceneFirstLoad` 5, `SceneDone` 6. `RestoreFirstScene` resumes: 1-3 ask again (the "again" lines), 4 calls the builder back to the unfinished hut (or 5 if it stands), 5 waits for the hut's next load. A game whose opening is already cast starts at 6.

Read-only state for scripts and tests: `CurrentPhase` (`Waiting, Approach, Asking, Leading, AtSpot, Celebrating, Resigned, Ending`), `ActiveSpeaker`, `PitchedProposal`, `AskHandle`, `StandSpot`, `WaitingForPlayer`, `Unanswered`, `Mood` (`PitchMood`: Calm, Keen, Pressing, Urgent), `PitchCount`, `Declines`, `Fetches`, `Calls`, `ResignedCount`, `EndingPlayed`, `EndingFinished`, `EndingOutcome`, `Said` (every line: tick, speaker id, text, ask), event `Spoke`, `Casts` (per cast of your people: proposal, kind, speaker, first pitched tick, cast tick), `Now` (game seconds), `SpeakerFor(proposal)`, `KindOf(proposal)`, `HudLine()`. Helpers: `CarrierFollowable(id)`, `CarrierTransform(id)`, `ProposalMark(id)` (package V's view through appendix B, else N's placeholder).

Tuning (static, game seconds and metres): `HurrySpeed` 8 m/s (coming to the player), `LeadSpeed` 7 m/s, `TagAlongSpeed` 5 m/s (the player walks 12.5 m/s), `PitchDistance` 8 m, `AskTimeout` 15 s, `RepitchAfter` {40, 30, 20} s, `WarningRepitch` 15 s, `ResignedSeconds` 90 (30 under a warning), `LaterSeconds` 60, `LagDistance` 18 m (he stops and calls), `ResumeDistance` 12 m, `FetchDistance` 50 m (8 s beyond it, or at once beyond 80 m, he comes back), `CallEvery` 8 s, `NudgeEvery` 12 s, `UnansweredBeforeResigned` 3. All timers run on sim ticks, so a faster time scale changes nothing but the wall clock.

### The loop

- The top proposal of your people (`FactionApi.Top`) is pitched by `SpeakerFor(p)`: `Speakers.Sawyer` the sawyer, `Speakers.Merchant` the merchant, anything else the prince. The speaker walks (lattice path, `HurrySpeed`) to a point 8 m from the player, 30 degrees to the side of the view nearer to them, stops, turns to the player and asks: an opener for the mood plus a body for the kind (hut, collector, sawmill, workshop, lodge, road, camp, city, banish), with the replies **Show me**, **Not this**, **Later** (spec 8.5). The question is an `Ask`, so P's bubbles draw "<name> calls you" with an arrow when the speaker is off screen. Until the question is asked he follows changes of the top proposal (another speaker may take over).
- **Show me**: he leads (`LeadSpeed`) to a free cell beside the target, one step past it on the side of the way in, and the player auto-follows him (`PlayerWalker.Follow`). If the player falls 18 m behind he stops, faces them, beckons and calls ("This way!" with the reply **Coming**, which makes the player follow); he walks on when they are within 12 m. If they stay beyond 50 m for 8 s (or go beyond 80 m) he walks back to fetch them and asks again ("You wandered off. Come, I'll show you again." with the same replies).
- At the spot he faces the player, points at the target (held arm) and says the proposal's reason (`Proposal.Reason`, one sentence from the sim) then "press the ground" in his voice; every 12 s in range he nudges; out of range (over 25 m) he calls; wandering off makes him fetch again.
- A cast (`SpellCast` of your people) closes the pitch: he cheers and says a line; 3 s later the next pitch may start. A refused press ("Walk closer to cast.", or the sim's refusal) gets a line from him (`SpellCaster.CastAttempted`).
- **Not this** submits `DeclineProposal` (the sim keeps it away for `DeclineCooldownTicks`), he answers, and the next top proposal is pitched as soon as the brain re-plans. **Later** waits `LaterSeconds` for that proposal (another top is pitched at once).
- No answer within 15 s (or the player walks 40 m away) counts as unanswered: the next pitch comes after 40, 30, then 20 s (15 s under an overrun warning), keener each time (mood Calm, Keen, Pressing; Urgent while your people are the defender of a warning). After three unanswered pitches he says a resigned line and walks 8 m behind the player for 90 s (30 s under a warning), then starts again, pressing. A new top proposal is pitched at once when nothing was left unanswered, else after 15 s.
- With nothing to show, the prince walks 7 m behind and 3 m to the left of the player (never ahead, never filling the view); the sawyer and the merchant stand at their spots by the home camp. People never block the lattice.
- One line each for: `SiteCompleted` of your buildings (when the prince is within 60 m), `OverrunWarningStarted` (defender: "... {secs} seconds to grow stronger"; attacker: "... hold on {secs} seconds"), `OverrunWarningCancelled`, `ProposalRemoved` ("expired" while pitched: "Forget what I said..."), `DemonPlaced` on your sites. A new line replaces what the villager was still saying (never a reaction stuck behind stale lines); the same sentence is never said twice in a row by one villager.
- Tapping a villager: during a question it unfolds the replies; the prince or the speaker of the waiting top proposal pitches at once; at the spot he repeats the reason; otherwise he says what he is doing. The builder says what he is building and what the site waits for (`EconomyApi.WaitingSentence`).

### The first scene (spec 2.C.5)

From tick 10 the prince walks from `CampLayout.PrinceStart` (20 m) to the player, faces them and asks exactly "I'm the prince. Come with me. We're building a woodcutter's hut." with "Lead the way", "What's it for?", "I'll catch up.". "What's it for?" answers with the proposal's reason and the other two replies; "Lead the way" leads with the player following; "I'll catch up." walks ahead and waits at the plot. At the plot (the sim's opening proposal, plot 631 on the default board) he says the reason and "Press the ground here, where it glows, to cast the spell." After the cast: "That's it! Builder, over here!", Tam the builder walks from the storehouse to the site and hammers while it is built; carriers bring the wood ("See the carrier? That's our wood arriving."); when the hut stands he says so and points at it. When the hut's first load is picked up by a carrier he points at the carrier and asks "There goes our first load of wood. Follow the carrier to the storehouse?" (**Follow** / **Not now**); following ends at the storehouse with "That's how wood gets home: ...". Then the loop starts. If unanswered, the opening question comes back with the "again" lines and the same escalation. If the carriers are busy he says so (once at 30 s, again at 90 s).

### Endings

On `GameEnded` (or a game already over) the pitch loop stops and a scene plays in bubbles in front of the player: the prince walks (or, if over 40 m away, appears) 7 m ahead. **Loss by overrun**: the Ash Clan's war leader and two warriors (red tunics, horns, spears) walk up; the prince and the war leader speak in turn (five lines). **Victory by overrun**: the prince, the sawyer and the merchant gather and cheer (five lines). Time-limit win, loss and draw: two lines from the prince. Last, `FactionApi.Outcome(s).Sentence` hangs in front of the player for good.

### HUD

`Hud.SetObjective(HudLine())` twice a second: "<speaker> wants: <title> (<distance> m)", "... is leading you to: ...", "... waits for your spell: ...", the ending sentence, or "The prince: nothing to build right now." (shown only with the HUD on, H).

## Npc (appendix B, extended)

`WalkTo(Vector3)` walks along the walking lattice (`WalkGrid.FindPath`, round camps, buildings and peaks) and ends exactly on the point (straight line if no path). `Follow(Transform, distance)` re-plans as the target moves. `IsWalking`, `StopWalking()`, `WalkSpeed` (default `StrollSpeed` 3 m/s), `Goal`, `FollowingTarget`, `MetresWalked`, `FaceTowards(point)`, `PointAt(point or null)`. `Directed` (a director moves it: no automatic greeting, turning or greeting box), `Hold` (stands still; the director holds villagers while the clock is paused), `OnTapped` (replaces the dialogue on a tap), `Doing` (said in a bubble on a tap when there is no dialogue), `HasShownBubble()`. The name plate is not drawn while one of the villager's bubbles shows (the bubble carries the name; the plate sat on the bubble's tail). `NpcIds.Prince`, `Sawyer`, `Merchant`, `Builder`, `RivalChief`.

## AutoPlay

Scripts (editor or `-autoplay <name>`; options `-timescale <x>`, `-princeDelay <s>`, `-princeMode follow|direct`, `-princeMinutes <m>`; in the editor `PrinceScripts.TimeScale`, `Delay`, `Direct`, `MaxMinutes`):

- `first_scene`: the exact opening line and replies, "What's it for?" (reason and two replies), "Lead the way", the press on plot 631 by a tap on the screen, the builder, the finished hut, "Follow" on the first load and the player at the storehouse when it arrives. Logs `[AutoPlay] first_scene OK`.
- `follow_prince`: answers every question after the delay (Lead the way / Show me / Coming / Not now), follows the speaker (or walks straight to the spot in `direct` mode), and presses the ground with a screen tap as soon as the target is in range; ends when the game ends. Logs one report line: result, game minute, casts, declines, metres walked, pitch-to-cast and cast-interval averages and medians, metres walked per cast, pitches, fetches, calls, strengths, casts by kind; then one line per cast.
- `prompt_game`: `follow_prince` with no delay; fails unless the game is won.
- `ignore_prince`: does nothing; fails if anything is cast or built for your people, if the pitching did not escalate (3 pitches, one resigned spell), if a villager repeated a sentence twice running, or if the game does not end in Defeat by overrun; then waits for the loss scene and captures it.
- `prince_captures`: the opening, leading, at the plot, the cast, then (with the F1 tools) to the merchant's road pitch; files `Evidence/screenshots/n_*.png`.

## Placeholders (remove when package V lands)

`NPlaceholderViews` is created only when `LandViews.Instance` is null and removes itself on the first tick after V's views exist. It draws camps, buildings by state, roads, carriers (`NPlaceholderCarrier`: `IFollowable`, `IInspectable`, a block in the ware colour) and a pulsing green ring (a strip for roads) on every open proposal of your people. The director only reaches carriers and marks through `CarrierFollowable` / `ProposalMark`, which ask `LandViews` first. V's views should give: a builder figure on sites (N's Tam only appears in the first scene), followable carriers from `LandViews.CarrierView`, and the ground mark from `LandViews.ProposalMarker`.
