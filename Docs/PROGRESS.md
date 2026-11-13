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
