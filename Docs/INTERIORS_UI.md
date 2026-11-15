# Interiors in the game (package I5)

Interiors are the crystal campaign's underground places (`Docs/PRD_CRYSTALS.md` 5.10, package I5): small connected maps on the walking lattice, walked with the same moves as the surface (60 degree turns, 5 m glided steps, the pointer, Tab map, H HUD). Namespace `World.Game`; code in `Assets/World/Game/Interiors/`, plus the region edits in `Game/Player/WalkGrid.cs` and `PlayerWalker.cs` and the interior mode of `Game/UI/MapScreen.cs`. Written 14 Nov 2026 on branch `cry/i5` (from `historic-base`). Original procedural shapes only.

Everything is built from the running campaign's definition (any pack: the authored Earth pack and the skeleton are both tested) and follows the sim: doors, stairs, crystals and tally sticks read `WorldState` and `CampaignApi`; nothing here writes state. With no campaign nothing is built, `Interiors.Current` stays null and the game is unchanged.

## Files

| File | What |
|---|---|
| `InteriorsInstaller.cs` | `Install(boot)`: creates the director and registers the AutoPlay script. |
| `Interiors.cs` | The public surface: `Interiors` (static), `InteriorSpace` (static), `InteriorModel`, `InteriorLink`. |
| `InteriorDirector.cs` | Builds, keeps in step, the player's interior, the stair crossings and the fade, "Lead me out", the explored cells. |
| `InteriorBuilder.cs` | The cave: rock walls, floor and ceiling of flat-shaded triangles; rubble; chalk way marks. |
| `InteriorProps.cs` | Marker objects and their views (stairs, doors, pedestal, altar, carvings, tally spot, threshold, duel spots, cairn, seat, lever, plinth), `InteriorThing` (tap to read). |
| `SurfaceWayView.cs` | The surface end of a way in: the stair mouth with its slab and braziers. |
| `InteriorLighting.cs` | The underground look (no sky, sun or moon; dark ambient; black fog; the lantern always lit). |
| `InteriorMapLayer.cs` | The interior map (an `IMapLayer`) and its side panel with "Lead me out". |
| `InteriorScripts.cs` | AutoPlay `quarry_walk` and helpers shared with the tests. |
| `Tests/PlayMode/InteriorTests.cs` | See Tests. |
| `AgentScripts/I5Run.cs` | CLI helpers: open the World scene, run `quarry_walk`, status line. |

## Where interiors are

`InteriorSpace.Origin(k)` is lattice cell (300, -150 + 40 k): interior k of the definition lies 1.3 km east of the board centre, one every 200 m to the north. Local cell (i, j) of the rows is world cell origin + (i, j), so the six steps are the surface's (`InteriorMaps.Steps`). The floor is at y = 0 (`InteriorSpace.FloorY`). Only the interior the player is in is active; the others are hidden.

## Walking

- `WalkGrid.AddOpenRegion(owner, cells, floorY)`: those cells are walkable floor although they lie beyond the board; every other cell beyond the board stays blocked ("edge"). `RemoveOpenRegions(owner)` removes them. Owner of the interiors: `InteriorDirector.RegionOwner` = "interiors".
- `WalkGrid.SetCellBlocked(owner, cell, blocked)`: a door cell is blocked until its `OpenWhen` fact is known (`WhyBlocked` gives the owner).
- `WalkGrid.SetCellAvoided(owner, cell, avoided)`: every stair, exit and door way end (surface ends too) is avoided by `FindPath` unless it is the goal, so an auto-walk or a villager never crosses one by accident. Keys still step onto it.
- `WalkGrid.TryRegionFloor(cell, out y)`, `RegionOwner(cell)`, `RegionCellCount`, `IsAvoided(cell)`.
- `PlayerWalker.GroundHeight` returns the region's floor height on a region cell, so the walker and `Npc` stand on interior floors.

## Ways through (stairs, exits, door ways)

