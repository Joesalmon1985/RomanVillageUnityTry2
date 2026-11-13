# Economy UI (road economy in both views)

How the unified road economy (14 wares, 20 building types, radius-4 board: 570 flags, 630 road segments of 20 m) is shown and played in first person and God View. Written 13 Nov 2026 (main, after the unified economy merge); replaces the feat/econview version (that branch was not merged; its shell was ported, the rest rewritten).

Code: `Assets/World/Game/View/Economy` (world views, first person, installer) and `Assets/World/Game/UI/God` (God View toolbar, tools, HUD, commission driver). Namespace `World.Game`. Installed by one line in `WorldBootstrap.Awake`: `EconomyInstaller.Install(this)`.

Rules kept: views read `WorldState.Economy` and the sim's helpers (`EconomyApi.DescribeType`, `BuildMenu`, `RecipeText`, `CostText`, `WareTitle`, `WareColour`, `ProducersOf`, `ConsumersOf`, `WaitingFor(..).Sentence`, `Describe`, `DescribeJob`, `LegalBuildFlags`, `RoadCostFor`); every action is a command through `GodCommands` -> `WorldBootstrap.Instance.Sim.Submit`. Nothing in `Assets/World/Game` writes state. Nothing scans all 570 flags or 630 segments per frame (see Performance).

## World views (`EconomyViews`)

`EconomyViews.Instance` reconciles all views with the state after every `GameClock.Ticked` (create, update, remove). `RebuildFromState()` (after `DestroyAllViews()`, or after loading a save) reproduces the scene from state alone: building plots are a pure function of flag, type and the fixed village, carriers are placed from `Carrier` fields. `Synced` fires after each reconcile with that tick's events.

| View | Shows | Inspect (`IInspectable`) |
|---|---|---|
| `BuildingView` (per building or site; not the storehouse, which is the village building) | `BuildingModels` model (one original primitive model per type, 20 in all) on its plot beside the flag; status badge (diamond: green working, blue idle, amber waiting, red blocked, grey off; `Building.Blocked` and a robbed hex count as blocked), grows with God View zoom; turning part (saw, sails, hoist wheel, grindstone) and glow (oven, furnace, ember, firebox, forge, power coil) while working; sites: marked-out footprint, scaffold, delivered materials as ware-coloured crates, progress bar (amber delivering, green building); model rises while constructing | `EconomyText.BuildingLines`: `WaitingFor` sentence, purpose, recipe, needs, materials or stock, priority, on/off, deliveries (`DescribeJob`) |
| `BuildingSign` | signpost with the name at the flag | E: selects the building and opens a dialogue with the same commands as God View |
| `FlagView` (flag with a building, site or road end) | pole with pennant; a stack of crates beside it, one column per ware held by the building there, in `EconomyApi.WareColour` | - |
| `RoadView` (per 20 m segment) | paved strip with kerbs, 2.6 m | road cost rule, carriers on it |
| `CarrierView` (per carrier; `ActorFactory` placeholder figure) | walks `FromVertex -> ToVertex` by `(Progress + tick fraction) / Tuning.TicksPerEdge`, on a lane right of the centre line (lane by id); load crate on its head in the ware colour with a name tag (first person within 30 m, God View when zoomed in or selected); God View: disc in the ware colour, crate scaled with zoom; waiting at its last flag (`CarrierWaitTicks`): stands beside that flag; idle at the storehouse: a block in front of it | state, load, job, status sentence, segment progress |
| `BatchMarker` (hex with batch units left, all five resource terrains) | banner pole in the raw ware colour, one block per unit (up to 24), 20 m south of the hex centre (clear of peaks), tag "Logs batch 12" | `EconomyText.HexLines` (also on every `HexView` tile) |
| `Beacon` | temporary light column with a bobbing arrow and caption | - |

Placement (`BuildingPlots.For(state, layout, flag, type)`): square footprint by size class (half side Small 3.2, Medium 3.8, Large 4.4, Huge 5.0 m) in one of the hexes the flag touches, at least `TerrainBuilder.RoadMargin` + 0.6 m from the road line (corners: along the bisector, distance from the 60-degree geometry), shifted at most 4 m along the road (so plots of flags 20 m apart never meet) or pushed further in to clear the village (square, storehouse, market, bell, cottages, warehouse, NPC spots). The model's front faces the road. `TerrainBuilder.SetClearings(hex, circles)` removes trees, rocks and crops from the plots (rebuilds only that hex's prop mesh; props are seeded per hex so the rest is unchanged).

