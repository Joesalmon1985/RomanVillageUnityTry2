# Contracts for parallel work (from Milestone 0)

Binding for every agent building on M0. Change Core only with the coordinator; extend through your own folder.

## Namespaces and folders

| Folder | Namespace | Owner |
|---|---|---|
| `Assets/World/Sim/Core`, `Sim/Board` | `World.Sim` | coordinator only |
| `Assets/World/Sim/Economy` | `World.Sim.Economy` (state partials in `World.Sim`) | economy agent |
| `Assets/World/Sim/Strategy` | `World.Sim.Strategy` (state partials in `World.Sim`) | strategy agent |
| `Assets/World/Sim/Military` | `World.Sim.Military` (state partials in `World.Sim`) | military agent |
| `Assets/World/Sim/Encounters/Ward`, `/Rhythm` | `World.Sim.Encounters.Ward`, `.Rhythm` | Ward / Rhythm agents |
| `Assets/World/Game/**` | `World.Game` (assembly World.Game, refs World.Sim + Bedsit.Core) | view agents |
| `Assets/World/Tests/EditMode` | `World.Sim.Tests` (assembly World.Sim.Tests) | each agent adds its own files |
| `Assets/World/Tests/PlayMode` | `World.Game.Tests` | each agent adds its own files |

All of `Assets/World/Sim` is one assembly, `World.Sim` (noEngineReferences, Newtonsoft.Json only).

## Core types (`World.Sim`)

- `Rng` (SplitMix64): `ulong State` (public field, the whole state); `NextULong()`, `NextInt(n)` [0,n), `NextRange(min, maxExcl)`, `Chance(num, den)`, `Shuffle(IList<T>)`, `Fork(label)` (derived stream; does not advance the parent), `Clone()`.
- `Ware` enum (unified economy, 13 Nov 2026): raw Logs 0, Clay 1, Wheat 2, Sheep 3, Ore 4; processed Planks 5, Coal 6, Bricks 7, Flour 8, Bread 9, Meat 10, Rations 11, Iron 12, Weapons 13. `Wares.All`, `Wares.Raw`, `Wares.Count` (14), `Wares.RawCount` (5), `Wares.IsRaw`, `Wares.DisplayName` (lower case), `Wares.Title`. Coin is `PlayerState.Coin` (int), never a ware. Save version 3 (older saves are refused).
- `WorldState` (sealed **partial**): `ulong Seed`, `long Tick`, `Rng Rng`, `Board Board`, `List<PlayerState> Players` (index = id), `Player(id)`.
- `PlayerState` (sealed **partial**): `int Id`, `string Name`, `int Coin`. `PlayerIds.Human = 0`, `PlayerIds.Rival = 1`.
- `Command` (abstract): `int PlayerId`. Your commands subclass it with plain public fields.
- `CommandResult` (readonly struct): `bool Ok`, `string Reason` (code), `string Message` (sentence). `Accept()`, `Reject(code, message)`, `RejectDefault(code, params args)` (sentence from `Reasons.Sentence`).
- `ICommandHandler`: `Type CommandType`, `Validate(WorldState, Command)` (must not mutate), `Apply(WorldState, Command, EventBuffer)` returns `CommandResult`. Prefer `CommandHandler<T>` with typed `Validate`/`Apply`.
- `SimEvent` (abstract): `long Tick` (stamped by `EventBuffer.Add`). Core events: `CommandApplied { Command }`, `CommandRejected { Command, Result }`.
- `EventBuffer`: `Add(SimEvent)`, `Items`, `Count`.
- `ISimSystem`: `void Tick(WorldState s, EventBuffer ev)`.
- `SystemOrder` constants (tick order from BUILD_PLAN 3): Rounds 100, Harvest 200, Production 300, Construction 400, Jobs 500, Carriers 600, Power 700, Barracks 800, Squads 900, Combat 1000, Demons 1100, Score 1200, Diagnostics 1300. Sub-order with +1..+99.
- `WorldSim`: `State`, `const int TicksPerSecond = 10`, `AddSystem(int order, ISimSystem)`, `RegisterHandler(ICommandHandler)` (one per command type, duplicates throw), `Check(Command)` (validate only, for UI previews), `Submit(Command)` (validate now; if Ok, queue), `Step()` / `Step(n)`, `PendingCommands`, `DrainEvents()`.
- `Step()` order: `Tick += 1`; queued commands in submission order, each re-validated then applied (failure emits `CommandRejected`, nothing applied); then systems by (order, registration). An unregistered command type is refused with `disabled`.
- `Reasons`: closed stall list `no_road, no_carrier, waiting_ware, demon_on_route, output_full, input_missing, no_power, no_batch, invalid_site, no_turn_action, disabled` (constants `Reasons.NoRoad` etc., `All`, `IsKnown`, `Sentence(code, subject, detail)`). Stalls must use only these. Command refusals may use other codes; define those constants in your own folder and always give a sentence.
- `WorldSimFactory.CreateNew(ulong seed)`: state + board + 2 players, then `EconomyModule.InitState`, `StrategyModule.InitState`, `MilitaryModule.InitState`, then the three `Register(sim)` in the same order.

