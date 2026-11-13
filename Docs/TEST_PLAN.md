# Test plan

Two parts. Part A is what the build run must prove at each gate before moving on. Part B is the script for the owner to play the result and report back. Requirement IDs refer to `PRD.md`.

## Part A: gates for the build run

Three kinds of check:
- **EditMode**: NUnit tests on `World.Sim` (no scene, runs in seconds).
- **PlayMode**: tests that load the startup scene and drive it with scripted input and commands.
- **Capture**: Play Mode screenshots saved to `Evidence/` and inspected before the gate is claimed.

A gate passes only when its tests pass, the console shows 0 errors, and the captures show what the row says. Anything not run is written down as not run.

### G1 Walkable village

| Check | Kind | Pass when |
|---|---|---|
| Board topology | EditMode | Radius 4 gives 61 hexes, 150 corners, 210 hex edges, 570 flags, 630 segments; corners have degree 2 or 3, intermediate flags 2; corner neighbours are 3 segments apart; an interior corner touches 3 hexes, an intermediate flag the 1-2 hexes of its hex edge (was radius 2: 19 hexes, 54 vertices, 72 edges) |
| Scene boots | PlayMode | Startup scene loads with one avatar, one sim, three NPCs, storehouse, market, warehouse |
| Walk to hosts | PlayMode | Scripted walk from spawn reaches Mira and the party host in under 15 s each; greeting shows inside 4 m |
| View toggle | PlayMode | Tab while walking and while a building is inspected: avatar position and facing are identical before and after to 1 mm and 0.1 degree; same sim instance; inspected object stays selected |
| Captures | Capture | Spawn view with both hosts and lit warehouse door; God View of the whole settlement |

**Coverage as of 13 Nov 2026.** Board topology: EditMode `BoardTopologyTests`. Scene boots: PlayMode `WorldBootTests`, `VillageTests.Scene_Boots_WithVillage` (seven NPCs now). Walk to hosts: `VillageTests.Walk_FromSpawn_ReachesMiraAndDoorHost_GreetingShows`. View toggle: `VillageTests.Tab_WhileWalkingAndInspecting_KeepsAvatarAndSelection`. Captures: taken and looked at in Milestone 1 (`PROGRESS.md`). All run in the editor; the packaged `boot_tour` run covers boot and Tab in the exe.

### G2 Opening encounters

| Check | Kind | Pass when |
|---|---|---|
| Ward feedback vectors | EditMode | Against Ward [Stone, Stone, Flame] style repeats, mapped to this pool: Ward [B,B,A]: guess [B,A,A] gives 2 fractures, 0 echoes, 1 fade; [A,A,A] gives 1/0/2; [C,B,B] gives 1/1/1; [A,C,C] gives 0/1/2. Plus the four-slot `shared_fixtures/feedback_cases.json` and `duplicate_edge_cases.json` run through the same scorer |
| Ward outcomes | EditMode | Victory, defeat, both exhausted, player exhausted first, same-tick clash each produce the documented result and reveal both Wards |
| Ward rival | EditMode | Seeded Mira never casts a weave inconsistent with her own feedback; with a fixed seed her cast sequence is identical on every run; over 200 seeds she breaks a three-locus, three-essence Ward within ten casts in at least 95% of duels |
| Recovery and timeout | EditMode | A cast inside 2.0 s of the previous one is refused with a reason; 60 s idle casts a random legal weave and spends a cast |
| Light unlock | EditMode | Duels 1 to 3 offer three essences; duel 4 offers four; count survives save and load |
| Rhythm judgement boundary | EditMode | Presses at +44, -44, +46, -89, +91, -139, 0, -141 ms give perfect, perfect, great, great, accepted, accepted, perfect, stray |
| Rhythm holds | EditMode | A 1.0 s hold from t=1.0 released at 1.86, 1.84, 1.5, 1.1 gives credit 1.0, 0.84, 0.5, 0.1 |
| Rhythm blocks | EditMode | All four pass: stages 0,1,2,3 and bass revealed at beat 16. Miss then three passes: stages 0,0,1,2, bass revealed at beat 32, block families drums, drums, bass, drums. Block credit 0.6 passes, 0.59 fails |
| Route choice | EditMode | Window opens at beat 48 and locks at 60.5; no choice gives bar; bar and balcony each commit when chosen; failed final block gives no route and offers retry; three passes with a passing final block gives success |
| DSP sync | PlayMode | Over one phrase, scheduled cue times and stem start times differ from the DSP grid by less than 1 ms; an autoplayer hitting at exact cue times scores all perfect |
| Complete, lose, replay | PlayMode | From the startup scene: win and lose a duel, pass and fail a phrase, rematch and retry each, and return to the pre-encounter position and facing each time. Village clock tick count is unchanged across each encounter |
| Tab in encounter | PlayMode | Tab during a duel and during a phrase switches camera; casts used, cues judged and song time are unaffected |
| Captures | Capture | Duel with both wizards and the feedback table; rhythm lanes mid-phrase; bar lit; balcony lit |

