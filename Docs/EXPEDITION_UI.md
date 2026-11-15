# Expedition views (crystals package I6)

Mira's expedition in play: the director, the party's bodies, her conversation, leaving and rejoining, the cues, the sites and towers, the Ash runners, the journal and the map layer (`Docs/PRD_CRYSTALS.md` 5.5, 5.6, 5.7, appendix B, package I6). Namespace `World.Game`; code in `Assets/World/Game/Expedition/`. Written 15 Nov 2026 on branch `cry/i6` (from tag `historic-base`).

Nothing here writes `WorldState`. The package reads `WorldState.Campaign` and `CampaignApi`, animates the tick's events and sends these commands: `SetCompany`, `AcceptLead`, `DeclineLead`, `AdviseMira`, `CommitEncounter`, `WithdrawFromEncounter`, `ConfirmRestore`, and `ResolveEncounter` only through the duel stand-in (below). Original primitive shapes and the pack's own text only.

## Files

| File | What |
|---|---|
| `ExpeditionInstaller.cs` | Entry point (after `InteriorsInstaller`, before the F1 tools). Registers the AutoPlay scripts, sets `PrinceDirector.Hush`, and creates the director when the world has a campaign: at once for a load, or on the tick `StartCampaign` is applied (a later `CampaignStarted` replaces the director: "Begin again"). With no campaign nothing is created. `Disabled` switches it off for other packages' tests. |
| `ExpeditionDirector.cs` | Lifecycle, the tick's events, commands (`Commands` keeps every one sent). |
| `ExpeditionDirector.Party.cs` | Mira and the six: legs, formation, gestures, the crystals in her hands, the scout, the retreat with one staying, the fallen, the led viewpoint of the stand-in interiors. |
| `ExpeditionDirector.Talk.cs` | Lines one at a time, Mira's question, the opening, leads, calls, choices, thresholds, the tower, the tap menu, company, cues, the duel stand-in, the prince. |
| `ExpeditionLines.cs` | Every word comes from the pack through `CampaignApi.Line`. Ids the game needs and the authored pack lacks are in `ExpeditionLines.Missing` and used only while the pack has no line of that id. |
| `ExpeditionSpace.cs` | Marker to world point: surface markers by `CampaignPlaces`, the home camp's by the camp layout, interior cells by `InteriorSpace` or the stand-in. |
| `ExpeditionSites.cs` | Site and tower views, pedestals, altars, crystals, keepers, clearings, the restoration effect. `ExpeditionThing` (a tap names it), `ExpeditionStair` (a tap offers "Go down"). |
| `ExpeditionStandIn.cs` | Floor, walls, roof, doors and lit stairs for interiors while package I5 is not merged. Delete at integration. |
| `ExpeditionAsh.cs` | The Ash runners on the surface. |
| `ExpeditionJournal.cs` | The journal on the map, the HUD line. |
| `ExpeditionMapLayer.cs` | The expedition's `IMapLayer`. |
| `ExpeditionScripts.cs` | AutoPlay `earth_first`, `earth_stolen`, `earth_stolen_win`, `mira_alone`, `expedition_captures`. |
| `ExpeditionDev.cs` | Load a shipped pack, `StartCampaign`, send the Ash expedition ahead, settle a pending duel. I8's `CampaignStart` replaces the starting part. |
| `Tests/PlayMode/ExpeditionTests.cs` | The package's PlayMode tests. |
| `AgentScripts/ExpeditionDrive.cs` | CLI driver for Play Mode runs and captures. |

## The party

