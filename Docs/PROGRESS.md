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