**Coverage as of 13 Nov 2026.** Ward feedback, outcomes, rival, recovery and timeout, Light unlock: EditMode `WardTests`, `WardBalanceTests` (the Light count surviving save and load: `SaveTests` and PlayMode `SaveLoadTests`). Rhythm judgement, holds, blocks, route choice: EditMode `RhythmTests` (route rule changed to R9 as built: success at 3 of 4 blocks, route only if the final block passed; `ENCOUNTERS_UI.md`). DSP sync, complete/lose/replay, Tab in encounter: PlayMode `EncounterTests`. Captures: Milestone 2. Not run: DSP sync on a real, stable audio device by ear (nobody has listened).

### G3 Road economy

| Check | Kind | Pass when |
|---|---|---|
| Conservation | EditMode | Over 20,000 ticks with seeded random valid and invalid commands, total wares equal starting wares plus produced minus consumed at every tick; no ware is in two places; reserved wares are never spent |
| Single authorisation | EditMode | A production draw creates one batch per matching hex; re-running the round start does not create a second |
| Recipe once | EditMode | One sawmill cycle consumes exactly 1 timber and yields exactly 1 planks |
| Construction | EditMode | A site with all wares delivered completes; a site missing one ware reports `waiting_ware` naming it |
| Stall reasons | EditMode | Each reason in E5 that applies to the road economy is produced by a minimal scenario and cleared by the matching fix |
| Interrupted route | EditMode and PlayMode | Remove a road under a shipment: the job reports `no_road`; rebuild it: the same shipment arrives once and the destination count rises by exactly one |
| Commission, headless | EditMode | A scripted player places the four buildings and roads and reaches 10 planks and 10 stone from the opening map and seed, without trading, inside the tick budget for a 6-minute play |
| Commission, in scene | PlayMode | The same script through God View commands completes the commission; the quartermaster acknowledges it |
| Invalid placement | EditMode | Woodcutter away from forest, road not touching the network, building on an occupied flag are each refused with a sentence |
| Captures | Capture | Carrier mid-road with a visible load, seen from first person and from God View in the same tick; inspect panel on a stalled site |

**Coverage as of 13 Nov 2026.** Conservation, single authorisation, recipe once, construction, stall reasons, invalid placement, headless commission: EditMode `EconomyTests`, `UnifiedEconomyTests`, `EconomySimulationTests` (the commission is now six buildings and 18 planks and 18 bricks, not four buildings and 10 + 10). Interrupted route in scene: PlayMode `EconomyViewTests.RemovingTheRoadUnderAShipment_ShowsNoRoad_RebuildingDeliversOnce`. Commission in scene: `EconomyViewTests.Commission_CompletesInScene_ThroughTheScript` and AutoPlay `commission` (editor and packaged exe). Captures: road economy UI session.

### G4 Strategy and military

