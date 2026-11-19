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

## Integration 1: sim, player, views and dance circle joined (13-14 Nov 2026, branch `refactor/prehistoric`, tag `pre-integrated-1`)

Merged in order `refactor/sim` (4868ca0), `pre/p`, `pre/v`, `pre/d` (four merge commits; the only conflicts were the change logs and this file, all sides kept). The prince (`pre/n`) and lighting (`pre/l`) are not merged; `PrinceInstaller` is still the F-0 stub, so nobody leads the player yet.

Joins fixed:

- **Carriers and shipments against the real sim.** The carrier views needed no change: they already read `EconomyApi.CarrierPosition` and the carrier's fields. New test `IntegrationTests.CarrierViews_FollowTheRealSim_PositionsLoadsAndPorters` grows the people with their own proposals and checks one view per carrier, each moving carrier within 5 cm of the sim's position, loads shown, porter look on main-road legs.
- **Terrain clearings.** `LandViews.SyncClearings` adds `CampLayout.Clearings`: no trees or crops in the dance circle, tents, Mira's duel ground or the spawn view. Checked by eye, no test. The city palisade (8 m) and store piles (2.2 m) were checked against the dressing by numbers (nearest prop edge 8.9 m from the corner) and by eye in a late-game capture of the home city; no change was needed. Plot 635 is reserved in the sim, so nothing is built on the circle.
- **Pointer and casting.** P's raycast already takes the ground from the hex tile behind view triggers; the hover ring now prefers "press to cast" over a thing standing on a proposal's ground. `SpellCaster` ignores a second press on the same proposal before the next tick (found by `cast_all`, which cast one proposal 16 times in a row at first). Test `Cast_OnTheRealViews_ByScreenPress_SiteCarriersBuilding`: too far is refused, a screen press on the mark casts, the site appears, a carrier delivers, the hut is finished.
- **Light.** The cast mark's beam is slim and soft and hidden within 9 m; the cast effect is a slim beam; the roll's pay beam and token halo (which also whited out the view in the late game) are toned down.
- **No HUD in normal play.** The dance's score strip, key hints, judgement words, banners and choice bar show only with the HUD on (H). Sol calls the count-in, blocks kept and missed, the bass, the ending choice and the ending by speech bubble; the fire jumps on a kept block and sinks on a missed one. Villagers' nameplates show only with the HUD. The duel's key-hint line shows only with the HUD (it also still said Tab switches the camera). The top-left debug text was already gone with package P. The duel's own panels and the dance's result panel stay.
- **Dance input** from P's touch strips was already wired (`TouchStrips.Pressed/Held`); now tested with `SimulatePress`. `duel_dance` and the encounter tests' `Accept` walk the lattice, tap the villager and answer by the bubble's reply.
- **People** wear hides and furs (actor factory and land figures). **Map and HUD** show both strengths, the overrun margin from the state's tuning and the rival's current pace (`FactionApi.RivalCastDelay`).
- F1 tools `AddWares` and `SpawnDemon` stay unavailable (the sim has no such commands). `Assets/World/Sim` was not edited.

New: AutoPlay `cast_all` (`Assets/World/Game/Boot/CastAllScript.cs`), usable packaged as `-autoplay cast_all [-speed N]`.

Ran (editor, 14 Nov):

- `dotnet test DotNet`: 201 passed.
- Unity EditMode: 201 passed. PlayMode: 51 passed (47 from the packages plus 4 new `IntegrationTests`, one of which plays `cast_all` to an ending at time scale 8). Console: 0 errors.
- AutoPlay in Play Mode at 1920x1080: `walk_tour` OK (camp to the neighbouring corner in 4.43 s); `duel_dance` OK (duel won after 3 casts, dance 4 blocks passed, Spirit call, HUD off); `cast_all` OK twice at time scale 8 after the first casts at 1: Victory by overrun at game minute 14.8 with 36 casts, and Victory by overrun at minute 12.7 with 29 casts, 2570 m walked (about 90 m per cast), every cast by a screen press. The two runs differ because walking is in real frames, so the script is not deterministic.
- Frame rate, editor, 1920x1080, late game after the ending (6 camps, 26 buildings, 38 carriers), time scale 1: average 283 to 315 fps, worst frame 25 ms. At time scale 8 the editor dropped to 32 to 45 fps (several ticks and view syncs per frame).
- Captures looked at (`Evidence/screenshots`, not committed): spawn, the cast mark, a cast and its site and the hut rising, a road site with a porter walking to it, carriers on the home camp's local paths, the home city with palisade, the late game, the map, the HUD, the dance mid-phrase with the HUD off, Mira's duel.

Not done, rough, or not verified:

- Nothing was played by hand. No Windows build, no packaged run of `cast_all`.
- The capture of a shipment on a main road shows the road and camps but the porter is too far away to judge; porters were seen close only walking to a road site.
- A gatherer of the Gleaners' basket walks in a straight line to the stock patch and so crosses the dance circle; during the dance (clock held) one can stand frozen among the dancers.
- Idle carriers wait 4 m from the storehouse, on the cells the player walks through; a figure within 1.1 m of the eye is now hidden, but one just beyond that fills the edge of the view.
- Tents and other dressing do not block walking, so the player can walk through a tent.
- From the spawn the first cast mark is behind the player's left shoulder; without the prince nothing points to it.
- Late-game playability by hand at 1x (walking 50 to 90 m between casts) is unproven; the sim-side weaknesses listed under package S stand.
- The clearings and the palisade check have no automated test.

## Package L: lighting pass, day-night cycle, the game starts at night (14 Nov 2026, branch `pre/l` = `pre/v` + `pre/d`)

Built (all new code in `Assets/World/Game/View/Light/`; how it works, tuning and hooks in `Docs/LIGHTING.md`):
- `DayNight`: the hour is a pure function of the sim tick (12 real minutes per day, a new game starts at 22:30, about 3 minutes of full night before first light), sun and moon directional lights (one shadow caster at a time), V's sky dome repainted by the hour, stars, moon and sun discs, horizon glow at dawn and dusk, ambient and fog by the hour. F2 / Shift+F2 / F3 developer scrub.
- `WorldLights` / `FireLight`: flickering point lights with ground glow, far halo and sparks, a budget of 6 per-pixel point lights (no per-vertex lights), the player's lantern.
- `LightDecorator`: finds the other packages' views at runtime: hearth and entrance torches on every camp (warm for your people, green for the Ash Clan), door torches on finished buildings, war fire, overrun fire, demon glow, slim beam and light on the top cast mark, the dance fire (adopted), braziers by Mira and the spawn, a carried light for the prince when N lands; night variants of materials for people, loads, stock and number tokens.
- Edits outside `View/Light`: one installer line in `Boot/WorldBootstrap.cs`; `Shaders/WorldLit.shader`, `WorldLitVertexColor.shader` (`_SelfLit`, `_Rim`, global `_WorldNight`) and `WorldUnlit.shader` (`_NightDim`, global `_WorldNightTint`), all default 0. New: `Tests/PlayMode/LightingTests.cs`, `AgentScripts/LightCaptures.cs`. No other package's C# was edited; `Assets/World/Sim` untouched.