- **Who.** Mira is the camp's Mira, taken over (`Directed`, `OnTapped`): a tall staff with a pale stone and a violet cloak so she reads from the back of the file, and a carried light. The six are spawned through `Npc.Spawn` (so through `ActorFactory`) with the definition's actor ids (`companion_garrick` ...). Each has a sash in a colour of their own (the same colour as their dot on the map) and a mark: Garrick a round shield and helm, Ivo a lamp on a pole (a real light), Petra an apron and tongs, Nell a rope coil and paddle, Odo a long axe and a sprig, Tansy a feathered cap and bow. A rigged cast replaces the bodies through the same factory and ids.
- **Where.** The party is where the sim says. A leg (`PartySetOut`, or rebuilt from state after a load) is a path: on the surface the lattice path from where Mira stands to the marker (`WalkGrid.FindPath`), inside an interior the sim's `LegCells`, across a stair the two ends (she sinks out of sight at one and rises at the other). Mira's body is placed on it at `CampaignApi.PartyPosition`'s fraction every frame, so she arrives on the sim's tick. **No pace is written here**: the speed is the path's length over the leg's ticks (`LastLeg.Speed`; 2.5 m/s walking, 1.7 m/s underground, about 7 m/s when `PartySetOut.Running`). At a thing (stone, pedestal, altar, witness) she stops a pace short of it on the side she came from.
- **The file.** The six walk two abreast along the trail Mira leaves; through a stair they follow one by one. At a place they stand three to each side of her and the thing, never straight behind, so whoever follows sees what she does. At a threshold they face the keeper.
- **Gestures.** Examine and Search: she bends to it and points, the mark-reader leans in. Take, Restore: the cast gesture, the crystal lifts off the pedestal into her hands or rises out of them into the tower. Talk: she faces the witness. Carried crystals show at her chest (`CarriedShown`).
- **The scout.** While `Scout.Out`, that companion leaves the file and runs the straight line between the party's place and the target (`ScoutPosition`), shown on the map.
- **A companion covers the retreat.** On a lost duel the sim has already placed the party at the safe point. When the duel's screen is gone the director plays 4.6 s: everyone back where they stood, the volunteer steps into the gap towards the keeper and holds (arm raised), says the pack's `volunteer` line, Mira calls the retreat, the others run back the way they came. Then the party is at the safe point as the state says, the fallen lies at the gap with a cairn in their colour (a tap names them), and Mira says her `memorial` line. After a load the fallen lie at the encounter's volunteer marker. A draw plays the same run with nobody staying. `RetreatsPlayed`, `LastVolunteer`, `RetreatPlaying`.
- The roster follows state every frame, so a companion killed or revived by a dev command changes too.

## Mira's conversation

One question of Mira's is open at a time (`AskHandle`, `AskKind`), with at most three replies; lines are said one after another (`ScriptPending`), each for a time that grows with its length. Bubbles keep game time (a faster clock closes them sooner), so timings in game minutes do not depend on the time scale. A line is said only when its speaker's body is with the player (same space, within 75 m); otherwise the journal has it.