| Check | Kind | Pass when |
|---|---|---|
| City placement | EditMode | One legal city is accepted; one adjacent to an existing city is refused; one with no connecting road is refused; each refusal names the rule |
| Trade | EditMode | 4 of one good for 1 of another updates storehouse stock exactly; insufficient stock is refused |
| Draft | EditMode | Each age: three cards each, pick, pass, pick; no card appears twice; rival pick is deterministic for a seed; discard pays coin |
| Card effect | EditMode | With the timber card built, the next forest batch is larger than without it, same seed |
| Demons and breach | EditMode | Infection adds a demon to a drawn flag; a fourth on one flag adds a breach instead; a demon flag stalls a delivery with `demon_on_route`; hero arrival clears it and the delivery resumes; 4 breaches ends the game as a loss |
| Power | EditMode | With supply below demand the barracks queue does not advance and reports `no_power`; restoring fuel resumes it from the same progress |
| Supplied squad | EditMode | Rations and ingots must arrive at the barracks flag before training starts; a missing ware is named; a trained squad ordered to attack moves, fights on fixed ticks and resolves to a deterministic result |
| Victory paths | EditMode | A scripted game reaches 8 points and wins; another reaches 4 breaches and loses; another ends Age III and compares points |
| Full playthrough | PlayMode | A scripted 10 to 15 minute economy game runs to an ending with 0 console errors |
| Captures | Capture | Second city founded; draft hand; squad engaging the outpost; breach counter at 3 |

**Coverage as of 13 Nov 2026.** City placement, trade, draft (now one pick per round, 4 cards at the age start), card effect, demons and breach, power, supplied squad, victory paths: EditMode `StrategyTests`, `MilitaryTests`, `StrategyPlaythroughTests`, `StrategyConservationTests`. In scene: PlayMode `StrategyViewTests` (6); full playthrough: AutoPlay `full_game` (editor and packaged exe). The draft test now lets the game's own clock apply the pick (it had been stepped by hand, which hid a deadlock for human players; fixed in `GameClock`).

### G5 Save and package

| Check | Kind | Pass when |
|---|---|---|
| Round trip | EditMode | Save then load gives a state equal to the original; running both forward 1,000 ticks gives equal states |
| Shipment in flight | EditMode and PlayMode | Save with a carrier mid-road, quit Play, reload: the load arrives once |
| Player transform | PlayMode | Position and facing restored |
| Windows build | Build | Build succeeds; the packaged game starts, the first-five-minutes script completes under the autoplayer flag, log has 0 errors |
| Guide | Review | `PLAY_GUIDE.md` matches the actual keys and steps |

**Coverage as of 13 Nov 2026.** Round trip: EditMode `SaveTests`. Shipment in flight: EditMode `SaveTests` and `UnifiedEconomyTests`; PlayMode `SaveLoadTests.Save_CarrierMidRoad_LoadsEqual_DeliversOnce_RestoresPlayer` (through the F5/F9 path and a scene reload; the loaded game then matches a headless run tick for tick); packaged: AutoPlay `save_load` in the exe. Player transform: same PlayMode test and `save_load`. Refusals (corrupt, older, missing save; saving during an encounter): `SaveLoadTests`. Windows build and packaged runs: see `PROGRESS.md` (packaging session). Guide: `Docs/PLAY_GUIDE.md` and the pause menu were checked against the key bindings in code by the agent; not reviewed by the owner.

## Part B: owner playtest

Allow about 45 minutes. Double-click `Builds/Windows/WorldMVP.exe` (it opens full screen; Alt+Enter for a window). If there is no exe, open `Assets/Scenes/World.unity` in Unity and press Play. Wear headphones or use speakers for the rhythm part. Note the time on the clock for anything that confuses you; that is the most useful feedback. The steps and numbers below are the game as built on 13 Nov 2026; targets (points, breaches, commission goods) are read from the game, so if the game shows other numbers, the game wins. `Docs/PLAY_GUIDE.md` has the same controls and a walkthrough.

**Controls.** WASD move, mouse look, Shift run, E interact, left click inspect, Tab switch view, Esc pause menu with the guide (Save, Load, New game, Quit), F5 save, F9 load. God View: WASD or middle-drag pan, wheel zoom, H home, M whole island, left click select, right click back to Inspect; tools B Build, R Road (X remove), V Inspect, P Problems, G Wares, T Market, C Cards, F City, J Hero, N Army, O Wonder, L Go here (fast travel), End Round. Duel: 1-4 essences, Backspace, Enter lock / cast (Space casts too), R rematch. Rhythm pads: D F J K; route choice: Left (bar) and Right (balcony) arrows; [ ] timing; R retry; Esc twice leaves during the song.

### B1 First five minutes (target: under 5 minutes, no reading of the guide)

