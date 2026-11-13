# Game API (World.Game, from Milestone 1)

For agents building views, encounters and panels on the walkable village. Namespace `World.Game` unless noted. All scene content is built in code from `WorldBootstrap.Awake`; add your own builder with one call there (or from a component you add in it). Never write `WorldState`.

## WorldBootstrap (`Boot/WorldBootstrap.cs`)
`Instance`; M0: `Sim`, `Clock`, `MainCamera` (the first-person camera, parented to the avatar head), `Sun`, `Ground`. M1: `Layout` (VillageLayout), `Avatar`, `GodCam`, `Views`, `Picker`, `Warehouse`, `Hud`, `AutoPlay`, `Encounters` (EncounterHost), `ActorsRoot`. Map rework: `Outpost` (BuildingInfo of the rival outpost), `GroundSize` (6,000 m sea plane at `SeaY` -0.3).

## Board coordinates (`Board/BoardSpace.cs`)
Board metres = Unity metres (X east, Z north), ground flat at `BoardSpace.GroundY` (0). Radius-4 board (61 hexes, about 950 m across): "vertex" = FLAG (570, 20 m apart; ids below `CornerCount` are the 150 hex corners), "edge" = road SEGMENT (630 x 20 m); coarse hex edges (210 x 60 m) join corners. See `Docs/CONTRACTS.md` (Board).
- `Vertex(v)` (any flag), `Hex(h)`, `EdgeMid(e)` (segment), `EdgeEnds(e, out a, out b)`, `EdgeYaw(e)`, `HexEdgeMid(topo, he)`, `HexEdgeEnds(topo, he, out a, out b)`, `HexCorner(topo, h, k, scale)` (exact, from `HexCorners`; scale lerps to the centre). `HexSize` 60, `SegmentLength` 20.
- Reverse: `NearestVertex(p, maxDist)` (flag), `NearestCorner(p, maxDist)`, `NearestEdge(p, maxDist)` (segment; scans the 210 hex edges, then that edge's 3 segments), `NearestHexEdge(p, maxDist)`, `HexAt(p)` (all return -1 for none). `DistanceToSegment`, `Flat`, `YawOf(dir)` / `DirOf(yaw)` (0 = +Z, 90 = +X). `BoardRadius` (outermost corner, about 476 m, cached), `IslandRadius` (+70 m: wooded rim and beach; sea beyond).

## Views (`Player/`)
- `ViewSwitcher.Instance`: `CurrentView` (`ViewMode.FirstPerson|God`), `Toggle()`, `SetView(mode)`, `event ViewChanged`, `ActiveCamera`, `AddGodViewOnly(go)` (shown only in God View), `TabEnabled`. It owns the cursor lock. Fog is off in God View.
- `FirstPersonAvatar`: `Position` (feet), `EyePosition`, `Yaw`, `Pitch`, `Teleport(pos, yaw, pitch)`, `SetLook`, `LookAt(point)`, `SetScriptedMove(worldDir, run)` (next frame only), `SetControlEnabled(bool)`. Named locks: `AddLock/RemoveLock(reason)` (move+look), `AddMoveLock`, `AddLookLock`; `CanMove`, `CanLook`. Used reasons: `GodView`, `Encounter`, `Dialogue`, `Menu`, `External`. Walk 5 m/s, run 8.5.
- `GodCamera`: orthographic, pitch 62, camera 900 m back; `Camera`, `Focus`, `Size` (ortho half-height, clamped to `MinSize` 12 .. `MaxSize`), `SetFocus(point, size)`, `FrameHome()` (`HomeFocus` = village centre at `DefaultSize` 115: village, its plots and the neighbouring hexes), `FrameIsland()` (`MaxSize` = `SizeToFrame(IslandRadius)`, about 495: whole island), `KeyboardPan` (turn off while you read keys; it also gates the H = home and M = whole-island keys). Wheel zooms about the mouse point. Ground labels in `ScaledLabels` grow with zoom-out (up to x3.2); NPC name plates show only up to `NameplateMaxSize` 60.

## Picking
- `IInspectable { string Title; IEnumerable<string> Lines; }`, `IInteractable { string Prompt; void Interact(); }`: implement on any component; the Picker uses `GetComponentInParent` on the hit collider (triggers count).
- `Picker.Instance`: `Selected`, `Hovered`, `Target`, `Select(x)`, `ClearSelection()`, `InteractWithTarget()`, `event SelectionChanged`, `InputEnabled`. First person: crosshair ray, 4 m; E interacts, click selects. God View: mouse ray; click selects (or clears). Selection survives Tab. `InspectPanel` draws `Selected` in both views (`InspectPanel.ContainsMouse()` for your click guards).
- `BoardPicker` (God View tools): `VertexUnderMouse()` (flag), `EdgeUnderMouse()` (segment), `CornerUnderMouse()`, `HexEdgeUnderMouse()`, `HexUnderMouse()`, `VertexAt/EdgeAt/CornerAt/HexEdgeAt/HexAt(screenPoint)` (`FlagAt`/`SegmentAt` are aliases), `GroundPointUnderMouse(out p)`. Radii: flag 5 m, segment 3 m, corner 10 m, hex edge 6 m. City and hero tools should pick corners (`CornerAt`); buildings and roads flags and segments.
- `HexView` (per hex tile, inspectable), `BuildingInfo` (`Kind`, `DisplayName`, `Vertex`, `StaticLines` or `LinesProvider` to show live stock). `VillageBuilder.Storehouse`, `.Market`, `.Cottages` (BuildingInfo), `.Bell` (`TownBell`, `static event Rung`). `OutpostBuilder.Outpost` (= `WorldBootstrap.Outpost`, "Rival outpost", `Vertex` = `Board.RivalOutpostVertex`). `GodViewMarker.Create(parent, text, world, colour)`: screen-space landmark name drawn only in God View.

## People (`Actors/`)
- `ActorFactory.Create(id, colour[, pos, yaw])` returns `Bedsit.ICharacterActor` (today `PlaceholderActor`; the only creation point, swap for Sims here). One-shots: `ActorKeys.Wave/Greet/Cheer/Cast/Clap/Surprise/Stretch`; `FaceTowards`, `SetVisible`, `PlayIdle`.
- `Npc`: `Id`, `DisplayName`, `Role`, `Greeting`, `Lines` (List), `Options` (List of `DialogueOption`, max 3 shown), `DialogueBuilder` (Action<Npc>, runs before each open), `Stage` (`EncounterStage{Position,Yaw}`), `Actor`, `IsGreetingShown`, `PlaceAt(pos, yaw)`. `Npc.Find(id)`, `Npc.All`. Greets and faces the player within 4 m.
- `DialoguePanel`: `Open(npc)` or `Open(speaker, lines, options, npc)`, `Close()`, `Choose(i)`, `IsOpen`, `event Opened/Closed`. Keys 1-3 or click; Esc closes (Hud). Holds the avatar, does not pause the clock.
- `DialogueOption.Do(label, action)`, `.Encounter(label, encounterId)` (shows "(Not available yet)" until registered), `.Leave(label)`.

| Id (`NpcIds`) | Name | Where | Options now |
|---|---|---|---|
| `mira` | Mira | north side of the square, 7 m ahead-left of spawn, faces spawn | `ward_mira`, Leave |
| `doorman` | Juno | on the path 5 m in front of the warehouse door | Head inside (sets objective), Leave |
| `host` | DJ Sol | behind the decks, `Warehouse.DecksPosition`; Stage = dance floor facing decks | `rhythm_party`, Leave |
| `quartermaster` | Quartermaster Bram | in front of the storehouse | Leave (wire building/roads) |
| `market_keeper` | Hesta | behind the market counter | Leave (wire trade) |
| `captain` | Captain Orla | south-east edge of the square | Leave (wire units) |
| `scribe` | Scribe Pell | south-west edge of the square | Leave (wire cards/wonder) |

## Encounters (`Encounters/EncounterHost.cs`)
- `IEncounter { Title; Begin(EncounterContext); Tick(unscaledDt); DrawGui(); IsFinished; End(); }`. Read your own keys in Tick; draw with UiStyle in DrawGui.
- `EncounterDirectory.Register(id, () => new MyEncounter())`, `TryStart(id, npc)`, `IsRegistered`. Ids: `EncounterDirectory.WardMira` = "ward_mira", `.RhythmParty` = "rhythm_party". Register early, e.g. `[RuntimeInitializeOnLoadMethod]`.
- `EncounterHost.Instance`: `Start(enc, ctx = null)`, `Abort()`, `IsRunning`, `Current`, `Context`, `event Started`, `event Ended(enc, aborted)`. Start pauses the clock (`PauseReasons.Encounter`), saves the avatar position/yaw/pitch, move-locks the avatar, hides HUD prompts and NPC labels. On finish/abort: `End()`, exact avatar restore, unlock, resume. Tab still switches the camera.
- `EncounterContext`: `World`, `Sim`, `Clock`, `Avatar`, `Views`, `FirstPersonCamera`, `GodCamera`, `Npc`, `NpcActor`, `Warehouse`, `Stage`, `AllowMouseLook` (default false: cursor free), `Aborted`, `MoveAvatarToStage(pitch)`.
- `DebugEncounter`: test-only, finishes after three Space presses (`Press()`).

## WarehouseSet (`View/WarehouseSet.cs`)
`SetRouteOpen(Route.Bar|Route.Balcony, bool)` (removes the barrier, turns on that area's lights and neon), `IsRouteOpen`, `Barrier(r)`, `RouteLights(r)`, `RouteDoorPosition(r)`, `RouteInsidePosition(r)` (balcony deck at `BalconyHeight` 3.4), `RouteApproachPosition(r)` (hall floor in front of the route door), `RampFootPosition`, `RampHeadPosition`, `FootprintCorners()`, `DecksPosition`, `DecksYaw`, `DecksFrontPosition`, `DanceFloorPosition`, `DoorPosition`, `OutsideDoorPosition`, `PartyLightsRoot`, `DoorMusic` (AudioSource, synthesised 174 BPM loop, 3D, low-passed outside the hall), `SetDoorMusic(bool)`, `IsInsideHall(p)`. Turn door music off while your rhythm stems play. Size (map rework): hall 13 x 22 m (`HallHalfWidth` 6.5, `HallDepth` 22, `HallHeight` 7) plus a 4.75 m bar annex (`BarAnnexWidth`) = about 17.75 x 22 m; dance floor 8 x 10 m; balcony 3.5 x 11 m along the +X wall, ramp beside the door. Use the position properties, never local coordinates.

## HUD (`UI/Hud.cs`)
`Hud.SetObjective(text)`, `Hud.Objective`, `Hud.MenuOpen`, `OpenMenu()/CloseMenu()` (pauses with `PauseReasons.Menu`), `Hud.Quit(code)`. `UiStyle.DrawSolidPanel(rect)` for opaque panels.

## AutoPlay (`Boot/AutoPlay.cs`)
`new AutoPlayScript(name).Teleport(..).WalkTo(pos or Func, stop, run, timeout).LookAt(..).Turn(dYaw, s).Interact().ToggleView().Wait(s).Call(action).Check(func, msg).Capture(name).Log(line).Quit(code)`; run with `WorldBootstrap.Instance.AutoPlay.Run(script)` (a Coroutine; check `Failed`, `FailMessage`). Captures go to `Evidence/screenshots/<name>.png`. Named scripts: `AutoPlayScripts.Register(name, factory)`; the player honours `-autoplay <name>` (`boot_tour` logs `[AutoPlay] boot_tour OK` and quits 0; failure logs `FAILED` and quits 1).

## Layout (`View/VillageLayout.cs`)
`Home` (home corner H), `WarehouseVertex` (flag W, 40 m out along a hex edge), `Out` (unit H to W), `Right`, `SquareCentre` (H + 5 m Out, radius 12 m), `VillageCentre`, `Spawn`/`SpawnYaw` (on the square, Mira, Juno and the lit door within 25 m and in view), `WarehouseDoor` (H + 28 Out + 9 Right: the hall stands beside flag W in the hex to the Right and faces the square; corner H + 60 Out stays free), `Storehouse` (8 x 6 m), `Market`, `Bell`, `Cottages` (5 houses, 6 x 5 m), `Outpost`, NPC spots, `IsClear(p)` (props are kept out of a 58 m clearing round the village and a 24 m clearing round the outpost). Everything is within about 55 m of H and at least 9 m from other flags and 7 m from hex-edge lines (road plots stay free).

## Terrain (`View/TerrainBuilder.cs`, `View/OutpostBuilder.cs`)
One tile per hex (outer ring through its 18 flags, MeshCollider), props combined into one mesh per hex with no colliders except 3 convex mountain peaks per Mountain hex; props keep `RoadMargin` 4 m off the hex edges. Terrain looks: Forest about 170 trees, Hills mounds and rocks, Fields 8 m crop rows, Mountain peaks 38-58 m, Pasture (green) with a hex fence (gate), two sheep flocks and a trough, Meadow flowers and bushes. Hex-edge lines (0.5 m), a stone disc and pennant post at every corner. God View overlay (`AddGodViewOnly`): hex labels (terrain + number, raised and shifted so they read over the hex), a plot disc at every intermediate flag, larger discs at corners, faint blue rings at `Board.SuggestedCitySites`, a red ring and "Rival outpost" / "Home village" names. Island rim with trees and hills, beach, sea plane. First-person fog 140-700 m; camera far plane 1,500 m. `TerrainBuilder.PropCounts` reports what was placed.