`Interiors.Links` lists both ends of every way: each interior `Exit`, `Stairs` (and `Door` with `To`) marker, and every surface marker an interior way names. A link is open when every fact it needs is known: both ends' `OpenWhen` and every `Connection` joining them (`InteriorLink.Needs`, `IsOpen`). `OutHeading` is the heading you face on arriving (inside: towards the most open floor; on the surface: towards the location's anchor, or its safe point when the way is on the anchor). `Up` is true for a way to the surface or to a shallower level (depth counts stairs from a level that has a way to the surface; equal depths go by definition order).

Stepping onto an open way's cell (a completed step, by key or auto-walk) takes the player through it: about a second, locked, walking 2.4 m on into the stair (down or up) while the view goes black, then placed on the other end's cell facing its `OutHeading`, then walking out of the stair while the view clears. The party's 12 s stair time is the sim's; the player's crossing is brisk. A shut way says "<name>: the way is shut." and nothing happens. Walking down and up a stair returns to the same cell and heading (tested).

## The public surface (for I6, I7 and later packages)

| Member | Use |
|---|---|
| `Interiors.Current`, `Changed` | The player's interior (null on the surface) and its change. |
| `Interiors.Crossing` | Raised (from, to) when the player starts through a way. |
| `Interiors.Built` | Raised after the interiors were (re)built or cleared. |
| `Interiors.All`, `Get(id)`, `IndexOf(id)` | Built interiors (`InteriorModel`: `Id`, `Name`, `Location`, `Index`, `Def`, `Origin`, `Width`, `Height`, `Floor` (world cells), `Markers` (with I, J), `Root`, `Depth`, `ToCell(i, j)`, `World(i, j)`, `IsFloor`, `Marker(id)`, `Ref(markerId)`, `Centre`). |
| `Interiors.TryCell(MarkerRef, out cell)`, `TryWorld(MarkerRef, out point)` | World cell or point of any marker, surface or interior (surface: `CampaignPlaces`). |
| `Interiors.PathWorld(interior, i0, j0, i1, j1)` | World points of `InteriorMaps.Path` over the floor through doors whose facts are known. |
| `Interiors.IsDoorOpen(interior, marker)`, `Knows(fact)` | Door state and the facts the interiors go by. |
| `Interiors.LinkAt(MarkerRef)`, `Links` | Ways and their ends. |
| `Interiors.PartyWorld(state, tickFraction, out pos, out dir, out interior)` | Where the campaign's party is in the world now: along its leg (interior legs follow `CampaignApi.LegCells`), across a crossing (on the near side until half way), or at its place. For I6's party figures. |
| `Interiors.Enter(MarkerRef)` | Puts the player on a marker with the fade (e.g. follow Mira down, a duel's staging). |
| `Interiors.Exit()` | Up to the surface at once by the nearest open way, with the fade (e.g. after a retreat). |
| `Interiors.LeadOut()`, `IsLeadingOut` | "Lead me out": auto-walk to the nearest open way up and on through every level to the surface. I6 offers it on any party member's bubble ("Lead me out" reply). |
| `Interiors.Explored(interior)`, `SaveExtra()`, `LoadExtra(text)` | Cells seen (map). The save extra for I8: `SaveSystem` may keep `Interiors.SaveExtra()` under the key `interior` and call `LoadExtra` after a load. |
| `InteriorSpace.Origin(k)`, `ToCell`, `ToWorld(interior, i, j)`, `TryLocal(cell, out interior, out i, out j)` | The lattice placement. |
| `InteriorDirector.Instance.FindWayOut(cell, out first, out surface, out steps)` | The way out from a cell (what "Lead me out" walks). |
| `InteriorLighting.Instance.Underground`, `SkyDark` | The underground look is on; at the end of the last frame no sun, moon or sky drew. |
| `Interiors.FactsOverride` | Tests only: facts instead of the campaign's. |

Villagers and party figures walk interior cells with the normal `Npc.WalkTo` (it uses `WalkGrid.FindPath` and `PlayerWalker.GroundHeight`); a path from the island into an interior does not exist (the regions are not joined to the board), so a figure going through a way is placed at the other end (`TryWorld(link.To)`), as the player is. For a duel underground, `DuelStageSpec` positions come from `Interiors.TryWorld` of the encounter's `Stage` markers (`DuelCaster`, `DuelOpponent`, volunteer at the threshold).

## What is built

- **Rock.** Each floor cell is a hexagon of the lattice (5 m across the flats). Floor and a low domed ceiling (3.2 to 5.2 m, higher where the cave is open) are fans of flat-shaded triangles; every side towards rock is a wall of three panel rows that swells into the passage at waist height. Every shared point is a function of its place alone, so walls, floor and ceiling meet without gaps, and the cave is the same on every build. Loose stones along the walls; a dead end (one open side, no marker) gets a fallen heap (the "fallen adit").
- **Chalk way marks.** At every fork a pale chevron on the floor points the shortest way to that level's nearest way up.
- **Ways up** (exits, stairs up): steps rising to an arch of dressed stone in the rock, a soft light falling down them (pale for the surface, warm for a level above) and a brazier: exits and stairs up are always lit. **Ways down**: a kerbed well with steps fading into the black and a torch. A shut way has timbers lashed across it.
- **Doors**: a barred gate of logs between stone posts across the passage; it swings open when its fact is known.
- **Pedestal**: a cut-stone column; its crystal floats over it, glowing in the element's colour (the tower's beam colour) with its own light, while the crystal is `AtSite` there. Roots hang from the roof over an Earth pedestal.
- **Altar**: a dark block with horned posts and ash, two of the Ash Clan's cold green braziers; the crystal over it while it is `AtRivalAltar` there.
- **Inscription**: carved marks (cuts, chevrons, a ring) on a dressed stone face against the wall. Tap to read: the lines of the story's `Examine`/`Search` events at that marker (guards applied), else a plain sentence. An inscription next to a pedestal or altar is a floor spot instead: an Ash tally stick (five notches and a cross, a green rag) lies there while the crystal is in rival hands.
- **Threshold**: ash-grey stones across the way, posts with green rags and bone. **Safe point**: a cairn. **Duel spots**: a ring of pale stones (caster), a scorched ring (opponent). **Witness**: a stone seat. **Lever**: a lever in a block. **Plinth** (and anything else): a low plinth.
- **Surface ways**: a stair mouth like a dolmen over a kerbed well; shut, a slab cut with three marks and a ring lies over it; open, the slab is pushed aside and two braziers burn. A 6 m clearing is kept round it (`LandViews.AddClearing`).