## Board (`World.Sim`)

- `HexCoord` (axial Q, R; `S`, `DistanceTo`, `Neighbour(dir)`, `Ring`); `Terrain` {Meadow 0, Forest 1, Hills 2, Fields 3, Mountain 4, Pasture 5}; `TerrainInfo.RawWare(t)` (Forest Logs, Hills Clay, Fields Wheat, Pasture Sheep, Mountain Ore; null for Meadow), `TerrainOf(rawWare)`, `IsResource(t)` (numbered: everything but Meadow).
- Two graph levels in one `BoardTopology.Get(radius)` (cached, immutable, derived; never store it). Ids are plain `int`, `-1` = none.
  - COARSE (Catan): hexes, hex CORNERS and HEX EDGES (60 m). Cities, the rival outpost, the city distance rule and the hero live here.
  - FINE (Widelands): each hex edge is split into 3 road SEGMENTS (20 m) by 2 intermediate points. FLAGS = corners + intermediate points. Buildings stand on flags, roads are per segment, carriers, squads and raids walk segment to segment.
  - Naming rule: every `Vertex*`/`Edge*` member is the FINE graph (vertex = flag, edge = segment); coarse members say `Corner` or `HexEdge`.
- Fine members: `VertexCount` (flags), `EdgeCount` (segments), `VertexHexes(v)` (corner: 1-3 hexes; intermediate flag: the 1-2 hexes sharing its hex edge), `VertexEdges(v)`, `VertexNeighbours(v)`, `Degree(v)` (corner 2-3, intermediate 2), `EdgeVertexA/B(e)` (A = lower flag id), `OtherEnd(e, v)`, `EdgeBetween(v1, v2)`, `FindPath(from, to, Func<int,bool> edgeAllowed = null)` (BFS by segments, ties by ascending flag id, flag array incl. both ends, null if unreachable; block a flag by rejecting its segments), `PathEdges(path)`, `Distance(...)` (segments; unfiltered calls use a cached BFS table), `DistancesFrom(v)`, `NearestVertex(x, z)`, `HexAt(x, z)`.
- Coarse members: `CornerCount`, `Corners` (flag ids 0..CornerCount-1), `IsCorner(v)`, `HexCorners(h)` (6, corner order), `HexSides(h)` (6 hex-edge ids; side k joins corner k and k+1), `HexFlags(h)` (18 boundary flags in ring order: corner k at 3k, then the two flags of side k), `CornerNeighbours(v)` (corners one hex edge away = exactly 3 segments), `CornerHexEdges(v)`, `HexEdgeCount`, `HexEdgeCornerA/B(he)`, `HexEdgeBetween(c1, c2)`, `HexEdgeSegments(he)` (3e, 3e+1, 3e+2 from the lower corner), `HexEdgeFlags(he)` (4 flags lower corner to higher corner), `HexEdgeOfSegment(s)` (= s / 3), `HexEdgeOfFlag(v)` (-1 for a corner), `CornerDistance(a, b)` (hex edges), `FindCornerPath(from, to)` (corner steps, e.g. hero moves), `NearestCorner(x, z)`, `HexEdgeMidX/Z`. `Hex(id)`, `HexIndex(coord)`, `Contains`, `HexCount`.
- Id ordering (stable, deterministic; pinned by tests): hexes by (R, Q) (centre hex = 30 at radius 4); corners first, flag ids 0..CornerCount-1 sorted south to north then west to east; then intermediate flags, id = CornerCount + 2 * hexEdge + j (j = 0 nearer the lower corner); hex edges by (lower corner id, higher corner id); segments id = 3 * hexEdge + k walking from the lower corner. Adjacency lists ascending; per-hex lists in corner order.
- Counts at radius 4: 61 hexes, 150 corners, 210 hex edges, 570 flags, 630 segments.
- Geometry (double metres; X east, Z north; Unity XZ): `BoardTopology.HexEdgeLength = 60` (hex edge = circumradius, the world scale), `EdgeLength = 20` (one segment, the distance between neighbouring flags), `SegmentsPerHexEdge = 3`, `HexX/HexZ`, `VertexX/VertexZ`, `EdgeMidX/EdgeMidZ` (segment midpoint). True pointy-top corners; corner k at angle 60k-30 deg; intermediate flags exactly at 1/3 and 2/3 of their hex edge.
- `Board` (in `WorldState.Board`): `Radius`, `Terrain[] HexTerrain`, `int[] HexNumber` (1..4, 0 for Meadow), `HomeCityVertex` (corner), `RivalOutpostVertex` (corner), `StorehouseVertex` (= home), `MarketVertex` (= home), `WarehouseVertex` (intermediate flag 2 segments from home, beside the square; never buildable), `SuggestedCitySites` (corner ids, nearest home first), `Topology` ([JsonIgnore]).
- `BoardGenerator.CreateMvpBoard(seed)`: fixed radius-4 layout (see its doc comment, `MvpRadius = 4`); numbers dealt by seed, evenly within each terrain; the three home hexes (Fields, Forest, Hills) get distinct numbers. Home corner 70 (a corner of the centre hex), rival outpost corner 97 (5 hex edges north-west, its own Forest/Hills/Fields), Mountain range of 4 hexes north-east outside the home territory, 14 suggested city sites. Occupancy (cities, buildings, roads, demons) belongs to the modules. See `Docs/MAP_MIGRATION.md` for what the views must change.

