# Map migration: radius-4 board with fine building plots

Simulation change of 13 Nov 2026 (branch `feat/sim-integration`). The board grew from 19 hexes with 20 m edges to 61 hexes with 60 m hex edges, and gained a fine level: every hex edge is three 20 m road segments. Contract details: `Docs/CONTRACTS.md` (Board section). This file lists what the Unity layer must change.

## What changed in the simulation, in one table

| Item | Before | Now |
|---|---|---|
| `BoardGenerator.MvpRadius` | 2 (19 hexes) | 4 (61 hexes) |
| Hex size | `EdgeLength` 20 m = hex edge = circumradius | `HexEdgeLength` 60 m = hex edge = circumradius; `EdgeLength` 20 m = one road SEGMENT |
| `VertexCount` / "vertex" | 54 hex corners | 570 FLAGS: 150 corners (ids 0..149) + 420 intermediate flags (2 per hex edge) |
| `EdgeCount` / "edge" | 72 hex edges | 630 road SEGMENTS (3 per hex edge, id = 3 * hexEdge + k) |
| Cities, outpost, hero, demons on corners | any vertex | cities, outpost and hero on corners only; demons on corners and occupied intermediate flags |
| Buildings, roads, carriers, squads | vertices / edges | flags / segments (all 20 m apart, as before) |
| `HexVertices(h)`, `HexEdges(h)` | 6 corners / 6 edges | removed; use `HexCorners(h)`, `HexSides(h)` (hex-edge ids) or `HexFlags(h)` (18 ring flags) |
| `WarehouseVertex` | home neighbour, 20 m | intermediate flag 2 segments (40 m) from home along the Fields/Hills hex edge |
| Territory | 3 edges | `TerritorySegments` 4 (80 m walking the flag graph) |
| Terrain | 5 kinds | adds `Terrain.Pasture` (= 5; no ware yet, gets numbers) |
| New on `Board` | | `SuggestedCitySites` (corner ids, nearest first) |

New coarse accessors: `IsCorner`, `CornerCount`, `Corners`, `HexCorners`, `HexSides`, `HexFlags`, `CornerNeighbours`, `CornerHexEdges`, `HexEdgeCount`, `HexEdgeCornerA/B`, `HexEdgeBetween`, `HexEdgeSegments`, `HexEdgeFlags`, `HexEdgeOfSegment`, `HexEdgeOfFlag`, `CornerDistance`, `FindCornerPath`, `NearestCorner`, `HexEdgeMidX/Z`, `DistancesFrom`.

## Unity layer changes needed

Found by grepping `WorldMVP/Assets/World/Game/**` and `Assets/World/Tests/PlayMode/**` (main checkout, 13 Nov 2026). Every use whose meaning or scale changed, with the recommended fix:

