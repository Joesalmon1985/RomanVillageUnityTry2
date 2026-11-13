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
