# Land views (package V)

How the world shows the simulation: the island, hex numbers and growing stock, camps of both peoples, plots, local paths, buildings and sites, main roads, carriers, warriors, demons, the overrun, and the "cast here" marks of the prince's proposals. Binding rules: `Docs/REFACTOR_SPEC.md` sections 2, 3, 8.4 and appendix B. Namespace `World.Game`; code in `Assets/World/Game/View/Land/`, `View/TerrainBuilder.cs`, `Board/BoardSpace.cs`. Original primitive shapes only (vertex-coloured meshes from `MeshBuilder`); nothing is imported.

## Entry point and lifecycle

- `LandViewsInstaller.Install(boot)` (third installer of `WorldBootstrap.Awake`): sky, terrain (with a cleared stock patch at every hex centre), then `LandViews.Create(boot)`.
- `LandViews` subscribes to `GameClock.Ticked`. After every tick it reconciles every view with `WorldState` (create, update, remove), plays the tick's effects (roll flare, paid beams, overrun burst) and raises `Synced(events)`.
- `RebuildFromState()` destroys every dynamic view and rebuilds from state alone. The scene reload of a load or new game does the same through `Bind`. `Snapshot()` (sorted strings: kind, id, state, counts, positions) is the test proof that rebuilt equals incremental.
- Per tick it walks only what exists: settlements, buildings, roads, carriers, warriors, the player's open proposals, and the hexes whose stock changed (stock, harvest and demon events) or is still growing. It never scans the 1350 plot ids or the 210 hex edges; demons are looked up only on existing camp corners and road edges. Per frame it only animates existing views (tokens and boards turn to the camera, figures bob, fires flicker, carriers move).
- World.Game never writes `WorldState`: the views read state and the sim's helpers (`EconomyApi`, `FactionApi`, `DemonApi`, `PlotLayout`).

## Appendix B surface (`LandViews`)

| Member | Meaning |
|---|---|
| `Instance` | the one LandViews |
| `event Action<IReadOnlyList<SimEvent>> Synced` | after the views caught up with a tick (or an `Inject`) |
| `Transform CarrierView(int carrierId)` | the carrier's figure (an `IFollowable`), or null |
| `Transform BuildingView(int buildingId)` | the building or site root on its plot, or null |
| `Transform ProposalMarker(int proposalId)` | the cast-here mark of an open proposal of your people, or null |
| `RebuildFromState()` | as above |

Additions (appendix B change log, V): `static ICarrierFeed CarrierFeed`, `Inject(events)`, `static Vector3 Eye`, typed lookups `Hex`, `Settlement`, `Building`, `Road`, `Carrier`, `Demons`, `Marker`, `WarCamp`, enumerations, `Snapshot()`, `RebuildCount`, `HexSyncs`, `TickFraction`.

## What is drawn, and from what