| Where | Member used | What changed | Recommended fix |
|---|---|---|---|
| `Board/BoardSpace.cs` `HexCorner(t, h, k, scale)` | `BoardTopology.EdgeLength` as hex radius | Hex radius is now `HexEdgeLength` (60); `EdgeLength` is the 20 m segment | Use `BoardTopology.HexEdgeLength`, or better return `Vertex(t, t.HexCorners(h)[k])` (exact, no trig) and lerp towards the centre for `scale`. |
| `View/TerrainBuilder.cs` `BuildHex` (tile mesh via `BoardSpace.HexCorner`) | hex radius | 3x larger tiles | Follows from the BoardSpace fix. Build the outer ring from `HexFlags(h)` (18 points) so tile edges pass exactly through the intermediate flags; scale the inner-ring jitter (+-1.5 m) and skirt to the bigger tile. |
| `View/TerrainBuilder.cs` props | `inR = EdgeLength * 0.82` and fixed prop counts (26 trees, 9 rocks, ...) | Hex area is 9x larger | `inR = HexEdgeLength * 0.82`; scale counts by about 9 (or by area), keep `NearAnyVertex(t, p, 2.5f)` (flags are still 20 m apart) so plots stay clear. Consider a draw-call budget: 61 hexes x 9x props. |
| `View/TerrainBuilder.cs` edge lines | loop over `EdgeCount`, `EdgeVertexA/B` | Now 630 segments (fine) | Still correct as road-plot lines. Optionally draw hex borders thicker from `HexEdgeFlags(he)[0]..[3]` per hex edge and the segments thinner. |
| `View/TerrainBuilder.cs` vertex posts | loop over `VertexCount` | Now 570 flags (was 54 corners) | Full posts only where `t.IsCorner(v)`; small ground markers (disc, no flag) at intermediate flags. 570 full posts would also triple the post mesh. |
| `View/TerrainBuilder.cs` hex labels (God View) | `HexTerrain`, `HexNumber`, label offsets `(0, 9, -6)`, size 2.2 | Hexes 3x wider; new `Terrain.Pasture` | Scale offset and text size about 3x; label Pasture with its number (it is numbered, `TerrainInfo.RawWare` is null for now); add a colour for Pasture in `TerrainColor`. |
| `View/TerrainBuilder.cs` `BuildOutskirts`, `BuildBounds` | board extent | Board radius about 450 m (was about 90 m) | Derive from `BoardSpace.BoardRadius`; the 400 m ground plane in `WorldBootstrap` must grow to at least about 1,000 m. Recheck camera far planes and fog. |
| `Board/BoardSpace.cs` `BoardRadius` | loop over `VertexCount` | 570 flags; still correct | Loop over `t.Corners` (same result, fewer points). |
| `Board/BoardSpace.cs` `NearestEdge` | linear scan over `EdgeCount` | 630 segments (cost x9) | Fine per click; if called per frame, find `HexAt` first and scan that hex's `HexFlags` segments. |
| `Player/BoardPicker.cs` | `VertexRadius = 5`, `EdgeRadius = 3` | Flags are still 20 m apart, so these radii stay valid for flags and segments | Keep. Add `CornerAt` (`NearestCorner`, radius about 10 m) for city and hero tools, which only accept corners. |
| `View/VillageLayout.cs` | `HomeCityVertex`, `WarehouseVertex`; `Out = W - H` | Warehouse flag is now 40 m out (was 20 m); the hex edge continues to a corner at 60 m | `Out` is still a good axis. Put `WarehouseDoor` at the warehouse flag (not `+2 m`), and check the 32 x 30 m footprint (`i * 7f` chain to about 70 m) against flags: it now covers the corner at 60 m and intermediate flags of the next hex edges, which carry roads and buildings. Shorten the chain or build the warehouse to the side of the hex edge (`Right`). Storehouse (`Right * 21`) and market (`-Right * 13`) sit next to intermediate flags 20 m from home on the other two hex edges; nudge them off those flags (about 6 m clearance). |
| `View/VillageBuilder.cs` | `StorehouseVertex`, `MarketVertex` | Unchanged meaning (home corner) | None. |
| `Boot/WorldBootstrap.cs` | `HomeCityVertex` for the camera / spawn | Unchanged meaning | None; check the god-view camera height and zoom range for a 900 m board. |
| Road / building / carrier views (any) | edge ids, `TicksPerEdge` | One road piece = one 20 m segment; carriers still take `EconomyTuning.TicksPerEdge` (60) per segment; squads `MilitaryTuning.TicksPerEdge` = 20 per segment (was 40 per edge) | Interpolate per segment as before. Road cost text: 1 planks pays for 3 segments (`EconomyApi.RoadCostFor`). |
| City and hero tools (any) | `StrategyApi.LegalCitySites`, `HeroMoveTargets` | Corners only; hero moves one hex edge (60 m) and clears the flags it passes | Highlight corners; animate the hero along the 3 segments of the hex edge. `CitySites` is still indexed by flag id (intermediate flags refused `bad_vertex`). |
| Demon views (any) | `StrategyState.Demons[flag]` | Demons can sit on corners and on occupied intermediate flags | Draw at any flag. |
| `Tests/PlayMode/WorldBootTests.cs` | `Assert.AreEqual(19, ...HexCount)` | 61 | Change to 61 (or `BoardTopology.Get(BoardGenerator.MvpRadius).HexCount`). |
| `Tests/PlayMode/VillageTests.cs` | `NearestVertex(home + (1, 0, -1), 5)`, `NearestEdge(EdgeMid(e), 1)` every 7th edge, `HexAt(Hex(h))`, `HexAt(Hex(4))`, `VertexEdges(home)[0]` picking | Same API, more ids | Should pass as is (flags 20 m apart); run them to confirm and expect a longer loop (630 segments). |
| Any hard-coded ids or distances (docs, scene scripts) | e.g. home vertex 24, warehouse 18, edges 29/21 | Ids changed | Now: home corner 70, warehouse flag 361, demo road flags 333 and 332, rival outpost corner 97 (default seed; terrain and ids are seed-independent). Never hard-code; read `Board` and `Economy.DemoVertexA/B`. |

