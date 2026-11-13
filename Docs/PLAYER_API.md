# Player API (package P)

Movement on the walking lattice, the free pointer, speech bubbles, casting, the Tab map, the H HUD strip and the F1 developer tools (REFACTOR_SPEC 2.D, 8.2, appendix B). Namespace `World.Game`. Nothing here writes `WorldState`: casts and the developer tools submit sim commands, everything else reads state.

## Keys and touch

| Action | Keys | Touch |
|---|---|---|
| Forward | W, Up, Numpad 8 | strip 2 |
| Step back | S, Down, Numpad 2, Numpad 5 | strip 3 |
| Turn left | A, Left, Numpad 4, Numpad 7 | strip 1 |
| Turn right | D, Right, Numpad 6, Numpad 9 | strip 4 |
| Reply 1-3 | 1, 2, 3 (nearest open question) | tap the reply |
| Map | Tab (Esc also closes) | - |
| HUD strip | H | - |
| Developer tools | F1 | - |
| Pause menu | Esc | - |
| Stop auto-walk / follow | any move key, right click | any strip |

The cursor is always free; there is no mouse look. Touch strips read real touches only (a mouse click anywhere is a pointer tap, because the near ground where you press to cast sits in the lower screen).

## Lattice and walking

- `WalkLattice` (static): `Step` 5; `Cell {I, J}` (with `==`, `+`, `-`, `* int`); `ToWorld(Cell)`, `ToCell(Vector3)`, `Dir(h)`, `Yaw(h)`, `HeadingOf(yaw)`. Headings 0..5, yaw 60 h (0 north, clockwise). Every corner, hex centre and plot centre is a cell (tested to 1 mm).
- `WalkGrid` (static): `IsBlocked(cell)`, `WhyBlocked(cell)` (`"edge"`, `"camp"`, `"building"`, or a static blocker's owner), `AddStaticBlocker(centre, radius, owner)`, `RemoveStaticBlockers(owner)`, `FindPath(from, to, maxNodes)` (A*, list from exclusive to inclusive; empty when equal; null when none or the goal is blocked), `NearestFree(cell, maxRing)`, `Distance(a, b)` (steps), `Invalidate()`. Blocked: beyond `BoardRadius + 4`; within 3 m of a finished camp's corner; within 5 m of a plot whose building is constructing, active or retiring; inside a static blocker. "Within" is strict, so a camp or building blocks exactly its own cell. People, trees, crops, roads, hex-edge lines, empty plots and sites never block. State blockers are cached per tick.
- `PlayerWalker` (MonoBehaviour, `Instance`, also `WorldBootstrap.Avatar` / `.Walker`): `Cell`, `Heading`, `Position` (feet), `EyePosition`, `Yaw`, `Pitch` (Unity sign: `FixedPitch` 8 = the spec's -8 degrees), `IsGliding`, `IsAutoWalking`, `AutoGoal`, `Following`, `IsOffLattice`, `IsLocked`, `CanMove`; `Forward()`, `Back()`, `TurnLeft()`, `TurnRight()`, `PressStrip(1..4)`; `WalkTo(cell)` (false if no path), `Follow(IFollowable)`, `StopAuto()`; `Teleport(cell, heading)`; `PlaceExact(pos, yaw, pitch)` + `RestoreCell()` (encounters); `AddLock/RemoveLock/HasLock(reason)`; events `Stepped`, `Bumped(cell)`, `AutoWalkEnded(arrived)`; counters `StepCount`, `BumpCount`. Constants: `StepSeconds` 0.4 (ease in-out), `TurnSeconds` 0.25, `BumpSeconds` 0.2, `FieldOfView` 70, `FollowCells` 2. Holding a key repeats; one further input is buffered (a newer one replaces it). Ground height follows the hex tiles (`GroundHeight(p)`). Follow walks to the cell two steps behind the person's direction of travel, re-plans when that cell moves more than one cell, and snaps to the exact cell once the person has stood still for 0.3 s. Auto-walk turns take 0.25-0.45 s depending on the angle.
- Compatibility for older callers: `Teleport(Vector3 feet, yaw, pitch)` is an exact pose (nearest cell and heading become the lattice pose; the next step glides back on), `AddMoveLock/AddLookLock`, `CanLook`, `Camera`, `Head`, `Marker`, `LookDirection`.
- Measured: 12 keyed steps along a hex edge (one corner to the next) take 4.8 s; the auto-walk from beside the home camp to the neighbouring corner (11 steps) takes 4.4 s.

## Pointer

- `PointerInput` (static): `Tapped` (every world tap, after the default handling), `Hovered`, `InputEnabled`, `Selected` (F1 God View selection), `LastTap` (`TapOutcome`), `LastHit`; `Raycast(screen)` -> `PointerHit {Ground, HasGround, Target, ProposalId}`; `TapAt(screen)` (exactly a click), `TapObject(component)`, `TapGround(point)`, `Tap(hit)`, `TargetOf(collider)`, `ProposalAt(point)`, `OfferFollow(who, component)`.
- Tap order: a bubble or reply; a person (an `IInteractable` talks, e.g. Mira's duel offer; any other `IFollowable`, e.g. a carrier, gets an inspect label with a "Follow" reply); the ground of your people's open proposal (cast); anything `IInspectable` (hex tiles included): a small world-anchored label with its title and up to four lines. A walking villager's dialogue also offers "Follow" when it has room. Right click stops auto-walk. Hover draws a pulsing ring (yellow people, white things, green "press to cast", orange "walk closer to cast") and the title by the cursor.
- `PointerController` (component) reads mouse and touches; in the F1 God View a click with the Select tool sets `PointerInput.Selected` (shown by `InspectPanel`).

## Speech bubbles

`SpeechBubbles` (static): `Say(who, text, seconds)`, `Ask(who, text, replies, onReply, timeoutSeconds)` (reply 0-based, -1 on timeout or when the speaker is destroyed; timeout <= 0 = none), `Close(h)`, `IsOpen(h)`; additions `SayAt(point, text, seconds)`, `Inspect(anchor, point, title, lines, seconds, replies, onReply)` (one at a time), `Choose(h, i)`, `AnswerNearest(i)`, `NearestAsk(min)`, `Expand(h)`, `IsShowing(h)`, `TextOf(h)`, `TitleOf(h)`, `RepliesOf(h)`, `OpenHandles()`, `ShownCount`, `InspectHandle`, `ScreenRectOf(h)`, `ReplyRectOf(h, i)`, `HandleTap(gui)`, `Contains(gui)`, event `Replied(h, i)`. One queue per speaker (the first open bubble shows; a Say's seconds and an Ask's timeout start when it shows). Bubbles keep their screen size (0.78-1x with distance); Says beyond 90 m are not drawn; a shown Ask off screen draws "<name> calls you" with an arrow at the screen edge. Replies fold to "tap to answer" beyond 20 m until tapped. `Say(null, ...)` / `Ask(null, ...)` hang 6 m in front of the player. Hidden during encounters, the map, the pause menu and the God View.

`DialoguePanel` keeps its old surface (`Open(npc)`, `Open(speaker, lines, options, npc)`, `Close()`, `Choose(i)`, `IsOpen`, `Opened/Closed`, `CurrentOptions`) and now shows the NPC's lines as an Ask bubble over its head (`DialoguePanel.Handle`). It never locks the player or pauses the clock; walking 30 m away closes it.

## Casting

`SpellCaster` (static): `TryCastAt(ground)` (your open proposal there, range check against `FactionApi.CastRangeMetres` from the player's feet to the proposal's target point, then `CastSpell`); `TryCast(id, pressedAt)`; `CastAnywhere(id)` (F1 tools: either people, no range check); `InRange(id)`, `DistanceTo(id)`, `LastResult`, `LastProposal`, event `CastAttempted(id, result)`. Codes: `no_proposal` (nothing said), `too_far` ("Walk closer to cast.", said in a bubble at the pressed point), or the sim's refusal sentence. A cast plays `SpellEffect` (light pillar, ground ring, sparks, point light; 1.8 s; `Played`, `Active`).

## Map, HUD, menu

- `MapScreen` (static): `IsOpen`, `Open()`, `Close()` (starts the auto-walk to the picked target; "There is no way there." if none), `Toggle()`, `Target`, `SetTarget(world)`, `ClearTarget()`, event `Toggled`. `MapScreenView.Instance`: `MapRect`, `TapScreen(screen)`, `WorldToGui`, `GuiToWorld`. Shows terrain hexes and numbers (6 and 8 red), camps (yours blue, the Ash Clan red, cities ringed, sites hollow), roads and road sites, demons with counts, your proposals (the top one labelled "The prince wants you here"), the prince (`PrinceDirector.Instance.Prince`) and you. Read only, no commands.
- `Hud`: `Visible` (H, off at the start), `StripLines(state)`, `NearestCamp(state, from)`, `Notice(text, error, seconds)`, `MenuOpen`, `OpenMenu()/CloseMenu()`, `SetObjective` (shown under the strip only when set to something else than the default). The strip: game time and the last roll, both strengths, the warning countdown or the ending, the stock of your nearest camp, your proposal queue with speakers and distances.

## F1 developer tools

`ViewSwitcher` (F1, `DevKey`): `CurrentView` (`FirstPerson|God`), `Toggle()`, `SetView`, `ViewChanged`, `ActiveCamera`, `AddGodViewOnly(go)`, `Avatar` (the walker), `God`. The walker holds the `GodView` lock while it shows. Toolbar (`GodToolbar`, `GodTool`, `GodOverlay` kept; `GodOverlay.Show(spots, bars)` added): Select (Q), Build (B, plots of the chosen people's camps coloured by `CheckBuilding`), Road (R, legal hex edges), Camp (C, legal corners), City (Y), Banish (X, demon sites), Cast (P, every open proposal of both peoples with Cast and Decline), Problems (O), Wares (U), Time (T: time scale 0-8, roll now, next demon). `DevTools` (static) for scripts: `PlaceBuilding(type, plot, faction)`, `PlaceRoad(edge, faction)`, `FoundCamp(corner, faction)`, `UpgradeCity(corner, faction)`, `Banish(kind, id)`, `Cast(id)`, `CastQueued(faction, rank)`, `Decline(id)`, `SetTimeScale(x)`, `ForceRoll()` (steps the clock to the next roll), `SkipToDemon(maxTicks)`, `AddWares(settlement, ware, count)` and `SpawnDemon(kind, id)` (use the sim commands `DevAddWares` / `DevPlaceDemon` if S adds them; the sim has none yet, so adding wares is unavailable and a demon can only be reached by skipping time), `Problems(state)`, `PlotsOfFaction`, `PlotPos`, `LastResult`, `Faction`, `BuildType`.

## AutoPlay helpers

`AutoPlayScript`: `Step(LatticeMove, times)`, `Turn(sixths)`, `TeleportCell(cell, heading)`, `WalkTo(cell)`, `WalkToObject(() => component)` (stands on the free cell beside it nearest the player, then faces it), `Follow(() => who)`, `TapObject(() => component)`, `TapGround(() => point)`, `PickReply(i)`, `CastAtProposal(rank)` (walks within range of your proposal and presses its ground), `OpenMap()`, `CloseMap()`, `ToggleHud()`, `ToggleView()` (F1), `WaitIdle()`. Older helpers map onto the lattice: `WalkTo(point, stop)` (stop under 5 m ends on the neighbouring cell), `LookAt(point)` (turns to the nearest heading), `Turn(degrees, s)` (rounded to sixths), `Interact()` (taps the nearest villager within 15 m), `Teleport(pos, yaw, pitch)` (exact). Scripts: `boot_tour`, `capture_tour`, `walk_tour` (steps round the camp with a bump at the storehouse, map, HUD, the walk to the neighbouring corner with its time logged, Mira's bubble and a reply; logs `[AutoPlay] walk_tour OK`).

## Encounters

`EncounterContext.Avatar` is the `PlayerWalker`; `PlaceAvatar` calls `PlaceExact`. `EncounterHost.Start` stops any auto-walk and locks the walker; the end puts it back with `RestoreCell()` (or at the exact pose when it stood off the lattice before, e.g. after a legacy exact teleport).