## God View tools (`GodToolbar`)

Bottom toolbar (God View only): **Build [B]**, **Road [R]** (X: build / remove), **Inspect [V]**, **Problems [P]** with a count, **Wares [G]**, **End Round**. Under it the status strip: round, drawn numbers, wares in the storehouse (planks and bricks always, others when held), coin, and any registered `StatusItems`. One tool is active at a time; right click returns to Inspect. The active tool's panel fills the left column; its overlay draws over the world; button tooltips explain (End Round: the sim's refusal sentence when it would be refused).

- **Build** (`BuildTool`): the menu is `EconomyApi.BuildMenu` (name, cost green/red against `StorehouseCount`, terrain, recipe or what it makes); the selected type's `Purpose`, `NeedsText`, `MakesText`, `RecipeText`. Only `LegalBuildFlags` glow (they are in territory by definition), recomputed when buildings, cities or the type change (`Recomputes` counter). Hover: `sim.Check(PlaceBuilding)`; a refused flag shows the sim's sentence in red ("A woodcutter must stand next to a forest."); click submits. API: `SelectType`, `TryPlace(flag)`, `Check`, `IsValid`, `LegalFlags`, `ForceHover(flag)` (captures).
- **Road** (`RoadTool`): build mode highlights only the segments touching your network (road ends, buildings, sites, cities) that the sim would accept; remove mode highlights your roads. The panel and tooltip show the cost rule ("1 planks and 1 bricks from the storehouse pays for 3 segments (2 already paid for)") and what the next segment costs (`EconomyApi.RoadCostFor`). Candidates are rebuilt when roads, buildings, cities or the storehouse planks/bricks change. API: `Place(edge)`, `Remove(edge)`, `TryAt(edge)`, `SetRemoveMode`, `Candidates`.
- **Inspect**: the M1 picker and `InspectPanel` (same panel in both views) for buildings, sites, carriers, roads, hexes (batch, robber, harvesters) and batch markers. For a building or site, `EconomyHud` adds buttons below the panel: priority Low / Normal / High, Switch on / off, or Cancel site.
- **Problems** (`ProblemsTool`): every stalled site, building (including a demon on its flag or a robbed hex) and delivery with the sim's sentence (`EconomyText.Problems`, rebuilt every 0.4 s from buildings and jobs); a click focuses the God camera and selects it (`ProblemsTool.Focus`).
- **Wares** (`WaresTool`): the 14 wares with storehouse stock, the amount in the village (`WaresInWorld`), made by (`ProducersOf`) and used by (`ConsumersOf`; planks and bricks also "buildings, roads").
- **End Round** (action): `GodCommands.EndRound()`. Refusals (`round_starting`, `draft_pending`, `game_over`) are shown as the sim's sentence in a toast and in the button tooltip; nothing else happens. The round start shows a banner ("Round 3: numbers 1 2 4. Batches: Forest 12 logs, Hills 12 clay.").

