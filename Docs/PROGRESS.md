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

## Milestone 4 (simulation half): strategy, military and save codec (13 Nov 2026, branch feat/sim-integration)

Built (pure C#, API in `Docs/STRATEGY_API.md`):
- `Sim/Strategy`: prologue until the commission, then 3 ages x 2 rounds hooked on the economy's round end/start; endings (8 VP, 4 breaches, points after Age III; outcome + reason); FoundCity (`too_close`, `no_road`, `occupied`, `cannot_afford`), market Trade/Sell/Buy (Market building and Trade Charter improve the rate); infection deck, demons blocking carriers and city batches, breaches, hero moves (`no_turn_action`); draft 3-pick-pass-pick for both sides with a deterministic rival, 18 original cards (6 per age) in one table, all effects through real hooks; wonder stages paid by delivered wares; score breakdowns; rival plan VP; UI query helpers.
- `Sim/Military`: power allocation (`IsPowered`, priority then id), barracks queues with wares delivered to and consumed at the barracks, training only while supplied and powered, squads (one per barracks, max 4), delegated Defend/Attack orders over any edge, fixed-tick simultaneous combat, outpost with garrison (+1 per age), raids from Age II that hold a road flag for a round.
- `Sim/Save`: `SaveCodec.ToJson/FromJson/CreateSim/Load` with version, player pose and string extras.
- Economy edit (minimal): `EconomyHooks.EndRoundGuards`, checked by `EndRound` (draft pending, game over).
- Design choices to review: Age I waits for the commission (at most 8 prologue rounds) and cities and wonder stages wait for Age I; batches at player cities grow by 2 per age; tuned costs (city Planks 2 Stone 3 Grain 1; wonder stage 1 Planks 4 Stone 6, stage 2 Rations 2 Ingots 2; unit 1 rations + 1 ingots; guards HP 8 damage 2; rival plan VP on age-rounds 2, 4, 6).

Ran:
- `dotnet test DotNet`: 171 passed, 0 failed (135 earlier + 36 new: StrategyTests 19, MilitaryTests 7, SaveTests 4, StrategyConservationTests 1, StrategyPlaythroughTests 5 with headless scripted games; WIP commit included).
- Balance (default seed 20261113, scripted players): competent points player wins 6-5 on points after Age III at tick 4473 (7.5 min); it wins 9 of 10 other seeds (5.8-8.5 min). Passive player (only ends rounds) loses 2-6. Completionist player (barracks, units, attack) reaches an ending on the default seed and 10 others in 11.7-18.4 min with all invariants checked every tick; its attacks destroy the outpost on 2 of 11 seeds.
- Conservation with strategy and military commands: 20,000 random-command ticks, all invariants held.

Not run / known limits: nothing in Unity (no compile, no EditMode run in the editor, no PlayMode); `.meta` files left for Unity. Throughput is bound by the economy's 4 carriers, so wonder stages rarely complete inside 6 rounds in the scripted games and no scripted game reached 4 breaches (breach loss is covered by EditMode tests only).
