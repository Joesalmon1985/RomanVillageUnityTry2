# WU-1a: the hub's places

20 Nov 2026, branch `godboard/wu1-hub-places` (from `fbe3b5b`, merged with `godboard/main` at `34afb86`, WU-0). First half of WU-1 (WORLD_UI_PLAN 2.1, section 5): places only. Tap-to-walk, sideways drag and "return restores the hub view" are WU-1b.

## Done

- **Hub layout** (`View/Camp/CampLayout.cs`, hub mode only, i.e. a dealt/god board island: `CampLayout.Hub`): the spawn heading is chosen so the dance circle and Mira are in the first view (`SpawnHeading`); the prince keeps his old start line (`PrinceHeading`, `PrinceStart`). `Places` holds the temple spot (bearing about 0 to -40 from the spawn), the healer's lodge (one turn left, about -100) and the duel ring (one turn right, about +105), found with the camp obstruction tests (strict, then relaxed). `Clearings` gained a hub-wide circle grid (radius 62 m round the hearth, island only) and each place. Legacy boards are untouched.
- **Green hub ground** (`HubInstaller`): one level, plain green mesh over the island part of the clearing (y 0.012), trees and crops cleared by the layout's clearings; the island beyond keeps its look.
- **Temple forecourt** (`TempleForecourt`): paved forecourt on the doorway side, lamb pen with lambs, offering baskets, timber stack and scaffold, braziers, two builders carrying planks, a temple keeper who greets within 14 m and walks to the altar; his dialogue "Make the offering" walks to the altar and then sends the normal `Sacrifice` (`GodBoardDirector.TrySacrifice`). The doorway/forecourt slab (`TempleDoorway`) routes to the altar's WU-0 two-tap action (preview, then confirm). Historic: the temple is gone, a tappable ruin stays, the keeper says the age has turned. The temple now stands at the layout's temple spot.
- **Council steward** (`CouncilSteward`, `CouncilDirector.StewardOpen/StewardSpot`): always at the tower foot; eligible/offered/in progress with the player helping: "Hear the dispute" starts the council through the existing accept path (entering an offered council counts as accepting); otherwise "No dispute is called yet." (or "settling it among themselves"); after the election the office holder sits there. `CouncilSite` keeps clear of hub places and the herbs place is at least 20 m from the lodge.
- **Rhythm** (`DanceStarter`, `SolGuide`, `DanceCircle`, `CampInstaller`): before spire 1 any drum or the fire ring starts `rhythm_party`; after spire 1 the circle is retired (D-6), Sol stays at the cold circle and "Lead me to the spires" walks him to spire 1 with the player following (walker follow), then he walks back.
- **Healer's lodge** (`HealersLodge`): thatched lodge, herb racks, cot under an awning, mortar and bowls, healer, assistant, and a runner who runs in on each `Outbreak`/`Epidemic` while the plague runs. Shell and people only (map table is WU-3). Plague running: says how many spirits are loose; not started: all quiet; ended/Historic: "The sickness is gone."
- **Mira's expedition camp** (`ExpeditionCamp`): packs, bedroll, lantern with light, hide map on a trestle (`MapTap` opens her conversation), two companions (Scout, Porter) around Mira. Entry is her existing conversation.
- **Duel ring** (`DuelRing`): fenced sand ring with a gate, practice posts, a rack of staves and bucklers, two villagers sparring (dressing), referee and two onlookers. Tapping the referee or the ring offers "Practice duel": Mira walks over to the gate, the existing `ward_mira` encounter starts, she walks home afterwards.
- **Zone tags**: each destination has a `Zone HUB-...` marker tagged with WU-0's `WorldZone` (`HubInstaller.Zone(id)`); the player's temple also keeps WU-0's own tag.
- `AgentScripts/WU1Hub.cs`: probe, capture and look drivers.

## Tests actually run (editor 6000.3.28f1)

- New `World.Game.Tests.HubTests`: **9 / 9 passed** (all zones tagged and people exist at night; first-view bearings and one-turn-away places; keeper greets, walks to the altar and the offering opens the table; doorway two-tap; steward placeholder and accept path; drum starts the dance then Sol leads to the spires; healer and runner; Historic ruin and lodge without plague; Mira's camp; referee, Mira walks over, duel starts, Mira walks back).
- `GodBoardTests` 10 / 10, `GodBoardVillagerTests` 7 / 7, `SpireTests` 6 / 6. PrinceTests, ExpeditionTests and others: see the end of this file.
- Not run: the full PlayMode suite, `DanceTests` (changed one assertion to `PrinceHeading`; not re-run if listed below as such), EditMode, a player build.
- Console: no errors from game code.

## Captures (local, not committed): `Evidence/worldui/`

`hub_spawn_844.png`, `hub_left_844.png`, `hub_right_844.png` (the views at the spawn heading and one turn each way, night, 844 x 390 rendering) and `close_{temple,lodge,ring,camp,tower}.png`. 1920 x 1080 not taken. (The game view capture file is 1280 x 720 whatever the render size.)

## What WU-1b must wire / open

- Tap-to-walk and sideways drag (`PlayerWalker`/`PointerInput`): the new pick boxes are low trigger slabs (forecourt, ring, lodge body is a tall box) which a ground tap will hit as non-ground targets; check `PointerInput.Raycast` keeps ground taps working over the forecourt, the ring and the lodge.
- "Return restores position and yaw" is not done.
- Entry points other than the doorway use conversations (keeper, steward, healer, referee, Mira, Sol). If the plan wants them as two-tap `IWorldAction`s, wrap the same calls (`TempleForecourt.BeginOffering`, `CouncilDirector.StewardOpen`, `DuelRing.StartDuel`, `SolGuide.LeadToSpire`).
- The hub framing assumes the dealt seed 7 board: temple 4 deg, tower 14 deg, circle -43 deg, Mira 38 deg, lodge -112 deg, ring +111 deg from the spawn heading. Other seeds pick the heading by circle and Mira only; the tower and temple depend on the clearest ground.
- Visual polish: the lodge and ring read dark at night beyond their glow; the council's own pavilions stand near the ring. Placeholder actors only.
- The prince's guide walks the player away from the spawn at once in a god board game (captures disable it).

## Regression results (after the merge with WU-0, final code)

`PrinceTests` 13 / 13, `ExpeditionTests` 9 / 9, `DanceTests` 6 / 6 (one assertion now reads `PrinceHeading`), `CouncilTests` 30 / 30, plus the counts above (`HubTests` 9 / 9, `GodBoardTests` 10 / 10, `GodBoardVillagerTests` 7 / 7, `SpireTests` 6 / 6). `WorldUiTests` (WU-0) was not re-run.
