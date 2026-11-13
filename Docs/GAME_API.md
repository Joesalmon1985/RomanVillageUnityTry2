# Game API (World.Game, from Milestone 1)

For agents building views, encounters and panels on the walkable village. Namespace `World.Game` unless noted. All scene content is built in code from `WorldBootstrap.Awake`; add your own builder with one call there (or from a component you add in it). Never write `WorldState`.

## WorldBootstrap (`Boot/WorldBootstrap.cs`)
`Instance`; M0: `Sim`, `Clock`, `MainCamera` (the first-person camera, parented to the avatar head), `Sun`, `Ground`. M1: `Layout` (VillageLayout), `Avatar`, `GodCam`, `Views`, `Picker`, `Warehouse`, `Hud`, `AutoPlay`, `Encounters` (EncounterHost), `ActorsRoot`.

## Board coordinates (`Board/BoardSpace.cs`)
Board metres = Unity metres (X east, Z north), ground flat at `BoardSpace.GroundY` (0).
- `Vertex(v)`, `Hex(h)`, `EdgeMid(e)`, `EdgeEnds(e, out a, out b)`, `EdgeYaw(e)`, `HexCorner(topo, h, k)`.
- Reverse: `NearestVertex(p, maxDist)`, `NearestEdge(p, maxDist)`, `HexAt(p)` (all return -1 for none). `YawOf(dir)` / `DirOf(yaw)` (0 = +Z, 90 = +X). `BoardRadius`.

## Views (`Player/`)
- `ViewSwitcher.Instance`: `CurrentView` (`ViewMode.FirstPerson|God`), `Toggle()`, `SetView(mode)`, `event ViewChanged`, `ActiveCamera`, `AddGodViewOnly(go)` (shown only in God View), `TabEnabled`. It owns the cursor lock. Fog is off in God View.
- `FirstPersonAvatar`: `Position` (feet), `EyePosition`, `Yaw`, `Pitch`, `Teleport(pos, yaw, pitch)`, `SetLook`, `LookAt(point)`, `SetScriptedMove(worldDir, run)` (next frame only), `SetControlEnabled(bool)`. Named locks: `AddLock/RemoveLock(reason)` (move+look), `AddMoveLock`, `AddLookLock`; `CanMove`, `CanLook`. Used reasons: `GodView`, `Encounter`, `Dialogue`, `Menu`, `External`. Walk 5 m/s, run 8.5.
- `GodCamera`: orthographic, pitch 62; `Camera`, `Focus`, `Size`, `SetFocus(point, size)`, `KeyboardPan` (turn off while you read keys).

## Picking
- `IInspectable { string Title; IEnumerable<string> Lines; }`, `IInteractable { string Prompt; void Interact(); }`: implement on any component; the Picker uses `GetComponentInParent` on the hit collider (triggers count).
- `Picker.Instance`: `Selected`, `Hovered`, `Target`, `Select(x)`, `ClearSelection()`, `InteractWithTarget()`, `event SelectionChanged`, `InputEnabled`. First person: crosshair ray, 4 m; E interacts, click selects. God View: mouse ray; click selects (or clears). Selection survives Tab. `InspectPanel` draws `Selected` in both views (`InspectPanel.ContainsMouse()` for your click guards).
- `BoardPicker` (God View tools): `VertexUnderMouse()`, `EdgeUnderMouse()`, `HexUnderMouse()`, `VertexAt/EdgeAt/HexAt(screenPoint)`, `GroundPointUnderMouse(out p)`. Radii: vertex 5 m, edge 3 m.
- `HexView` (per hex tile, inspectable), `BuildingInfo` (`Kind`, `DisplayName`, `Vertex`, `StaticLines` or `LinesProvider` to show live stock). `VillageBuilder.Storehouse`, `.Market` (BuildingInfo), `.Bell` (`TownBell`, `static event Rung`).

## People (`Actors/`)
- `ActorFactory.Create(id, colour[, pos, yaw])` returns `Bedsit.ICharacterActor` (today `PlaceholderActor`; the only creation point, swap for Sims here). One-shots: `ActorKeys.Wave/Greet/Cheer/Cast/Clap/Surprise/Stretch`; `FaceTowards`, `SetVisible`, `PlayIdle`.
- `Npc`: `Id`, `DisplayName`, `Role`, `Greeting`, `Lines` (List), `Options` (List of `DialogueOption`, max 3 shown), `DialogueBuilder` (Action<Npc>, runs before each open), `Stage` (`EncounterStage{Position,Yaw}`), `Actor`, `IsGreetingShown`, `PlaceAt(pos, yaw)`. `Npc.Find(id)`, `Npc.All`. Greets and faces the player within 4 m.
- `DialoguePanel`: `Open(npc)` or `Open(speaker, lines, options, npc)`, `Close()`, `Choose(i)`, `IsOpen`, `event Opened/Closed`. Keys 1-3 or click; Esc closes (Hud). Holds the avatar, does not pause the clock.
- `DialogueOption.Do(label, action)`, `.Encounter(label, encounterId)` (shows "(Not available yet)" until registered), `.Leave(label)`.

