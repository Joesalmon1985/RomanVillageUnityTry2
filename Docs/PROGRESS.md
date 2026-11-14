# Progress

What was built and what actually ran, per milestone. Newest last.

## Milestone 0: skeleton and shared contracts (13 Nov 2026)

Built:
- Assemblies: `World.Sim` (no engine references, Newtonsoft only), `World.Game` (refs World.Sim, Bedsit.Core), `World.Sim.Tests` (EditMode), `World.Game.Tests` (PlayMode).
- Sim core: `Rng` (SplitMix64), `Ware`, partial `WorldState` and `PlayerState`, `Command`/`CommandResult`/`ICommandHandler`, `SimEvent`/`EventBuffer`, `ISimSystem` with `SystemOrder` slots, `WorldSim` (Submit, Step at 10 ticks/s, DrainEvents), `Reasons` (closed list and sentences), `WorldSimFactory` with empty Economy, Strategy and Military module stubs; Ward and Rhythm namespace placeholders.
- Board: `HexCoord`, `Terrain`, `BoardTopology` (radius R, int ids, adjacency, exact pointy-top geometry at 20 m per edge, BFS paths with an edge filter), `Board`, `BoardGenerator.CreateMvpBoard(seed)`.
- Game: `Assets/Scenes/World.unity` (one `WorldBootstrap`), `GameClock` with pause reasons, sun, camera at the home city, 400 m ground plane, `MaterialFactory` with two own shaders kept in builds through Resources materials, `UiStyle`. World.unity is the only build scene; `AgentScripts/BuildWindows.cs` targets `Builds/Windows/WorldMVP.exe`.
- Fast harness: `DotNet/WorldMVP.sln` runs the EditMode test sources under .NET 10.
- Contracts for parallel agents: `Docs/CONTRACTS.md`.

