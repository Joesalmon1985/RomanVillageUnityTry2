# Economy API

Road economy of the unified design (`Docs/ECONOMY_DESIGN.md`, implemented 13 Nov 2026). Code: `Assets/World/Sim/Economy` (namespace `World.Sim.Economy`). The economy belongs to `PlayerIds.Human`; commands from other players are refused (`not_owner`). Proof that it works: `Docs/ECONOMY_PROOF.md`. Views read state and these helpers; they never write state.

## Wares (`World.Sim.Ware`, Core)

| Id | Ware | Kind | Made by | Suggested colour (`EconomyApi.WareColour`) |
|---|---|---|---|---|
| 0 | Logs | raw (Forest) | Woodcutter, 2 per batch unit | #7A4E2D |
| 1 | Clay | raw (Hills) | Clay pit | #B5653A |
| 2 | Wheat | raw (Fields) | Farm | #E3C04A |
| 3 | Sheep | raw (Pasture) | Sheepfold | #EDEDE4 |
| 4 | Ore | raw (Mountain) | Mine (eats rations) | #5E6670 |
| 5 | Planks | processed | Sawmill | #C89B5E |
| 6 | Coal | processed | Charcoal kiln | #2B2B2B |
| 7 | Bricks | processed | Brick kiln | #A33A2A |
| 8 | Flour | processed | Mill | #F2EBD9 |
| 9 | Bread | processed | Bakery | #C98A3D |
| 10 | Meat | processed | Butcher | #B5474B |
| 11 | Rations | processed | Tavern | #6E8B3D |
| 12 | Iron | processed | Smelter | #9AA3AD |
| 13 | Weapons | processed | Smithy | #4A5A78 |

`Wares.All`, `Wares.Raw`, `Wares.Count` (14), `Wares.RawCount` (5), `Wares.IsRaw(w)`, `Wares.DisplayName(w)` ("logs", for sentences), `Wares.Title(w)` ("Logs", for labels). `TerrainInfo.RawWare(terrain)` and `TerrainInfo.TerrainOf(rawWare)`. Coin is `PlayerState.Coin`, never a ware.

## Buildings (`BuildingType`, table in `EconomyTuning.Buildings`, index = (int)type)

| Id | Type | Kind | Size (cost) | Recipe / role |
|---|---|---|---|---|
| 0 | Storehouse | Store | (not placeable) | holds the village's goods, at the home corner |
| 1 | Forester | Forester | Small (1 P) | forest batches it touches are not halved |
| 2 | Woodcutter | Harvester, Forest | Small | 1 forest batch unit -> 2 logs (50 ticks) |
| 3 | Sawmill | Producer | Medium (1 P + 1 B) | 1 logs -> 1 planks (30) |
| 4 | ClayPit | Harvester, Hills | Small | 1 unit -> 1 clay |
| 5 | Farm | Harvester, Fields | Small | 1 unit -> 1 wheat |
| 6 | Mill | Producer | Medium | 1 wheat -> 1 flour (30) |
| 7 | Bakery | Producer | Medium | 1 flour -> 1 bread (40) |
| 8 | Mine | Harvester, Mountain | Medium | 1 unit -> 1 ore; pays 1 rations per 3 ore |
| 9 | Smelter | Producer, needs power | Large (2 P + 1 B) | 2 ore + 1 coal -> 1 iron (50) |
| 10 | PowerPlant | PowerPlant | Large | burns 1 coal per 600 ticks while something needs power; supplies 4 |
| 11 | Barracks | Plain, needs power | Huge (2 P + 2 B) | trains soldiers (Military) |
| 12 | Market | Plain | Large | market rate -1 |
| 13 | WonderSite | Plain | Huge | receives wonder stage goods (Strategy) |
| 14 | Sheepfold | Harvester, Pasture | Small | 1 unit -> 1 sheep |
| 15 | Butcher | Producer | Medium | 1 sheep -> 1 meat (40) |
| 16 | Tavern | Producer | Medium | 1 bread + 1 meat -> 2 rations (40) |
| 17 | CharcoalKiln | Producer | Medium | 1 logs -> 1 coal (40) |
| 18 | BrickKiln | Producer | Medium | 2 clay + 1 coal -> 2 bricks (40) |
| 19 | Smithy | Producer | Large | 1 iron + 1 planks -> 1 weapons (50) |