Feedback (`EconomyHud`, both views): `EconomyHud.Toast(text, error)` (accepted / refused commands, refusals carry the sim's sentence), `EconomyHud.Banner(text, seconds, big)`; the commission banner on `CommissionCompleted`.

## First person

- **Quartermaster Bram** (`EconomyFirstPerson` sets his `DialogueBuilder`; `VillageNpcs` keeps only fallback lines): the commission (the six buildings, the ones already working in green, 10 planks and 10 bricks), progress "buildings x/6, planks x/10, bricks x/10", and the next step (`NextStep`: plan the next missing building and where, else the stalled building's `WaitingFor` sentence, else wait / ring the bell). Options: "Show me where" (a `Beacon` on the spot for 45 s), "Open the building plans" (God View, Build tool, that type preselected), Leave. On `CommissionCompleted` he cheers, a beacon marks him and the objective says to talk to him; his dialogue then acknowledges it (`QuartermasterAcknowledged`).
- **Town bell** (E): `GodCommands.EndRound()`, the same refusals as the button.
- **Signs**: E selects the building and offers priority / switch / cancel. Clicking a building, site or carrier within reach shows the same inspect panel as God View.
- **Objective**: after the first talk to Bram or economy command, the HUD objective follows the commission ("Commission: buildings 3/6, planks 7/10, bricks 4/10 (Tab: God View)"); it refreshes only while nobody else has replaced the text.

## Extension points for later agents

### A God View tool, tab or panel (strategy, military)

```csharp
GodToolbar.Register(new GodTool
{
    Id = "cards", Label = "Cards", Order = 65, Hotkey = KeyCode.C,
    DrawPanel = (tool, area) => { /* IMGUI with UiStyle inside area */ },
    BadgeText = () => StrategyApi.DraftPending(s) ? "(draft)" : null,
    TipText = () => "Draft, build or discard development cards.",
});
GodToolbar.StatusItems.Add(() => "Age <b>" + StrategyApi.Status(s).Age + "</b>  VP <b>" + ... + "</b>");
```

- `GodTool`: `Id` (unique; registering the same id replaces), `Label`, `Hotkey`, `Order` (built-ins: Build 10, Road 20, Inspect 40, Problems 50, Wares 60, End Round 90; free gaps for Market 62, Cards 65, City 70, Hero 72, Military 75, Squads 78, Wonder 80), `IsAction` + `OnRun` (one-shot button), `CapturesClicks` + `OnWorldClick` (placement tools: clicks go to the tool, not to selection), `PanelWidth`, `PanelHeight(width)`, `IsAvailable` (greys the button), `TipText`, hooks `OnActivate`, `OnDeactivate`, `OnUpdate`, `DrawPanel`, `DrawOverlay`. Or subclass and override `Activate`, `Deactivate`, `Update`, `WorldClick`, `Panel`, `Overlay`, `Run` (see `BuildTool`, `RoadTool`, `ProblemsTool`, `WaresTool`).
- Register any time (from your own installer called next to `EconomyInstaller.Install`, or `[RuntimeInitializeOnLoadMethod]`). `GodToolbar.Instance.Activate(id)` opens a tool from code (e.g. an NPC option); `EconomyGodTools.OpenBuildPlans(type)`.
- Helpers: `GodCommands.Submit(command, okText)` (stamps the human player, submits, toasts the result with the sim's sentence; `Check` for previews; `Submitted` event; `LastResult`), `GodToolbar.AddUiRect(rect)` (clicks there never reach the world or the picker), `GodToolbar.Tooltip(text[, guiPoint])`, `GodToolbar.MouseOverUi()`, `GodToolbar.PanelTop/PanelBottom/BarTop`, `GodOverlay.ShowVertices(items)` / `ShowEdges(items)` / `Hide()` (sparse: pass only the ids to show, e.g. `StrategyApi.LegalCitySites` corners or `HeroMoveTargets`; rebuild the list only when state changes), `BoardPicker.CornerUnderMouse()` for city and hero tools (corners only), `VertexUnderMouse()` / `EdgeUnderMouse()` for flags and segments.
- The draft needs a panel that pauses the clock with `PauseReasons.Draft` while `StrategyApi.DraftPending`; until it exists, End Round is refused with `draft_pending` (shown as the sentence) once Age I starts.
- Inspect panels for new things: implement `IInspectable` on a component with a collider (triggers count), or `IEconomyBuilding` (adds the priority / switch / cancel buttons) for economy buildings; forward parts with `InspectForward`.

### Views for cities, hero, demons and squads

Follow the `EconomyViews` pattern: one reconciler per module subscribed to `GameClock.Ticked`, dictionaries keyed by id, create / update / remove from state every tick, a `RebuildFromState()` that works from state alone, and no per-frame scans of the board. Suggested:

- **Cities**: a reconciler over `Economy.Cities` (owner human, corner flags), a town model on the corner (corners are free of building plots: plots sit inside hexes) and its 80 m territory ring in God View (`GodViewMarker` / unlit ring like `TerrainBuilder.Ring`).
- **Hero**: one figure from `ActorFactory` at `Strategy.HeroVertex`, walking the hex edge (3 segments) after `HeroMoved`; highlight `HeroMoveTargets` with `GodOverlay.ShowVertices`.
- **Demons**: per flag with `Strategy.Demons[flag] > 0`, a marker with one figure per demon (cap 3; the 4th outbreaks); `Building.Blocked` already turns the building badge red and `HexStopped` marks robbed harvesters; `OutbreakOccurred` is an event to animate.
- **Squads**: per `Military.Squads` entry, figures interpolated like carriers using `MilitaryTuning.TicksPerEdge` (20 per segment); garrison at `Military.Outpost`.
- Labels: draw IMGUI tags in your own `OnGUI` with `GUI.depth = 10` (under panels), cull by distance in first person and by `GodCam.Size` in God View, and avoid overlaps like `EconomyViews.Place`.

## AutoPlay, tests, captures

- Named script `commission` (`CommissionDriver.Script`): God View; the Build and Road tools and the End Round button (the same calls a click makes, one command per tick when nothing is pending, rules of the headless EditMode `CommissionScript`); the normal clock is paused and the sim advanced 6 ticks per frame with `GameClock.StepOnce`; then checks the views, walks to Bram, checks his acknowledgement, logs `[AutoPlay] commission OK`. In a player, `-autoplay commission` quits with 0 (1 on failure).
- PlayMode `EconomyViewTests` (8): views per building / road / carrier and batch markers from state; every model inside its footprint and distinct, every plot in territory off the road lines and clear of the village; `RebuildFromState` reproduces counts and positions, no road rescans without road events; a loaded carrier mid-road, coloured crate, interpolated monotonically within the road; refusals with the sim's sentences (wrong terrain, unconnected road, occupied flag), highlights are exactly `LegalBuildFlags` and are cached; removing the road under a shipment shows `no_road` in carrier, site and Problems, rebuilding delivers once; Bram's progress, beacon and building plans, the bell, the sign; the commission through the script, then the `draft_pending` refusal of the bell and the button.
- `AgentScripts/EconCaptures.cs` (run with `unity command script run --file AgentScripts/EconCaptures.cs --entry EconCaptures.<Entry>`): capture states (`GodHome`, `FirstPersonCarrier`, `GodCarrier`, `BuildMenu`, `StalledSite`, `ModelGallery`, `Problems`, `Wares`, `TalkToBram`), `CommissionUntil(tick)`, `LateGame` (a commands-only driver to about 32 buildings and 50+ carriers), `MeasurePerf`.

## Performance

- Per tick (10 Hz): buildings, carriers and the 61 hexes are walked; the 630 road flags are rescanned only after `RoadPlaced` / `RoadRemoved` or a rebuild (`RoadScans`); flags and prop clearings are recomputed only when buildings or roads change.
- Per frame: carrier interpolation, badge and crate scale, labels (repaint only, culled). Picking (`BoardSpace.NearestVertex/NearestEdge/NearestCorner/NearestHexEdge` with radius up to 15 m) looks only at the hex under the point. The Build and Road overlays are rebuilt only on state change or hover change.
- Measured 13 Nov 2026, editor, 1920x1080 Game view, vSync on (`EconCaptures.LateGame` then `MeasurePerf`): 32 buildings and 66 carriers: God View home 129 fps, close 140, whole island 159, first person in the square 243; a second run with 26 buildings and 58 carriers (after the final fixes): 215, 223, 228 and 329 fps.

## Needed from the sim (not public today)

- `EconomyLogic.CommissionTypes` is internal: `EconomyLook.CommissionTypes` mirrors the six types. A public `EconomyApi.CommissionTypes` (or a `CommissionProgress` helper) would remove the copy.
- `EconomyLogic.CityAt` is internal (views use `Economy.Cities` directly).

## Known limits

- Labels and load tags are IMGUI; busy God View shots stack them upward, which can drift away from their buildings.
- Idle carriers stand in a block in front of the storehouse; with 60+ carriers it spills into the square.
- A plot that has to be pushed away from the village can stand 10-20 m from its flag; the sign at the flag says which building it is.
- No worker figures at the buildings (carriers are the only economy figures); work is shown by turning parts, glows and the badge.
- Rendering of the models is untextured vertex colour; nobody has played it by hand.
