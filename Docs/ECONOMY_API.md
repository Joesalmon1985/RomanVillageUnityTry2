# Economy API

Road economy (Milestone 3, simulation). Code: `Assets/World/Sim/Economy` (namespace `World.Sim.Economy`). The economy belongs to `PlayerIds.Human`; commands from other players are refused (`not_owner`).

**State** (`WorldState.Economy`, plain data, ids = list indices, never Dictionary-driven):
- `Tuning` (`EconomyTuning`), `Round` (from 1), `RoundStartPending`, `LastDraw` (numbers of this round), `RoundRng`.
- `HexBatchRound[hex]`, `HexBatchRemaining[hex]`; `Roads[edge]` (bool); `Cities` (`City{Id, Owner, Vertex}`).
- `Buildings` (`Building{Id, Type, Owner, Vertex, State: Site|Constructing|Active|Removed, Enabled, Priority, BuildProgress, WorkProgress, WorkHex, FuelLeft, Stock[], Reserved[], Incoming[], Requested[], Reason, ReasonWare}`; ware arrays indexed by `(int)Ware`; cancelled sites stay as `Removed`).
- `Jobs` (open `DeliveryJob{Id, Ware, SourceId, DestId, State: Waiting|ToPickup|Carrying, CarrierId, Reason, ...}`; finished jobs are removed).
- `Carriers` (`Carrier{Id, State: Idle|ToPickup|Carrying|Returning, AtVertex, CurrentEdge, FromVertex, ToVertex, Progress, HasLoad, Load, JobId}`). On an edge, interpolate `FromVertex -> ToVertex` by `Progress / Tuning.TicksPerEdge`; at a flag `CurrentEdge = -1`.
- `StorehouseId`, `DemoVertexA/B`, `Ledger` (Starting, Harvested, Produced, Added, Consumed per ware), `Commission{Complete, CompletedTick}`.

**Commands** (`WorldSim.Submit`; refusals give a code and a sentence):
`PlaceRoad{Edge}` (must touch your city/building/site/road; costs `Tuning.RoadCost` = 1 planks), `RemoveRoad{Edge}`, `PlaceBuilding{Type, Vertex}` (free flag, not the warehouse flag, in territory, terrain rule), `CancelSite{BuildingId}` (wares back to the storehouse at once), `SetPriority{BuildingId, Priority}`, `ToggleBuilding{BuildingId, Enabled}`, `EndRound`.
Refusal codes: `invalid_site` (placement) and `EconomyRefusals.*` (`not_connected`, `road_exists`, `no_road_here`, `not_enough_wares`, `unknown_building`, `not_a_site`, `not_toggleable`, `round_starting`, `not_owner`, `bad_place`, `unknown_type`).

**Events** (plain fields): `RoadPlaced/RoadRemoved{Edge}`, `SitePlaced/SiteCompleted/SiteCancelled{BuildingId, Type, Vertex}`, `BuildingStateChanged{BuildingId, State, Enabled, Reason, ReasonWare}`, `JobCreated{JobId, Ware, SourceId, DestId}`, `WarePickedUp/WareDelivered{JobId, CarrierId, Ware, BuildingId}`, `JobStalled{JobId, Reason}`, `JobResumed{JobId}`, `JobReleased{JobId, Reason}`, `HarvestCycle{BuildingId, Hex, Ware}`, `ProductionCycle{BuildingId, Output, Amount}`, `RoundStarted{Round, Numbers}`, `BatchAuthorised{Hex, Ware, Amount}`, `CommissionCompleted`.

