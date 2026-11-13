# Contracts for parallel work (from Milestone 0)

Binding for every agent building on M0. Change Core only with the coordinator; extend through your own folder. On branch `refactor/prehistoric` `Docs/REFACTOR_SPEC.md` overrides this file where they differ; the simulation sections below describe the refactored sim (package S, 13 Nov 2026).

## Namespaces and folders

| Folder | Namespace | Owner |
|---|---|---|
| `Assets/World/Sim/Core`, `Sim/Board` | `World.Sim` | coordinator only |
| `Assets/World/Sim/Economy` | `World.Sim.Economy` (state partials in `World.Sim`) | S |
| `Assets/World/Sim/Factions` | `World.Sim.Factions` (state partials in `World.Sim`) | S |
| `Assets/World/Sim/Demons` | `World.Sim.Demons` (state partials in `World.Sim`) | S |
| `Archive/Sim/Strategy`, `Archive/Sim/Military`, `Archive/Sim/Economy` | not compiled (kept for a later pass) | - |
| `Assets/World/Sim/Encounters/Ward`, `/Rhythm` | `World.Sim.Encounters.Ward`, `.Rhythm` | Ward / Rhythm agents |
| `Assets/World/Game/**` | `World.Game` (assembly World.Game, refs World.Sim + Bedsit.Core) | view agents |
| `Assets/World/Tests/EditMode` | `World.Sim.Tests` (assembly World.Sim.Tests) | each agent adds its own files |
| `Assets/World/Tests/PlayMode` | `World.Game.Tests` | each agent adds its own files |

All of `Assets/World/Sim` is one assembly, `World.Sim` (noEngineReferences, Newtonsoft.Json only).

## Core types (`World.Sim`)

- `Rng` (SplitMix64): `ulong State` (public field, the whole state); `NextULong()`, `NextInt(n)` [0,n), `NextRange(min, maxExcl)`, `Chance(num, den)`, `Shuffle(IList<T>)`, `Fork(label)` (derived stream; does not advance the parent), `Clone()`.
- `Ware` enum (prehistoric refactor): raw Wood 0, Clay 1, Grain 2, Sheep 3, Stone 4; worked Planks 5, Bricks 6, Bread 7, Meat 8, CutStone 9 (10 reserved for Ore). `Wares.All`, `Wares.Raw`, `Wares.Count` (10), `Wares.RawCount` (5), `Wares.IsRaw`, `Wares.DisplayName` (lower case, "cut stone"), `Wares.Title` ("Cut stone"). Coin is `PlayerState.Coin` (int, unused), never a ware. Save version 4 (older saves are refused).
- `WorldState` (sealed **partial**): `ulong Seed`, `long Tick`, `Rng Rng`, `Board Board`, `List<PlayerState> Players` (index = id), `Player(id)`.
- `PlayerState` (sealed **partial**): `int Id`, `string Name` ("Your people", "the Ash Clan"), `int Coin`. `PlayerIds.Human = 0`, `PlayerIds.Rival = 1`. Module state: `WorldState.Economy`, `.Factions`, `.Demons`, plus `.Ward`.
- `Command` (abstract): `int PlayerId`. Your commands subclass it with plain public fields.
- `CommandResult` (readonly struct): `bool Ok`, `string Reason` (code), `string Message` (sentence). `Accept()`, `Reject(code, message)`, `RejectDefault(code, params args)` (sentence from `Reasons.Sentence`).
- `ICommandHandler`: `Type CommandType`, `Validate(WorldState, Command)` (must not mutate), `Apply(WorldState, Command, EventBuffer)` returns `CommandResult`. Prefer `CommandHandler<T>` with typed `Validate`/`Apply`.
- `SimEvent` (abstract): `long Tick` (stamped by `EventBuffer.Add`). Core events: `CommandApplied { Command }`, `CommandRejected { Command, Result }`.
- `EventBuffer`: `Add(SimEvent)`, `Items`, `Count`.
- `ISimSystem`: `void Tick(WorldState s, EventBuffer ev)`.
- `SystemOrder` constants: Dice 100, Growth 150, Collect 200, Production 300, Construction 400, Jobs 500, Carriers 600, Demons 1100, Brain 1150, Strength 1200, Diagnostics 1300 (the older names Rounds, Harvest, Power, Barracks, Squads, Combat, Score stay as aliases of their slots). Sub-order with +1..+99.
- `WorldSim`: `State`, `const int TicksPerSecond = 10`, `AddSystem(int order, ISimSystem)`, `RegisterHandler(ICommandHandler)` (one per command type, duplicates throw), `Check(Command)` (validate only, for UI previews), `Submit(Command)` (validate now; if Ok, queue), `Step()` / `Step(n)`, `PendingCommands`, `DrainEvents()`.
- `Step()` order: `Tick += 1`; queued commands in submission order, each re-validated then applied (failure emits `CommandRejected`, nothing applied); then systems by (order, registration). An unregistered command type is refused with `disabled`.
- `Reasons`: closed stall list `no_road, no_carrier, waiting_ware, demon_on_route, output_full, input_missing, no_power, no_batch, invalid_site, no_turn_action, disabled` (constants `Reasons.NoRoad` etc., `All`, `IsKnown`, `Sentence(code, subject, detail)`). Stalls must use only these. Command refusals may use other codes; define those constants in your own folder and always give a sentence.
- `Reasons.Sentence(no_batch, subject, terrain)` now reads "{subject} is waiting for {terrain} stock to grow."; `no_power`, `invalid_site` and `no_turn_action` are unused.
- `WorldSimFactory.CreateNew(ulong seed)`: state + board + 2 players, then `EconomyModule.InitState`, `FactionsModule.InitState`, `DemonsModule.InitState`, then `SaveCodec.CreateSim`, which calls the three `Register(sim)` in the same order (also used for a loaded state).