Taps: every marker is an `InteriorThing` (`IInteractable`, `IInspectable`): a label with what it is, its lines, and replies: "Go up"/"Go down" on an open way (walks onto it), "Lead me out" on anything else underground.

## Lighting underground

While `Interiors.Current` is set (`InteriorLighting`, after the lighting package each frame): sun and moon off, sky dome and stars hidden, ambient (0.040, 0.040, 0.048) / (0.032, 0.030, 0.032) / (0.020, 0.018, 0.017), black fog from 5 to 36 m, the player's lantern always lit (1.25, 17 m) by day too. The interiors' fires are `WorldLights` lights with `DayLevel` 1 (so they burn by day underground), no ground glow or halo, and go through the budget: at most `WorldLights.PixelBudget` (6) per-pixel lights including the lantern (tested at the vault's altar, the Root Chamber and the quarry's exit). The surface is untouched: on the way up the dome comes back and the day-night cycle relights the sky in the same frame.

## The interior map (Tab)

Inside an interior the map opens on it (`MapScreen.Mode` = `Interior`, `MapScreen.InteriorShown`). A line at the top switches to the island and back ("tap here for the island"); on the island map the player is shown at the place the interior lies under, and open ways down are marked. The interior map draws the floor cells seen so far (cells within 2 open steps of where the player has stood; not through shut doors) as a hex plan with wall lines, the ways out in green (always, seen or not, pulsing when open), stairs down in blue, doors barred or open, discovered markers with their names (undiscovered ones hidden, or "?" while the companion with the lamp service lives), Mira and the party (from the sim), the walk target and the player's arrow. A tap on a cell walks there when the map closes; "Lead me out (n steps)" on the side panel starts the walk out.