**EconomyApi** (static, `WorldState` first):
- `Hooks(sim)` -> `EconomyHooks`: `OnRoundEnding`, `OnRoundStarted` (lists of `Action<WorldState, EventBuffer>`), `BatchModifiers` (`Func<WorldState, hex, Ware, amount, int>`, applied to round-draw batches before the forester rule), `IsVertexBlocked` (`Func<WorldState, vertex, bool>`, demons), `IsPowered` (`Func<WorldState, buildingId, bool>`, default true). Held per sim, not in state: add them in your `Register`, and re-add after building a sim around a loaded state.
- Rounds: `AuthoriseBatch(s, hex, amount, ev)` (once per hex per round; forest halved, rounded up, unless an active Forester touches the hex), `BatchRemaining(s, hex)`.
- Cities: `AddCity(s, player, vertex)` (-1 if the flag is taken), `IsInTerritory(s, player, vertex)` (within `TerritoryRadius` = 3 edges of a city), `IsVertexOccupied`, `CheckPlacement(s, player, type, vertex)`.
- Buildings: `FindBuilding(s, type, activeOnly)`, `BuildingAt(s, vertex)`, `GetBuilding`, `Def(s, type)`, `IsConnected(s, id)`, `IsFuelled(s, id)`, `HasRoad(s, edge)`.
- Wares: `RequestWares(s, id, ware, count)` (carriers bring them to that active building), `Available(s, id, ware)`, `TryConsume(s, id, ware, count)` (unreserved only), `StorehouseCount` (unreserved), `TrySpendFromStorehouse`, `AddToStorehouse`, `WaresInWorld`.
- Diagnostics: `Describe(s, buildingId)` / `DescribeJob(s, jobId)` -> `EconomyInfo{Title, Status, Reason, StockLines}`; e.g. "Sawmill is waiting for timber.", "No road connects the Storehouse to the Forester site.".

**Rules in brief.** Tick slots: Rounds (draw), Harvest, Production, Construction, Jobs (create + reserve), Carriers (assign + move), Power, Diagnostics (reasons, commission). Producers push outputs to a building that needs them (nearest, by priority), else to the storehouse; while an active recipe building uses the ware they keep up to `HoldForConsumer` units. Sites, recipe buildings (input buffer) and requests pull from the storehouse. Jobs are only created when a route exists. A carrier re-plans at every flag, so a removed road stops it at the next flag with its load (`no_road`, or `demon_on_route` when only a blocked flag is in the way) until a route returns. A job not yet picked up that has had no route for `ReleaseStalledJobTicks` is released (`JobReleased`) so its reserved wares can pay for the fix. Building reasons: site `no_road`/`demon_on_route`, else `waiting_ware` (named); active: `disabled`, `no_power`, `no_road`, `output_full`, `no_batch`, `waiting_ware` / `input_missing` (named).

**Rounds.** Round 1 starts on the first tick. Each round start draws 2 distinct numbers from 1..4 and authorises `BatchSize` (6) for every resource hex touching a player city with a drawn number (unused units carry over up to `BatchCap` 12). Starter sequence: round 1 = home forest + home hills numbers; round 2 = home fields + one of them (seeded); round 3 = the other one + a random number; then random.

**Commission.** Complete when an active, road-connected Forester, Woodcutter, Sawmill and Quarry exist and the storehouse holds >= 10 planks and >= 10 stone; `CommissionCompleted` fires once.

**Tuning defaults** (`EconomyTuning`): `TicksPerEdge` 60, `Carriers` 4, `BatchSize` 6, `BatchCap` 12, `NumbersDrawn` 2, `TerritoryRadius` 3, `InputBuffer` 3, `FuelBuffer` 2, `HarvestTicks` 30, `HoldForConsumer` 4, `ReleaseStalledJobTicks` 150, `RoadCost` 1 planks, `StarterStock` 14 planks + 4 stone, `Buildings` (table, index = `(int)BuildingType`): costs Forester/Woodcutter/Quarry 2 planks; Sawmill 2 planks + 1 stone; Farm 2+1; Grain mill, Bakery 2+2; Ore pit 3+1; Refinery 2+3; Power plant 3+2; Barracks 4+3; Market 3+2; Wonder site 2+2. Recipes: Sawmill timber->planks 30 ticks, Grain mill grain->flour 40, Bakery flour->rations 40, Refinery ore->ingots 60 (NeedsPower); Power plant burns 1 timber per 300 ticks; Barracks NeedsPower (plain store). Build time 50 ticks; output capacity 6.

**Starter layout, default seed 20261113.** Home city / storehouse / market vertex 24; warehouse flag 18 (not buildable). Home hexes: 5 Forest (#2), 9 Fields (#1), 10 Hills (#4). Demonstration road: edges 29 (24-19) and 21 (19-14); Forester site at vertex 14; carrier 0 starts on edge 29 carrying 1 planks for it. The headless script then places a Woodcutter at 19, a Sawmill at 25 and a Quarry at 30 (road edges 30 and 36) and completes the commission at tick 2052 (round 6).

**Not here (owned elsewhere).** The Market building is optional (its flag is the storehouse's): trade goes through the storehouse methods. Power supply logic, barracks queue, wonder stages, demons and rival cities plug in through the hooks and API above.