| Moment | What happens |
|---|---|
| Opening (scene step 0 to 2) | From 0.5 s after the campaign starts Mira comes to the player with the six behind her, tells the pack's `opening` lines (`mira_open_1` to `4`, the routes' own opening lines, `mira_open_six` while the six wave, `mira_open_cost`, `mira_open_practice`) and asks `mira_open_ask` with "Lead the way" (accepts her best lead), "Tell me more" (`mira_open_more`, `_2`, then two replies) and "Go on ahead, I'll find you" (`SetCompany` false). A tap on her skips to the question. If she sets out before it is answered (the sim's patience) the opening ends. |
| A lead (party Idle, or waiting with no tower) | The lead's reason with "Lead the way" (`AcceptLead`; the walker follows her), "Why there?" (`mira_why_<lead>` if the pack has it, else `mira_where_<crystal>`; then two replies), "Not that way" (`DeclineLead`). Not answered: the sim sets out after its patience and she says so. |
| On the road | If the player is more than 22 m behind and not following she beckons and calls ("Coming" follows her), at most every 8 s. She cannot wait mid-leg: the sim walks her. |
| At a marker | The quest event's lines in order, each by its speaker (the sawyer, a companion, the keeper). The first line of an event with two or more lines is said as she starts on it; the rest when it fires. |
| A choice | The question with the answers as replies; the reply sends `AdviseMira`. A wrong answer: the wrong line, and the question again. |
| A threshold | `mira_threshold_<crystal>` (names the keeper and the volunteer) with "Fight" (`CommitEncounter`), "Not yet" (`WithdrawFromEncounter`), "What happens if we lose?" (`mira_if_lose`, or `mira_nobody_left` with nobody alive; then two replies). Asked again for as long as she stands there. While the scout is out she says to wait. |
| The duel | `EncounterCommitted` goes to `CampaignDuel.Start` (package I7). While that returns false the **duel stand-in** pauses the world clock and asks, in a bubble in front of the player, how it ends ("Mira wins", "Mira loses", "Neither breaks"); the reply sends `ResolveEncounter` and steps the clock once. `DuelStandInOpen`, `ResolveStandIn(result)`. |
| A win | The keeper's and Mira's lines that the victory's facts unlock; cheers. |
| A tower with its crystal | `mira_tower_ask` with "Set it" (`ConfirmRestore`) and "Wait" (asked again after 25 s or on a tap). |
| A tap on Mira | Unfolds her open question; else `mira_what` with "What do we know?" (`mira_status`, then the map opens with the journal unfolded), "Go on without me" (`SetCompany` false), and "Practice duel" at a place on the surface or "Lead me out" underground. Away: "I'm with you" (`SetCompany` true), "What do we know?". |
| A tap on a companion | Role and their line; underground also "Lead me out". A fallen companion: name and where they fell. |

**Company.** `SetCompany(false)` is sent on "Go on ahead" and "Go on without me", when the player is more than 60 m from the party (or in another space) for 20 game seconds, and when the player lets the prince lead them away. `SetCompany(true)` is sent on "I'm with you" and with any answer to one of her questions. Away, Mira never opens a question.

**Cues.** While the player is away every journal entry of what Mira did (not the lines spoken at a place, which the journal also keeps) is hung as a short line in front of the player for a few seconds (at most four waiting; the journal keeps the rest), the first of an absence followed by how to rejoin. A cue waits for Mira's own lines and questions, not for the prince's. Word of the Ash expedition (the sim's reports) is cued whether the player is with Mira or not. With the HUD on, a line at the top right says where Mira is and the newest journal sentence. `Cues`, `LastCue`.

**The prince.** Mira says nothing new and closes her own question while the prince's people have the player's ear (`PrinceBusy`: an open question of theirs, one of them speaking within 40 m, or leading the player); she asks again after. The other way round needs one hook in `PrinceDirector` (below): while `MiraHasTheFloor` (her question or lines, the opening, the player in her company on the road or at a site, underground, a retreat) the prince starts no pitch, withdraws one he has not finished, and does not tag along, unless your people are under an overrun warning (he may then call the player, as `PRD_CRYSTALS` 5.9 says, and Mira yields). The camp's word on a lit tower (the pack's prince lines, topic `state`) is said by the prince when the player next stands by him.

## Sites, towers, crystals

- Every surface location gets cleared ground (`LandViews.AddClearing`), a way-post with its name, and a prop per marker: a standing stone (Inscription), foresters' posts, heaps (rubble, spoil), the tally stick, a keepers' pedestal, an Ash altar, and for a stair a lid over a dark hole. The glade's ring-marked slab slides aside and the Ash cut's bars lift when the link is open (`ExpeditionSpace.LinkOpen`: the markers' and the connection's facts); an open way down has a torch that is always lit. The sites that are not written yet (kiln, forge, causeway, cistern, shoulder, cliff) are the same generic props on their pending markers.
- **Towers.** A round tower on each tower corner with a band in its element's colour, the door towards the path, the Rooted Tower with stone roots. Dark until `Restored`; then a beam, a glowing band and door, a strong light seen from far off, a ring of light and new growth on the ground round it, and (when the restoration happens in this scene) a wave of light that crosses the island in about twelve seconds. The change in demons is the sim's (`SevensPerDraw`); the journal states it.
- **Crystals** are drawn once each from custody: on the pedestal (`AtSite`), on the altar (`AtRivalAltar`), at Mira's chest (`Carried`), in the tower's crown (`Restored`); lifting while she takes it, rising while she sets it; with the Ash runners while in transit. `ExpeditionSites.CrystalPlace(element)`.
- **Keepers** stand at their encounter's opponent marker while the crystal lies on the altar and after a win; a tap says their line.
- Everything follows state (custody, facts, restored list), so a load shows the same world and replays nothing. One thing a load cannot know: where Mira stood when a surface leg began (at the camp she sets out from beside the player, not from her fire), so after a load in the middle of that leg she is on the path from the leg's first marker, up to some 20 m from where she was, and arrives on the same tick.

## The Ash expedition

Three runners in the Ash Clan's red, drawn only when the player is on the surface within 170 m: marching from where the last step ended to the site, searching it, or carrying the seized crystal (the middle runner holds it) to the place of its altar. The position is the rival's step progress along a straight line between location anchors. What the player otherwise knows is the reports: cues, the journal, and the map with the report's age.

## Journal and map

- **Journal.** While a campaign runs the map's side column shows "Mira's journal" under the map's key: where Mira is and whether you are with her, each crystal's belief with the age of that word (a stale belief with the sim's "where to look" sentence), the demons' pace after a restoration, where next (her best lead and its reason), the six (alive, or where they fell), and as many of the latest entries as fit. A tap on the title or J unfolds the whole journal over the legend. With the HUD on, J opens it from the world. "What do we know?" opens it.
- **Map layer** (island mode): the towers (ringed once lit), believed crystal places with age and "?" when stale, Mira's leads (the best one named), the last Ash report with its age, the scout, Mira with her errand and the six as dots; "Mira is waiting for you here" when she waits. Underground she is shown at the way down, "(below)". A tap within 28 m of her marker sets the walk target there.

## Interiors (joined with I5 at integration, I8)

The stand-in interiors and the led viewpoint are gone (`ExpeditionStandIn.cs` deleted). The party walks I5's interiors: positions from `InteriorSpace.ToWorld`, the player follows Mira down and up through `Interiors.Enter` when the party takes a way with the player in company, `Interiors.LeadOut()` answers "Lead me out" (offered on Mira and on every party member underground), `Interiors.Current` is the player's space. A follow never crosses a way by itself (I5's `OnStepped` ignores way cells while the walker follows someone). One owner per object: I5 draws the pedestal, altar, inscriptions, tally and both ends of every way (`SurfaceWayView` on the surface); this package draws the crystals (one body each, following custody: pedestal, altar, Mira's hands, the runners, the tower; I5's stands hide their own crystal while `CrystalStandView.DrawnElsewhere`) and the keepers. The interior map (I5) draws the party.

## The duel (joined with I7)

`EncounterCommitted` is staged by I7's driver alone (`CampaignDuel.Start`, at the encounter's `caster`, `keeper` and `threshold` markers through `Interiors.TryWorld`); the stand-in bubble is gone. On `CampaignDuel.Staged` the director hides the six, any cairn and the keeper (I7 stages its own bodies) and shows them on `EncounterHost.Ended`; then the retreat plays as before. The duel's off-lattice camera keeps the player's interior lit (I5). Mira's threshold bubble adds `CampaignDuel.StakesLine` when the pack's line does not name the volunteer. The AutoPlay scripts play the real duel with `WardBot` (passive for a planned loss).

## AutoPlay

All start from `dev.historic` plus `StartCampaign` on the authored pack (the script rebuilds the scene on the developer start first when needed). Replies are tapped on the screen where the bubble draws them; the key path is used only when a reply is not on screen and is counted. `-timescale <x>` (editor: `ExpeditionScripts.TimeScale`); `ExpeditionScripts.Captures` writes `Evidence/screenshots/i6_*.png` at night.

| Script | What |
|---|---|
| `earth_first` | A prompt player: two bubbles of the story, a tap on Mira (her question at once), "Lead the way", the stone, the stair, the marks, the Deepstone taken, out, the Rooted Tower, "Set it". Fails if a duel was fought or anyone died. |
| `earth_stolen` | `DevRivalNextStep` puts the Deepstone on the Ash altar. The bare pedestal, the tally, the vault, "What happens if we lose?", "Fight", a loss (Garrick stays), back down, "Fight", a win, out by the Ash cut, "Set it". Then the map and the journal. |
| `earth_stolen_win` | The same with a win at the first attempt. |
| `mira_alone` | "Go on ahead, I'll find you". The Ash expedition is held back in the definition handed to `StartCampaign` (as the proof's away run does), so that Mira reaches the pedestal first alone. The cues, the map and the HUD line are checked; the map's marker walks the player to the tower; "I'm with you"; "Set it". |
| `ash_runners` | "Go on ahead", a walk to the glade's edge; the Ash runners come, seize the Deepstone and carry it off in sight; the word of it arrives as a cue. |
| `expedition_captures` | `earth_stolen` with captures. |

Each logs `[AutoPlay] <name> timings (game m:ss since the campaign began): ...` and `[AutoPlay] <name> OK`.

## Lines the authored pack lacked

These ids are in `authored_v1/lines.json` since the integration (I8), with the same text; `ExpeditionLines.Missing` keeps the text only as a fallback for other packs. The pack now also has a `volunteer` line for each of the six, a `memorial` line of Mira's for each, and prince `state` lines for two to four towers.

| Id | Used for | Stand-in |
|---|---|---|
| `reply_coming` | reply to her call | Coming |
| `reply_go_down` | reply on an open stair mouth | Go down |
| `mira_call` | she calls a player who lags | This way! Keep up. |
| `mira_go_on` | not answered in time, she goes | I'm going on. Catch me up when you can. |
| `mira_declined` | "Not that way" | Not that way, then. Let me think. |
| `mira_why_<lead id>`, `mira_why` | "Why there?" (else `mira_where_<crystal>`) | It's the best lead we have. |
| `mira_leave`, `mira_rejoin`, `mira_away_here` | leaving, "I'm with you", tapped while away | Go, then. I'll leave word of where I am. / Good. Stay close. / You found me. Are you with me? |
| `mira_not_yet` | "Not yet" at a threshold | Not yet, then. We step back and breathe. |
| `mira_tower_wait` | "Wait" at a tower | It will keep. Tell me when. |
| `mira_retreat`, `mira_draw` | the retreat after a loss, after a clash or stalemate | Back! Back the way we came! / Neither Ward broke. Back, and we try again. |
| `mira_wrong_answer` | a wrong answer when the choice has no wrong line | That wasn't it. We lost time. |
| `mira_nothing_open` | no lead open (journal) | Nothing more to chase just now. We wait for word. |
| `mira_lead_out` | "Lead me out" | The way out is lit. Follow the light. |
| `mira_wait_scout` | a threshold while the scout is out | Wait for {companion.tansy.name}. I won't fight with one of us still out there. |
| `cue_fell_behind`, `cue_rejoin` | cues | You fell behind. Mira has gone on without you. / The map (Tab) shows where Mira is. Find her and tap her to rejoin. |
| `companion_fallen` | a tap on a fallen companion | fell here, holding the way out. |


## Files outside `Game/Expedition`

- `Game/Prince/PrinceDirector.cs`: one additive hook, `static Func<bool> Hush` and `Hushed`, read in three places (no new pitch while waiting, a pitch in approach or asking is withdrawn, no tagging along). Null by default: nothing changes without the expedition.
- `AgentScripts/ExpeditionDrive.cs`: tooling.

## To join at integration

- **I5 (interiors).** Delete `ExpeditionStandIn.cs`; `StandInInteriors` becomes false by itself. Check: `Interiors.Enter(MarkerRef)` moves the player through a link when the party takes it; `Interiors.Current` is the interior id used in the pack; `InteriorSpace.ToWorld` is a pure translation of the lattice (the director uses `Lattice.CellOf` relative to the interior's cell (0, 0) for its floor test); who draws pedestals, altars, inscriptions and stair ends inside interiors (this package draws pedestal, altar, inscriptions, crystal and keeper there; I5 should not draw them twice); I5's interior map mode should draw the party (this layer draws only in island mode); the surface stair mouths here are views only, the walker's step onto the stair cell is I5's.
- **I7 (campaign duel).** `CampaignDuel.Start(ev)` returning true switches the stand-in off. The keeper's body stands at the opponent marker and Mira at the threshold: the duel's stage should reuse or hide them. The retreat scene starts when `EncounterHost` is no longer running.
- **I8.** The save extra `scene.expedition` = `ExpeditionDirector.SceneStep`, restored with `RestoreScene`. Without it a load infers the step from state (the opening is over once Mira has done anything). In the stand-in a player saved underground is led again after the load.