`BuildingDef`: `Type, Name, Kind (Store|Forester|Harvester|Producer|PowerPlant|Plain), Size (Small|Medium|Large|Huge), Cost[], NeedsTerrain, Terrain, Inputs[], CyclesPerInput (Mine 3), Outputs[], WorkTicks, Capacity (8), NeedsPower, BuildTicks (100), FuelTicks, Placeable, Purpose` (one line). Sizes cost (`EconomyTuning.SizeCost`): Small 1 planks; Medium 1 planks + 1 bricks; Large 2 planks + 1 bricks; Huge 2 planks + 2 bricks (`ApplySizeCosts()` after changing them).

## State (`WorldState.Economy`, plain data, ids = list indices)

- `Tuning` (`EconomyTuning`), `Round` (from 1), `RoundStartPending`, `RoundStartedTick` (added: tick of the current round's draw; the harvest season counts from it), `LastDraw`, `RoundRng`.
- `HexBatchRound[hex]`, `HexBatchRemaining[hex]`, `HexStopped[hex]` (true while the robber stops the hex; derived each tick), `Roads[segment]`, `RoadCredit`, `Cities` (`City{Id, Owner, Vertex}`; the rival outpost is a rival city).
- `Buildings` (`Building{Id, Type, Owner, Vertex, State: Site|Constructing|Active|Removed, Enabled, Priority, BuildProgress, WorkProgress, WorkHex, FuelLeft, InputCredit (Mine: ore cycles already paid), Blocked (a demon or raid on its flag: it does no work), Stock[], Reserved[], Incoming[], Requested[], Reason, ReasonWare}`).
- `Jobs` (open `DeliveryJob{Id, Ware, SourceId, DestId, State: Waiting|ToPickup|Carrying, CarrierId, Reason, ...}`), `Carriers` (`Carrier{Id, State: Idle|ToPickup|Carrying|Returning, AtVertex, CurrentEdge, FromVertex, ToVertex, Progress, HasLoad, Load, JobId, IdleTicks}`). On a segment interpolate `FromVertex -> ToVertex` by `Progress / Tuning.TicksPerEdge` (30); at a flag `CurrentEdge = -1`.
- `StorehouseId`, `DemoVertexA/B`, `Commission{Complete, CompletedTick}`, `Bonus` (`EconomyBonuses{ExtraOutput[type], RoadSegments}`: card effects the economy applies, written by Strategy).
- `Ledger` (`WareLedger`, per ware): `Starting, Harvested, Produced, Added, Consumed` (in world = Starting + Harvested + Produced + Added - Consumed) and the sink split `RecipeIn[type][ware], RecipeOut[type][ware], Cycles[type], Construction[], Roads[], External[]` (Consumed = sum RecipeIn + Construction + Roads + External; Produced = sum RecipeOut of producers), plus `BatchAuthorised[]` and `BatchLost[]` (raw units lost to the carry-over cap).

## Commands (`WorldSim.Submit`; refusals give a code and a sentence)

`PlaceRoad{Edge}` (one 20 m segment touching your network; every `RoadSegmentsPerCost` = 3 segments cost `RoadCost` = 1 planks + 1 bricks from the storehouse, paid on the first, Road Guild +3 segments), `RemoveRoad{Edge}`, `PlaceBuilding{Type, Vertex}` (any free flag in territory, terrain rule on `VertexHexes(flag)`; not a city, the outpost or the warehouse flag), `CancelSite{BuildingId}`, `SetPriority{BuildingId, Priority}`, `ToggleBuilding{BuildingId, Enabled}`, `EndRound`.
Refusal codes: `invalid_site` and `EconomyRefusals.*` (`not_connected`, `road_exists`, `no_road_here`, `not_enough_wares`, `unknown_building`, `not_a_site`, `not_toggleable`, `round_starting`, `round_too_soon`, `not_owner`, `bad_place`, `unknown_type`). `EndRound` refuses `round_too_soon` (added) until the harvest season is over: `Tuning.MinRoundTicks` = 600 ticks (60 s) after the round started ("The harvest season is still on: the bell can ring in 23 s."); it also refuses `draft_pending` and `game_over` (Strategy guards). Grey the bell out while `EconomyApi.BellTicksLeft(s) > 0` and show the countdown.

## Events (plain fields; `Tick` stamped)

`RoadPlaced/RoadRemoved{Edge}`, `SitePlaced/SiteCompleted/SiteCancelled{BuildingId, Type, Vertex}`, `BuildingStateChanged{BuildingId, State, Enabled, Reason, ReasonWare}`, `JobCreated{JobId, Ware, SourceId, DestId}`, `WarePickedUp/WareDelivered{JobId, CarrierId, Ware, BuildingId}`, `JobStalled{JobId, Reason}`, `JobResumed`, `JobReleased`, `HarvestCycle{BuildingId, Hex, Ware, Amount}`, `ProductionStarted{BuildingId}` (inputs used), `ProductionCycle{BuildingId, Output, Amount}`, `UpkeepPaid{BuildingId, Ware, Amount, Cycles}` (Mine ate rations), `FuelBurned{BuildingId, Ware, Amount}`, `RoundStarted{Round, Numbers}`, `BatchAuthorised{Hex, Ware, Amount}`, `CommissionCompleted`.

## EconomyApi (static, `WorldState` first)

- Hooks per sim (`Hooks(sim)`, not saved; modules add them in `Register`): `OnRoundEnding`, `OnRoundStarted`, `BatchModifiers`, `IsVertexBlocked` (demons, raids), `IsHexStopped` (robber), `IsPowered`, `PowerDemanded`, `EndRoundGuards`; `Blocks(s, v)`, `HexStopped(s, hex)`.
- Rounds: `AuthoriseBatch`, `BatchRemaining`, `BellTicksLeft(s)` (added: ticks until the bell may ring, 0 = now), `RoundElapsedTicks(s)` (added). Tuning: `EconomyTuning.BatchSizeFor(terrain)` (added). Cities: `AddCity`, `IsInTerritory`, `RoadCostFor(s, segments)`, `IsVertexOccupied`, `CheckPlacement`.
- Buildings: `FindBuilding`, `BuildingAt`, `GetBuilding`, `Def`, `IsConnected`, `IsFuelled`, `HasRoad`. Carriers: `AddCarrier`.
- Wares for other modules: `RequestWares`, `Available`, `TryConsume` (booked as External), `StorehouseCount` (unreserved), `TrySpendFromStorehouse`, `AddToStorehouse`, `WaresInWorld`.
- **UI helpers** (build every panel from these):
  - `WareName(w)`, `WareTitle(w)`, `WareColour(w)` ("#RRGGBB"), `CostText(int[])` ("1 planks and 1 bricks").
  - `DescribeType(s, type)` -> `BuildingTypeView{Type, Name, Purpose, Size, CostText, NeedsText ("Needs: 1 clay and 1 coal per cycle."), MakesText ("Makes: 2 bricks per cycle."), RecipeText ("1 clay and 1 coal -> 2 bricks (4 s)"), TerrainText, NeedsPower, Placeable}`; `BuildMenu(s)` (all placeable types); `RecipeText(s, type)` (includes card bonuses).
  - `LegalBuildFlags(s, type)` (flags where a site may be placed now; roads not needed), `ProducersOf(s, ware)`, `ConsumersOf(s, ware)`.
  - `WaitingFor(s, buildingId)` -> `WaitInfo{BuildingId, Reason, Ware, Working, Sentence}`; `Describe(s, id)` -> `EconomyInfo{Title, Status, Reason, StockLines}`; `DescribeJob(s, jobId)`.
  - Sentences include: "Sawmill is waiting for logs.", "Mine needs rations to work.", "Smelter has no power.", "A demon on its flag stops the Sawmill.", "A demon blocks the route to the Forester site.", "A demon on your city stops the forest producing for the Woodcutter.", "No road connects the Storehouse to the Forester site.".

## Rules in brief

Tick slots: Rounds (refresh `Blocked`/`HexStopped`, then the round-start draw), Harvest, Production, Construction, Jobs (create + reserve), Carriers (advance, assign, act and step), Power, Diagnostics (one reason per building, commission).
- **Harvest season.** A round lasts at least `MinRoundTicks` = 600 ticks: `EndRound` is refused (`round_too_soon`) before.
- **Batches.** Each round start draws `NumbersDrawn` = 3 distinct numbers of 1..4 and authorises a batch on every resource hex touching one of your cities with a drawn number: `TerrainBatchSize` (added, per terrain: forest 12, hills 10, fields 8, pasture 8, mountain 5; 0 = `BatchSize` 10) +1 per age + card bonuses; unused units carry over up to `BatchCap` = 24. Forest batches are halved (rounded up) unless an active Forester touches the hex. Prologue starter sequence: until the commission (at most `StarterRounds` = 6) the draw is the home forest's number plus, in turn, the home hills' and the home fields'. A hex whose every touching player city holds a demon gets no batch and its harvesters stop (the robber).
- **Harvest.** A harvester takes one unit from a touching hex of its terrain and works `HarvestTicks` = 50; the Woodcutter yields 2 logs per unit. The Mine pays 1 rations before every 3 cycles (`InputCredit`); without rations it reports `input_missing` (rations).
- **Recipes.** Inputs are used at the start of a cycle, outputs appear at the end (plus `Bonus.ExtraOutput` on the main output). No progress without power or while `Blocked`.
- **Economy targets** (added, Widelands): a workshop does not start a cycle while the storehouse holds (unreserved) `StockTarget[output]` of its output (planks 24, bricks 24, coal 8, rations 12; flour, bread, meat, iron, weapons 4; raw wares none); it reports `output_full` with that ware, and `Describe` says "The storehouse has enough planks: the Sawmill waits until some is used."
- **Power.** Each fuelled plant gives 4; smelters need 2 while working or holding a cycle of inputs, barracks 2 while a unit is queued; allocation by priority then id (Military). A plant lights a new coal only while some building needs power.
- **Deliveries.** Producers push outputs to a consumer that needs them (priority first, then the one with the least on hand in cycles, then the nearest), else keep `HoldForConsumer` = 4 for a local recipe consumer and send the rest to the storehouse. Sites, recipe buildings, the Mine's rations, fuel and external requests pull from the storehouse the same way. Carriers serve jobs to buildings before stockpiling jobs; after a delivery a carrier waits `CarrierWaitTicks` = 150 at that flag, then walks home (taking jobs at any flag on the way). Carriers re-plan at each flag (removed road or demon: they stop with their load). A job not yet picked up that has had no route for 150 ticks is released.
- **Carriers.** 8 at the start, +2 per finished building (`CarriersPerBuilding`), +2 per founded city (Strategy).
- **Reasons.** Site: `no_road`/`demon_on_route`, else `waiting_ware` (named); constructing under a demon: `demon_on_route`; active: `disabled`, `demon_on_route` (own flag blocked), `no_power`, `no_road`, `output_full`, `no_batch`, `waiting_ware`/`input_missing` (named).
- **Commission.** Complete when an active, road-connected Forester, Woodcutter, Sawmill, Clay pit, Charcoal kiln and Brick kiln exist and the storehouse holds >= 18 planks and >= 18 bricks; `CommissionCompleted` fires once.

**Starter (default seed 20261113).** Home corner 70 (storehouse and market), warehouse flag 361. Home hexes: 22 Forest, 30 Fields, 31 Hills. Demonstration road 70-333-332 with a Forester site on 332 and its planks already on carrier 0. Starting stock 16 planks, 6 bricks, 2 coal, 4 rations; 8 carriers. The scripted player completes the commission at 3.2 minutes on every seed (the prologue draws are the fixed starter sequence); see `Docs/ECONOMY_PROOF.md` for the full games.