Ran:
- `dotnet test DotNet`: 43 passed, 0 failed.
- Unity recompile: completed, 0 compile errors; `unity command console status`: 0 errors (1 warning: Unity's Input Manager deprecation notice).
- `unity command test run --mode editor`: 43 passed, 0 failed (same sources as the .NET run).
- `unity command test run --mode playmode --async-tests`: 1 passed (`WorldBootTests.WorldScene_Boots_AndSimTicks`: scene loads, one bootstrap, sim ticks, pause reasons hold the clock).
- Play Mode capture `Evidence/screenshots/m0_boot.png` (git-ignored), inspected: green ground to the horizon under the default sky, debug overlay showing the tick advancing.

Not run: no Windows player build (not part of M0); nothing beyond the M0 gate.

## Milestone 1: walkable village (13 Nov 2026)

Built (all in `Assets/World/Game`, generated from `WorldBootstrap`; API for the next agents in `Docs/GAME_API.md`):
- Terrain: one flat walkable tile per hex (mesh collider, tinted, inspectable `HexView`), trees, rocks and low mounds, crop rows, convex-collider mountain peaks; pale edge lines and a post with disc and pennant at every vertex; hex labels in God View only; outskirts trees and hills; invisible circular bounds plus an avatar clamp; gradient sky dome, fog, trilight ambient. New shaders `World/Unlit`, `World/Glow` (additive), `World/Text` (depth-tested TextMesh), shipped through Resources materials.
- Village: paved 30 m square at the home vertex with curb, lamps and benches; enterable storehouse with door and sign; market stall with keeper spot; town bell (E rings it, `TownBell.Rung`); warehouse 24 m from spawn with neon door, coloured light spill and hue-cycling lights, dance floor, DJ decks, bar annex and ramp-reached balcony behind striped barriers (`WarehouseSet.SetRouteOpen`), procedurally synthesised 174 BPM drum loop at the door (3D, low-passed outside).
- Player: `FirstPersonAvatar` (CharacterController, locks), `GodCamera` (ortho), `ViewSwitcher` (Tab), `Picker` + `IInspectable`/`IInteractable`, `InspectPanel`, `BoardPicker`, `BoardSpace`.
- People: `PlaceholderActor` (procedural idle, walk swing, wave/greet/cheer/cast/clap/surprise/stretch), `ActorFactory`, `Npc`, `DialoguePanel`; Mira, Juno (door host), DJ Sol (decks), Quartermaster Bram, Hesta, Captain Orla, Scribe Pell.
- `EncounterHost`/`IEncounter`/`EncounterContext`/`EncounterDirectory` with `DebugEncounter`; `Hud` (crosshair, prompt, view, clock, objective, Esc pause menu); `AutoPlay` with `-autoplay <name>` and the `boot_tour` script.
- Agent scripts: `AgentScripts/M1Captures.cs` (capture states, boot_tour, fps), `AgentScripts/GameViewSize.cs` (Game view 1920x1080); `MakeWorldScene.cs` also creates the new Resources materials.

Ran:
- Unity recompile: 0 compile errors; `unity command console status`: 0 errors after the final runs (1 warning: Input Manager deprecation notice).
- `unity command test run --mode editor`: 43 passed, 0 failed (no sim changes in this milestone).
- `unity command test run --mode playmode --async-tests`: 9 passed, 0 failed: `WorldBootTests.WorldScene_Boots_AndSimTicks`; `VillageTests.Scene_Boots_WithVillage` (one avatar, one bootstrap/sim, 7 NPCs = the six roles plus the doorman, storehouse, market, bell, warehouse; Mira and Juno within 25 m and in the spawn camera view; door under 40 m from the square), `Walk_FromSpawn_ReachesMiraAndDoorHost_GreetingShows` (walk speed: Mira 0.84 s, Juno 3.89 s; greeting shown inside 4 m), `Tab_WhileWalkingAndInspecting_KeepsAvatarAndSelection` (twice: position under 1 mm, yaw/pitch under 0.1 deg, same sim, selection kept, no movement and marker in God View), `EncounterHost_DebugEncounter_PausesSurvivesTabAndRestores` (tick unchanged while running, survives Tab both ways, exact restore, clock resumes; abort path), `EncounterDirectory_And_DialogueOptions`, `Warehouse_SetRouteOpen_TogglesBarrierAndLights`, `Warehouse_Routes_BlockedWhenClosed_WalkableWhenOpen` (scripted walks into the bar and up the ramp onto the balcony deck, blocked when closed; walk door to decks, DJ greets), `BoardSpace_And_BoardPicker_RoundTrip`.
- Play Mode `boot_tour` via AutoPlay in the editor: logged `[AutoPlay] boot_tour OK` (editor does not quit; the `-autoplay` flag in a packaged player was not run).
- Captures at 1920x1080 in Play Mode, inspected and iterated (git-ignored, `Evidence/screenshots/`): `m1_spawn.png` (Mira, Juno and the lit warehouse door in view), `m1_godview.png` (whole settlement and most of the board with hex graph, labels and avatar marker; Storehouse selected), `m1_warehouse_interior.png` (dance floor, decks with DJ Sol, bar barrier, balcony), `m1_dialogue.png` (Mira's dialogue, duel option "Not available yet"), `m1_pause.png`.
- Editor frame rate at 1920x1080 (Game view, smoothed): 260 to 400 fps across the five capture states.

Not run: no Windows player build and no packaged `-autoplay boot_tour` run; no human playtest; audio was not listened to (the loop is generated and plays, volume and mix unchecked by ear).
## Milestone 3 (simulation half): road economy (13 Nov 2026, branch feat/economy)

Built (pure C#, `Assets/World/Sim/Economy`, API summary in `Docs/ECONOMY_API.md`):
- State partial `WorldState.Economy`: stores with reserved/incoming counts, roads on edges, cities, buildings and sites (data-driven `BuildingDefs` in `EconomyTuning`), carriers, delivery jobs, per-hex batches, rounds, ware ledger, commission.
- Commands: PlaceRoad, RemoveRoad, PlaceBuilding, CancelSite, SetPriority, ToggleBuilding, EndRound; refusals carry a code and a sentence.
- Systems in the BUILD_PLAN slots: round draw (seeded starter sequence, then 2 random numbers), harvest against authorised batches (forester rule), recipes, construction, job creation with reservation, carrier assignment and flag-by-flag movement, power plant fuel, diagnostics (one reason per building and job, `Describe`/`DescribeJob` sentences) and the commission.
- Hooks for other modules per sim (`EconomyApi.Hooks(sim)`): round ending/started, batch modifiers, blocked flags, power predicate.
- Starter: storehouse stock 14 planks + 4 stone, 4 carriers, 2-edge demonstration road, a Forester site at its end and its first planks already on a walking carrier at tick 0.

Ran:
- `dotnet test DotNet`: 69 passed, 0 failed (43 from M0, 26 economy: conservation over 20,000 ticks with random valid and invalid commands and random demons/power, single batch per hex per round, sawmill recipe once, construction and waiting_ware naming the ware, cancel conserves, nine stall reasons produced and cleared, interrupted route arriving once, invalid placements, road cost, requests/consume, hooks, headless commission, JSON round-trip continuing identically for 1,000 ticks).
- Headless commission (scripted player, no trading): default seed 20261113 at tick 2052; 20 other seeds all complete, worst at tick 2173 (budget 3600).

Not run: nothing in Unity (no compile there, no EditMode run in the editor, no PlayMode, no views); `.meta` files for the new scripts are left for Unity to generate after merge. Note: Windows Application Control intermittently blocked freshly built test DLLs; rebuilding with a different hash got past it.
- Ward duel rules (M2, feat/ward): scorer, Mira rival, time-stepped session, WardProgress in WorldState; 71 EditMode tests pass under `dotnet test DotNet`; API in `Docs/WARD_API.md`. Not run in Unity.
- Rhythm (feat/rhythm): `World.Sim.Encounters.Rhythm` (RhythmTuning, Chart, PackJson, FallbackChart, PadJudge, PhraseSession), 38 EditMode tests in `RhythmTests.cs` covering the G2 Rhythm rows, owner's lv1_warehouse pack and stems staged in `Resources/Rhythm/lv1_warehouse`, API in `Docs/RHYTHM_API.md`. `dotnet test DotNet`: 81 passed. Not run in Unity.

## Milestone 2: opening encounters as playable scenes (13 Nov 2026, main)

Built (details in `Docs/ENCOUNTERS_UI.md`): Ward duel with Mira (setup, active, result, rematch/return, cracking Wards and weave bolts, first-time help) and the rhythm party with DJ Sol (DSP-scheduled owner stems, count-in, 4-lane highway, route choice with doorway preview, result, retry/return, calibration), `SynthAudio` fallback loops, `AudioGuard`, AutoPlay `first_five`. Rule changes: R9 split (`Success` = 3+ passes, route iff final block passed, retry always), `WardTuning.Practice` + `ward.json` (Mira think 6-10 s, first cast 3-6 s), `WardProgressApi.RecordDuel` (sanctioned write, WardProgress only). Outside owned paths: `WarehouseSet` low-pass cutoff written in coarse steps and only when audio is healthy (ADTM fatal-error fix).

Ran:
- `dotnet test DotNet`: 141 passed (Windows Application Control blocks the deterministic test DLL at times; `-p:Deterministic=false` gets past it).
- Unity EditMode (`--async-tests`): 141 passed. PlayMode: 16 passed (9 existing + 7 new encounter tests). Console 0 errors.
- Mira balance: scripted first-time player wins 54% (3 essences) / 60% (4 essences) over 300 seeds.
- AutoPlay `first_five` in editor Play Mode: `[AutoPlay] first_five OK` (duel Victory after 3 casts; phrase 4/4 blocks, 20 perfect, balcony).
- Captures inspected (1280x720 Game view, git-ignored): m2_duel_setup, m2_duel_active, m2_duel_result, m2_rhythm_countin, m2_rhythm_midphrase, m2_rhythm_route_choice, m2_rhythm_result, m2_bar_open, m2_balcony_open.

Not run / caveats: nobody listened to the audio. This machine's audio output was unstable during the run (FMOD fell back to emulated output; the DSP clock stalled several times), so some rhythm runs used the real-time clock fallback; the drum playhead vs DSP schedule check ran once (0.0 ms, emulated output). No packaged build, no human playtest.
## Milestone 4 (simulation half): strategy, military and save codec (13 Nov 2026, branch feat/sim-integration)

Built (pure C#, API in `Docs/STRATEGY_API.md`):
- `Sim/Strategy`: prologue until the commission, then 3 ages x 2 rounds hooked on the economy's round end/start; endings (8 VP, 4 breaches, points after Age III; outcome + reason); FoundCity (`too_close`, `no_road`, `occupied`, `cannot_afford`), market Trade/Sell/Buy (Market building and Trade Charter improve the rate); infection deck, demons blocking carriers and city batches, breaches, hero moves (`no_turn_action`); draft 3-pick-pass-pick for both sides with a deterministic rival, 18 original cards (6 per age) in one table, all effects through real hooks; wonder stages paid by delivered wares; score breakdowns; rival plan VP; UI query helpers.
- `Sim/Military`: power allocation (`IsPowered`, priority then id), barracks queues with wares delivered to and consumed at the barracks, training only while supplied and powered, squads (one per barracks, max 4), delegated Defend/Attack orders over any edge, fixed-tick simultaneous combat, outpost with garrison (+1 per age), raids from Age II that hold a road flag for a round.
- `Sim/Save`: `SaveCodec.ToJson/FromJson/CreateSim/Load` with version, player pose and string extras.
- Economy edit (minimal): `EconomyHooks.EndRoundGuards`, checked by `EndRound` (draft pending, game over).
- Design choices to review: Age I waits for the commission (at most 8 prologue rounds) and cities and wonder stages wait for Age I; batches at player cities grow by 2 per age; tuned costs (city Planks 2 Stone 3 Grain 1; wonder stage 1 Planks 4 Stone 6, stage 2 Rations 2 Ingots 2; unit 1 rations + 1 ingots; guards HP 8 damage 2; rival plan VP on age-rounds 2, 4, 6).

Ran:
- `dotnet test DotNet`: 171 passed, 0 failed (135 earlier + 36 new: StrategyTests 19, MilitaryTests 7, SaveTests 4, StrategyConservationTests 1, StrategyPlaythroughTests 5 with headless scripted games).
- Balance (default seed 20261113, scripted players): competent points player wins 6-5 on points after Age III at tick 4473 (7.5 min); it wins 9 of 10 other seeds (5.8-8.5 min). Passive player (only ends rounds) loses 2-6. Completionist player (barracks, units, attack) reaches an ending on the default seed and 10 others in 11.7-18.4 min with all invariants checked every tick; its attacks destroy the outpost on 2 of 11 seeds.
- Conservation with strategy and military commands: 20,000 random-command ticks, all invariants held.

Not run / known limits: nothing in Unity (no compile, no EditMode run in the editor, no PlayMode); `.meta` files left for Unity. Throughput is bound by the economy's 4 carriers, so wonder stages rarely complete inside 6 rounds in the scripted games and no scripted game reached 4 breaches (breach loss is covered by EditMode tests only).

- M4 balance pass (feat/sim-integration): each founded city adds 2 carriers (`EconomyApi.AddCarrier`), infection intensifies from Age II (discard back on top), returning raids only refill the garrison, scripted players wait for in-flight work and the hero targets flags near breaching. `dotnet test DotNet`: 172 passed. Measured: competent player reaches 8 VP on the default seed (9.3 min, both wonder stages) and 10/10 other seeds (7.0-12.1 min), wonder stage 1 in 9/10; full player ends in 12.3-15.0 min, full squads of 4 took the outpost 4 of 7 times; careless player (no hero) loses by breach on the default seed (13.0 min) and 7 of 11 seeds; passive player loses; commission unchanged (tick 1813, 3.0 min). Not run in Unity.

## Map rework: radius-4 board with fine building plots (13 Nov 2026, feat/sim-integration)

Built (pure C#; ECONOMY_DESIGN section 10, Map row):
- `BoardTopology` on two levels: coarse hexes, 150 corners and 210 hex edges (60 m) for cities, outpost, distance rule and hero; fine 570 flags (corners + 2 intermediate points per hex edge) and 630 road segments (20 m) for buildings, roads, carriers and squads. All `Vertex*`/`Edge*` members now mean the fine graph; new coarse members (`IsCorner`, `Corners`, `HexCorners`, `HexSides`, `HexFlags`, `CornerNeighbours`, `HexEdgeSegments`, `HexEdgeOfSegment`, `CornerDistance`, `FindCornerPath`, ...). `EdgeLength` = 20 (segment), new `HexEdgeLength` = 60. `HexVertices`/`HexEdges` removed. Cached BFS distance tables. Ids documented and pinned in tests.
- `BoardGenerator`: fixed radius-4 layout, `Terrain.Pasture` (no ware yet), numbers dealt evenly per terrain, home corner 70, rival outpost corner 97 (5 hex edges, own Forest/Hills/Fields), Mountain range of 4 hexes outside the home territory, `Board.SuggestedCitySites` (14 corners), warehouse flag 361 (2 segments out).
- Rules: buildings on any free flag within `TerritorySegments` = 4 segments (80 m) of a city corner; roads per segment, 1 planks pays for `RoadSegmentsPerCost` = 3 segments (`RoadCredit`, `EconomyApi.RoadCostFor`); cities only on corners, no city on a `CornerNeighbour`; hero moves one hex edge and clears the flags it walks over; infection deck = the flags of the home territory (`InfectionRadius` = 4 segments), drawn intermediate flags only take a demon while they hold a building, road end or city (chosen so demons still hit the home cluster, keeping the careless-player and demon tests meaningful); squads 20 ticks per segment (was 40 per edge; the outpost is now 15 segments away).
- Scripted players adapted: hero routes over corners, city roads limited to 3 new segments (the old 60 m limit), road costs via `RoadCostFor`, the power plant gets high priority with the barracks.
- Docs: CONTRACTS (Board), ECONOMY_API, STRATEGY_API, ECONOMY_DESIGN (Map row done), new MAP_MIGRATION.md (Unity changes needed).

Ran:
- `dotnet test DotNet`: 181 passed, 0 failed (172 before; new topology, layout, territory, road-credit, infection-deck and hero-walk tests; no test removed).
- Before / after (same scripted players): headless commission default seed tick 2052 -> 1452 (3.4 -> 2.4 min), worst of 20 seeds 2173 -> 1692; strategy player's commission 1813 -> 1692. Competent player: default seed 9.3 -> 8.4 min (both wonder stages), 10/10 -> 10/10 other seeds win on points, 7.0-12.1 -> 7.6-9.7 min, wonder stage 1 in 9/10 -> 10/10. Full player: 12.3-15.0 -> 11.4-15.1 min, full squads of 4 took the outpost 4 of 7 -> 6 of 8. Careless player (no hero): breach loss on the default seed 13.0 -> 14.2 min, 7 of 11 -> 6 of 11 seeds. Passive player still loses.

Not run: nothing in Unity. The Unity layer in the main checkout still assumes radius 2 and 20 m hexes; see `Docs/MAP_MIGRATION.md`.

## Map migration: Unity layer on the radius-4 board (13 Nov 2026, main)

Merged `3e732c4` (map rework) into main (PROGRESS conflict only; main's Encounters rules kept; Unity-generated `.meta` files committed). Then migrated `Assets/World/Game` and the PlayMode tests (details ticked off in `Docs/MAP_MIGRATION.md`, API in `Docs/GAME_API.md`):
- Terrain: 61 tiles of 60 m hexes through the 18 flags of each hex; props one mesh per hex, 4 m off the hex edges, counts scaled about 7x; new Pasture look (fence with gate, two sheep flocks, trough); hex-edge lines; corner posts; God View overlay with hex labels (grow with zoom-out), plot discs at the 420 intermediate flags, rings at suggested city sites; island rim, beach and sea; fog 140-700 m.
- Village: compact, within about 55 m of the home corner inside a 58 m clearing; square radius 12 m; storehouse 8 x 6 m, market stall, bell, 5 cottages 6 x 5 m, all 9+ m from other flags and 7+ m from the edge lines; warehouse rescaled from about 32 x 28 m to 17.75 x 22 m (hall 13 x 22 x 7 m, bar annex, balcony at 3.4 m), beside warehouse flag 361 with its door facing the square (spawn to door 22 m, to Mira 7 m, to Juno 17 m).
- Rival outpost placeholder (stockade, watchtower, banner; inspectable "Rival outpost") at corner 97, about 215 m north-west.
- God camera: default framing = village + neighbouring hexes (size 115), max zoom = whole island (about 495), H / M keys, wheel zooms about the mouse; BoardPicker `CornerAt`, `HexEdgeAt`; BoardSpace corner/hex-edge helpers; NPC name plates hidden when zoomed out.
- AutoPlay `MeasureFps` step (boot_tour, first_five) and `-novsync`.

Ran:
- `dotnet test DotNet` after the merge: 187 passed, 0 failed.
- Unity: recompile 0 errors; EditMode 187 passed; PlayMode 21 passed (16 earlier + 5 new `MapLayoutTests`); console 0 errors.
- Editor Play Mode (1920x1080 Game view): AutoPlay `boot_tour` OK and `first_five` OK (duel Victory after 3 casts; phrase 4/4 blocks, 20 perfect, Balcony); about 320 fps first person, 285 fps God View.
- Windows player rebuilt (`BuildWindows.Run`, Succeeded): `-autoplay first_five` at 1280x720 exit 0, `[AutoPlay] first_five OK`, no errors or exceptions in the log (60 fps, vSync on); `-autoplay boot_tour -novsync` at 1920x1080: 818 fps first person at spawn, 1,123 fps God View default, 1,108 fps whole island.
- Captures inspected (git-ignored): map_god_whole_island, map_god_home, map_spawn, map_warehouse_interior, map_village_from_hill, map_outpost, map_pasture.

Not done / caveats: no economy views (roads, buildings, carriers; feat/econview not merged); no fast travel; the outpost has no colliders (walk-through placeholder); mountain peaks keep convex colliders; nobody played it by hand.

## Unified economy (simulation) and its headless proof (13 Nov 2026, feat/sim-integration)

Built (pure C#; `Docs/ECONOMY_DESIGN.md` sections 1-9, all rows of section 10; status IMPLEMENTED, tuned numbers in its section 11):
- Wares (Core): raw Logs, Clay, Wheat, Sheep, Ore (Pasture yields Sheep); processed Planks, Coal, Bricks, Flour, Bread, Meat, Rations, Iron, Weapons. Timber/Stone/Grain/Ingots removed. Save version 2 (older saves refused).
- Economy: 20 building types (Clay pit, Mine, Smelter replace Quarry, Ore pit, Refinery; new Sheepfold, Butcher, Tavern, Charcoal kiln, Brick kiln, Smithy); construction cost in Planks/Bricks by size; roads 1 planks + 1 bricks per 3 segments; the Mine eats 1 rations per 2 ore; power plants burn coal only while something needs power; a demon or raid on a building's flag stops it; the robber stops a city's hexes; fair-share deliveries (least on hand first), stockpiling jobs last, carriers wait at their last flag, +2 carriers per finished building; detailed sink ledger (recipes, construction, roads, external) and batch ledger; new commission (six chain buildings, 10 planks + 10 bricks); prologue starter draws; UI helpers (ware names and colours, build menu with needs/makes/recipe text, legal build flags, waiting-for).
- Strategy: outbreaks to the connected neighbouring sites with chains (each site once per draw), epidemic at each age change, infection deck = the village core plus city corners; age-end military comparison (+1/+3/+5, -1, mirrored, recorded); 18 cards in five families with real hooks (batches, workshop output, market, coin, roads, points, shields, training, damage, hero moves, demon cap, infection draws); city cost 3 planks + 3 bricks + 2 rations; wonder stages Planks + Bricks then Iron + Rations (2 and 3 points); target 12, breach limit 5; rival with a visible plan, garrison shields and raids. Forecast, shields summary, score breakdowns, cards by family.
- Military: soldiers cost 1 weapons + 1 rations delivered to a powered barracks; shields; smelter power demand only when ready.
- Scripted players rewritten for the new chain (commission, food chain, iron chain, barracks, wonder, cities, coal management, chain-aware placement, round ending when the forest is felled); `GameRuns` shares the full games between the playthrough tests and the proof.
- Proof: `EconomyProofReport` writes `Docs/ECONOMY_PROOF.md` (ware audit, five traced chains from real events, six paired cross-system runs, conservation over four 20,000-tick random runs, endings on 11 seeds x 4 players, balance table with verdicts, known weaknesses) and asserts every claim.
- Docs: ECONOMY_API and STRATEGY_API rewritten; CONTRACTS, MAP_MIGRATION, ECONOMY_DESIGN updated.

Ran:
- `dotnet test DotNet`: all passed (number in the commit message of this step; 181 before; no test removed, every adapted; new `UnifiedEconomyTests` and the proof).
- Proof headlines (`Docs/ECONOMY_PROOF.md`): every ware made and used; five raw chains traced draw -> harvest -> carries -> recipes -> final use; conservation error 0 over 4 x 20,000 random ticks; competent player wins 11 of 11 seeds in 8.1-13.3 min; careless player loses 10 of 11 (8 at the breach limit); passive player loses 11 of 11 (by breach, about 3.3 min); logs are the binding bottleneck in 9 of 11 games; commission at 1.8 min on every seed (target was 4-5: a known weakness).

Not run: nothing in Unity (no compile there, no EditMode or PlayMode run in the editor). The Unity layer still uses the old ware and building names; it must be rebuilt from `Docs/ECONOMY_API.md` and `Docs/STRATEGY_API.md` once the owner has read the proof.

## Unified economy merged into main (13 Nov 2026, main)

Merged feat/sim-integration (`c76a8a3`, `58d8c2e`, `b94c7a4`) into main; conflicts only in PROGRESS and MAP_MIGRATION (both sides kept). Unity-generated `.meta` files for the new sim and test files committed. `Assets/World/Sim` untouched. Game-layer changes, kept minimal:
- `Actors/VillageNpcs.cs`: Quartermaster Bram's greeting says planks and bricks (no stone); his lines are rebuilt each time the dialogue opens and describe the new commission from the economy helpers (`EconomyApi.DescribeType(..).Name` for the six buildings, `EconomyApi.CostText` with `Tuning.CommissionPlanks/Bricks`, `StorehouseCount`/`WareName` for current stock; "done" once `Commission.Complete`). The six types mirror the documented rule because the sim keeps its list internal. Hesta says bread instead of grain.
- `View/TerrainBuilder.cs`: the hex inspect line uses `Wares.DisplayName` (Pasture now gives sheep; the "gives nothing yet" branch is gone).
- `Tests/EditMode/EconomyProofReport.cs`: `[Timeout(1200000)]` on the proof test; Unity's EditMode runner timed it out at its 180 s default (it passes under .NET). No assertion changed.

Ran:
- `dotnet test DotNet`: 209 passed, 0 failed (203 from the branch + main's 6 encounter rule tests).
- Unity: recompile 0 errors; EditMode 209 passed (first run 208 + the proof test timing out, then 209 after the timeout attribute); PlayMode 21 passed; console 0 errors.
- Editor Play Mode (1920x1080 Game view): AutoPlay `boot_tour` OK (about 323 fps first person, 304 God View) and `first_five` OK (duel Victory after 3 casts; phrase 4/4 blocks, 20 perfect, Balcony). Quartermaster lines read back in Play Mode.

Not done: no economy views (feat/econview still unmerged and written against the old board and wares); Windows player not rebuilt; no captures; nobody played it by hand.

## Road economy in the game: views, God View tools, first-person commission (13 Nov 2026, main)

feat/econview was not merged: its God View shell (toolbar, tools, commands, overlay, HUD, problems, inspect forwarding, beacons) was ported with light edits and committed first; everything else was rewritten against the unified economy helpers (`Docs/ECONOMY_UI.md`). `Assets/World/Sim` untouched.

Built:
- Views (`View/Economy`): an original primitive model for each of the 20 building types (inside its size-class footprint; turning parts, work glows), placed beside its flag clear of the road line and the village (`BuildingPlots`), with sites (footprint, scaffold, delivered materials, progress), a badge that also turns red for `Building.Blocked` and robbed hexes, and a sign; roads per 20 m segment; flags with ware stacks in `EconomyApi.WareColour`; carriers interpolated by `Tuning.TicksPerEdge` with a coloured load crate (and a God View disc), waiting beside their last flag, idle block at the storehouse; batch markers on every hex with units left. Reconciled every tick and rebuilt from state alone; roads rescanned only on road events; terrain props cleared from building plots per hex; picking looks only at the hex under the mouse.
- God View: Build (`BuildMenu`, cost/needs/recipe, only `LegalBuildFlags` highlighted and cached, refusals as the sim's sentence), Road (per segment around the network, build/remove, cost rule and next-segment cost), Inspect (buildings, sites, carriers, roads, hexes, batches; priority, on/off, cancel), Problems (every stalled thing with its sentence, click to focus), Wares (14 wares, stock, producers, consumers), End Round (refusals such as `draft_pending` and `game_over` shown as the sentence, also in its tooltip). Extension points: `GodToolbar.Register`, `GodToolbar.StatusItems`, `GodCommands`.
- First person: Bram's commission dialogue (progress buildings x/6, planks, bricks; next step with a beacon; building plans; acknowledgement), the bell ends the round like the button, signs offer the same commands, the objective follows the commission.
- AutoPlay `commission` (God View tools, rules of the headless CommissionScript), PlayMode `EconomyViewTests` (8), `AgentScripts/EconCaptures.cs` (capture states, a commands-only late-game driver, fps measurement), `Docs/ECONOMY_UI.md`.

Ran:
- Unity recompile 0 errors; EditMode 209 passed; PlayMode 29 passed (21 earlier + 8 new `EconomyViewTests`), run twice (the second after the last fixes); console 0 errors.
- Editor Play Mode, 1920x1080 Game view: AutoPlay `boot_tour` OK (271 / 235 / 255 fps), `first_five` OK (duel Victory after 3 casts; phrase 4/4 blocks, 20 perfect, Balcony), `commission` OK (complete at tick 1138, round 2, 10 tool actions, 1 round ended).
- Performance (editor, vSync on, late game built by `EconCaptures.LateGame` through commands): 32 buildings and 66 carriers: God View home 129 fps, close 140, whole island 159, first person 243; after the final fixes, 26 buildings and 58 carriers: 215 / 223 / 228 / 329 fps.
- Captures looked at (git-ignored `Evidence/screenshots/econ_*.png`): loaded carrier from first person and God View, build menu with hover, build refusal sentence, road tool, stalled site inspect panel, problems, wares, Bram's dialogue, home territory late in the commission (wide and close), late game (wide, close, smelter inspect, first person tavern), all 20 models (God View and first person). Fixed from them: buildings overlapping cottages, labels over the toolbar, Bram hemmed in by idle carriers, carriers on one road overlapping, batch tags over hex labels, a sinking ladder, "used by -buildings".

Not run / caveats: `-autoplay commission` was not run in a packaged player (the Windows player was not rebuilt, as instructed); nobody played it by hand. No draft panel exists, so once Age I starts End Round stays refused with `draft_pending` for a human player (the late-game driver picks cards by command). Demons, cities, hero and squads have no views yet, so "demon on its flag" badges show without a visible demon. The commission building list is internal in the sim (`EconomyLogic.CommissionTypes`); the Game layer mirrors it. Captures are 1280x720 (the capture tool's size); fps was measured at 1920x1080.

## Economy rebalance: the four games connected for real (13 Nov 2026, feat/sim-integration)

The owner's review of the proof found 7 Wonders, the ages and Catan's "every resource matters" connected only nominally. Fixed by rule changes and tuning; the public surface is unchanged (nothing renamed or removed), additive API listed below. Save version 2 -> 3.

Why cards scored 0: the score line was right. On the default seed (the one the proof printed) the competent player built Drill Yard, Lumber Camp and Ward Stones, no civic card: it had drafted the Shrine but never had the 1 coin it costs (coin started at 0), and Great Hall came in Age III after the game ended. Over the 11 seeds it drafted 4-6 cards (2 per age, games ended in Age II-III) and built 1-3, because the goals reserved every ration and brick; Monument was the only civic card built (8 of 11). The rival built nothing (`RivalMaxBuilt` = 0). Why ages were not felt: rounds cost nothing to end and unharvested batches carry over, so the scripted player rang the bell 15 s into a round whenever the forest had no batch; an age was mostly its last round.

Rule changes (code): harvest season (`EndRound` refused with `round_too_soon` until `MinRoundTicks` = 60 s after the round started); one draft pick per round (4 cards each at the age start, 3 picks, hands passed between rounds; `DraftPickPerRound`, `DraftNextStep`, `DraftResumed`); the rival builds the point and shield cards it drafts (2 per age, 1 of them shields; values a card it cannot build only as denial); 4 coin when Age I begins; economy targets (`StockTarget`: a workshop pauses while the storehouse holds enough of its output, reason `output_full`); per-terrain batch sizes (`TerrainBatchSize`); 6 new cards (24); varied prologue was tried and dropped (see weaknesses).

Tuning log (each step re-ran the 44 scripted games with the `BalanceProbe` explicit test):
1. Harvest season 60 s, per-round draft, rival builds, commission coin, 6 cards: rounds 60-100 s; rival 3-9 card points; competent wins fell to 6-8 of 11 (rival military cards too strong).
2. Recipes: charcoal 1 log (from 2), bricks 2 clay (from 1), bread and meat 1 per input (from 2), smelter 2 ore (from 1); harvest 60 ticks; batch 10, age bonus +1: waste fell to 0-6% except ore; logs still bound 10 of 11.
3. Per-terrain batches, rival plan +1, scripted card values by age and build-from-store, wonder stage 2 ordered without stockpiled iron: cards from 3+ families 9 of 11, wonder stage 2 9 of 11.
4. Economy targets: binding ware spread to logs 3, iron 2, rations 2, ore 2, clay 1, weapons 1 (logs had looked binding because surplus planks and coal were made from every log).
5. Rival limited to 1 shield card per age, Stockade and Siege Works 1 shield, flat garrison 2: losses from the Age III comparison stopped.
6. Hero 6 moves (from 4) and the scripted hero clears demons on cities first, the squad drives off raids, more soldiers while the rival's shields match: competent 10-11 of 11.
7. Prologue: random starter tried (commission 2.9-7.9 min), back to the fixed starter; commission 18 + 18 (from 10 + 10): 3.2 min. Cover goals for pasture and mountain hexes, mine 1 rations per 3 ore: ore waste 14% -> 0%.
8. Forest 12, mountain 5, target 18 (from 12), harvest 50 ticks: 10 of 11 competent and 10 of 11 full games in 10-15 min.
9. One carrier per building tried: 33 carriers 87-92% busy, games 14-16 min, logs waste 11.5%: reverted to 2.

Before -> after (11 seeds, `Docs/ECONOMY_PROOF.md`):
| Item | Before | After |
|---|---|---|
| 1. 7 Wonders | player card points 0 on the shown seed (0-4 over seeds), 1-3 cards built, 1-2 families; wonder stage 2 in 0 of 11; rival builds 0 | 6-7 cards, 3-4 families in 11 of 11, civic points 2-6 in 11 of 11; wonder stage 2 in 11 of 11; rival builds 5-6 cards (6-7 points, 2-4 shields); paired runs: commerce (Road Guild 4 -> 2 road wares), military (Age I Loss -> Win), lore (Ward Stones breach 1 -> 0), draft (first-card drafter: rival 9 points 2 shields; competent drafter: 7 points 3 shields) |
| 2. Ages | Age I rounds at 3.3, 3.6, 3.8 min; rounds 15-100 s | rounds 60-90 s (mean 68); Age I 3.0-3.9, Age II 3.0-4.1, Age III up to 3.9 min; a pick and a harvest every round |
| 3. Catan | logs bind 9 of 11; 13-29% of clay, wheat, sheep, ore lost to the cap; a win without ore; 2 wins without soldiers | binding: rations 5, planks 4, logs 1, ore 1; lost to the cap 0.0-0.3% per raw ware (worst game 3%); all five raw wares harvested and used in 11 of 11; soldiers in all 22 competent and full games |
| 4. Length | 5 of 11 under 10 min (8.1-13.3), target 12 | target 18; competent 10 of 11 (10.3-15.6), full 10 of 11 (10.2-15.3) in 10-15 min; iron and weapons in all |
| 5. Commission | 1.8 min | 3.2 min (same on every seed) |
| 6. Keep | competent 11/11, careless loses 10/11 (8 breach), passive 11/11, conservation 0, carriers 48-50 | competent 11/11 (full 11/11), careless loses 11/11 (8 breach), passive 11/11 (breach, 10-12 min), conservation 0, carriers 54 (unchanged rule) |

Additive API: `EconomyApi.BellTicksLeft`, `EconomyApi.RoundElapsedTicks`, `EconomyRefusals.RoundTooSoon` (`round_too_soon`), `EconomyState.RoundStartedTick`, `EconomyTuning.MinRoundTicks`, `TerrainBatchSize`, `BatchSizeFor(terrain)`, `StockTarget`; `StrategyState.DraftNextStep`, `DraftResumed` event, `StrategyTuning.DraftPickPerRound`, `CommissionCoin`, `RivalBuildsPerAge`, `RivalShieldCardsPerAge`, `StrategyLedger.CoinFromCommission`; cards `village_green`, `watch_post`, `aqueduct`, `stockade`, `forum`, `siege_works`. Behaviour the Unity layer must follow: `DraftPending` becomes true at every round start of an age; the bell is refused for 60 s after each round start; the rival emits `CardBuilt` with `PlayerId` 1.

Ran:
- `dotnet test DotNet`: 207 passed, 0 failed (203 before; no test removed; adapted: batch sizes, recipes, draft and save tests, coin, hero moves, target; new: harvest season, per-round draft, rival build limits, per-round draft save round trip; explicit `BalanceProbe` not part of the run).
- `Docs/ECONOMY_PROOF.md` regenerated; new checks in the proof for every goal above (families, civic points, rival cards, wonder stage 2, the draft changing the rival, card links, bottleneck spread, cap losses, round length, game length, iron chain, commission window, competent >= 10, careless >= 9 mostly breach).

Not run: nothing in Unity. Still weak (proof section G): the prologue is identical on every seed; one competent and one full game run 15.3-15.6 min; carriers 54; surplus clay, logs and sheep wait in the storehouse (economy targets stop the workshops, so nothing is wasted, but they are not scarce); the rival has no economy; the scripted players are rules of thumb.

## Strategy and military UI: a whole game playable from the prologue to an ending (13 Nov 2026, main)

Merged feat/sim-integration (economy rebalance, `d630f5f`) first, then built the strategy presentation (`Docs/STRATEGY_UI.md`). `Assets/World/Sim` untouched; the Game layer reads every number, name and cost from state, tuning or the sim's helpers (the full_game driver keeps the headless player's own heuristics: card worth, round length and kiln thresholds).

Built:
- Draft modal (pauses the clock with `PauseReasons.Draft`, opens itself in God View, Scribe Pell in first person): one pick per round, each card's family, cost, effect and affordability, the rival's picks and built cards, your hand with build-at-a-city and discard-for-coin; Cards panel with built cards per city.
- HUD strip in both views (age, round, numbers, points you : rival with breakdown on hover/click, breach track, coin, power, next infection draw) and a state-driven What now? line; toasts and banners for every strategy and military event; objective follows the headline; end screen (outcome, why, minute, both score breakdowns, comparisons; New game with a fresh seed reloads the scene from state; Keep looking around).
- God View tools Market, Cards, City (legal corners, verdict on hover), Hero (targets, demons cleared on the walk), Army (power, barracks status/progress/queue, shields and comparisons, squads with Attack outpost / Defend home / order to a flag / Cancel, raids), Wonder (stage costs, delivered goods, build stage).
- World views: walled cities with territory rings, demon creatures on flags (count tags, FULL at the cap, outbreak and epidemic bursts), robbed hexes marked, the hero walking the hex edge, squads (soldiers, guards, raiders) walking and fighting, outpost flames and ruin, wonder stages, power bolts on smelter, barracks and plants.
- First person: Hesta opens the market, Captain Orla the barracks and squads, Scribe Pell the draft (or explains the age; cards, wonder).
- God View label declutter (building tags only for selected, hovered or stalled buildings when zoomed out; tags kept clear of the strip); toolbar labels fit 12 tools.
- Rebalance fallout: End Round and the bell show the harvest-season countdown (`BellTicksLeft`); `output_full` reads as idle (blue), not as a problem; the commission driver and the economy PlayMode tests wait out the season by state.
- AutoPlay `full_game` (`FullGameDriver`: the headless competent/completionist player ported, every command through the UI paths); PlayMode `StrategyViewTests` (6); `AgentScripts/StrategyCaptures.cs`.

Ran:
- `dotnet test DotNet`: 213 passed, 0 failed (after the merge).
- Unity: recompile 0 errors; EditMode 213 passed; PlayMode 35 passed (29 earlier, 2 of them adapted to the harvest season, + 6 new `StrategyViewTests`); console 0 errors.
- Editor Play Mode, 1920x1080 Game view: `boot_tour` OK (373 / 295 / 295 fps), `first_five` OK (duel Victory after 3 casts; phrase 4/4 blocks, 20 perfect, Balcony), `commission` OK (complete at tick 2766, round 4, 12 tool actions, 3 rounds ended), `full_game` OK three times (the last after the final fixes): Victory by points 20-9 at game minute 13.0, 14.3 and 13.0 (13 rounds, 3 cities, 7 cards, wonder 2 stages, 5 soldiers, outpost destroyed, 0 breaches, 0 refusals; the start tick differs by the half second before the driver takes over).
- Performance, late game (Age III, round 1, competent driver; 2 cities, 3 demon sites, 3 robbed hexes, 3 squads), editor, vSync on, 1920x1080: God View home 178 fps (worst frame 10.3 ms), wide 168, whole island 151 (worst 21.4 ms), first person 241.
- Captures looked at (git-ignored `Evidence/screenshots/strat_*.png`, 1280x720 from the capture tool): draft hand (first person strip and God View modal), second city with its ring, market, wonder after stage 1, demons on flags in God View and on foot, robbed hexes, hero tool targets, barracks stalled and training, a squad fighting raiders at the mine, a squad engaging the outpost guards, HUD at breach 4 of 5, end screen (victory and defeat). Fixed from them: clipped toolbar labels, strip overflow and overlap with the inspect panel, banners over the left panel, toasts over the end screen, the power bolt hidden behind the name tag, tiny demons and soldiers under big discs in God View, guards not meeting attackers at the outpost, "After the trade: -4 clay", noisy red barracks toasts, labels behind the strip.

Not run / caveats: `-autoplay full_game` was not run in a packaged player (the Windows player was not rebuilt, as instructed). Nobody has played it by hand. The draft only opens itself in God View. Late-game God View still stacks demon tags round the home village when many sites are full. Squads at one flag share a small circle. Not in scope: save/load keys, the play guide.

## Save, guide and Windows build: the MVP packaged (13 Nov 2026, main)

Built:
- Save and load (`Boot/SaveSystem.cs`): one slot under `Application.persistentDataPath` (`worldmvp_save.json`; AutoPlay runs use `worldmvp_autoplay_save.json`). F5 saves, F9 loads, and Save / Load / New game / Quit buttons in the Esc menu (Load, New game and Quit confirm with a second click). The file is the sim's `SaveCodec` JSON (version 3, WorldState including Ward progress) plus game-layer extras: position, yaw, pitch, view mode, God View framing, opened warehouse routes, Bram's acknowledgement, objective line. Loading hands the parsed state to `WorldBootstrap.Awake` and reloads the scene, so every view is rebuilt from the loaded state (the New game path). Refusals with a sentence: saving during the duel or party, saving with an order still queued, loading with no save, a damaged file, an older or newer version.
- Pause menu and guide (`UI/Hud.cs`): Controls and How to play pages matching the real keys, targets read from state; a notice line for save, load and pause messages over every screen.
- Rhythm phrase and pause: the known issue (pause menu during the phrase left the music playing) is handled by not pausing: during the count-in and phrase Esc shows "The music can't pause during the phrase. Press Esc again to leave the party."; a second Esc within 3 s aborts the party (stems stopped by the existing abort). The duel already paused with the menu.
- Fast travel: God View tool "Go here" (L, `UI/God/TravelTool.cs`): click open ground inside the board and the avatar stands there in first person; refused off the board, on peaks, roofs and rocks, inside building and city volumes, or where something solid stands.
- Bug found and fixed: the draft deadlocked for a human player. The draft pauses the clock; a pick is a queued command that applies at the next tick; so the pick never applied and the game stayed paused. The PlayMode draft test stepped the clock by hand and hid it (confirmed: with the fix disabled the updated test fails). `GameClock` now steps one tick while it waits only for the draft and a command is queued.
- `AgentScripts/BuildWindows.cs`: World scene to `Builds/Windows/WorldMVP.exe`, release (no development watermark), fullscreen window at native resolution (Alt+Enter for a window), product WorldMVP, managed stripping off (Newtonsoft reflection), Burst debug folder removed, and the probe's `Resources` folder (Sims 2 animation names) parked outside the build and moved back afterwards.
- AutoPlay: `ContinueAfterLoad` (scripts survive a load's scene reload), `save_load`, `capture_tour`; `[World] ready: ...` log line at boot.
- Docs: `PLAY_GUIDE.md` (new), `TEST_PLAN.md` (Part B rewritten for the game as built; Part A coverage notes per gate), `GAME_API.md`, `HANDOFF.md`, `README.md`.

Ran:
- `dotnet test DotNet`: 213 passed, 0 failed (no sim change).
- Unity: recompile 0 errors; EditMode 213 passed; PlayMode 40 passed (35 earlier + 5 new `SaveLoadTests`: carrier mid-road save, F9 load through the scene reload, state equal at load, the shipment delivered exactly once and the loaded game equal tick for tick to a headless load of the same file after 400 ticks, pose/view/God View framing/Ward count/opened route restored; corrupt, older, empty and missing saves refused and the game goes on; saving refused in an encounter; Esc during the phrase; Go here; plus the draft test now using the real clock); console 0 errors.
- Captures (editor, git-ignored): pause menu both pages, save notice. Packaged (beside the exe, git-ignored): spawn, menu both pages, warehouse interior, God View home and island: no magenta materials, fonts and UI scale correct at 1280x720.
- Build: Succeeded, 0 errors, 100 MB reported (103 MB folder), about 9 s (incremental). Contents checked: no LocalData, LocalBaked, config.json, `*.package` or probe animation list; the only Sims-related bytes are the compiled code assemblies (OpenTS2.dll, Bedsit.Sims2.dll, RomanVillage.Probe.dll), which are never called by the game.
- Packaged runs of the final build (`-novsync -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile`, one at a time): `boot_tour` exit 0, 22 s, OK (fps 1043 / 868 / 787 at 1280x720); `first_five` exit 0, 68 s, OK (duel Victory after 3 casts; phrase 4/4 blocks, 20 perfect, balcony); `commission` exit 0, 7 s, OK (complete at tick 1882, round 4); `save_load` exit 0, 15 s, OK (saved at tick 11 with planks in flight; damaged and version-2 files refused; loaded at tick 11 with pose, view and Ward count restored; the shipment arrived exactly once); `full_game` exit 0, 10 s wall time, OK (Victory 20-9 at game minute 12.9, 13 rounds, 0 refusals); `capture_tour` exit 0, 13 s, OK. 0 errors or exceptions in every log. A normal launch (no flags) reached `[World] ready: new game ... 1920x1080`, ran 20 s with 0 errors and closed cleanly.
- The ADTM audio fatal error ("Attempting to update control while a mix is ongoing...") did not appear in any packaged or editor log of this session, and no run crashed. It needs an audio device failure to show, so this is not proof it cannot happen.
- Fixed from packaged runs: `save_load` failed once because AutoPlay's `Start` ran before the save's pose was applied (the script now waits for the load); the damaged-save message was a raw JSON parser error (now a plain sentence); the smoke runs' window size was remembered by Unity for the next normal launch (registry prefs reset; HANDOFF explains).

Not run / caveats: nobody has played the build by hand and nobody has listened to the audio. The rhythm phrase and the duel in the exe ran only under their autoplayers. `full_game` and `commission` drive the sim faster than real time, so they say nothing about pacing. Fast travel and the pause menu buttons were exercised by tests and scripts, not by mouse. Saving keeps nothing of a running encounter (refused instead). The Bram greeting after a loaded completed commission falls back to the opening line until the next event. The round-start banner can overlap the What now line on a 720p screen.

## Prehistoric refactor: binding spec (13 Nov 2026, branch `refactor/prehistoric`)

Written: `Docs/REFACTOR_SPEC.md`, the binding spec for the owner-agreed refactor (automatic dice from a 36-card deck, growing stock on hexes, camps with fixed plots and primitive collectors, main roads on hex edges, per-camp stock and shipments, the prince's proposal queue and free build spells, a rival that overruns the weaker people, minimal demons, lattice movement with a free pointer, the dance circle). It gives the rules with starting numbers, wares, buildings and costs, the data model, commands, events, refusals and read helpers (appendix A), the Unity-side interfaces (appendix B), the keep/change/remove map, six work packages (S, P, D, V, N, F) with file ownership and acceptance checks, open risks, and the 37 choices the spec writer made where the owner's decisions left a gap.

Ran: nothing. No code was written and no tests or builds were run; `main` is unchanged.

## F-0: Unity skeleton on the prehistoric sim (13 Nov 2026, branch `refactor/prehistoric`, tag `pre-F0`)

Merged: `refactor/sim` at `4bc636c` (S-0, fast-forward): the new sim surface of REFACTOR_SPEC appendix A (board numbers 2..12, `StartCorners` 70 / 97, plots, dice and stock, first versions of settlements, logistics, brain, demons, overrun), Strategy/Military sim code and tests in `Archive/`, `ApiSurfaceTests`.

Built (details and every decision in REFACTOR_SPEC appendix B.1):
- Archived to `Archive/` (not compiled, `.meta` dropped): `Game/Strategy/**`, `Game/View/Economy/**`, `View/OutpostBuilder`, `View/VillageBuilder`, `View/VillageLayout`, `Actors/VillageNpcs`, the old-economy God tools (`BuildTool`, `RoadTool`, `ProblemsTool`, `WaresTool`, `EconomyGodTools`, `EconomyHud`, `GodCommands`, `CommissionDriver`, `TravelTool`), `Boot/SaveLoadScript`, PlayMode `VillageTests`, `EconomyViewTests`, `StrategyViewTests`, `MapLayoutTests` and the MVP `SaveLoadTests`, agent scripts `EconCaptures`, `StrategyCaptures`, `M1Captures`, `MapCaptures`, `EncounterCaptures`, docs `STRATEGY_API`, `STRATEGY_UI`, `ECONOMY_UI`, `MAP_MIGRATION`.
- `WorldBootstrap.Awake` calls `PlayerInstaller`, `CampInstaller`, `LandViewsInstaller`, `PrinceInstaller`, `DevToolsInstaller` in that order. Stubs with the appendix B names in their owners' folders: P `WalkLattice` (implemented: pure geometry), `WalkGrid`, `PlayerWalker`, `PointerInput`, `SpellCaster`, `TouchStrips`, `IFollowable`, `ISpeaker`, `SpeechBubbles`, `MapScreen`, `Hud.Visible`; D `CampLayout` (working first version); V `LandViews`; N `PrinceDirector`, `Npc` walking and speaker members; F `GuideText`.
- Interim game: the old first-person avatar spawns at `CampLayout.Spawn` (4 steps from home corner 70 along the Forest spoke); the terrain shows the new numbers; Mira stands at the home camp and her duel works; Tab still opens the old God View camera (no tools). No warehouse in normal play: the rhythm party is reachable only through `WarehouseSet.BuildStandalone` (tests and `first_five`) until package D's dance circle. `PauseReasons.Draft` and the draft step are gone. The save drops the warehouse and Bram extras.
- AutoPlay: `boot_tour` checks the new board and the spawn, walks to Mira, God View and back; `capture_tour` captures Mira instead of the warehouse; `first_five` plays the duel, then the phrase in the test warehouse.

Ran:
- `dotnet test DotNet`: 179 passed, 0 failed.
- Unity: recompile 0 errors; EditMode 179 passed, 0 failed; PlayMode 14 passed, 0 failed: `WorldBootTests` (boots and ticks; new board, start corners 70 / 97, numbers, 61 hex views with the new numbers, spawn, Mira at `CampLayout.MiraSpot`, no warehouse or village objects; lattice round trip of all 150 corners and 61 hex centres and the six headings; `boot_tour` passes), `WardDuelTests` (3, unchanged), `RhythmPartyTests` (4, on the test warehouse), `SaveLoadTests` (3: round trip of state and pose, refusals including a version 3 save, refused during an encounter). Console 0 errors (warnings: the Island mesh-collider triangle size, as before, and the expected save refusals).
- Play Mode in the editor: `first_five` logged `[AutoPlay] first_five OK` (duel Victory after 3 casts; phrase 4/4 blocks, 20 perfect, balcony). Captures inspected (scratch, not kept): spawn among the Forest 6 trees with Mira in view; God View of the whole island with the new numbers.

Not run / caveats: no Windows build and no packaged runs (not asked). Nothing new is drawn for camps, plots, roads, carriers or proposals: the sim runs underneath with nothing showing it but the God View toolbar code, which is not installed. The spawn stands among Forest trees (the only clearing is 12 m round each start corner) until V clears plots and spokes. The interim avatar walks freely, not on the lattice.

## Prehistoric refactor, package S: simulation, brain, proof and tuning (13 Nov 2026, branch `refactor/sim`)

Built (all in `Assets/World/Sim` and `Assets/World/Tests/EditMode`; API in `Docs/ECONOMY_API.md`, names in the spec's appendix A and its change log):
- S-0 (`4bc636c`): the appendix A surface, board numbers 2..12 and start corners 70/97, `PlotLayout`, dice and hex stock, placement checks, strength, warning and endings, demons, the proposal queue and spells. Strategy, Military and the old economy moved to `Archive/Sim` with their tests (`Archive/Tests/EditMode`).
- Logistics (`7bc97ba`): per-camp carriers, demand jobs in serving order with reservations, local and main-road legs, shipments between storehouses, routes avoiding demons, carriers waiting on a demon-held road until the banish, cancelled sites bringing goods home, real `CarrierPosition`. Job ageing (30 s) added after shipments starved behind outputs.
- Brain (`16c82e1`): expansion target, the next road (merchant) and the camp, the scarcest-ware bonus, workshops, city, lodge, warning bonus, speakers; the rival on the same rules. Scripted players (prompt, fixed delay, slow, decliner, ignore, random) and `GameRunner` for whole-game tests.
- Proof (`900bcf2`): `ProofReport` writes `Docs/ECONOMY_PROOF.md` (sections A-G plus a cast-delay sensitivity table), builds it twice and requires byte-identical text, and fails on any pass criterion.

Tuning log (each line: the change on top of the previous ones; 11 seeds; "prompt" is the modelled human of spec 8.1; "W" wins by overrun, "L" losses by overrun; numbers from probe runs, the final ones from the proof):

| Step | Change | Prompt | Delay 36 s | Ignored prince | Note |
|---|---|---|---|---|---|
| 0 | spec starting values, full logistics and brain | 0 W (all time-limit wins, 5-3) | 0 W | 0 L (rival 2.3 at 20 min) | nobody grows: carriers and casts are the limits |
| 1 | carriers 5 m per 10 ticks (spec 15) | 7 W, median 17.0 | 0 W | 1 L | carriers were the bottleneck, batch size and cap made no difference |
| 2 | 4 sheep at start (spec 2) | 9 W, median 12.7 | 0 W | 0 L | a second camp no longer waits for a pasture |
| - | (probe) no demons at all | 6 W | 0 W | 10 L | the rival spent about half of its casts banishing (13 of 27 on seed 2) |
| 3 | one workshop per type, surplus bonus, banish on a road edge 40, demons on every 2nd seven | 11 W, median 11.7 | 0 W | 2 L | the rival had built two sawmills and kilns and blocked its three site slots |
| 4 | 5 / 3 carriers per start / new camp (spec 3 / 2), grace 5 min, demons on every 3rd seven | 11 W, median 12.0 | 1 W | 10 L | but with expansion +15 and press +40 only 2 building types were ever built |
| 5 | no expansion bonus, no press; city 90, lodge 60 | 11 W, median 10.0 | 0 W | 7 L | 9-10 types built |
| 6 | rival cast delay 40 s (spec 45 s) | 10 W, median 13.1 | 0 W | 9 L | Quarry never built |
| 7 | +30 to strength builds while ahead of a people whose strength has not changed for 5 min | 10 W, median 13.1 | 0 W | 11 L | all 11 types built in some prompt game |
| 8 | hex cap 16 (spec 12) | proof: 10 of 10 W, median 12.1 | proof: 0 W, 7 time wins, 4 draws | proof: 10 of 10 L, median 14.7 | two seeds lost 20-23% of clay or stone to the cap at 12 |

Cast-delay sensitivity (proof, 11 seeds, overrun wins / median minutes): prompt 11 / 13.1; 10 s 11 / 9.7; 20 s 10 / 13.2; 28 s 5 / 20.0; 36 s 0 / 20.0 (7 time wins, 4 draws); 45 s 0 (3 time wins, 2 draws, 6 time losses); 60 s 0 (2 draws, 8 time losses, 1 overrun). Winning by overrun needs the player to cast within about 30 s of each pitch; this is the biggest design risk (spec risk 2) and must be measured in the first playtest.

Ran:
- `dotnet test DotNet`: 193 passed, 0 failed (about 25 s; the proof test about 20 s of it).
- The proof: all criteria pass (prompt 10/10 overrun wins, median 12.1 min; ignored prince overrun 10/10, median 14.7 min; zero drift in 10 random-cast games; longest missed import 0 ticks; longest job wait under 2 minutes; the rival never more than 46 s without a proposal; no ware over 15% lost to the cap; every placeable type built in at least one prompt game).

Not run: Unity (no EditMode or PlayMode run in the editor for this package); nothing was played by hand. The modelled human is crude (straight-line walking, fixed talk). Warriors are trained in none of the 10 prompt games before they end, so the lodge, meals and hunger are proven by unit tests and the paired run, not by play.

## Prehistoric refactor, package S: second tuning pass (13 Nov 2026, branch `refactor/sim`)

Asked for: a real person casting about 45-60 s after each pitch must win; the middle and late game (warriors, cities, every worked good, demons) must happen in those wins; the first pass's hidden-information rule replaced with something visible. Numbers and brain/rival logic only; the surface grew (spec A.5 items 21-24), `ApiSurfaceTests` unchanged.

Changed (spec value -> game value; all in the `Tuned()` factories):
- Rival pace: 45 s from when a proposal became top -> 100 s from its last spell (`RivalPacePerCast`; the top must have settled 15 s), 60 s sooner per point of strength it leads by, never under 10 s. Replaces the first pass's +30 to strength builds while the other people's strength had been static for 5 minutes (hidden information).
- No overrun warning in the first 8 minutes (`WarningGraceTicks` 4800): stops games ending before the workshops appear.
- A camp held by demons adds no strength until banished (spec: it counts).
- Demons: grace 5 min, a draw on every 2nd seven (first pass: 3rd), and a draw picks a people first (equal chances), then one of its sites (spec: one site among all, so the bigger people got most demons).
- Lodge also without a city once a Bakery and a Smokehouse are active (spec: city only); lodge score 60 -> 90.
- Workshops +30 once a people has 2 camps; improved collectors +15 once it has 4 (without it the Farm was never built).
- A banish on a road edge ranks like a camp's while it holds something up (a carrier or a road or camp site waiting).
- Pitches within 60 m of the last cast get +8 (walkable clusters).

Tuning log (11 seeds per row; W/Wt = wins by overrun / at the time limit; L = overrun losses; probe runs, last line from the proof):

| Step | Change | Prompt | 45 s | 60 s | 90 s | Ignored prince |
|---|---|---|---|---|---|---|
| 0 | first-pass values | W11, median 13.1 | W0, Wt3, Lt6 | W0, Lt8, L1 | Lt6, L5 | L11, median 14.7 |
| 1 | no press bonus, rival delay ramp 90 -> 40 s over 15 min | W11, median 8.6 | W1, Wt6 | W1, Wt3 | Lt8, L2 | L2 (Lt9) |
| 2 | + workshops +30 from 2 camps, lodge without a city, lodge 70-90 | W11, median 10.3 | W7, Wt3, L1; city, warriors and all goods in 9-10 wins | W1, Lt7 | Lt7, L2 | L1 |
| 3 | + demon-held camps count 0, draws on every 2nd seven | W11 | W5-8 | W1-3, L0-3 | L0-4 | L6-9 |
| 4 | + the rival paced per spell (its 45-90 s delays were reset by every reorder of its queue) and bolder when ahead | W11 | W4-7 | W1-2, Wt4-5, L0-1 | L1 | L8 |
| 5 | + demons pick a people first | W11, median 10.9 | W9, Wt2 | W1, Wt7, L1 | L2-4 | L10-11 |
| 6 | + no warning before 8 min | W11, min 9.5 | W9, Wt2, median 15.8 | W1, Wt7, L1 | L4 | L11 |
| 7 | + blocking road banishes rank high (a 60 s game had sat 10 minutes behind one) | W11 | 9+ wins | 7+ wins, L0 | - | L9 |
| 8 | rival 100 s base, 60 s per point of lead, 10 s floor | W11, median 10.6 | W11, median 15.9 | W3, Wt6, L0 | D7, L4 | L11, median 14.7 |
| 9 | + improved collectors +15 from 4 camps | proof: W11, median 10.6, min 9.5 | proof: W11, median 15.9 | proof: W3, Wt6, D1, Lt1, L0 | proof: D7, L4 | proof: L11, 11.3-19.5, median 14.7 |

Ran:
- `dotnet test DotNet`: 201 passed, 0 failed (new `TuningRulesTests`: each added rule on and off).
- The proof (`Docs/ECONOMY_PROOF.md`): all criteria pass, including the new ones: 45 s player wins 11 of 11 (median 15.9 min); 60 s player wins 9 of 11 (3 by overrun, 6 at the time limit) and is never overrun; 90 s player: 7 draws, 4 overrun losses; ignored prince overrun 11 of 11 (median 14.7 min, earliest 11.3); prompt games 9.5-13.6 min. In the 45 s player's 11 wins: fed warriors in 10, a city in 9, all five worked goods made and used in 9, warriors made the margin in 5; 2.8 banishes per game; never banishing (paired run) turned a 15.9-minute win into a 12.1-minute defeat. Mean walk between casts 60-90 m (5-7 s at 12.5 m/s); 17-24 casts per game.

Not run: Unity; nothing played by hand. Still weak: the 90 s player loses 4 of 11 by overrun (an idle player and a 90 s player differ by about one point of strength, and the rule that overruns every idle player also overruns some slow ones); prompt games end at 9.5-13.6 minutes, mostly right after the 8-minute warning grace, often before the first warrior; the rival is weak against an active player (final strength 1-2 against the 45 s player), partly because its camps are often demon-held.