`MapScreenView` also gives layers drawing helpers: `DrawDisc`, `DrawRing`, `DrawLabel`, `DrawLine`, `PixelsPerMetre`, `LabelStyle`, `TitleStyle`, `SmallStyle`, `DiscTexture`, `RingTexture`. In interior mode the map draws every layer too: an island layer should check `MapScreen.Mode`.

## Saves

A save made underground keeps the walker's cell (far east) and heading as before; on load the director builds the interiors from the loaded campaign in `Awake`, before `SaveSystem` puts the walker back, so the player is restored in place, in the right interior, lit as underground (tested). The explored cells are not in the save until I8 adds the `interior` extra (`Interiors.SaveExtra`/`LoadExtra`); without it the map starts again from where the player stands.

## AutoPlay `quarry_walk`

`-start dev.historic -autoplay quarry_walk` (or from the editor: `AgentScripts/I5Run.cs`, `I5Run.QuarryWalk`; it rebuilds the world on `dev.historic` itself when needed). Starts the authored pack with `StartCampaign` and drives the story by commands only (accepting Mira's top lead whenever she is idle, the sim stepped fast): Mira reads the boundary stone; the player walks to the Stonemark Glade, down the stair, to the survey marks (tapped and read), looks back at the lit way out, the Root Chamber with the Deepstone, the interior map, and is led out to the glade. Then `DevSetCustody` puts the Deepstone on the Ash altar, Mira follows the trail (the bare pedestal and the tally, the stair down); the player goes down again, sees the bare pedestal and the tally stick, takes the stair to the Tally Vault, its turnings, the threshold and the altar with the Deepstone, the vault's map; Mira reaches the threshold, the keeper's duel is committed and resolved as a victory by commands (the campaign duel is package I7's), the barred door opens and the player leaves by the Ash cut. Captures `i5_*.png` in `Evidence/screenshots` (git-ignored). Logs `[AutoPlay] quarry_walk OK`.

## Tests (`Tests/PlayMode/InteriorTests.cs`)

`RegionBuild_EqualsTheDefinition_AuthoredAndSkeleton` (every floor cell walkable, every rock and outside cell blocked, doors blocked, region cells = floor cells, markers, both ends of every way, avoided way cells, arrival faces open floor), `NoCampaign_NothingIsBuilt_AndTheMapIsTheIsland`, `Walls_Block_AndPathsFollowTheFloor`, `Stairs_LinkBothWays_ToTheSameCellAndHeading_AndShutWaysHold`, `Doors_ObeyTheirFacts`, `Map_ShowsOnlyExploredCells_AndAlwaysTheWayOut`, `LeadMeOut_ReachesTheSurface_ByTheNearestOpenWay`, `SaveAndLoad_Underground_RestoresThePlayerInPlace`, `NoSkyLight_Underground_ByNightOrDay`, `FrameRate_AndLightCount_InBudget_Underground`, `Markers_FollowTheSim_PedestalTallyAndAltar`, `NoWorldStateWrites_InInteriorFiles`.

## Not done

- The party and companions as figures underground, Mira's "the stair stays lit" line, the duel staged on the vault's markers: I6 and I7, with the members above.
- The explored cells are not saved until I8 adds the extra.
- The surface ends of ways show the mouth only; the site's own view (the boundary stone, the spoil at the Ash cut) is I6's.