| Id (`NpcIds`) | Name | Where | Options now |
|---|---|---|---|
| `mira` | Mira | square, 6 m left of centre, faces spawn | `ward_mira`, Leave |
| `doorman` | Juno | outside the warehouse door (5 m out, 2.5 m right) | Head inside (sets objective), Leave |
| `host` | DJ Sol | behind the decks, `Warehouse.DecksPosition`; Stage = dance floor facing decks | `rhythm_party`, Leave |
| `quartermaster` | Quartermaster Bram | in front of the storehouse | Leave (wire building/roads) |
| `market_keeper` | Hesta | behind the market counter | Leave (wire trade) |
| `captain` | Captain Orla | back right of the square | Leave (wire units) |
| `scribe` | Scribe Pell | back left of the square | Leave (wire cards/wonder) |

## Encounters (`Encounters/EncounterHost.cs`)
- `IEncounter { Title; Begin(EncounterContext); Tick(unscaledDt); DrawGui(); IsFinished; End(); }`. Read your own keys in Tick; draw with UiStyle in DrawGui.
- `EncounterDirectory.Register(id, () => new MyEncounter())`, `TryStart(id, npc)`, `IsRegistered`. Ids: `EncounterDirectory.WardMira` = "ward_mira", `.RhythmParty` = "rhythm_party". Register early, e.g. `[RuntimeInitializeOnLoadMethod]`.
- `EncounterHost.Instance`: `Start(enc, ctx = null)`, `Abort()`, `IsRunning`, `Current`, `Context`, `event Started`, `event Ended(enc, aborted)`. Start pauses the clock (`PauseReasons.Encounter`), saves the avatar position/yaw/pitch, move-locks the avatar, hides HUD prompts and NPC labels. On finish/abort: `End()`, exact avatar restore, unlock, resume. Tab still switches the camera.
- `EncounterContext`: `World`, `Sim`, `Clock`, `Avatar`, `Views`, `FirstPersonCamera`, `GodCamera`, `Npc`, `NpcActor`, `Warehouse`, `Stage`, `AllowMouseLook` (default false: cursor free), `Aborted`, `MoveAvatarToStage(pitch)`.
- `DebugEncounter`: test-only, finishes after three Space presses (`Press()`).

## WarehouseSet (`View/WarehouseSet.cs`)
`SetRouteOpen(Route.Bar|Route.Balcony, bool)` (removes the barrier, turns on that area's lights and neon), `IsRouteOpen`, `Barrier(r)`, `RouteLights(r)`, `RouteDoorPosition(r)`, `RouteInsidePosition(r)` (balcony deck is at y 3.6), `DecksPosition`, `DecksYaw`, `DecksFrontPosition`, `DanceFloorPosition`, `DoorPosition`, `OutsideDoorPosition`, `PartyLightsRoot`, `DoorMusic` (AudioSource, synthesised 174 BPM loop, 3D, low-passed outside the hall), `SetDoorMusic(bool)`, `IsInsideHall(p)`. Turn door music off while your rhythm stems play.

## HUD (`UI/Hud.cs`)
`Hud.SetObjective(text)`, `Hud.Objective`, `Hud.MenuOpen`, `OpenMenu()/CloseMenu()` (pauses with `PauseReasons.Menu`), `Hud.Quit(code)`. `UiStyle.DrawSolidPanel(rect)` for opaque panels.

## AutoPlay (`Boot/AutoPlay.cs`)
`new AutoPlayScript(name).Teleport(..).WalkTo(pos or Func, stop, run, timeout).LookAt(..).Turn(dYaw, s).Interact().ToggleView().Wait(s).Call(action).Check(func, msg).Capture(name).Log(line).Quit(code)`; run with `WorldBootstrap.Instance.AutoPlay.Run(script)` (a Coroutine; check `Failed`, `FailMessage`). Captures go to `Evidence/screenshots/<name>.png`. Named scripts: `AutoPlayScripts.Register(name, factory)`; the player honours `-autoplay <name>` (`boot_tour` logs `[AutoPlay] boot_tour OK` and quits 0; failure logs `FAILED` and quits 1).

## Layout (`View/VillageLayout.cs`)
`Home` (home vertex, square centre, radius 15 m), `WarehouseVertex`, `Out` (unit H to W), `Right`, `Spawn`/`SpawnYaw`, `WarehouseDoor` (W + 2 m along Out), `Storehouse`, `Market`, `Bell`, NPC spots, `IsClear(p)` (props are kept out of the village footprint).