| Thing | Class | Source in state | Look |
|---|---|---|---|
| Number token | `HexLandView` | `Board.HexNumber`, `DiceRolled` | post at the hex centre, 2.1 m cream disc at 4.4 m turned to the camera; 6 and 8 in red; pips under the number. A roll of its number: the token swells and glows for 3 s. A dark purple cross while `HexStockView.Stopped`. |
| Roll pays | `HexLandView.Paid` | `StockAdded` | a fading beam and an expanding ring in the ware's colour over the patch and "+4" rising, 3 s |
| Stock patch | `HexLandView.Sync` | `EconomyApi.HexStock` | 6 m patch (forest loam with stumps, tilled field, fenced pasture, red diggings, gravel) with one item per unit up to the cap (8 round the outside, 4 inside): broadleaf saplings to trees, seedlings to ripe sheaves, lambs to sheep, in stage 0, 1, 2 and mature. Clay and stone: nothing young; mature as dug piles, and a yellow "ready" flag with pick and shovel while any is mature. |
| Camp | `SettlementView` | `Settlement` | hearth with fire on the corner, the storehouse piles (one block per unit per ware, 10 piles on a 2.2 m ring, up to 12 blocks, a cone above 12), a ring of stones in the people's colour, the totem (yours: sky-blue wings, sun disc; the Ash Clan: red wings, horned ash skull). City: taller totem and a stake palisade at 8 m with gaps on the spokes and hex edges; the upgrade site raises it stake by stake with its goods and bar beside. Camp site: stakes and rope, goods, bar, builder. |
| Empty plots | `SettlementView` | `EconomyApi.PlotsOf`, `Plot` | ten small stones in a 4.3 m ring for each `Free` plot of a finished camp; nothing for locked or reserved plots |
| Local paths | `SettlementView` | `PlotLayout.PathPoints` | 1.5 m earth path from the storehouse ring along the spoke to each plot with a site or building |
| Building | `BuildingView` | `Building` | per type (table below), facing `PlotYaw`, a pennant in the people's colour |
| Building site | `SiteParts` | `Delivered`, `Def.Cost`, `BuildProgress` | stakes and rope, one block per unit of the cost (solid ware colour when delivered, dark wire frame while missing), progress bar on a post (goods are the first half, build time the second); while constructing the model rises in a scaffold and a builder works |
| Gatherer | `BuildingView` | `WorkProgress`, `WorkHex` | while a collector's cycle runs its gatherer walks (6 m/s) to the edge of the hex's patch, works 1.1 s, walks back with a bundle of the ware; stands by the door otherwise and while the camp is demon-held |
| Workshop worker | `BuildingView` | `WorkProgress` | a figure bending at the work while producing |
| Warriors | `BuildingView` (lodge) | `Economy.Warriors` by `Lodge` | fed: standing with spear and crest; hungry: slumped with an empty bowl |
| Main road | `RoadView` | `Road` | site: pegs and rope along the whole edge, goods and bar; constructing: laid from one end with a builder; built: 3 m earth road with kerb stones and a people's pennant by a cairn at its middle |
| Carrier | `CarrierView` | `ICarrierFeed` (default `EconomyApi.CarrierPosition`) | figure in the people's colour exactly on the feed position while moving; a bundle in the ware's colour on the head; idle carriers wait 4 m from the corner in the gaps between paths and roads |
| Porter (shipment) | `CarrierView` | `LegCorners` or `Job.IsShipment` | basket on the back with one bundle per unit (1-4) and a staff |
| Demons | `DemonView` | `DemonApi.DemonsAt` on camp corners and road edges | one horned figure per demon, a scorched ring, a dark red fading spire, the count on a dark token |
| Held camp | `SettlementView` | demons on its corner | fire out, grey smoke puffs, dark ground with a purple ring; its collectors start no cycle (gatherers idle); its hexes' tokens crossed when every adjacent camp is held |
| Proposal mark | `ProposalMarkerView` | `FactionApi.Proposals(s, 0)`, `ProposalTarget` | pulsing gold ring over the press area (blue for banish) with turning inward arrows; roads: a 6 m strip along the edge with chevrons; the top proposal is bright and has a light pillar seen from afar |
| War camp | `WarCampView` | `FactionApi.Warning`, outcome | three tents, war fire, two standards, four warriors facing the defender and a board with the seconds left, 18 m along the hex edge of the attacker's camp nearest the defender, 3.5 m to the side; stays after an overrun |
| Overrun | `LandViews.Burst`, `SettlementView` | `FactionOverrun`, outcome | a red flash rising over every camp of the loser, then their camps burn and their totems fall |

Building models (front +Z, 4.5 m radius): Log pile (stacked logs, stump with stone axe), Clay scrape (scraped red hollow, lumps, digging stick, hide bag), Gleaners' basket (big woven basket, sheaves, flint sickle), Thorn fold (ring of thorn bushes, hay), Rock heap (cairn, hammerstone); Woodcutter's hut (round thatched hut, log rack, chopping block), Clay pit (dug pit, ramp, lean-to with drying blocks), Farm (hut, granary on stilts, furrows), Shepherd's hut (hut, wattle pen, crook), Quarry (stepped face, cut blocks, tripod hoist); Sawmill (saw-pit trestles under a hide roof, planks), Kiln (clay dome and chimney, bricks), Bakery (bread oven under a roof, quern, loaves), Smokehouse (tall hide tent, meat racks), Stonecutter (bench, cut blocks, small hut), Warrior lodge (longhouse, spear rack, shields in the people's colour).

