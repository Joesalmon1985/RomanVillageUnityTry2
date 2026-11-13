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
- `Ware` enum: Timber, Planks, Stone, Grain, Flour, Rations, Ore, Ingots. `Wares.All`, `Wares.Count`. Coin is `PlayerState.Coin` (int), never a ware.
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

- `HexCoord` (axial Q, R; `S`, `DistanceTo`, `Neighbour(dir)`); `Terrain` {Meadow, Forest, Hills, Fields, Mountain}; `TerrainInfo.RawWare(t)`.
- Ids are plain `int` (hex, vertex, edge; `-1` = none). `BoardTopology.Get(radius)` (cached, immutable, derived; never store it):
  `HexCount/VertexCount/EdgeCount`, `Hex(id)`, `HexIndex(coord)`, `HexVertices(h)` (corner order), `HexEdges(h)`, `VertexHexes(v)`, `VertexEdges(v)`, `VertexNeighbours(v)`, `Degree(v)`, `EdgeVertexA/B(e)`, `OtherEnd(e, v)`, `EdgeBetween(v1, v2)`, `FindPath(from, to, Func<int,bool> edgeAllowed = null)` (BFS, vertex array incl. both ends, null if unreachable; block a flag by rejecting its edges), `PathEdges(path)`, `Distance(...)`, `NearestVertex(x, z)`, `HexAt(x, z)`.
- Ordering: hexes by (R, Q); vertices south-to-north then west-to-east; edges by (low vertex, high vertex); adjacency lists ascending.
- Geometry (double metres; X east, Z north; Unity XZ): `BoardTopology.EdgeLength = 20`, `HexX/HexZ`, `VertexX/VertexZ`, `EdgeMidX/EdgeMidZ`. True pointy-top corners; corner k at angle 60k-30 deg.
- `Board` (in `WorldState.Board`): `Radius`, `Terrain[] HexTerrain`, `int[] HexNumber` (1..4, 0 for Meadow), `HomeCityVertex`, `RivalOutpostVertex`, `StorehouseVertex` (= home), `MarketVertex` (= home), `WarehouseVertex` (home neighbour), `Topology` ([JsonIgnore]).
- `BoardGenerator.CreateMvpBoard(seed)`: fixed radius-2 layout (see its doc comment); numbers dealt by seed; the three home hexes (Fields, Forest, Hills) get distinct numbers; Mountain only away from home. Occupancy (cities, buildings, roads, demons) belongs to the modules.

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