Ran:
- `dotnet test DotNet`: 179 passed, 0 failed. `unity command test run --mode editor`: 179 passed, 0 failed.
- `unity command test run --mode playmode --async-tests`: 36 tests; final full run 35 passed, 1 failed: the existing `RhythmPartyTests.Phrase_FailFinalBlock_NoEnding_RetryWorks_AbortStopsAudio` (avatar restored 3.3 mm off against a 0.1 mm limit); it passed in the previous full run of the same code path and the fixture passed 4 of 4 when rerun alone, so it is recorded as intermittent, not fixed. `LightingTests` (5): starts at night with lights on; time follows the game clock and holds while paused; lights fade at dawn and come back at dusk; late-game light budget; restore from a saved game time gives the same lighting.
- Console after the runs: 0 errors (1 warning: Input Manager deprecation notice).
- Editor frame rate at 1920x1080, night, clock running, late game (8 camps, 87 buildings, 90 carriers, 118 registered fires): 155 to 207 fps over six views (home camp 179, rival camp 207, a building 155, demons 205, war camp 194, God View 207); 7 lights enabled at once (moon + 6 per-pixel point lights), 0 per-vertex. Early game 393 fps at the spawn; 219 fps mid-dance (9 lights: 4 are D's short drum lights). These are editor figures from `Time.smoothDeltaTime` with the Game view at 1920x1080; the Windows player was not built or measured.
- Captures inspected and iterated (git-ignored, `Evidence/screenshots/l_*.png`, rendered from the game camera at 1920x1080 without IMGUI): spawn, hearth and tents, cast mark, forest token, dance mid-phrase, Mira, rival camp, dawn, midday, dusk, overview at night, and late-game stock, demons, carriers, war camp, overview.

Not run: no Windows build; nobody played it by hand; shadow shimmer and flicker were judged from stills and code, not from watching motion; speech bubbles, the HUD and P's Tab map do not exist on this branch (stubs), so their night legibility is unverified (the interim God View is checked).

## Integration 2: lighting merged, the game starts at night, camp fixes (14 Nov 2026, branch `refactor/prehistoric`, tag `pre-integrated-2`)

Merged `pre/l` (dac217f): day-night cycle, fires and torches, night readability. Conflicts only in this file and the spec's change log (both sides kept). The prince (`pre/n`) is still not merged.

Lighting joins:

- **One owner per effect.** The cast mark's beam stays V's (`ProposalMarkerView`): the lighting decorator no longer swaps its mesh and adds only the mark's light and ground glow. At night the ring and arrows show at half their daylight strength and the beam a little stronger. The roll's pay beam, ring and halo keep the daylight values of integration 1 with L's night fade on top. Looked at by capture: no white-out at night, the mark and the roll effect still read by day.
- The lantern already followed the lattice walker's camera (`WorldBootstrap.Avatar.Camera`); now tested. The night map fill also applies while the Tab map is open. The F1 Time tool has a time-of-day row (-1 h, +1 h, Dawn, Noon, Dusk, Night, time-lapse; `DevTools.TimeOfDay`); L's F2 / F3 keys clash with nothing.
- The game starts at 22:30 (L's test and a new one).
- `RhythmPartyTests.Phrase_FailFinalBlock_NoEnding_RetryWorks_AbortStopsAudio` (failed once on `pre/l`, viewpoint restored 3.3 mm off): passed 4 of 4 runs on the merged branch. On `pre/l` the test stood the old way, at an exact pose off the lattice; here `Accept` stands on a lattice cell and the walker's `RestoreCell` puts it back exactly, so the limit was left alone. Not reproduced, so the cause on `pre/l` is an inference, not a measurement.

Rough items from integration 1, fixed:

- **Gatherers go round the dance circle** (`BuildingView.GathererRoute`, 10 m from its centre); test checks every collector's route. Carriers never cross it (no path leads there). No one stood in the circle in the night dance capture.
- **Idle carriers stand round the hearth**, 1.5 m from the corner inside the storehouse cell: at least 3 m from every cell the player can stand on (tested).
- **Tents, the lean-to, racks, the wood pile and totems are solid** (`WalkGrid.AddSolid`: blocks the cells inside and every step that would glide through). Test: the spawn, the line to the prince's start, Mira and Sol stay reachable, and the cells either side of a blocked step are still joined another way.
- **Nothing grows in `CampLayout.Clearings`**: test over the recorded prop points of the hexes round the home camp. The check found a real gap first: crop rows were tested only at their two ends, so a 7 m row could lie across a small clearing; rows are now tested at five points.

Ran (editor, 14 Nov, night start):

- `dotnet test DotNet`: 201 passed. Unity EditMode: 201 passed. PlayMode: 59 passed (51 from integration 1, 5 `LightingTests`, 3 new `IntegrationTests`). Console: 0 errors.
- AutoPlay at 1920x1080 from the 22:30 start: `walk_tour` OK (4.43 s to the neighbouring corner); `duel_dance` OK (duel won after 3 casts, 4 blocks passed, Spirit call, HUD off); `cast_all` OK at time scale 8: Victory by overrun at game minute 12.7, 29 casts, all by screen press, 2550 m walked.
- Frame rate, late game at night (6 camps, 26 buildings, 38 carriers), time scale 1, 1920x1080: average 277 fps far from home and 372 fps at the home camp, worst frame 7.6 ms.
- Captures looked at: night spawn, Mira's bubble with replies, the cast mark and the site, the map, the HUD strip, the dance with the HUD off, Mira's duel, the late-game home camp and hearth with idle carriers, the same view at dawn and at noon.

Not done, rough, or not verified:

- Nothing was played by hand; no Windows build; no packaged AutoPlay run.
- The hover ring was not seen at night (it needs a real mouse); the cast ring was.
- The cast effect's sparks were not caught in a night capture (the capture came a moment late); by day they were.
- At night the building site and distant plots are dark until the lantern or a torch reaches them; the site board is hard to read from 10 m.
- A porter on a built main road has still only been seen from afar.
- From the spawn the first cast mark is still behind the player; the prince is to lead there.
- Dressing solids block steps for the player only; `Npc.WalkTo` still walks straight lines (the prince package owns that).

## Prehistoric refactor, package N: the prince, proposals as speech, the first scene (14 Nov 2026, branch `pre/n`)

Merged: `pre/p` (base), sim `f18df39`, then sim `4868ca0` (the second tuning pass); all numbers below are on `4868ca0`.

Built (details in `Docs/PRINCE_API.md`, interface additions in REFACTOR_SPEC B.1): `PrinceDirector` (the pitch loop of 8.5 with "Show me" / "Not this" / "Later", leading with wait, call and fetch, the explanation at the spot from `Proposal.Reason`, reactions, escalating pestering, decline through `DeclineProposal`, the first scene of 2.C.5 with its exact lines, the builder and the followed first load, overrun warnings voiced, win and loss scenes in bubbles, a HUD line); the sawyer and the merchant by `Proposal.Speaker`; original line banks per speaker, kind and mood with no sentence twice in a row; `Npc` walking on the lattice; the name plate hidden under a bubble; P's reply column kept off the speaker. AutoPlay `first_scene`, `follow_prince`, `prompt_game`, `ignore_prince`, `prince_captures`. Placeholder land views (`Game/Prince/Placeholder`) until package V lands.

Ran:
- `dotnet test DotNet`: 201 passed, 0 failed.
- Unity EditMode 201 passed; PlayMode 43 passed (13 of them `PrinceTests`), 0 failed; console 0 errors from game code.
- In the editor (default seed, 1920x1080, time x2-x10; every timer runs on game time): `first_scene` OK (the prince asks 2.5 game s after the start, the hut, the builder, the first load followed to the storehouse at about 147 game s); `ignore_prince` OK (nothing built, 12 pitches with moods Calm, Keen, Pressing, Urgent, 4 quiet spells, Defeat by overrun at 11.9 min, loss scene played); `prince_captures` OK; `follow_prince` at four delays (below).

Pace measured by `follow_prince` (the player answers each pitch after the delay, follows the speaker and presses the ground by a screen tap as soon as it is in range; "direct" walks straight to the spot instead):

| Delay | Mode | Result | Casts | Pitch to cast (avg / median) | Between casts (avg / median) | Walked per cast (avg / median) |
|---|---|---|---|---|---|---|
| 3 s | follow | Victory by overrun, 14.5 min | 26 | 16.6 / 17.8 s | 30.9 / 29.1 s | 101 / 100 m |
| 3 s | direct | Victory by overrun, 13.7 min | 27 | 14.6 / 12.1 s | 29.8 / 23.6 s | 91 / 77 m |
| 10 s | follow | Draw at 20 min (2-2) | 17 | 22.7 / 22.4 s | 36.3 / 30.9 s | 97 / 95 m |
| 20 s | follow | Victory by overrun, 19.6 min | 21 | 43.0 / 37.2 s | 53.0 / 47.3 s | 95 / 115 m |
| 30 s | follow | Victory at 20 min (3-2) | 16 | 50.8 / 42.1 s | 75.6 / 76.0 s | 75 / 65 m |

What a real walk costs: a typical pitch sends the player 60-200 m (median about 100 m); following the leading villager at 7 m/s that is about 14 s of walking, plus 2-3 s of turning and reaching range, so pitch to cast is about 17 s with a 3 s answer (12 s walking straight at the player's own 12.5 m/s). The headless model's "30-45 s per pitch" is therefore answer delay plus about 15 s of walking. Above a 15 s answer delay the question times out and comes back later (40/30/20 s escalation), which is why delays of 20-30 s cost far more than the delay itself. The 10 s and 20 s results (a draw, then a win) show the outcome is noisy around those delays (demons and the rival's pace); one game per setting only.

Not done / caveats: package V's views are missing, so camps, buildings, carriers and the ground ring are N's plain placeholders (players stand among trees on plots until V clears them); one seed only; no packaged run (`-autoplay` is wired but no build was made, as asked); captures at time x2-x10 in the editor; screen taps sometimes land on a bubble or person and the script falls back to a ground press (counted as `groundFallbacks`).

## Integration 3: the prince joined (14 Nov 2026, branch `refactor/prehistoric`, tag `pre-integrated-3`)

Merged `pre/n` (abaece4). Conflicts: `Actors/Npc.cs` (name plates: now only with the HUD on, and never under a bubble), `WorldBootTests` (Sol and N's villagers both allowed), this file and the spec's change log (both sides kept).

Joins:

- N's placeholder views are deleted. Carriers ("Follow" on the first load) and the ground mark come from V's `LandViews`. On the first hut V's own site builder is hidden while Tam works there, so there is one builder, not two.
- `cast_all` now runs with the prince present; `PrinceInstaller.Disabled` / `-noprince` keeps a world without him (one short test).
- At night the prince (with his carried light), the sawyer, the merchant and the builder were looked at in captures: readable.

N's rough items that were ours:

- Trees at pitches: a proposed camp's corner (10 m) and a proposed road's middle (9 m) are cleared and stay cleared. Plots and paths of existing camps were already clear.
- Inside the ring of a proposal in cast range the press now wins over a bubble's body and over a person standing on it; a reply line still answers.
- A line said off screen is now drawn small at the screen edge with the speaker's name and an arrow.
- The prince wears a fan of tall red and white feathers and carries a sun standard, so he reads from across the camp.

Pace (changed in the game layer only; no sim number changed):

- A question now stays open 25 s (was 15), and comes back after 12, 10, then 8 s while the player stands within 25 m (40, 30, 20 s only when they walked off). Walking 10 m nearer to the spot, or into cast range, counts as an answer: the villager goes ahead and waits there. This also works between questions and while he is resigned.
- A deadlock was found with the new stall line in `follow_prince` and fixed: a villager standing on his pitch spot more than 10.5 m from the player never asked (it happened when the cells near the player were blocked, which the solid tents made more likely); twice it held a game up for two minutes. He now asks from up to 25 m.
- `first_scene` failed twice on "the followed carrier is not in view" because the script's own sight check counted a villager standing in the line of sight as hiding the carrier; the check now ignores people.

Pace table (editor, night start, time scale 8, `follow_prince` in follow mode, final code):

| Answer delay | Result | Game minute | Casts | Pitch to cast, average (median) |
|---|---|---|---|---|
| 3 s | Victory by overrun | 14.3 | 27 | 19.2 s (18.1) |
| 10 s | Victory by overrun | 14.8 | 26 | 23.3 s (22.5) |
| 20 s | Victory by overrun | 15.0 | 19 | 33.1 s (34.4) |
| 30 s | Victory at the time limit | 20.0 | 18 | 48.3 s (45.3) |
| ignore | Defeat by overrun | 11.9 | 0 | - |

Earlier runs of the same day, before the deadlock fix: 20 s gave one Draw at the time limit (2 v 2, no cast for the last 8 minutes; the cause was not captured, the stall line was added after it), then Victory by overrun at 16.2, 14.7, 14.8 and 15.0 minutes. So 20 s won 5 of 6 runs, and the one draw is not explained. The runs are not deterministic (walking is in real frames).

Ran (editor, 14 Nov): `first_scene` OK at time scale 1 (the prince asks after 0.3 game seconds; the load is followed to the storehouse at 147 game seconds); PlayMode 75 passed on the tree that already held the F-1 save code (prince tests 13, lighting 5, integration 8, the rest as before); `dotnet` and EditMode were not rerun for this step (no sim change). The Unity editor crashed once between runs, right after an audio device change on the machine (`ADTM: Attempting to update control while a mix is ongoing`); it was reopened and nothing was lost.

Rough: the left-pointing "calls you" box clips its last letter; at 30 s the win is only on the clock; a slower player than that was not tried.

## F-1: save and load, guide, Windows build; the refactor is finished (14 Nov 2026, branch `refactor/prehistoric`, tag `refactor-done`)

- **Save and load** (F5, F9, pause menu; save version 4 unchanged): the file now also holds the walker's lattice cell and heading, whether the HUD is on, and the step of the prince's first scene. The time of day needs no saving (it follows the tick). The prince's pitch loop is not saved: it starts again from the open proposals. Saving and loading are refused during the duel and the dance. New PlayMode tests: walker, HUD, first-scene step and time of day restored and the prince speaks again after the load; a carrier saved mid-path delivers its load exactly once after the load.
- **Guide**: the pause menu's Controls and How to play pages and `Docs/PLAY_GUIDE.md` are rewritten for this game.
- **Fine graph**: not removed. Eight game files and eight test files still use the topology's vertex and edge members, so it was not a quick, safe change.
- **Docs**: `HANDOFF.md`, `README.md`, `CLAUDE.md`, `RESUME.md` updated; `docs/next-ages` merged (four documents, docs only): what comes next is `Docs/NEXT_START_HERE.md`.

Ran (14 Nov):

- `dotnet test DotNet`: 201 passed. Unity EditMode: 201 passed. PlayMode: 75 passed. Console: 0 errors.
- Windows build: `Builds/Windows/WorldMVP.exe`, Succeeded, 0 errors, 7 warnings; the folder is 101 MB without captures (the exe file itself is 0.7 MB; the game is in `WorldMVP_Data`). The probe's `Resources` list was parked for the build and is back.
- Packaged runs (windowed 1280x720, `-novsync`, night start; logs in `Builds/logs`; "errors" = lines with "exception", "FAILED" or an error marker in the player log):

| Run | Exit code | Errors | Result |
|---|---|---|---|
| `walk_tour` | 0 | 0 | OK, 23 s |
| `first_scene` | 0 | 0 | OK, 152 s |
| `duel_dance` | 0 | 0 | OK, 54 s |
| `follow_prince -princeDelay 3 -timescale 8` | 0 | 0 | Victory by overrun, minute 14.4, 27 casts, pitch to cast 19.7 s on average |
| `ignore_prince -timescale 8` | 0 | 0 | Defeat by overrun, minute 11.9, 0 casts |
| `cast_all -speed 8` | 0 | 0 | Victory by overrun, minute 12.5, 28 casts, all by screen press |
| `save_load` | 0 | 0 | OK: refused a corrupt and an older file, loaded, the carried load arrived once |
| `capture_tour` | 0 | 0 | OK |
| plain launch, no window flags, `-quitAfterReady` | 0 | 0 | `[Boot] playable`: hour 22.6, night 1.00, 1920x1080 FullScreenWindow, prince present |

- The window-size registry key of this product (`HKCU\Software\Joe Salmon\WorldMVP`) was deleted after the windowed runs; the plain launch that followed wrote full screen at the native size again. The AutoPlay save file was deleted.
- Packaged captures looked at (`Builds/Windows/Evidence/screenshots`): the spawn at night, the prince's opening bubble with its three replies, the pause menu's guide, the map, the loss scene. No magenta, fonts as in the editor, night readable.

Not done, rough, or not verified:

- Nobody has played it by hand: not the walking, not the pointer, not the dance by ear, not a whole game at 1x.
- One `follow_prince` run at a 20 s answer delay ended in a Draw and was not explained (5 other runs at 20 s were won).
- The Unity editor crashed once after an audio device change on the machine; the game's own audio guard logged the change just before. Not reproduced, not fixed.
- In the opening the prince can stand half behind the brazier beside the spawn.
- After a load, spots cleared for earlier proposals grow back until they are proposed again; the dance and duel cannot be saved; the time-of-day scrub of the F1 tools is not saved.
- The hover ring at night, a porter on a built road from close by, and the cast sparks at night were not seen in captures.
- `refactor/prehistoric` is not merged into `main` and was not pushed by this session.

## Historic base: H1 and crystal sims merged into the game, crystals I0 Unity half, package shells (14 Nov 2026, branch `historic/main`, tag `historic-base`)

- The refactor's Windows build was copied first to `Builds/Refactor_2026-11-14` (kept; not rebuilt).
- `historic/main` from `refactor-done`; merged tag `sim-H1-cry-S1` (no conflicts) and then, at the coordinator's request, `sim-H1-cry-S1-tuned` (the campaign race tuning; no conflicts). The hand-written `.meta` files of the sim lanes were checked (GUIDs well formed and unique, folder metas marked); Unity wrote the two that were missing (`UpgradeTests.cs.meta`, `UpgradeBenefitTests.cs.meta`).
- Game layer, minimal: the prince's words handle the new proposal kinds (`KindOf` gives "upgrade" and "town", any later unknown kind falls back to the plain collector line; `Args` fills both; two placeholder banks in `PrinceLines`); `LandViews` clears the ground of a town's outer plots and their paths, and takes extra clearings from other packages (`AddClearing`); the save shell needed no change (it already writes `SaveCodec.CurrentVersion`, now 5, and reads version 4 through the sim's migration). A real version-4 save written by the refactor's packaged build (`save_load` run, tick 194) is a PlayMode fixture and loads as a Prehistoric skirmish and plays on. Map, HUD and proposal markers read the sim's helpers, which already know `Upgrade` and `Town`: no change.
- Tests for Unity's runner: `ProofReport` (20 min), `HandoverProof` (20 min) and `CampaignProof` (60 min) carry `Timeout`; the two heavy ones are `[Category("HeavyProof")]` so a routine Unity run may leave them out (`dotnet test` always runs them). `WardOpponentTests` now finds `ward_opponents.json` from Unity's working directory too (it failed in Unity only).
- Crystals I0, Unity half: `DuelStageSpec` (with `VolunteerActorId`, `VolunteerPos`), `WardDuelRequest` in `EncounterRequest.cs`, `EncounterContext.Request` with `EncounterDirectory.TryStart(id, npc, request)`, `ward_campaign` registered through the I7 shell `CampaignDuel.Create`; installer shells `InteriorsInstaller`, `ExpeditionInstaller` (called after the prince, before the F1 tools) and `GradeViewsInstaller` (after the land views); shells `Interiors`, `InteriorSpace`, `ExpeditionDirector`; `IMapLayer`, `MapMode`, `MapScreen.AddLayer`. The sim `Lattice` equals `WalkLattice` (PlayMode test). Owners and entry points: `Docs/NEXT_START_HERE.md` section 10.
- Developer starts: `Game/Boot/DevStart.cs`, F1 tool "Starts" (G), command line `-start dev.historic [-seed n]`; `AgentScripts/Integ.cs` gained `DevHistoric` and `Ages`.
- An editor quirk on this machine: after the merge the editor kept a script compilation pending and never started it, and later would not reload the domain (unfocused editor). Ticking the compilation pipeline once by reflection compiled; entering Play Mode reloaded. Later recompiles worked normally.

Ran (14 Nov, editor):

- `dotnet test DotNet`: 442 passed on `sim-H1-cry-S1`; 450 passed after the tuned merge (7 min 13 s).
- Unity: 0 compile errors; console 0 errors. EditMode 442/442 minus one (`WardOpponentTests` file lookup) before the fix and the tuned merge; after: **450 passed** (23 min 26 s; `CampaignProof` 18 min, `HandoverProof` 3.7 min, economy proof 55 s). PlayMode **79 passed** (75 before plus 4 in `HistoricBaseTests`: lattice, version-4 save, encounter request, dev.historic boot).
- AutoPlay in the editor from the night start: `walk_tour` OK (27 s); `first_scene` OK (152 s at time scale 1); `duel_dance` OK; `follow_prince` (3 s, time scale 8) OK: Victory by overrun at game minute 14.4, 30 casts by the player; `ignore_prince` (time scale 8) OK: Defeat by overrun at minute 11.9, 0 casts, 12 pitches.
- What moved in the skirmish: with the handover on (PRD_AGES 14.2 default), the player's prince took over at game minute 7.4 in `follow_prince` and then built 6 things alone besides the player's casts (30 casts against 27 on 13-14 Nov; the end minute 14.4 against 14.3). The Ash Clan's handover fired at minute 13.5 there and 11.3 in `ignore_prince`, where it changes nothing (the rival was already autonomous): an ignoring player never delivers a worked good, so his prince never takes over and the game is lost as before.
- F1 developer start `dev.historic` from a running night game: the start built in 1.4 s, the scene reloaded at tick 12,210 (Historic, DevStart, both peoples handed over, 14 camps, 58 buildings), the prince announced and built on his own (7 then 8 autonomous casts), the map opened, console 0 errors. No views for campaign things (expected).

Not done or not verified:

- Nobody played any of it by hand. The packaged build was not rebuilt; the packaged runs were not repeated.
- The map's side text in a Historic game still speaks of falling to an overrun ("they fall in 33 s"), which is a setback there, not a defeat; the overrun wording is H1-4's or H2-7's.
- `follow_prince` was run once (no repeat at 10, 20, 30 s delays).
- The window-size registry key written by one windowed run of the refactor build (made to capture the version-4 save) could not be deleted from this session: the owner's next double-click may open in a 1280 x 720 window. Delete `HKCU\Software\Joe Salmon\WorldMVP` to restore full screen.

## Crystals I7: the campaign duel (15 Nov 2026, branch `cry/i7`, tag `cry-I7`)

- `CampaignDuel` is real: a per-scene driver stages every `EncounterCommitted` through the `EncounterHost` (`ward_campaign`), mode, profile, attempt, seed, names and stage read from the definition (surface markers, interior markers through `InteriorSpace`, the finale at the tower the party stands at); exactly one `ResolveEncounter` per attempt at the duel's end event, then one clock step, then `Resolved`; a pending attempt without a duel (after a load, or a duel left without surrender) continues by `RestartAttempt`. Friendly and practice requests never touch the campaign; a practice request is the shipped practice duel.
- `WardDuelEncounter` reads the request: Mira (factory body with her id) casts, the opponent faces her with its profile's timing, pool and tempo, the volunteer stands by beside Mira; stakes card first in lethal mode; no rematch; presentation hold (1.6 s on a break, 3 s on a lethal loss while the volunteer steps into the gap); the confirmed "Leave encounter" surrenders (one added line in `Hud.cs`). Struck cues name whose Ward was hit with totals only. Phone layout `DuelLayout` (compact below 700 px, inside the safe area, 44 px targets, expandable history). Details: `Docs/ENCOUNTERS_UI.md`, "Campaign duel".

Ran (15 Nov, editor wt-cry-i7):

- `dotnet test DotNet`: 450 passed (8 min 6 s). (It rewrites the three proof documents with other line endings only; they were restored.)
- Unity PlayMode: **89 passed** (79 before plus 10 in `CampaignDuelTests`, 11.6 min). Console: 0 errors from game code. Unity EditMode was not rerun (no simulation or EditMode file changed; the same sources passed under `dotnet test`).
- AutoPlay in the editor: `duel_dance` OK (1920 x 1080); `duel_keeper_win` OK at 1920 x 1080 and at 844 x 390 (dev.historic, authored pack, the Deepstone stolen by DevRivalNextStep, leads followed to the Tally Vault threshold at tick 15366, Victory after 2 casts by Mira and 1 by Dolm, one resolve, Deepstone carried); `duel_keeper_loss` OK at 844 x 390 (and earlier at 1080 on the first build of this package): Dolm breaks the Ward in 3 casts, Garrick dies once, +900, save and load at once: still one death, one resolved attempt, nothing pending, no restart; `duel_friendly` OK at 1920 x 1080 and 844 x 390 (two Orrin duels, nothing recorded, the clock did not move).
- Captures (`Evidence/screenshots/i7_*_1920x1080.png`, `i7_*_844x390.png`) looked at: see the hand-off report.

Not done or not verified:

- Nobody played it by hand; the packaged build was not built. 932 x 430 is covered by the layout test only (no capture).
- The duel stages from interior markers through I5's `InteriorSpace`; in this worktree that is the I0 stub, so the Tally Vault duel stands near the board centre on the surface. A stage at a far-east interior spot is covered by a test with a hand-made request.
- I6 must hide its own party bodies while a campaign duel runs (`CampaignDuel.Staged`, `EncounterHost.Ended`); I8 writes the checkpoint on `CampaignDuel.Resolved` (saving during an encounter is still refused by `SaveSystem`).

## Crystals I6: the expedition director and views (15 Nov 2026, branch `cry/i6`, from `historic-base`)

Mira's expedition in play (`PRD_CRYSTALS` 5.5 to 5.7, appendix B, package I6). The reference is `Docs/EXPEDITION_UI.md`.

- `Game/Expedition/**`: the director (Mira and the six as bodies placed where the sim says, legs walked in the leg's ticks, formation, gestures, the retreat with one staying, the fallen), her conversation in bubbles (opening, leads, calls, choices, thresholds, the tower, the tap menu), company (`SetCompany` on leaving, falling behind, rejoining), cues while away, sites and towers (stone, stair lid, Ash cut, four towers with the restoration effect, pedestals, altars, crystals, keepers, way-posts for the unwritten sites), the Ash runners, the journal on the map and the HUD line, the map layer, AutoPlay scripts, dev helpers.
- Interiors are package I5's. Until it is merged the party's interior cells are laid out far east of the island with a plain floor and walls, and a player in Mira's company is led there (the walker cannot step off the board). The duel is package I7's: until `CampaignDuel.Start` does something a stand-in bubble asks how the duel ends and sends `ResolveEncounter`.
- One hook outside the package: `PrinceDirector.Hush` (no new pitch, a pitch withdrawn, no tagging along while Mira has the player's ear; an overrun warning still gets through). Tooling: `AgentScripts/ExpeditionDrive.cs`.

Ran (15 Nov, editor, other editors busy on the same machine):

- `dotnet test DotNet`: 450 passed (the sim is untouched).
- Unity console: 0 errors from game code (the editor's own search indexer logs an exception at start).
- PlayMode: 88 tests (79 from the base and 9 in `ExpeditionTests`). The last full run passed 87; the one failure was a check in `ExpeditionTests` that expected one of two possible cues, corrected afterwards, and `ExpeditionTests` then passed 9 of 9 (6 min 43 s). In an earlier full run `DanceTests.Drums_LightForTheRightPads_AtCueMinusFall_AutoplayAllPerfect` failed once on its frame-timing check (0.31 s late against 0.1 s, the machine was loaded); alone it passed, and it passed in the last full run.
- AutoPlay in the editor at time scale 3 (game-minute timings do not depend on it), seed 20261113, the Ash march on the Deepstone 3:09: `earth_first` OK (Deepstone taken 2:30, restored 6:03), `earth_stolen` OK (as `expedition_captures`: threshold 7:19, loss 7:28, win 10:28, restored 12:24), `earth_stolen_win` OK (win 7:28, restored 9:24), `mira_alone` OK (she takes the Deepstone alone at 6:18, waits at the tower from 10:45, restored 11:58), `ash_runners` OK (seized 3:38, word 4:08). Captures at night in `Evidence/screenshots/i6_*.png` (not committed).

Not done or not verified:

- Nobody played it by hand. No packaged build, no phone-sized window pass (I8).
- Not run against real interiors (I5) or the real duel (I7): `Interiors.Enter`, `Interiors.LeadOut` and `CampaignDuel.Start` are called but were never exercised.
- No dialogue to suggest a scout (Mira sends Tansy herself when a belief is stale; the scout is shown in the world and on the map). The finale and the endings are Slice 2: an ending's lines are only cued.
- Lines the authored pack lacks are stand-ins in `ExpeditionLines.Missing` (listed in the hand-off).

## Crystals I8: the Earth slice joined and packaged (15 Nov 2026, branch `historic/main`, tag `earth-slice-1`)

Merged `cry/i5` (interiors), `cry/i7` (campaign duel), `cry/i6` (expedition) in that order onto `historic-base` and joined them (`PRD_CRYSTALS` 8.1, I8).

- **Interiors and the party.** `ExpeditionStandIn.cs` and the led viewpoint are deleted; the party and the player use I5's interiors (`Interiors.Enter` when the party takes a way with the player in company, `LeadOut` on Mira and on every party member underground). A follow never takes the player through a way end by itself (that sent the player back up the quarry stair). I5 draws pedestal, altar, inscriptions, tally and both ends of every way; I6 draws the crystals (one body each) and the keepers; I5's interior map draws the party.
- **Duel.** I7's driver alone stages every `EncounterCommitted` at the encounter's markers through `Interiors.TryWorld`; the stand-in bubble is gone (I6 calling `CampaignDuel.Start` a frame late re-staged a duel that had been left); I6 hides its bodies on `CampaignDuel.Staged` and shows them on `EncounterHost.Ended`; the duel's off-lattice camera keeps the interior lit; the threshold bubble uses `CampaignDuel.StakesLine` when the pack line does not name the volunteer; the resolved-attempt guard is per world and per campaign (a pending attempt of a load is staged); Mira's memorial line waits until the player is back with the party.
- **Save.** `SaveSystem`: checkpoint to the main slot on `CampaignDuel.Resolved` (not from editor runs without a test slot), extras `interior` and `scene.expedition`.
- **Start and tools.** `Boot/CampaignStart.cs` (`-start dev.historic -campaign authored_v1`), F1 tool "Campaign" (`UI/God/CampaignDevTool.cs`: start, send the Ash ahead, Mira to her next marker by stepping the clock, kill or revive a companion).
- **Look.** `View/CrystalLook.cs`: rough irregular clusters on a lump of rock with a palette per element (the Deepstone moss to amber, soft warm glow), used on the pedestal, altar, Mira's hands, the runners and the tower; no spin or bob at rest. Underground ambient, lantern and fog raised (`InteriorLighting`, budget unchanged).
- **Pack** (`authored_v1`): I6's missing lines moved in under the same ids; a `volunteer` and a `memorial` line for each of the six; prince lines for two to four towers; no line opens on a lower-case token; the game's sawyer is now Osric. The Ash opening march is 45 s longer (`MarchCost` 2250 to 2700, spread 600): the format has no field to start the march when the party sets out, so the opening's length was added to the march. The proof's check H was relaxed from 3-8 to 2-8 stolen for the 45 s player (now 2 of 11); `PackTests` and `CampaignRaceTests` bounds follow; `CAMPAIGN_PROOF.md` regenerated. Journal near-doubles (the runtime's and the pack's sentence for one deed) are hidden in the journal view.
- Scripts: `quarry_walk` and `duel_keeper_*` adjusted to run beside the expedition (`duel_keeper_*` switch the director off).

Ran:

- `dotnet test DotNet`: 450 passed. Unity EditMode was not run separately (the CLI's category filter did not take `!HeavyProof`; the same sources run under dotnet).
- PlayMode: 110 passed (full run, before the last script-only edits); then `InteriorTests` 12 passed.
- Editor AutoPlay (dev.historic, night captures): `earth_first` OK (1920 x 1080), `earth_stolen` OK (844 x 390 and 1920 x 1080: a real duel lost by a passive Ward player, Garrick held the way, retreat, a real duel won, out by the Ash cut, restored), `mira_alone`, `quarry_walk`, `duel_keeper_win`, `walk_tour`, `first_scene`, `duel_dance` OK. Console 0 errors.
- Windows build (`Builds/Windows/WorldMVP.exe`). Packaged, exit 0 and 0 error lines each: `first_scene`, `duel_dance`, `earth_first` (-timescale 4), `earth_stolen` (-timescale 4, two checkpoints written), `save_load`, `duel_keeper_loss` (save and load after a loss), `-start dev.historic -campaign authored_v1`, plain launch (night, the prince). The runs used the stored windowed size; one more launch full screen at 1920 x 1080 restored it.

Not done or not verified:

- Nobody played the route by hand. Packaged AutoPlay runs from `dev.historic` play by day (its clock); night was checked in editor captures only.
- The duel is played by `WardBot` through the encounter's methods, not by pointer taps; the replies to Mira are taps.
- Capture "retreat" shows the duel's result screen; the retreat scene itself plays after it and was not captured.
- The restored tower's ground growth is bright green cones; the beam colour is still the pack's green-gold.

## Characters WP-6 (Unity import) and WP-7 (rigged actor and factory) (15 Nov 2026, branch `char/unity`)

Details and the API for later packages: `Docs/CHARACTERS_UNITY.md`.

- Local art: `Tools/characters/sync_unity_local.py` copies the 28 character FBX (14 humans x 2 costumes) and `cast_manifest.json` into the git-ignored `Assets/LocalBaked/Characters` (it refuses unless `git check-ignore` agrees) and the 22 animation-only FBX (refused if any holds a mesh) with their manifests into the tracked `Assets/Characters/Animation`. Nothing under `Assets/LocalBaked` was staged.
- Import (WP-6): `CharacterImport` postprocessor (flat normals as authored, blend shapes, no tangents, sockets and chains kept, clip loop/root/additive/events from the manifests) and `CharacterBuilder` (Humanoid from the contract map for all 28 builds and all 22 animation files; role-slot `World/Lit` materials; one merged skinned mesh per build, one submesh per slot; prefabs in `Assets/LocalBaked/Characters/Resources/Characters`). Two import faults found and fixed: the part mesh `Head` clashed with the bone `Head` (Humanoid refused every character; meshes renamed on import), and animation-only FBX files default to a take's first frame, so their avatars were built from a posed skeleton and the retargeted walk was wrecked (avatars now take the rest pose of the rig the file was baked on). Forward axis +Z, scale 1 confirmed; no rotation needed.
- Runtime (WP-7): `RiggedActor` (PlayableGraph: idle variety, phase-synchronised gaits driven by ground covered, start/stop/turn clips timed by their root curves, full-body or upper-body gestures, talk loop, additive layer; face, gaze, cloth), `IExpressiveActor`, `IMotionShaper` (Npc speed shaping for the weight shift before the first step and the planted stop), `CastRegistry` (prince, Mira, champion by default; `-allCast` for the rest; `-placeholder-actors`), `ActorFactory` moved out of `PlaceholderActor.cs`. The prince's carried light is moved onto a small torch in his left hand (it washed out his face).

Ran (15 Nov, editor on `wt-char-unity`):

- `dotnet test DotNet`: 450 passed (12 min 30 s; it does not see the art).
- Unity, art present: EditMode **538 passed** (450 plus 88 `CharacterImportTests`, HeavyProof included, 25 min); PlayMode **87 passed** (79 plus 8 `ActorTests`); console 0 errors.
- Unity, `Assets/LocalBaked/Characters` moved out of the project: EditMode 450 passed, 6 character cases skipped with a message, 1 animation-file test passed, and `CampaignProof` failed once on a log line from the editor's command server (`/api/exec` connection aborted while this session polled it); run again alone it passed (18.6 min). PlayMode 80 passed, 7 `ActorTests` skipped with a message; console 0 errors. Art moved back afterwards and both character suites re-run green.
- AutoPlay in the editor from the night start with the rigged prince and Mira: `first_scene` OK (twice), `duel_dance` OK (twice: Victory after 3 casts, the dance phrase passed).
- Measured: Walk clip planted-foot speed at native speed (EditMode, own avatar) prince 0.053, Mira 0.017, champion 0.037 m/s. Root moved at the game's speeds (PlayMode, prince): 1.4 m/s 0.011, 2.2: 0.029, 3: 0.12, 5: 0.38, 7: 0.47, 8: 0.75 m/s mean; the clips themselves measure Jog 0.35 and Run 0.56 m/s at their native speed. 10 m lattice walk at 3 m/s with weight shift and stop: 4.03 s (3.33 s without shaping), ends on the cell. Gaze error 0.35 to 1.7 degrees on a target 35 degrees aside. Frame rate at night, 1920 x 1080, vSync off, three rigged plus 28 placeholder and land figures: 122 to 185 fps in the editor (one run under load: 62).
- Captures (local, `Evidence/screenshots/characters/`, `Evidence/video/`): approach and greeting video `Evidence/video/prince_approach_greet.mp4` (14 s, 30 fps PNG sequence, tracking camera), player-eye greeting, the prince's face at 2.5 m, the point at the plot (at 23:30 by the F1 scrub; the real run had reached dawn), Mira at her spot, the three faces close up, an 844 x 390 frame, the duel with rigged Mira.

Not done or not verified:

- Nobody played it by hand. No player build was made (not asked).
- The prince stops about 10 m from the player at the opening (the director's distance): at 844 x 390 and the walking view's 70 degree field of view his head is about 10 pixels, so the face does not read on a phone from the player's eye; it reads at dialogue distance.
- Weight shift and planted stops are there for walks started by `Npc`; the stop from a run first slows to walking pace (about 0.3 s more per walk). The running gaits slide (clip data). Standing turns pivot round the root, so the planted foot swivels a little.
- The point at the plot reads weakly (a low arm, no finger). Talk is a loop and a jaw ticker, not lip sync. Cloth is a light spring, not a simulation; the cape's back face reads pale when it lifts at a run.
- Humanoid clips carry no curves for the cape, hood and skirt bones; the runtime springs drive them.
- Over budget: `champion@prehistoric` 3,521 and `sol@prehistoric` 3,416 triangles (hero budget 3,200); art data, logged by the tests.
- Only the prince, Mira and the champion are on by default; the other 11 bodies (`-allCast`) were built and imported but not looked at in the game. `ward_player` maps to Juno (off by default), so the duel opponent stays a placeholder.
- `Evidence/video/` is not git-ignored in this repository (only `Evidence/screenshots/` and `Evidence/raw/` are); nothing there was staged.
- The `PrinceVillagers` decorations (feathers, sun standard) apply only to the placeholder prince.

## Crystals I8, step 5: the rigged cast merged (15 Nov 2026, tag `earth-slice-1-cast`)

Merged `char/unity` (rigged Prince, Mira and champion from the git-ignored `Assets/LocalBaked/Characters`, placeholders without it; `Docs/CHARACTERS_UNITY.md`). Only `Docs/PROGRESS.md` conflicted (both kept). Local art copied with `Tools/characters/sync_unity_local.py` (the script refuses a source inside the project, so `NEWSTUFF/characters_work/integrated` was copied to a temporary folder outside it first) and built with `CharacterBuilder.Run` (28 builds, Humanoid). Nothing under `Assets/LocalBaked` is staged.

- In the campaign duel underground Mira is her rigged body (factory id `mira`) and Dolm uses the champion body (`CastRegistry`: `keeper_*` -> champion); seen in the editor capture of `earth_stolen` at 1920 x 1080. The six companions stay placeholders (not in the reviewed set).
- PlayMode with the art: 118 passed. Without it (`Assets/LocalBaked` moved aside for the run): 111 passed, 7 `ActorTests` skipped (they need the art), 0 failed.
- Rebuilt (123 MB with the local prefabs). Packaged, exit 0 and 0 error lines: `first_scene`, `earth_stolen` (two checkpoints), full screen 1920 x 1080.
- Not checked: the rigged bodies by hand, or the companions' and keepers' look beyond the one capture.


## Historic H1-4 scene merged; the skirmish ending explained (15 Nov 2026, branch `historic/main`, tag `historic-h1-scene`)

Merged `ages/h1-4` (the prince's handover scene, calm announcements, advice by conversation, the "Show me" invitation, the story trigger for Mira's opening, age wording). No conflicts.

- **Tests.** PlayMode with the local art present: 127 run, 0 skipped. (The 7 skips seen in the worktree are the `ActorTests` that `Assert.Ignore` when `Assets/LocalBaked/Characters` is absent; here it is present, so they run.) One real failure from the merge, fixed: `ExpeditionTests.GoOnAhead_CuesMapAndHud_...` (in a developer Historic start the prince now speaks after the handover and his lines hold Mira's cues until she has arrived; the test now runs without the prince). One failure that was my own: a stray console command logged an error during `CastAll_AutoPlay_PlaysToAnEnding`, which then passed alone (94 s).
- **Why `follow_prince` stopped ending.** Not the sim, not the clock. The invitation into the Historic age is a speech bubble with the reply "Show me", the same label as a pitch's, and the script answered every "Show me" after its 3 s delay. So it accepted the invitation at about minute 7.5, the game became Historic, and there an overrun is a setback (the sim's rule: overrun and the time limit end only the Prehistoric skirmish), so it ran on to the script's 21 minutes at strength 11 to 1. Fixed in the script: it tells the invitation by its "Not yet" reply, declines it, and reports `path=declined` or `path=story`. After the handover the prince casts by himself, so the player casts less and the ending comes later than the old minute 14.4: `follow_prince` now ends Victory by overrun at minute 16.9. `ignore_prince`: Defeat by overrun at 12.4.
- **What a prompt player sees.** The handover comes at about minute 7 to 8, the invitation soon after: the story path is "Show me", into Historic and Mira's opening (about minute 11 in `handover`, 7.5 in `follow_prince_story`), and the game goes on with no victory or defeat screen. The Prehistoric skirmish ending (overrun, or the time limit) is for a player who keeps declining. New AutoPlay `follow_prince_story` (accepts "Show me", plays two more minutes, fails on any ending screen): passed, no ending at minute 9.5.
- **Restyle.** The ground round a restored tower is low moss patches, small ferns and tiny muted flowers (`ExpeditionSites.cs`), no green cones. Not looked at in a capture; compiled and run through `earth_first` only.
- **AutoPlay in the editor (night start):** `first_scene` OK (minute 2.6), `handover` OK (handover 8.2, Show me 11.7, Mira 11.8), `follow_prince` OK (victory by overrun 16.9, declined), `follow_prince_story` OK (Historic at 7.5, still playing at 9.5), `ignore_prince` OK (defeat by overrun 12.4), `duel_dance` OK, `earth_first` OK (restored 5:48 after the campaign began), `earth_stolen` OK.
- **Packaged** (`Builds/Next`, new folder, built with `AgentScripts/BuildNext.cs`; `Builds/Windows` and `Builds/Refactor_2026-11-14` untouched), all full screen 1920 x 1080, exit 0 and 0 error lines: plain launch to the playable night scene, `first_scene` (time x2), `handover` (x6; handover 7.3, Show me 11.0, Mira 11.3), `earth_first`.

## Ages H1-5 merged into `historic/main` (17 Nov 2026, merge commit `b0e58c3`)

Merged `ages/h1-5` (Historic grade visuals for the 16 building types: `BuildingDress.cs`, `GradeViews.cs`, `GradeViewsInstaller.cs`, `HistoricShapes.cs`, `SettlementDress.cs`, `Assets/World/Tests/PlayMode/GradeViewTests.cs`, `Docs/LAND_UI.md`). **Clean merge, no conflicts**: `ages/h1-5` touched only its own new Grade-view files (none of which existed on `historic/main`), and `historic/main` had only added one docs commit (`e120cf8`, the previous session's start-here note) since the branches diverged — so the two sides' changes never overlapped.

- **Recompile and console.** `unity command script recompile`: up to date, 0 errors. `unity command console status`: 0 errors, 4 warnings (pre-existing: Input Manager deprecation, two unused-field/-variable warnings in `PrinceScripts.cs`/`DanceCircle.cs`/`PrinceDirector.cs`).
- **Tests.** EditMode: **538 passed, 0 failed, 0 skipped**. PlayMode (full suite, async, ~21 minutes wall time including two domain reloads): **132 passed, 0 failed, 0 skipped**. Console stayed at 0 errors through both runs.
- **Build.** `AgentScripts/BuildNext.cs` into `Builds/Next/WorldMVP.exe`: succeeded, size 123 MB, 0 errors, 4 warnings, build time 17.4 s (incremental: `boot.config` and other changed data files got new timestamps; unchanged assets like `globalgamemanagers.assets` kept theirs — the Editor's own build report is the source of truth, not file mtimes).
- **Packaged AutoPlay** (`Builds/Next/WorldMVP.exe`, windowed 1280x720, `-novsync`), all exit 0, `[AutoPlay] <name> OK`, 0 error lines in any log: `first_scene`, `handover` (handover 8.3, declined 8.8, announced 9.5, advice at 10.6, invitation again 11.8, Show me 11.8, Mira's opening 12.0), `follow_prince` (`-princeDelay 3 -timescale 8`), `ignore_prince` (`-timescale 8`, Defeat by overrun at minute 11.9, as expected), `duel_dance` (Victory after 3 casts), `earth_first` (`-timescale 4`, Deepstone taken 2:18, restored 5:49), `earth_stolen` (`-timescale 4`, duel 1 Defeat as scripted at 7:07 with the companion lost, duel 2 Victory at 10:10 recovering the Deepstone, restored by minute ~12). `council_full` was **not** run — the council merge has not happened yet, per the owner's standing instruction.
- **Housekeeping.** Removed the stray `worldmvp_autoplay_save.json` the smoke runs created under `%LOCALAPPDATA%\LocalLow\Joe Salmon\WorldMVP`; the player's own `worldmvp_save.json` was untouched by any of this. **Not done:** the `HKCU\Software\Joe Salmon\WorldMVP` window-size registry key was not reset (the agent's registry-edit permission was refused); the owner's next double-click of the exe may open in whatever window size/mode the smoke runs left it in rather than full screen at the native resolution.
- **Not merged, not touched, per instruction:** `council/main` and `council/content` (being handled separately, gated on work not yet finished). No new feature work started.
- **Not pushed.** Commit is local only; the owner has not yet been asked.

## Council (incl. council/content) merged into `historic/main` (19 Nov 2026, merge commit `9efabb4`)

Merged `council/main` (readability pass `814874a`, with `council/content`'s dispute-text merge `319504a` already folded into it) into `historic/main` (on top of `ages/h1-5`, `b0e58c3`). **Clean auto-merge**: only `Assets/World/Game/Actors/PlaceholderActor.cs` needed a real merge (no conflict markers left behind, both sides' changes kept); everything else was a clean add. `NEWSTUFF/` left untracked, as instructed.

- **EditMode.** 519 passed, 0 failed, 0 skipped (job `6762cb0442f44fb5ac175fc568dcca91`, finished `2026-11-19T10:16:26Z`). Includes `CouncilProof.cs`'s ~23,000-election statistical balance check, which legitimately runs several minutes under sustained CPU — not a hang.
- **PlayMode.** 162 passed, 0 failed, 0 skipped (full suite, async, confirmed clean at `2026-11-19T11:08:52Z`). Up from the pre-merge 132: the increase is council's own view-layer `World.Game.Tests` now included. One tooling false alarm along the way: a background polling watcher crashed on a Windows console-encoding bug (`cp1252` decode) unrelated to Unity and separately timed out after a PlayMode job ID went stale across the domain reload (a known CLI gotcha); the real result was confirmed directly from `unity command console get` and `unity job status`, not from the watcher.
- **Build.** `AgentScripts/BuildNext.cs` into `Builds/Next/WorldMVP.exe`: succeeded, size 123 MB, 0 errors, 8 warnings, build time ~19.7 s.
- **Packaged AutoPlay** (`Builds/Next/WorldMVP.exe`, windowed 1280x720, `-novsync`): **12/12 passed**, all exit 0, `[AutoPlay] <name> OK`, 0 error/exception lines in any log — `boot_tour`, `walk_tour`, `first_scene`, `handover`, `follow_prince`, `ignore_prince`, `duel_dance`, `cast_all`, `save_load`, `earth_first`, `earth_stolen`, and **`council_full` for the first time (clean pass, no prior data to compare against)**.
- **Housekeeping.** `worldmvp_autoplay_save.json` was created by the smoke runs; the player's own `worldmvp_save.json` was untouched. **Not done, same as last time:** the `HKCU\Software\Joe Salmon\WorldMVP` window-size registry key reset was refused by the permission classifier — the owner's next double-click may open at whatever window size/mode the smoke runs left it in rather than full screen at the native resolution; clear it by hand with `reg delete "HKCU\Software\Joe Salmon\WorldMVP" /f` if that matters.
- **Branches now consolidated onto `historic/main`:** `ages/h1-5`, `council/main`, and `council/content` are all in. `council/main` and `council/content` worktrees are unaffected and can be cleaned up by the owner whenever convenient; nothing further is planned on them.
- **Still to do, not started here:** in-game debug shortcuts for jumping to each minigame, a final single packaged exe for the owner, and a short Word document with a manual test plan. The economy explainer is already delivered (`wt-docs/Economy_Explained.docx`).
- **Not pushed.** Commit is local only; the owner has not yet been asked.

## In-game debug shortcuts for Ward duel and the rhythm party (19 Nov 2026)

Added `Assets/World/Game/UI/God/PlaytestDevTool.cs`, a new F1 developer tool ("Playtest", hotkey `J`, registered in `DevToolsInstaller.cs` beside `CampaignDevTool`), following the existing `CouncilDevTool`/`CampaignDevTool` pattern exactly: it is only reachable from the F1 God View toolbar, like the Council and Campaign tools already are, and it never mutates sim state — it only calls `EncounterDirectory.TryStart`, the same entry point normal dialogue uses.

- **How to reach each minigame now (F1 God View, then the Playtest panel):**
  - **F1 → Playtest (or press `J`) → "Ward duel with Mira"**: starts the Ward duel at once, from wherever the player is standing. No campaign needed.
  - **F1 → Playtest → "Rhythm party with Sol"**: starts the dance/rhythm minigame at once, from wherever the player is standing.
  - **Council election** and the **crystal/earth quest** already had their own F1 tools before this session ("Council", and "Campaign" — the latter starts the Earth route on `dev.historic`/the authored pack, per `CampaignDevTool.cs`); nothing new was needed for those two.
- **Verified in Play Mode** (`unity command editor.playmode start` + `unity command script eval`, calling the exact same `EncounterDirectory.TryStart` line the new panel's buttons call): Ward duel starts `WardDuelEncounter`; after `EncounterHost.Instance.Abort()` to end it, the rhythm party starts `RhythmPartyEncounter`. Both confirmed by reading back `EncounterHost.Instance.Current`'s type, not just a return value.
- **EditMode** re-run after adding the tool: 519 passed, 0 failed, 0 skipped — no regression.
- **Build.** `AgentScripts/BuildNext.cs` into `Builds/Next/WorldMVP.exe`: succeeded, size 123 MB, 0 errors, 4 warnings, build time ~11.3 s. The shortcuts are in this build.
- **Not done:** no new automated test was added for the Playtest panel itself (it is a thin debug-only wrapper over an already-tested entry point; the manual Play Mode check above stands in for one). No PlayMode smoke script exercises it either, since it is a developer convenience, not a player-facing path.
- **Not pushed.** Commit is local only; the owner has not yet been asked.