Not checked: `AgentScripts/*` scene builders and any code added in the main checkout after the grep.

## Unity migration: done in main (13 Nov 2026)

- [x] `BoardSpace.HexCorner`: exact from `HexCorners(h)`, `scale` lerps to the centre. New: `HexSize`, `SegmentLength`, `NearestCorner`, `NearestHexEdge`, `HexEdgeMid/HexEdgeEnds`, `DistanceToSegment`, `IslandRadius`.
- [x] `TerrainBuilder` tiles: outer ring through the 18 `HexFlags` (sorted by angle), inner-ring jitter 4 m, skirt kept. All 61 hexes.
- [x] Props: sampled over the 60 m hex, kept `RoadMargin` 4 m off the hex edges (replaces `NearAnyVertex`), counts scaled about 7x (Forest about 170 trees, Hills 26 mounds + 70 rocks, Fields 8 m crop rows instead of 1.4 m boxes, Meadow 300 flowers, Mountain 3 peaks of 38-58 m), one combined mesh per hex, colliders only on mountain peaks.
- [x] Edge lines: one 0.5 m line per hex edge (210); the 630 segments lie on them.
- [x] Vertex posts: stone disc + pennant post only at the 150 corners (none inside the square); intermediate flags get a small plot disc in the God View overlay only.
- [x] Hex labels: size 6.5 (grow to x3.2 when zoomed out), raised 70 m and shifted against the camera pitch so they read over the hex; Pasture has its own colour, look (fence, sheep, trough) and label with its number.
- [x] `BuildOutskirts` / `BuildBounds`: island rim (trees, hills) and beach out to `IslandRadius`, 6,000 m sea plane (was a 400 m ground plane), 48 bound walls at `BoardRadius` + 6. Fog 140-700 m, first-person far plane 1,500 m, God far plane 2,500 m.
- [x] `BoardRadius`: loops over `Corners`, cached.
- [x] `NearestEdge`: nearest of 210 hex edges, then its 3 segments.
- [x] `BoardPicker`: radii kept (flag 5 m, segment 3 m); added `CornerAt/CornerUnderMouse` (10 m), `HexEdgeAt/HexEdgeUnderMouse` (6 m), `FlagAt/SegmentAt` aliases.
- [x] `VillageLayout`: rebuilt. Square centred 5 m out from H (radius 12); storehouse, market and 5 cottages in the three sectors between the hex edges at H, at least 9 m from other flags and 7 m from the edge lines; warehouse rescaled to about 17.75 x 22 m and placed beside flag W in the hex to the Right of the H-W edge, door facing the square (H + 28 Out + 9 Right), so it covers no flag and the corner 60 m out stays free. Everything within about 55 m of H, inside a 58 m clearing.
- [x] `VillageBuilder`: storehouse 8 x 6 m, cottages, paths to door/storehouse/market.
- [x] `WorldBootstrap`: spawn and camera unchanged in meaning; God camera `MaxSize` from the island radius, `HomeFocus` = village centre; `Outpost` built at `RivalOutpostVertex`.
- [ ] Road / building / carrier / city / hero / demon views: deferred, not built (economy views wait for the simulation proof; branch feat/econview not merged).
- [x] PlayMode tests: hex count 61; village tests use `WarehouseSet` positions instead of local coordinates; new `MapLayoutTests` (61 tiles + Pasture, village within 70 m and off the plots, outpost at its corner, God camera default and max framing, picker ids for flags, segments, corners, hex edges and hexes).
- [x] No hard-coded ids in the Unity layer.