Rules on the two levels (Economy, Strategy, Military): buildings on any free flag in territory (flags within `EconomyTuning.TerritorySegments` = 4 segments of one of your city corners; terrain from `VertexHexes(flag)`); roads per segment; cities only on corners, needing a road segment touching the corner and no city or outpost on a `CornerNeighbour`; the hero moves one hex edge per move; demons sit on flags, and an outbreak spreads to the connected neighbouring sites (straight-line walk on the flag graph to the first flag that can hold demons). Details in `Docs/ECONOMY_API.md` and `Docs/STRATEGY_API.md`; the economy design is `Docs/ECONOMY_DESIGN.md` and its proof `Docs/ECONOMY_PROOF.md` (written by the test `EconomyProofReport`).

Cross-module hooks (per sim, `EconomyApi.Hooks(sim)`, re-added by every module's `Register`): `OnRoundEnding`, `OnRoundStarted`, `BatchModifiers`, `IsVertexBlocked` (demons, raids), `IsHexStopped` (the robber), `IsPowered` and `PowerDemanded` (Military), `EndRoundGuards`. Card effects the economy applies are written into `EconomyState.Bonus` by Strategy (`SyncEconomyBonuses`), so static API calls see them.

## How a module plugs in (no edits outside your folder)

1. State: add `Economy/WorldState.Economy.cs` with `namespace World.Sim { public sealed partial class WorldState { public EconomyState Economy; } }`. Same pattern for `PlayerState`.
2. `XModule.InitState(WorldState s)`: create your starting state (read `s.Board`, `s.Seed`; draw randomness from `s.Rng.Fork("economy")` or your own stored `Rng`).
3. `XModule.Register(WorldSim sim)`: `sim.AddSystem(SystemOrder.Carriers, new CarrierSystem());` and `sim.RegisterHandler(new PlaceRoadHandler());`.
4. Commands and events: subclasses of `Command` / `SimEvent` in your folder, plain public fields.
5. Cross-module reads are fine (`s.Economy` from Military); cross-module writes go through a small public method owned by that module, never by poking another module's fields from a system.
6. Never edit `Core/`, `Board/`, `WorldSimFactory.cs` or another module's folder. Need a Core change? Ask the coordinator.

## Coding rules for `World.Sim`

- C# 9 at most (no `record struct`, file-scoped namespaces, global usings, raw strings). Nullable off.
- No `UnityEngine` (the asmdef enforces it). No wall clock, threads, `Guid.NewGuid`, `System.Random` or `HashCode`-dependent order.
- No `float`/`double` in sim state. Integers and ticks only. Board geometry doubles are derived, never stored.
- Deterministic iteration: never drive logic by enumerating a `Dictionary`/`HashSet` unless you sort first. Use lists or arrays indexed by id.
- State is plain data for Newtonsoft: public fields or auto-properties, parameterless constructors, no references to systems, delegates or topology; mark derived helpers `[JsonIgnore]`. `WorldState` must round-trip through `JsonConvert` (see `WorldSimTests.WorldState_RoundTripsThroughJson`).
- Tests for `World.Sim` go in `Assets/World/Tests/EditMode` and must not use `UnityEngine` (they also run under .NET). Use classic NUnit 3 asserts (`Assert.AreEqual`, `Assert.That`).

## World.Game rules

- `WorldBootstrap.Instance` exposes `Sim`, `Clock`, `MainCamera`, `Sun`, `Ground`. Never write `WorldState`; read it, `Submit` commands, animate events.
- `GameClock`: steps at 10 Hz; `Pause(reason)`, `Resume(reason)`, `IsPaused`, `IsPausedFor(reason)`; `PauseReasons.Encounter/Menu/Draft`. It drains sim events after each tick and raises `event Action<IReadOnlyList<SimEvent>> Ticked`; subscribe to it, do not call `DrainEvents` yourself.
- Materials only via `MaterialFactory.Create(color)`, `Shared(color)`, `CreateVertexColored(tint)`, `Paint(go, color)`; never the default primitive material.
- IMGUI only via `UiStyle` (`Label`, `Title`, `Small`, `Button`, `Panel`, `Px(px1080)`, `DrawPanel`, `ShadowLabel`, `SolidTexture`).
- Scene content is built in code from `WorldBootstrap`; `AgentScripts/MakeWorldScene.cs` regenerates the scene, base materials and build list.

## Running tests

- Fast, outside Unity, from the project root: `dotnet test DotNet` (compiles `Assets/World/Sim/**` and `Assets/World/Tests/EditMode/**`).
- In Unity: `unity command test run --mode editor` (add `--filter <Fixture>`); PlayMode: `unity command test run --mode playmode --async-tests`, then poll `unity command test status`.
- Then `unity command console status`: `consoleErrors` must be 0.