## Board (`World.Sim`)

- `HexCoord` (axial Q, R; `S`, `DistanceTo`, `Neighbour(dir)`, `Ring`); `Terrain` {Meadow 0, Forest 1, Hills 2, Fields 3, Mountain 4, Pasture 5}; `TerrainInfo.RawWare(t)` (Forest Wood, Hills Clay, Fields Grain, Pasture Sheep, Mountain Stone; null for Meadow), `TerrainOf(rawWare)`, `IsResource(t)`, `IsMined(t)` (Hills, Mountain), `DisplayName(t)`, `Count` (6).
- `BoardTopology.Get(radius)` (cached, immutable, derived; never store it). The refactored sim uses only the COARSE level: hexes, hex CORNERS (ids 0..149) and HEX EDGES (ids 0..209, 60 m). The FINE members (`Vertex*`, `Edge*`, flags and segments) remain for the first pass, unused by any state or rule, and are removed by package F after integration. Coarse members: `CornerCount`, `Corners`, `IsCorner(v)`, `VertexHexes(corner)` (1-3 hexes), `HexCorners(h)`, `HexSides(h)`, `CornerNeighbours(v)`, `CornerHexEdges(v)`, `HexEdgeCount`, `HexEdgeCornerA/B(he)`, `HexEdgeBetween(c1, c2)`, `CornerDistance(a, b)`, `FindCornerPath(from, to)`, `NearestCorner(x, z)`, `HexEdgeMidX/Z`, `HexAt(x, z)`, `Hex(id)`, `HexIndex(coord)`, `HexX/HexZ`, `VertexX/VertexZ` (corner positions).
- Id ordering (stable, pinned by tests): hexes by (R, Q) (centre hex = 30); corners sorted south to north then west to east; hex edges by (lower corner id, higher corner id). Counts at radius 4: 61 hexes, 150 corners, 210 hex edges.
- Geometry (double metres; X east, Z north; Unity XZ): `BoardTopology.HexEdgeLength = 60`; true pointy-top corners. Plots: `PlotLayout` (spec 4.2): plot id = corner * 9 + sector * 3 + slot; positions, facing and local paths; every corner, hex centre and plot centre lies on the 5 m walking lattice.
- `Board` (in `WorldState.Board`): `Radius`, `Terrain[] HexTerrain`, `int[] HexNumber` (2..12, never 7; 0 for Meadow), `StartCorners` ([70, 97]), `SuggestedSettlementCorners`, `ReservedPlots` ([635], the dance circle), `Topology` ([JsonIgnore]).
- `BoardGenerator.CreateMvpBoard(seed)`: fixed radius-4 terrain layout; start corners 70 (player) and 97 (rival) on every seed; numbers per spec 2.A.1 (start hexes Forest 6, Hills 8, Fields 5; Mountains a seeded order of {5, 6, 9, 10}; the other 28 resource hexes a seeded shuffle of `NumberPool`).

Rules on the board (Economy, Factions, Demons): camps (settlements) only on interior corners with no camp on a `CornerNeighbour`; buildings on plots of their own camp's sectors; main roads on hex edges; carriers walk local plot paths and corner routes over their own people's roads; demons sit on finished camp corners and built road edges. Details and the read helpers: `Docs/ECONOMY_API.md`; the binding names: spec appendix A; the proof: `Docs/ECONOMY_PROOF.md` (written by the test `ProofReport`).

Cross-module hooks (per sim, `EconomyApi.Hooks(sim)`, re-added by every module's `Register`): `IsCornerBlocked`, `IsEdgeBlocked`, `IsSettlementHalted`, `OnRolled`, `OnSiteCompleted`.

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