## Inspecting

Everything has a trigger collider and an `IInspectable` with a title and one or two sentences from the sim's own helpers: hex (`HexStock`), building or site (`DescribeBuilding`, `WaitingSentence`, `CostText`), camp and storehouse (`SettlementName`, stock, `WaitingSentence`, `DemonApi.Effect`), road (`WaitingSentence`), carrier (`DescribeCarrier`), demons (`DemonApi.SiteName`, `Effect`), proposal mark (`ProposalTitle`, the proposal's reason), war camp (`FactionApi.Warning`), warriors and gatherers (`LandInspectTag`). Colliders are triggers, so nothing blocks. The proposal mark's collider is 10 cm high, so a hit on it lies on the ground of the press area.

## Terrain (`TerrainBuilder`)

- One tile per hex, props merged per hex. Props keep 4 m off hex edges (roads), 8.5 m off every hex centre (stock patch and token), and, through `SetClearings`, 10 m off every camp corner (sites included), 6.8 m off all nine plots of the camp and 2.4 m off its local paths (recomputed only when the set of camps or the war camp changes; only touched hexes rebuild).
- Mountains: six crags per Mountain hex, 41 m out towards the hex-edge midpoints, base radius about 6.5 m: at least 8 m from any possible plot and 7 m from the hex-edge lines. Each is registered with `WalkGrid.AddStaticBlocker(centre, radius, "land.peaks")` (removed and re-added on rebuild).
- Pasture: grass tufts and bushes only (the sheep are the stock patch's). Corners: a low stone disc (no posts). God View overlay: hex labels 22 m south of the centre (the token and patch stay visible) and rings at the suggested camp corners.
- `BoardSpace` adds `Corner`, `Plot`, `PlotYaw`, `PlotPath`, `HexEdgeDir`; its flag and segment members stay until F removes the fine graph.

## Testing the carriers before the real logistics

`ICarrierFeed { Count, TryGet (once per tick: pose, load, shipment), Position (per frame) }`. `SimCarrierFeed` is the default. `LandViewTests.FakeDriver_CarrierAndPorter_FollowFeed_WithLoads` sets a fake feed with a carrier on a plot path, a porter on a hex edge and an idle carrier, checks the views sit on the feed positions within 0.05 m, the loads and porter basket, the idle spot, and feeds `CarrierLegStarted`, `WarePickedUp`, `WareDelivered` through `Inject`. With the real logistics nothing changes in the views: they read the same helper and fields (`Carrier.State`, `HasLoad`, `Load`, `LoadCount`, `LegCorners`, `JobId`, `Job.IsShipment`).

## Tests (`Tests/PlayMode/LandViewTests.cs`)

Views per state item for both peoples (camps, plots, buildings, roads, carriers, warriors' lodges, markers on their targets); stock stages equal `HexStockView` after rolls (all three young stages seen, mined hexes draw nothing young and show the ready flag); a harvest takes one visible unit and the gatherer goes out; building site to finished building with builder; road site to built; a demon on a camp, the held look, no collection while held, banish clears it; warning shows the war camp, overrun burns the loser; rebuild equals incremental; only changed or growing hexes are recomputed; the fake carrier feed; inspect sentences. `LandLateGame` (in the same file) grows both peoples with the direct build commands of 5.1 (and, for stress fixtures only, tops up storehouses and fills hexes to the cap).

## Integration notes (13 Nov 2026, `pre-integrated-1`)

- The terrain honours `CampLayout.Clearings` (dance circle, tents and props, Mira's duel ground, the spawn view) through `LandViews.SyncClearings`.
- The top proposal's beam is slim and soft and hidden within 22 m of the viewer (`ProposalMarkerView.PillarHideMetres`, `PillarShown`).
- Carriers, porters, gatherers, builders and warriors wear hides and furs in their people's colour; a figure the eye is inside of (within 1.1 m) is not drawn.
- `IntegrationTests.CarrierViews_FollowTheRealSim_PositionsLoadsAndPorters` checks the carrier views against the real logistics (one view per carrier, on the sim's position within 5 cm, load shown, porter look on main-road legs).