| Step | Do | Expect | Pass / fail / notes |
|---|---|---|---|
| 1 | Start the game and move the mouse and WASD | You are standing in a square and can walk at once | |
| 2 | Look around without moving | Mira is close on the left; ahead is a doorway with coloured light and music | |
| 3 | Walk to Mira, press E, pick the duel | A duel starts in the square; you set a three-part Ward (keys 1-4, Enter) | |
| 4 | Cast until the duel ends | After each cast you see three totals (fractures, echoes, fades); Mira casts on her own; the end shows both Wards | |
| 5 | Choose rematch once (R), then return (Enter) | You are back where you stood, facing the same way | |
| 6 | Follow the lights into the warehouse, talk to DJ Sol, accept | A count-in, then notes on four lanes in time with drums | |
| 7 | Play the phrase; in the last block press Left or Right | Bass joins after a good block; with 3 of 4 blocks passed it is a success, and the doorway you chose opens if you passed the last block | |
| 8 | Walk into the opened area | You can enter it | |
| 9 | Walk back to the square and find Quartermaster Bram by the storehouse | He asks for six buildings and planks and bricks and points at the next step; carriers are already hauling planks to a forester site | |
| 10 | Press Tab, then Tab again | Overhead view of the same village; back to exactly where you were | |

### B2 Road economy (target: 5 to 8 minutes)

| Step | Do | Expect | Pass / fail / notes |
|---|---|---|---|
| 1 | In God View finish the commission's six buildings: forester (already started), woodcutter, sawmill, clay pit, charcoal kiln, brick kiln, each joined to the storehouse by road (Build B, Road R) | Only legal spots glow; a bad spot is refused with a sentence; sites ask for planks and bricks | |
| 2 | Watch one load go along a road; follow it on foot for part of the way | The same carrier and load are visible in both views | |
| 3 | Remove one road segment a carrier needs (Road, X) | The shipment shows why it is stalled (no road) | |
| 4 | Rebuild the segment | The shipment arrives once | |
| 5 | Reach the commission's planks and bricks (18 and 18 as built; Bram and the top line show the numbers) | Bram marks the commission done; talk to him | |

### B3 Strategy and military (target: 10 to 15 minutes; expect rough edges)

| Step | Do | Expect | Pass / fail / notes |
|---|---|---|---|
| 1 | Ring the bell in the square (E) or press End Round | Refused with a countdown until the 60 s harvest season is over; then numbers are drawn, matching hexes show a batch, the rival moves, demons may appear | |
| 2 | At each round of an age, take one card from the draft (it opens by itself in God View; on foot talk to Scribe Pell or press Tab); build one card at a city (Cards), discard one for coin | One pick per round; the built card changes something you can name (the card text says what) | |
| 3 | Try to found a city next to your own, then at a legal (glowing) corner (City F) | First is refused with the rule; second succeeds and scores | |
| 4 | Trade at the market (Market T, or talk to Hesta) | Stock changes by the stated rate | |
| 5 | Move the hero onto a demon (Hero J) | The demon clears; any blocked delivery resumes | |
| 6 | Build a mine, smelter, power plant and barracks; queue soldiers (Army N) | A missing supply or low power is named; training starts when supplies arrive | |
| 7 | Order the squad to attack the rival outpost | It walks there and the fight resolves in view | |
| 8 | Play to an ending | Win at the points target (18 as built), lose at the breach limit (5 as built), or a points comparison after the last age; the end screen says why | |

### B4 Save

| Step | Do | Expect | Pass / fail / notes |
|---|---|---|---|
| 1 | Wait until a carrier is walking a road with a load; walk somewhere else; press F5 | "Game saved" at the top | |
| 2 | Walk away, press F9 | "Game loaded"; you are standing where you saved, facing the same way, in the same view; the carrier finishes its delivery once | |
| 3 | Press F5 during the duel or the party | Refused with a sentence | |
| 4 | Quit (Esc, Quit twice), start the game again, press F9 | The saved game comes back | |
| 5 | Esc during the rhythm song | It does not pause (the music can't); a second Esc leaves the party | |

### What to send back

For each section: what you expected and did not get, where you stopped knowing what to do, anything that felt mistimed (rhythm) or unfair (duel), anything unreadable, and the three things you would change first. If the rhythm felt early or late, say which and roughly by how much.
