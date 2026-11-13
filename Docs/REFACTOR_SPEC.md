# Refactor spec: the prehistoric village (binding)

Written 13 Nov 2026 on branch `refactor/prehistoric` (cut from `main` at `189466f`). The owner agreed the decisions behind it (A to F below). Several agents build from this document in parallel without talking to each other, so the names, ids, shapes, commands, events and helpers in sections 4, 5 and the appendices are binding. Where the owner's decisions left a gap, the spec writer chose the simplest rule; every such choice is listed in section 10 so the owner can overrule it.

Reading guide: sections 1 to 3 are the game (about 8 minutes). Sections 4 to 6 are the simulation contract. Section 7 says what happens to the existing code. Section 8 is who does what. Appendix A is the simulation interface in C#; appendix B is the Unity-side interface between packages.

The existing rules still apply: `World.Sim` is pure C#, deterministic, integer state, commands in, events out, saveable; `World.Game` never writes state; sim and its tests before views; fast harness `dotnet test DotNet`; the repo is public (no Sims 2 data, no third-party art or audio).

---

## 1. Summary

The game becomes a small prehistoric island. Two peoples, yours and the rival Ash Clan, each start with one camp on a hex corner. Dice roll by themselves every 10 seconds; when a hex's number comes up and a camp touches it, young stock appears on that hex (saplings, lambs, seedlings, or unseen clay and stone), grows for 75 seconds and can then be collected by a collecting building of a neighbouring camp. Carriers walk collected goods to the camp's storehouse along short local paths; main roads along hex edges join camps and carry shipments between storehouses. Raw goods pay for expansion (roads, camps); workshops turn them into worked goods that pay for development (cities, warriors). You do not command anything. You are a floating viewpoint that steps through the world like Dungeon Master; the prince (the faction AI) and other villagers walk up and propose what to build, where and why, and nothing is built for your people unless you walk to the spot and press the ground: a free spell that starts the construction site. The rival plays by the same rules without spells. If it becomes clearly stronger than you, it warns, then overruns you; if you outgrow it, you overrun it. Demons, drawn on a roll of 7, sit on camps and roads and stop them until banished, again by a spell. The rhythm game moves to a dance circle round a fire with four drums; Mira's Ward duel stays as it is. Cards, the wonder, ages and the fuller military leave the build (kept in `Archive/` and in history) until a later pass.

---

## 2. Rules

Every number below is a tuning field in code (section 4 names the tuning class); the value given is the starting value. 10 ticks = 1 second, as now.

### 2.A Production

| Rule | Value |
|---|---|
| Dice | Two dice, from a deck of the 36 ordered outcomes (d1, d2 each 1-6), shuffled with the seeded `Rng`, one card per roll, reshuffled when empty. |
| Roll timing | One roll every `RollIntervalTicks` = 100 (10 s) of game time; the first at tick `FirstRollTick` = 100. No bell, no rounds, no lockout. The clock pauses only for the pause menu and encounters, as now. |
| Hex numbers | Every resource hex has a number 2-12 (never 7); Meadow has 0. Dealing in 2.A.1. |
| A roll of N (not 7) | Every resource hex numbered N with at least one adjacent camp (any people, finished, not demon-held, see 2.C.4) gains a batch of `BatchSize` = 4 young stock, up to the hex cap. |
| A roll of 7 | Produces nothing. After the grace period (`DemonGraceTicks` = 1800, 3 min) it triggers the demon draw (2.C.4). Before it, nothing happens. |
| Growth | A batch matures `GrowTicks` after it was added: 750 ticks (75 s) for every terrain (per-terrain table, all 750 to start). It shows three young stages (first, second and last third of its growth), then it is mature. |
| Cap | Young plus mature stock on one hex is at most `HexCap` = 12. Units that would exceed it are lost (ledgered as `StockLost`). |
| Mined terrains | Hills (clay) and Mountain (stone) follow exactly the same rules; only the views differ (nothing young is drawn, mature stock shows as dug piles). No finite reserves. |
| Collecting | Only a collecting building on a plot in that hex's sector of an adjacent camp can take mature stock. Each cycle takes 1 mature unit at its start and yields the building's `Yield` at its end. Two collectors on one hex (same or different peoples) compete: whoever starts a cycle first takes the unit; ties in one tick go by ascending building id. |
| Starting stock on hexes | Each hex adjacent to a starting camp holds `StartMature` = 4 mature units at tick 0. |
| Ore | Not in the first pass. Hook only: ware id 10 is reserved for Ore; stock cohorts carry their ware, so a Mountain hex can later receive Ore cohorts beside Stone; an improved mountain collector variant (Mine) will collect them. Out of scope: no Ore code now. |

**2.A.1 Number dealing** (`BoardGenerator`, seeded, `Rng(seed).Fork("board.numbers")`):
1. The three hexes of each starting camp get fixed numbers by terrain: Forest 6, Hills 8, Fields 5 (both peoples alike, so the start is fair under shared dice).
2. The four Mountain hexes get a seeded order of {5, 6, 9, 10}.
3. The other 28 resource hexes get a seeded shuffle of the pool 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 6, 6, 6, 8, 8, 8, 9, 9, 9, 10, 10, 10, 10, 11, 11, 12.
No adjacency rule. The terrain layout, the player's start corner (70) and the rival's (97) stay as generated today.

Pips (chance out of 36, used by the brain and the HUD): 2 and 12: 1; 3 and 11: 2; 4 and 10: 3; 5 and 9: 4; 6 and 8: 5.

### 2.B Camps, plots and roads

"Camp" is the player-facing word for a settlement; code says `Settlement`.

**Settlements.** At most one per hex corner. Only interior corners (three board hexes meet) may hold one. Catan spacing: no other settlement or settlement site of any people on a corner one hex edge away (`CornerNeighbours`). A new settlement needs a built main road of its people ending at that corner, connected through that people's built roads to one of its finished settlements. Each settlement has its own storehouse (at the corner) and its own stock; there is no shared stock.

**City.** An upgrade of the same settlement (same id, same corner, same stock). It unlocks the third plot of each sector and adds strength (2.C.3).

**Plots.** Each settlement corner has three sectors, one per adjacent hex, and three plot slots per sector:

| Slot | Position (in metres, see 4.2 for the exact formula) | Available |
|---|---|---|
| 0 | 30 m from the corner towards the hex centre, 8.66 m to the left | settlement and city |
| 1 | the same, 8.66 m to the right | settlement and city |
| 2 | 45 m from the corner towards the hex centre, on the spoke | city only |

So a settlement has 6 plots and a city 9. One plot is reserved and never buildable: slot 2 of the home camp's Fields sector holds the dance circle, so the home city has 8. Plots are 5 m in radius, at least 17 m from each other, at least 16 m from any hex edge (main roads stay clear), and 10 m from the hex centre, where a 6 m stock patch shows the hex's stock. Plots of two settlements on the same hex never overlap (their corners are at least two hex edges apart). Every plot centre and every corner lies on the walking lattice (2.D).

**Buildings on plots.** A collector must stand on a plot in the sector of the hex it collects from, and that hex must be its terrain. A workshop may stand on any free plot of the settlement. At most one building of each type per settlement. A new settlement starts with one primitive collector on slot 0 of each resource sector (none for a Meadow sector). An improved collector is built on another plot of the same sector; when it is finished, the primitive collector of that hex in that settlement retires (it finishes its cycle, its store is carried away, then its plot is free).

**Local paths.** Every plot is joined automatically to its settlement's storehouse by a fixed path: from the corner along the sector's spoke to 30 m, then to the plot (slots 0 and 1), or straight along the spoke (slot 2). No player-laid local roads. Carriers walk these paths; the sim treats them as `LocalSteps` long (8 steps for slots 0 and 1, 9 for slot 2; a step is 5 m).

**Main roads.** Built along whole hex edges (corner to corner, 60 m = 12 steps) with raw goods. A road site needs one end corner to hold a finished settlement of its people or to be the end of one of its built roads, and neither end corner may hold another people's settlement or settlement site. One road per hex edge, any people. Roads carry carriers between storehouses and to road and settlement sites. Carriers may route only over their own people's built roads and through corners that are not another people's settlement and not demon-held.

**Shipments.** The storehouse is where goods pass between the local paths and the main roads. When a settlement needs a ware it does not hold (a site, a workshop input), it imports it from the nearest connected settlement of its people that has it spare (2.B.1).

**2.B.1 Logistics rules (all deterministic; ties by ascending id).**
- Carriers belong to a settlement: 3 at each starting settlement, 2 at a new settlement, +1 per finished plot building there, +1 when it becomes a city. A carrier carries 1 unit on a local trip and up to `ShipmentSize` = 4 units of one ware on a shipment. Walking speed: `CarrierTicksPerStep` = 15 ticks per 5 m step (main road edge 180 ticks). A carrier always returns to its storehouse after a delivery.
- Demands, in serving order: construction sites (buildings, roads, settlements) first, then workshop inputs, then outputs going to the storehouse, then shipments.
- Building sites and workshop inputs draw from their own settlement's storehouse. Road and settlement sites draw from their *source settlement* (the nearest connected finished settlement of that people by road edges when the site is placed, ties by lower corner id), carried along the roads.
- A city upgrade draws its materials straight from its own storehouse stock (no carrier trip): each tick it moves what it can from `Stock` to `UpgradeDelivered`.
- Collectors and workshops hand every output unit to their storehouse by carrier. Workshops keep `InputBuffer` = 2 cycles of inputs on hand or on the way.
- Import: for each settlement and ware, unmet need = demand that its storehouse cannot cover now. If it is positive and no shipment of that ware is already heading there, the nearest connected settlement of the same people whose unreserved stock exceeds its own needs sends `min(ShipmentSize, need, spare)` units with one of its carriers.
- Economy targets: a workshop does not start a cycle while its storehouse holds `StockTarget` of its output (Planks 6, Bricks 6, Bread 4, Meat 4, Cut stone 4); it reports `output_full`.
- A collector's store holds at most `CollectorStore` = 4; full, it waits (`output_full`).

**Placement costs and times** are in section 3. Sites wait for their goods; casting never needs the goods to be present (the brain only proposes what its people can pay, 6.2).

**Board.** Stays radius 4, 61 hexes, 150 corners, 210 hex edges, 60 m hex edges. The fine graph (570 flags, 630 segments) is no longer used by any state or rule (section 4.1).

### 2.C The player, the prince and spells

**2.C.1 The player** is a floating viewpoint (no body, no sim state) that villagers talk to. It never sees a build menu. It moves on the walking lattice (2.D) and uses a free mouse pointer.

**2.C.2 Proposals and spells.** Each people has a faction AI, the "prince's brain" (section 6), that keeps a queue of at most `QueueLength` = 3 open proposals, best first. A proposal says what (building type, main road, new settlement, city upgrade, or banish demons), where (settlement, plot, hex edge or corner, or demon site), and why (one sentence), and which villager pitches it (`Speaker`). For your people, nothing is built and no demon is banished unless the player casts a spell: walk to the proposed spot, press that bit of ground with the pointer while standing within `CastRangeMetres` = 15 m of the target point. Casting is free; it only starts the site (or banishes). Carriers still deliver the materials and the build time still runs. This holds for every construction: buildings, main roads, settlements, city upgrades. Replies: "yes" leads to the spot; "no" declines the proposal (the next one becomes the pitch; the same proposal is not offered again for `DeclineCooldownTicks` = 600); walking away or ignoring changes nothing in the sim. An ignored prince means nothing is built, and the rival eventually overruns you.

The F1 developer tools keep direct build commands (section 5) for testing and AutoPlay; normal play never shows them.

**2.C.3 The rival and the overrun.** The Ash Clan (`PlayerIds.Rival` = 1) uses the same production, building and logistics rules and the same brain. It needs no spells: its brain casts its own top proposal automatically `RivalCastDelayTicks` = 450 (45 s) after that proposal became its top proposal.

Strength of a people, recomputed every tick:

| Source | Strength |
|---|---|
| Finished settlement (not a city) | 1 |
| City | 2 |
| Fed warrior | 1 |

Warriors are trained automatically at a Warrior lodge (section 3) while it has Bread and Meat delivered, up to 3 per lodge. Each warrior eats 1 Bread (or, if there is none, 1 Meat) every `MealTicks` = 900, taken straight from its settlement's storehouse; a warrior that misses a meal is hungry and counts 0 until its next successful meal (it tries again every 100 ticks).

**Overrun rule.** When one people's strength is at least the other's plus `OverrunMargin` = 3, an overrun warning starts: attacker, defender, end tick = now + `WarningTicks` = 900 (90 s). It is visible (a war camp at the attacker's settlement nearest the defender, the prince says so, the HUD counts down). If at any tick during the warning the margin no longer holds, the warning is cancelled. If it still holds at the end tick, the attacker overruns the defender and the game ends. Only one warning exists at a time. What the defender can do to hold: complete strength (finish a settlement or city site, train and feed warriors at a lodge); under a warning the brain ranks such proposals first (6.2).

**Win and lose (first pass).**
- Win: your people overrun the Ash Clan.
- Lose: the Ash Clan overruns your people.
- Time limit: at `TimeLimitTicks` = 12000 (20 min) the stronger people wins; equal strength is a draw.
No points, no breach track.

**2.C.4 Demons (the Pandemic layer, minimal).**
- Sites: every finished settlement corner (both peoples) and every built main-road hex edge (both peoples).
- On a 7 after the grace period: draw one site uniformly with the demons' own `Rng` from the sorted site list (corners ascending, then hex edges ascending) and add `DemonsPerSeven` = 1 demon.
- A site holds at most `DemonCap` = 3. A 4th demon causes an outbreak instead: each neighbouring site gains one (neighbours of a corner: built road edges touching it; neighbours of a road edge: its end corners that are finished settlements, and built road edges sharing an end corner). Each site outbreaks at most once per draw; chains are allowed. No breach track.
- Effects: a demon-held settlement's collectors stop (no cycles start), it sends and receives no shipments, and a roll adds no stock to a hex whose every adjacent finished settlement is demon-held. Workshops keep working. A demon on a road edge blocks carriers on that edge and stops a road site there.
- Answer: banish, a spell like any other. The brain proposes "banish" for its own people's demon sites; casting it removes every demon from that site. The rival's brain auto-casts its banish proposals with its usual delay. A people can only banish its own sites.

**2.C.5 First scene (tutorial).** The world runs from tick 0 and keeps running during every conversation. The prince starts 20 m from the player and walks up. He says: "I'm the prince. Come with me. We're building a woodcutter's hut." Replies: "Lead the way", "What's it for?", "I'll catch up."
- "Lead the way": he walks to the plot and the player auto-follows him.
- "What's it for?": he gives the proposal's reason sentence, then asks again with the two other replies.
- "I'll catch up.": he walks to the plot and waits there; the player walks alone.
At the plot he explains why in one sentence (the proposal's reason) and asks the player to press the ground. The player casts; the site starts; a builder walks from the storehouse and carriers bring 2 Wood; the hut is finished. When it hands its first load of wood to a carrier, the prince points it out and the player can tap the carrier and choose "Follow" to walk behind it to the storehouse. The sim guarantees the first proposal (6.2: the opening rule): Woodcutter's hut, home settlement, Forest sector slot 1 (plot 631 on the default board). Later, other villagers pitch proposals the same way: the sawyer pitches improved wood collection and the Sawmill, the merchant pitches main roads; the prince pitches the rest (table in 6.3).

### 2.D Movement and interface

**Walking lattice.** A triangular lattice with step `Step` = 5 m, origin at the board centre (centre of hex 30), aligned with the hex board. Cell (i, j) is at world X = 5 · (√3 / 2) · i, Z = 5 · (i / 2 + j). Every hex corner, every hex centre and every plot centre is a lattice cell; every hex edge is a straight run of 12 steps in one heading; every corner-to-centre spoke is 12 steps.

| Heading h | Yaw (0 = north, clockwise) | Step (di, dj) |
|---|---|---|
| 0 | 0 | (0, +1) |
| 1 | 60 | (+1, 0) |
| 2 | 120 | (+1, -1) |
| 3 | 180 | (0, -1) |
| 4 | 240 | (-1, 0) |
| 5 | 300 | (-1, +1) |

Moves: forward one cell in the heading, step back one cell against it, turn left (h - 1) or right (h + 1) by 60 degrees. Steps glide over 0.4 s (ease in-out), turns over 0.25 s; holding a key repeats; one further input is buffered. Eye height 1.7 m, fixed pitch -8 degrees, field of view 70 (no mouse look).

Blocked cells (a move into one does a short bump and stays): beyond `BoardSpace.BoardRadius` + 4 m; within 3 m of a finished settlement's corner (storehouse and hearth); within 5 m of a plot holding a building that is constructing, active or retiring (empty plots and sites stay walkable, so the player can stand on them to cast); within registered static blockers (mountain peak bases, the dance fire and drums). Trees, crops and people never block. Main roads and the lattice lines along hex edges stay walkable except at settlement corners, which the player walks round.

**Keys.**

| Action | Keys | Touch |
|---|---|---|
| Forward | W, Up, Numpad 8 | strip 2 |
| Step back | S, Down, Numpad 2, Numpad 5 | strip 3 |
| Turn left | A, Left, Numpad 4, Numpad 7 | strip 1 |
| Turn right | D, Right, Numpad 6, Numpad 9 | strip 4 |
| Reply 1-3 | 1, 2, 3 | tap the reply |
| Map | Tab | - |
| HUD on/off | H | - |
| Developer tools | F1 | - |
| Pause menu | Esc | - |
| Save / load | F5 / F9 | - |

Touch strips: the bottom 30% of the screen split into four equal vertical strips, left to right 1-4. They are invisible. The same strips are the four drums in the dance (2.E). Taps above the band are pointer taps. D is both "turn right" and drum 1; movement is locked during the dance, so they never meet.

**Pointer.** The cursor is always free. Click or tap: on a thing, a small bubble says what it is (`IInspectable`); on a speech reply, answers; on a person (villager or carrier), offers "Follow" (auto-walk on the lattice to the cell 2 steps behind them, re-planned when they move more than one cell; any movement key or a right click stops it); on the ground of an open proposal of your people, casts if within range, else a bubble says "Walk closer to cast."

**Screen.** No on-screen buttons in normal play. Without the HUD the player sees the world and speech bubbles above heads (replies as tappable lines under the bubble). H toggles the HUD (off at start): game time and last roll, your strength against the rival's, the warning countdown when one runs, the stock of the nearest settlement of yours, your proposal queue (read only). Tab opens the map: a read-only top-down view of the whole island (hexes with numbers, settlements by people, roads, proposals, demons, the player and the prince); the world keeps running; tapping a spot sets an auto-walk target that starts when the map closes (no teleport). F1 opens the developer tools: the existing God View camera and toolbar with build tools rewritten for plots, hex edges and corners, plus Cast (any proposal, from anywhere) and Banish; AutoPlay uses them.

### 2.E Dance circle and setting

The game starts as a hunter-gatherer people with a hearth, a store pile and three primitive collectors per camp. The square, cottages, market, bell, warehouse, Quartermaster Bram, Hesta, Captain Orla, Scribe Pell and Juno go.

The rhythm game keeps the same music (owner's stems), chart, `PhraseSession` rules and tuning. It moves to a dance circle round a fire at the home camp (the reserved plot, 2.B): four drums on a 4 m circle round the fire, the player at 6 m facing the fire with drums 1-4 left to right in front. Cues light their drum instead of falling down a lane: a drum glows from cue time minus the fall time (the same lead the lanes used) and flashes on the judgement; holds stay lit. Played on D F J K (drums 1-4) and the four touch strips. DJ Sol becomes Sol the drummer at the circle.

The bar/balcony route choice becomes the ending choice of the dance, inside the same window (beats 48 to 60.5): Left arrow (or tapping the left of the fire) = "Fire leap" (`Route.Bar`), Right arrow (or the right of the fire) = "Spirit call" (`Route.Balcony`). On a successful phrase the dancers leap the fire (orange flare) or blue spirit smoke rises. It is cosmetic and not saved. `PhraseSession` and its tests do not change.

Mira's Ward duel stays exactly as it is; Mira stands at the home camp.

---

## 3. Wares, buildings, recipes, costs

### 3.1 Wares (`World.Sim.Ware`)

| Id | Ware | Kind | From | Used for |
|---|---|---|---|---|
| 0 | Wood | raw (Forest) | collectors | roads, settlements, tier 1 buildings, Sawmill, Kiln, Smokehouse |
| 1 | Clay | raw (Hills, mined) | collectors | roads, settlements, tier 1 buildings, Kiln |
| 2 | Grain | raw (Fields) | collectors | settlements, Bakery |
| 3 | Sheep | raw (Pasture) | collectors | settlements, Smokehouse |
| 4 | Stone | raw (Mountain, mined) | collectors | Stonecutter |
| 5 | Planks | worked | Sawmill | tier 2 buildings, city |
| 6 | Bricks | worked | Kiln | tier 2 buildings, city |
| 7 | Bread | worked | Bakery | city, warriors, meals |
| 8 | Meat | worked | Smokehouse | warriors, meals |
| 9 | CutStone | worked | Stonecutter | city |
| (10) | Ore | reserved, later age | - | - |

`Wares.Count` = 10, `Wares.RawCount` = 5. `Wares.Title(CutStone)` = "Cut stone", `DisplayName` = "cut stone". Terrain to raw ware: Forest Wood, Hills Clay, Fields Grain, Pasture Sheep, Mountain Stone, Meadow none. Coin stays a `PlayerState` field and is unused.

### 3.2 Buildings (`BuildingType`) and recipes

| Id | Type | Name | Kind | Terrain | Recipe | Ticks |
|---|---|---|---|---|---|---|
| 0 | LogPile | Log pile | primitive collector | Forest | 1 mature unit -> 1 Wood | 100 |
| 1 | ClayScrape | Clay scrape | primitive collector | Hills | 1 unit -> 1 Clay | 100 |
| 2 | GrainBasket | Gleaners' basket | primitive collector | Fields | 1 unit -> 1 Grain | 100 |
| 3 | ThornFold | Thorn fold | primitive collector | Pasture | 1 unit -> 1 Sheep | 100 |
| 4 | RockHeap | Rock heap | primitive collector | Mountain | 1 unit -> 1 Stone | 100 |
| 5 | WoodcutterHut | Woodcutter's hut | improved collector | Forest | 1 unit -> 2 Wood | 60 |
| 6 | ClayPit | Clay pit | improved collector | Hills | 1 unit -> 2 Clay | 60 |
| 7 | Farm | Farm | improved collector | Fields | 1 unit -> 2 Grain | 60 |
| 8 | ShepherdHut | Shepherd's hut | improved collector | Pasture | 1 unit -> 2 Sheep | 60 |
| 9 | Quarry | Quarry | improved collector | Mountain | 1 unit -> 2 Stone | 60 |
| 10 | Sawmill | Sawmill | workshop | any | 1 Wood -> 1 Planks | 100 |
| 11 | Kiln | Kiln | workshop | any | 2 Clay + 1 Wood -> 2 Bricks | 150 |
| 12 | Bakery | Bakery | workshop | any | 1 Grain -> 1 Bread | 100 |
| 13 | Smokehouse | Smokehouse | workshop | any | 1 Sheep + 1 Wood -> 2 Meat | 150 |
| 14 | Stonecutter | Stonecutter | workshop | any | 1 Stone -> 1 CutStone | 150 |
| 15 | WarriorLodge | Warrior lodge | workshop | any | 1 Bread + 1 Meat -> 1 warrior (max 3 per lodge) | 300 |

Primitive collectors are not placeable: they come with a settlement. Every building's purpose sentence (`BuildingDef.Purpose`) is in the code table; the brain's reasons reuse them.

### 3.3 Costs and build times

| Thing | Pays with | Cost | Build ticks (after all goods arrived) |
|---|---|---|---|
| Main road (one hex edge) | raw | 1 Wood + 1 Clay | 100 |
| New settlement | raw | 1 Wood + 1 Clay + 1 Grain + 1 Sheep | 300 |
| Woodcutter's hut, Clay pit | raw | 2 Wood | 150 |
| Farm, Shepherd's hut, Quarry | raw | 2 Wood + 1 Clay | 150 |
| Sawmill | raw | 2 Wood + 1 Clay | 150 |
| Kiln | raw | 1 Wood + 2 Clay | 150 |
| Bakery, Smokehouse, Stonecutter | worked | 2 Planks + 1 Bricks | 150 |
| Warrior lodge | worked | 2 Planks + 2 Bricks | 150 |
| City upgrade | worked | 2 Planks + 2 Bricks + 2 CutStone + 2 Bread | 400 |
| Warrior | worked | 1 Bread + 1 Meat delivered to the lodge | 300 |
| Warrior meal | worked | 1 Bread (else 1 Meat) per 900 ticks | - |

Starting storehouse stock of each starting settlement: 4 Wood, 2 Clay, 2 Grain, 2 Sheep. New settlements start empty.

---

## 4. Data model

### 4.1 What replaces the fine ids

| Old (fine graph) | New |
|---|---|
| Flag / `Vertex` id (570) | **Corner id** (0..149, unchanged numbers: corners were flags 0..149) for settlements and road ends; **plot id** for buildings |
| Segment / `Edge` id (630) | **Hex edge id** (0..209, unchanged) for main roads and road demons |
| Building on a flag | Building on a **plot** |
| Carrier at a flag or on a segment | Carrier on a **leg** between two places (storehouse, building, road site, settlement site); position from `EconomyApi.CarrierPosition` |
| Territory (4 segments) | None: a people builds on its own settlements' plots, roads from its network |

`BoardTopology` keeps its fine members (derived, unused) for the first pass so existing game helpers still compile; package F removes them after integration (section 7).

**Plot id** = `corner * 9 + sector * 3 + slot`, where `sector` (0..2) is the index of the hex in the corner's adjacent hex ids sorted ascending, and `slot` 0..2 as in 2.B. Plot ids exist for every interior corner whether or not a settlement stands there (max 1349). Example, default board: home corner 70, sectors 0 = hex 22 Forest, 1 = hex 30 Fields, 2 = hex 31 Hills; plots 630-638; log pile on 630, woodcutter's hut proposed on 631, dance circle reserved on 635.

### 4.2 Plot geometry (`PlotLayout`, pure, in `Sim/Board`)

Let c be the corner position, h the hex centre, b = (h - c) / 60 (unit spoke), left = (-b.z, b.x) (90 degrees counter-clockwise seen from above, X east, Z north). Then:
- slot 0 centre = c + 30 b + 5√3 left; slot 1 = c + 30 b - 5√3 left; slot 2 = c + 45 b.
- `PlotRadius` 5 m, `StorehouseRadius` 3 m, `StockPatchRadius` 6 m (centred on the hex centre).
- Facing: slots 0 and 1 face the spoke point c + 30 b; slot 2 faces c.
- Path points from the storehouse: slots 0 and 1: [c, c + 30 b, plot centre]; slot 2: [c, plot centre]. `LocalSteps`: 8 and 9.
- Dance circle: the centre of the reserved plot (`Board.ReservedPlots`), radius 7 m.

### 4.3 State per area (field names binding; full shapes in appendix A)

**Board** (`WorldState.Board`): `Radius`, `HexTerrain[]`, `HexNumber[]` (2..12, 0 Meadow), `StartCorners` (int[2]: [0] player 70, [1] rival 97 on every seed), `SuggestedSettlementCorners` (int[]), `ReservedPlots` (int[]: the dance circle plot). Removed: `HomeCityVertex`, `RivalOutpostVertex`, `StorehouseVertex`, `MarketVertex`, `WarehouseVertex`, `SuggestedCitySites`.

**Economy** (`WorldState.Economy`, `EconomyState`):
- `Tuning` (`EconomyTuning`), `Dice` (`DiceState`: `Deck` int[] of remaining outcomes encoded `d1 * 10 + d2`, top = last; `Rng`; `NextRollTick`; `RollCount`; `LastD1`, `LastD2`, `LastRollTick`).
- `Hexes` (`HexStock[61]`: `Hex`, `Mature`, `Young` = list of `StockCohort{Ware, Count, AddedTick, MatureTick}` in adding order).
- `Settlements` (list, id = index): `Id, Faction, Corner, State (Site|Active), IsCity, Upgrade (None|Site|Constructing), SourceSettlement, BuildProgress, UpgradeProgress, Stock[], Reserved[], Incoming[], Delivered[] (settlement site), UpgradeDelivered[], FoundedTick, Reason, ReasonWare`.
- `Buildings` (list, id = index; retired ones stay): `Id, Type, Faction, Settlement, Plot, Hex, State (Site|Constructing|Active|Retiring|Retired), Stock[], Reserved[], Incoming[], Delivered[], BuildProgress, WorkProgress, WorkHex, Reason, ReasonWare`.
- `Roads` (list, id = index): `Id, Faction, HexEdge, State (Site|Constructing|Built), SourceSettlement, Delivered[], Incoming[], BuildProgress, Reason, ReasonWare`; `RoadOnEdge` (int[210], road id or -1).
- `Jobs` (open, ascending id): `Id, Faction, Settlement (whose carrier), Ware, Count, FromKind, FromId, ToKind, ToId, IsShipment, State (Waiting|ToPickup|Carrying), CarrierId, Reason, CreatedTick`; `NextJobId`. `PlaceKind`: Storehouse 0 (id = settlement), Building 1 (id = building), RoadSite 2 (id = road), SettlementSite 3 (id = settlement).
- `Carriers` (list, id = index): `Id, Faction, Home, State (Idle|ToPickup|Carrying|Returning), JobId, HasLoad, Load, LoadCount, LegFromKind, LegFromId, LegToKind, LegToId, LegCorners (int[]: corner path for a main-road leg, empty for a local leg), LegTicks, Progress`.
- `Warriors` (list, id = index): `Id, Faction, Lodge, Settlement, Fed, NextMealTick`.
- `Ledger` (`WareLedger`): per ware `Starting, Collected, Produced, Consumed`; sinks `RecipeIn[type][ware]`, `RecipeOut[type][ware]`, `Construction[]`, `Training[]`, `Meals[]`; hex stock per raw ware `StockAdded[], StockLost[], StockTaken[]`. Invariants in 4.4.

**Factions** (`WorldState.Factions`, `FactionsState`):
- `Tuning` (`FactionTuning`), `Factions` (list, index = `PlayerIds`): `Id, Name ("Your people", "the Ash Clan"), Auto (false for the player, true for the rival), Strength, Settlements, Cities, FedWarriors, HungryWarriors, NextPlanTick`.
- `Proposals` (open only, sorted by faction then rank): `Id, Faction, Kind (Building|Road|Settlement|City|Banish), BuildingType (-1 if none), Settlement, Plot, HexEdge, Corner, DemonSiteKind, DemonSiteId, Reason, Speaker, Score, Rank, CreatedTick, TopSinceTick`; `NextProposalId`.
- `Declined` (list of `{Faction, Key, UntilTick}`; `Key` = `Proposal.Key` string, 6.1).
- `Warning` (`Active, Attacker, Defender, StartTick, EndTick`).
- `GameOver, Outcome (None|Victory|Defeat|Draw), OutcomeReason ("overrun" | "time"), EndedTick`.
- `Casts` (per faction: spells cast), `OpeningDone` (bool per faction).

**Demons** (`WorldState.Demons`, `DemonsState`): `Tuning` (`DemonTuning`), `Corner` (int[150]), `Edge` (int[210]), `Rng`, `Placed`, `Banished`, `Outbreaks`.

**Kept**: `WorldState.Seed, Tick, Rng, Board, Players` (`PlayerState{Id, Name, Coin}`), Ward progress.

### 4.4 Invariants (tested every 100 ticks in the proof)

- Wares: for each ware, Starting + Collected + Produced = Consumed + in world (settlement stock + building stock + site deliveries + city deliveries + carrier loads).
- Hex stock: for each raw ware, StockAdded = StockLost + StockTaken + young + mature on all hexes.
- Collected (wares) = sum over collector types of units taken x yield.
- Reserved never exceeds Stock; Incoming equals the open jobs heading there.

---

## 5. Commands, events, refusals, read helpers

All commands go through `WorldSim.Submit`; `PlayerId` is the people (0 player, 1 rival). Every refusal has a code and a sentence. Systems run in this order: Dice 100, Growth 150, Collect 200, Production 300 (workshops, lodges, meals), Construction 400, Jobs 500 (demands, imports), Carriers 600, Demons 1100 (refresh blocked flags), Brain 1150 (plan; rival auto-cast), Strength 1200 (strength, warning, endings), Diagnostics 1300.

### 5.1 Commands

| Command | Fields | Who | What |
|---|---|---|---|
| `CastSpell` | `ProposalId` | normal play | Re-checks the proposal's placement and applies it (site placed, or demons banished); removes the proposal (`cast`). |
| `DeclineProposal` | `ProposalId` | normal play | Removes it (`declined`) and blocks the same key for `DeclineCooldownTicks`. |
| `PlaceBuilding` | `Type, Plot` | F1 tools, tests, CastSpell | Building site on a plot. |
| `PlaceRoad` | `HexEdge` | F1, tests, CastSpell | Main-road site; source settlement chosen by rule. |
| `FoundSettlement` | `Corner` | F1, tests, CastSpell | Settlement site; source settlement chosen by rule. |
| `UpgradeCity` | `Corner` | F1, tests, CastSpell | City upgrade of the settlement there. |
| `Banish` | `SiteKind, SiteId` | F1, tests, CastSpell | Removes every demon from one of your sites. |
| `CancelSite` | `Kind (SiteKind), Id` | F1, tests | Cancels an unfinished site; delivered goods go back to the source storehouse stock. |

Removed: `EndRound`, `SetPriority`, `ToggleBuilding`, `RemoveRoad`, old `PlaceRoad{Edge}`, old `PlaceBuilding{Type, Vertex}`, and every strategy and military command.

### 5.2 Refusal codes and sentences

Constants in `EconomyRefusals`, `FactionRefusals`, `DemonRefusals`. Sentences are exact; `{x}` is filled in.

| Code | Sentence |
|---|---|
| `game_over` | The game is over. |
| `not_owner` | That belongs to another people. |
| `unknown_type` | There is no such building. |
| `not_placeable` | {Name} comes with a camp and cannot be built. |
| `unknown_plot` | There is no plot there. |
| `no_settlement` | None of your camps owns that plot. |
| `plot_taken` | Something already stands on that plot. |
| `plot_locked` | That plot opens when the camp becomes a city. |
| `plot_reserved` | That ground is kept for the dance circle. |
| `wrong_terrain` | A {name} must stand by {terrain}. |
| `already_built` | This camp already has a {name}. |
| `bad_corner` | A camp needs a corner where three lands meet. |
| `occupied` | A camp already stands there. |
| `too_close` | Too close to another camp: leave a road's length between camps. |
| `not_connected` | None of your roads reaches there. |
| `road_exists` | A road already runs there. |
| `blocked_by_rival` | A camp of another people blocks that road. |
| `not_a_settlement` | There is no camp of yours there. |
| `already_city` | This camp is already a city. |
| `upgrade_in_progress` | This camp is already being raised to a city. |
| `not_a_site` | There is no unfinished site there. |
| `unknown_proposal` | That proposal is gone. |
| `no_longer_legal` | That can no longer be done: {underlying sentence} |
| `no_demons` | There are no demons there. |

Stall reasons keep the closed list in `Reasons` (`no_road`, `no_carrier`, `waiting_ware`, `demon_on_route`, `output_full`, `input_missing`, `no_batch`, `disabled`, ...). `no_batch` now means "no mature stock": its default sentence becomes "{subject} is waiting for {terrain} stock to grow.". `no_power`, `invalid_site` and `no_turn_action` stay in the list but are unused.

### 5.3 Events (plain fields; `Tick` stamped)

Economy: `DiceRolled{D1, D2, Sum, RollIndex}`, `StockAdded{Hex, Ware, Count, MatureTick}`, `StockLost{Hex, Ware, Count}`, `StockMatured{Hex, Ware, Count}`, `CollectStarted{BuildingId, Hex}`, `Collected{BuildingId, Hex, Ware, Amount}`, `ProductionStarted{BuildingId}`, `ProductionCycle{BuildingId, Ware, Amount}`, `SitePlaced{Kind, Id, Faction}`, `SiteCompleted{Kind, Id, Faction}`, `SiteCancelled{Kind, Id, Faction}`, `BuildingStateChanged{BuildingId, State, Reason, ReasonWare}`, `BuildingRetired{BuildingId}`, `SettlementFounded{SettlementId, Corner, Faction}`, `CityUpgraded{SettlementId, Corner, Faction}`, `RoadBuilt{RoadId, HexEdge, Faction}`, `JobCreated{JobId, Ware, Count, FromKind, FromId, ToKind, ToId, IsShipment}`, `CarrierLegStarted{CarrierId, FromKind, FromId, ToKind, ToId, LegTicks}`, `WarePickedUp{JobId, CarrierId, Ware, Count}`, `WareDelivered{JobId, CarrierId, Ware, Count, ToKind, ToId}`, `JobStalled{JobId, Reason}`, `WarriorTrained{WarriorId, Lodge, Faction}`, `WarriorFed{WarriorId, Ware}`, `WarriorHungry{WarriorId}`.

Factions: `ProposalAdded{ProposalId, Faction, Rank}`, `ProposalsReordered{Faction, TopProposalId}`, `ProposalRemoved{ProposalId, Faction, Why ("cast" | "declined" | "expired")}`, `SpellCast{ProposalId, Faction, Kind, BuildingType, Plot, HexEdge, Corner, DemonSiteKind, DemonSiteId}`, `StrengthChanged{Faction, Strength}`, `OverrunWarningStarted{Attacker, Defender, EndTick}`, `OverrunWarningCancelled{Attacker, Defender}`, `FactionOverrun{Attacker, Defender}`, `GameEnded{Outcome, Reason, PlayerStrength, RivalStrength}`.

Demons: `DemonPlaced{SiteKind, SiteId, Count}`, `DemonOutbreak{SiteKind, SiteId, SpreadKinds[], SpreadIds[]}`, `DemonsBanished{SiteKind, SiteId, Count, Faction}`.

### 5.4 Read helpers the game layer calls

Static, `WorldState` first, never mutate. Full signatures in appendix A.3.

- `PlotLayout` (geometry): `PlotId`, `CornerOf`, `SectorOf`, `SlotOf`, `SectorHex`, `SectorOfHex`, `IsSettlementCorner`, `PlotX/PlotZ/PlotYaw`, `PathPoints`, `LocalSteps`, constants.
- `EconomyApi`: dice (`LastRoll`, `TicksToNextRoll`, `Pips`), hex stock (`HexStock` -> `HexStockView` with stage counts), settlements (`Settlements`, `SettlementAt`, `SettlementName`, `StockOf`), plots (`Plot` -> `PlotView`, `PlotsOf`), buildings (`Def`, `DescribeBuilding`, `WaitingSentence`), roads (`RoadOn`), carriers (`CarrierPosition`, `DescribeCarrier`, `DescribeJob`), checks (`CheckBuilding`, `CheckRoad`, `CheckSettlement`, `CheckCity`), wares (`WareName`, `WareTitle`, `WareColour`, `CostText`).
- `FactionApi`: `Proposals`, `Top`, `GetProposal`, `ProposalTitle`, `ProposalTarget`, `ProposalAt`, `CastRangeMetres`, `Strength`, `Warning`, `Outcome`, `Headline`.
- `DemonApi`: `DemonsAt`, `DemonSites`, `SiteName`.

---

## 6. The faction AI (the prince's brain) and the rival

Port of the competent `StrategyPlayer` from the sim tests into `Sim/Factions/Brain.cs`, rewritten for the new rules. Pure, deterministic, one instance of the rules for both peoples.

### 6.1 Queue maintenance

Every `PlanIntervalTicks` = 20 per faction (player at ticks where `Tick % 20 == 0`, rival at `Tick % 20 == 10`):
1. Build the candidate list (6.2): only actions legal now, affordable now (6.2) and not declined within the cooldown.
2. Sort by score descending, ties by `Key` ordinal ascending. Keep the first `QueueLength` = 3.
3. A candidate whose `Key` matches an open proposal keeps that proposal's id and `CreatedTick` (score, rank and reason are updated). New ones get the next id (`ProposalAdded`). Open proposals not kept are removed (`expired`). If the top proposal changed, `TopSinceTick` = now and `ProposalsReordered` fires.
`Key` = `Kind|BuildingType|Plot|HexEdge|Corner|DemonSiteKind|DemonSiteId` as text.
Game over: no planning.

### 6.2 Candidates and scores

A faction plans only while it has fewer than `MaxOpenSites` = 3 unfinished sites (banish candidates ignore this limit). "Affordable" = the cost is covered by the unreserved stock of the settlements connected to the target's settlement (or source settlement) by that people's roads. The brain never reserves goods; sites claim them when placed.

| Candidate | Score | Reason sentence (template) |
|---|---|---|
| Opening: Woodcutter's hut at the starting settlement, Forest sector, slot 1 | 95, only while `OpeningDone` is false; set true when it is cast | "A woodcutter's hut brings in twice the wood of the log pile, and wood builds everything." |
| Banish at an own demon site | 100 + 10 x demons | "{Count} demon(s) sit on {site}: {effect}." |
| Improved collector for an own hex that has only its primitive | 40 + 2 x pips + 10 if its ware is the faction's scarcest raw ware for its next goal | "{Name} doubles the {ware} we take from {terrain} {number}." |
| Next main road towards the expansion target | 50 + value / 2 | "A road towards {target} lets us found a camp there." |
| Settlement at the expansion target (at a road end) | 60 + value / 2 | "A camp at {target} gives us {wares}." |
| Sawmill / Kiln (once the faction collects Wood / Clay) | 45 / 44 | "{Purpose}" |
| Stonecutter / Bakery / Smokehouse (once it collects Stone / Grain / Sheep) | 42 / 41 / 40 | "{Purpose}" |
| City upgrade (the settlement with the most buildings, ties lower corner) | 55 | "A city gives us three more plots and counts double against the Ash Clan." |
| Warrior lodge (once a city exists) | 38 | "Fed warriors make us stronger against the Ash Clan." |
| Under an overrun warning, as defender | +50 on settlement, city and warrior lodge candidates | "{reason} The Ash Clan is gathering: this keeps us standing." |

Expansion target: the best legal settlement corner within `ExpansionReachEdges` = 3 hex edges of the faction's network, value = sum of pips of its resource hexes + 6 per raw ware the faction does not yet collect + 3 per distinct terrain - 4 per road edge still needed; ties by lower corner id. The road candidate is the next edge of the shortest corner path (by hex edges, ties by lower corner ids) from the network to the target over legal edges.

Plot choice: a collector goes on the free plot of its hex's sector, preferring slot 1, then 0, then 2. A workshop goes to the settlement that collects its first input (the lowest settlement id if several), else the lowest settlement id with a free plot; within it the free plot with the lowest id.

S may tune scores and constants to meet the proof's targets, but must keep: the opening rule, one-sentence reasons, determinism, and the queue mechanics of 6.1.

### 6.3 Speakers

`Proposal.Speaker` is a villager id fixed by rule: `"sawyer"` for Woodcutter's hut and Sawmill proposals after the opening; `"merchant"` for main roads; `"prince"` for everything else, including the opening and banishing.

### 6.4 The rival

The Ash Clan runs the same brain. In the Brain system, if its top proposal has been top for at least `RivalCastDelayTicks` = 450, the brain applies it exactly as `CastSpell` would (same validation, same events, including `SpellCast` with `Faction` 1). It never declines. Its war camp and warriors are drawn by V; its proposals are in state but not shown in normal play (the F1 tools show them).

---

## 7. Keep, change, remove

Save version: `SaveCodec.CurrentVersion` = 4, `OldestVersion` = 4 (version 3 saves are refused with "The save is from an older version (3) with a different world."). `CreateSim` registers Economy, Factions, Demons in that order.

Archive: removed code that is worth re-attaching later moves to `Archive/` at the repo root (outside `Assets`, so neither Unity nor `dotnet test` compiles it; drop the `.meta` files). The last working MVP is commit `189466f` on `main`.

### 7.1 Simulation (`Assets/World/Sim`)

| File / system | Fate |
|---|---|
| `Core/WorldState`, `Rng`, `Commands`, `Events`, `WorldSim`, `SystemOrder` constants | keep |
| `Core/Ware.cs` | change: 10 wares (3.1) |
| `Core/Reasons.cs` | change: `no_batch` sentence only |
| `Core/WorldSimFactory.cs` | change: Economy, Factions, Demons modules; no Strategy or Military |
| `Board/HexCoord.cs` | change: `TerrainInfo.RawWare` to the new wares |
| `Board/BoardTopology.cs` | keep (fine members unused; removed by F after integration) |
| `Board/Board.cs` | change: fields of 4.3 |
| `Board/BoardGenerator.cs` | change: numbers 2..12 (2.A.1), `StartCorners`, `SuggestedSettlementCorners`, `ReservedPlots`; layout and start corners unchanged |
| `Board/PlotLayout.cs` | new |
| `Economy/*` | rewrite in place (dice, stock, settlements, plots, buildings, logistics, warriors); file names free, public names per appendix A |
| `Factions/*` | new (brain, proposals, spells, strength, warning, endings) |
| `Demons/*` | new |
| `Strategy/*` (cards, wonder, ages, draft, hero, market, infection deck) | remove to `Archive/Sim/Strategy` |
| `Military/*` (power, barracks, squads, outpost, raids) | remove to `Archive/Sim/Military` |
| `Encounters/Ward/*`, `Encounters/Rhythm/*` | keep unchanged |
| `Save/SaveCodec.cs` | change: version 4, `CreateSim` modules |

### 7.2 Sim tests (`Assets/World/Tests/EditMode`)

| File | Fate |
|---|---|
| `RngTests`, `WardTests`, `WardBalanceTests`, `RhythmTests`, `RhythmPackData` | keep |
| `WorldSimTests`, `BoardTopologyTests`, `BoardGeneratorTests`, `SaveTests` | change (new numbers, start corners, v4, JSON round trip of the new state) |
| `EconomyTests`, `UnifiedEconomyTests`, `EconomySimulationTests`, `EconomyTestKit`, `BalanceProbe`, `GameRuns`, `StrategyTests`, `StrategyTestKit`, `StrategyConservationTests`, `StrategyPlaythroughTests`, `MilitaryTests` | remove to `Archive/Tests/EditMode` |
| `StrategyPlayer` | remove to the archive after S has ported its logic into `Sim/Factions/Brain.cs` |
| `EconomyProofReport` | rewrite as `ProofReport` (section 8, S) |
| new | `PlotLayoutTests`, `DiceAndStockTests`, `SettlementTests`, `LogisticsTests`, `BrainTests`, `SpellTests`, `DemonTests`, `OverrunTests`, `ConservationTests`, `ApiSurfaceTests` (reflection: every public name in appendix A exists with that signature) |

### 7.3 Game (`Assets/World/Game`)

| Area | Fate | Package |
|---|---|---|
| `Boot/WorldBootstrap`, `GameClock` (drop `PauseReasons.Draft` and the draft step) | change | F-0 then P |
| `Boot/AutoPlay` | change: lattice steps (`Step`, `Turn`, `WalkTo(cell)`, `Follow`) | P |
| `Boot/SaveSystem` | change: v4 extras | F |
| `Boot/SaveLoadScript` | change | F |
| `Player/FirstPersonAvatar` | replace by `PlayerWalker` | P |
| `Player/ViewSwitcher`, `GodCamera`, `Picker`, `BoardPicker` | change: God View only under F1; Tab is the map; `Picker` becomes `PointerInput` | P |
| `UI/Hud` (HUD strip, pause menu shell), `UI/InspectPanel`, `UI/DialoguePanel` (becomes speech bubbles) | change | P |
| `UI/God/*` (toolbar, Build, Road, Problems, Wares, God commands, overlay) | change: F1 developer tools on the new commands; `CommissionDriver` removed; `TravelTool` removed | P |
| `Board/BoardSpace` | change: corners, hex edges, plots; no flags or segments | V |
| `View/TerrainBuilder` | change: plot and stock-patch clearings, number tokens, peaks off plots | V |
| `View/Economy/*` | rewrite as land views (`View/Land/*`) | V |
| `View/OutpostBuilder` | remove (the rival is ordinary settlements) | V |
| `Strategy/*` (views, HUD, panels, God tools, `FullGameDriver`) | remove to `Archive/Game/Strategy` | F-0 |
| `View/VillageBuilder`, `VillageLayout`, `WarehouseSet`, `Signs` | remove; `CampLayout`, `CampBuilder`, `DanceCircle` new | D |
| `Encounters/RhythmPartyEncounter` | change: dance circle, drums, ending choice | D |
| `Encounters/WardDuel*`, `EncounterHost`, `EncounterUi`, `WardBot` | keep (Mira moved by D) | - |
| `Actors/Npc`, `PlaceholderActor` | keep (Npc gains walking: `WalkTo`, `Follow`) | N |
| `Actors/VillageNpcs` | replace by `Prince/*` (prince, sawyer, merchant) and D's Mira and Sol | N, D |
| `Audio/*`, `Rendering/*`, `Shaders/*` | keep | - |

### 7.4 PlayMode tests (`Assets/World/Tests/PlayMode`)

`WorldBootTests` change (F-0). `VillageTests`, `EconomyViewTests`, `StrategyViewTests`, `MapLayoutTests` move to the archive. `EncounterTests` change (D: dance; duel untouched). `SaveLoadTests` change (F). Each package adds its own file (section 8).

### 7.5 Docs

| Doc | Fate | Owner |
|---|---|---|
| `REFACTOR_SPEC.md` | this file; S may append to the "API change log" at the end of appendix A only | coordinator |
| `ECONOMY_DESIGN.md`, `ECONOMY_API.md`, `ECONOMY_PROOF.md` | rewrite for the new rules | S |
| `CONTRACTS.md` | sim sections rewrite (S), game sections (F) | S, F |
| `STRATEGY_API.md`, `STRATEGY_UI.md`, `ECONOMY_UI.md`, `MAP_MIGRATION.md` | move to `Archive/Docs` | F-0 |
| `PLAYER_API.md` (new), `LAND_UI.md` (new), `DANCE_UI.md` (new), `PRINCE_API.md` (new) | API notes per package | P, V, D, N |
| `GAME_API.md`, `HANDOFF.md`, `PLAY_GUIDE.md`, `TEST_PLAN.md`, `README.md`, `PROGRESS.md`, `CLAUDE.md` | update | F |
| `RHYTHM_API.md` | add the dance section | D |

---

## 8. Work packages

### 8.0 Order and branches

1. **S-0** (S): the public sim surface of appendix A compiles under `dotnet test DotNet`, with the board, plot layout, dice and stock working and the rest as simple first versions. Old sim and test code archived. Tagged commit `pre-S0` on `refactor/prehistoric`.
2. **F-0** (F): from `pre-S0`: the Unity project compiles with 0 errors. Old game code that no longer compiles is archived (7.3); `WorldBootstrap.Awake` calls, in order, `PlayerInstaller.Install(this)`, `CampInstaller.Install(this)`, `LandViewsInstaller.Install(this)`, `PrinceInstaller.Install(this)`, `DevToolsInstaller.Install(this)`; each is an empty static class created by F-0 in its owner's folder (from then on owned by that package). Tagged `pre-F0`.
3. **P, D, V, N and the rest of S** in parallel, each on its own branch from `pre-F0` (`pre/p`, `pre/d`, `pre/v`, `pre/n`, `pre/s`), each touching only the files it owns. Merge into `refactor/prehistoric` in the order S, P, V, D, N.
4. **F-1** (F): save, guide, build, docs, final runs.

If S must change a name in appendix A after S-0, it adds the new name, keeps the old one working, and records both in the API change log; nobody else edits appendix A.

### 8.1 (S) Simulation and proof

Owns: `Assets/World/Sim/**`, `Assets/World/Tests/EditMode/**`, `DotNet/**`, `Archive/Sim/**`, `Archive/Tests/EditMode/**`, docs per 7.5.

Builds: everything in sections 2.A-2.C, 3, 4, 5, 6 and the proof.

Assumes nothing from the others.

Proof (`ProofReport` test writes `Docs/ECONOMY_PROOF.md`; it must be byte-identical across two runs of the same seeds). Scripted players: **prompt** (casts the player's top proposal after a modelled delay: 5 s talk + walking time at 12.5 m/s on the straight line from the previous cast target, plus 5 s), **slow** (casts after 90 s), **decliner** (declines each new top proposal once, then casts the next), **ignored** (never casts). Required sections and pass criteria:

| Section | Content | Pass |
|---|---|---|
| A. Ware audit | every ware's producers and consumers; every building type with its purpose; use counts in the prompt games | every ware has at least one producer and one consumer; every placeable type is built in at least one prompt game |
| B. Traced chains | one tick-stamped trace per raw ware from the roll to its final use (e.g. roll 6 at t, Forest +4 saplings, mature at t + 750, log pile cycle, carrier to the storehouse, delivered to a road site / Sawmill, Planks into a city upgrade) | five complete chains, each ending in a consumption |
| C. Cross-system links | paired runs on one seed, each changing one thing: a demon on a settlement (its collections stop), a demon on a road (a shipment waits or reroutes), declining the top proposal (a different build happens), cast delay 0 / 60 s / never (outcome changes), unfed warriors (strength drops, a warning starts), an uncollected hex (stock lost to the cap) | each pair shows the stated difference |
| D. Conservation | 4.4 checked every 100 ticks over 10 seeds x 20 minutes with random legal casts | zero drift |
| E. Endings | 10 seeds per scripted player | prompt wins by overrun in at least 8 of 10, median game length 10-15 minutes; ignored is overrun in 10 of 10 before the time limit; slow and decliner reported; one forced-tuning run reaches the time-limit draw |
| F. Balance | per seed: time to woodcutter's hut, second camp, first workshop, city, first warrior, warning, ending; per ware produced, consumed, lost to the cap; which ware the top proposal waited for most; dice histogram against expectation; demons placed, banished, outbreaks; strength of both peoples per minute | the rival never goes 3 minutes without a proposal; no ware is lost to the cap above 15% |
| G. Known weaknesses | honest list | present |

Acceptance: `dotnet test DotNet` all green (including `ApiSurfaceTests` and determinism: two runs of one seed give identical state JSON at tick 12000); proof criteria met; save round trip of a mid-game state with a shipment in flight continues identically; `ECONOMY_API.md` matches the code.

### 8.2 (P) Player movement, pointer, speech bubbles, map, HUD toggle, F1 tools

Owns: `Assets/World/Game/Player/**`, `UI/**` except `UI/GuideText.cs`, `Boot/AutoPlay.cs`, `Boot/GameClock.cs`, `Boot/WorldBootstrap.cs` (after F-0), `PlayerInstaller`, `DevToolsInstaller`, `Tests/PlayMode/PlayerTests.cs`, `Docs/PLAYER_API.md`.

Builds: `WalkLattice`, `WalkGrid`, `PlayerWalker` (2.D), `PointerInput`, inspect bubbles, `SpeechBubbles`, `SpellCaster` (ground press to `CastSpell`, range check), follow and auto-walk, `MapScreen` (Tab), HUD strip (H), pause menu shell, F1 developer tools (God View camera and toolbar; Build on plots, Road on hex edges, Camp on corners, City, Banish, Cast any proposal, Inspect, Problems, Wares), AutoPlay lattice steps. Interfaces in appendix B.

Assumes: S-0 helpers; V's and N's components implement `IInspectable`, `IFollowable`, `ISpeaker`; V and D register static blockers with `WalkGrid`.

Acceptance (PlayMode): every corner, hex centre and plot centre maps to an exact cell and back (under 1 mm); walking a hex edge is 12 forward steps without turning; six turns return the heading; a blocked cell bumps; follow ends 2 cells behind a walking NPC; a ground press within 15 m of an open proposal submits `CastSpell` and out of range shows the sentence; map and HUD open and close with no state change and no commands; F1 shows the God View tools and a dev build places a site; touch strips map to the four moves; no `WorldState` writes (grep).

### 8.3 (D) Dance circle and prehistoric camp dressing

Owns: `Assets/World/Game/View/Camp/**` (`CampLayout`, `CampBuilder`, `DanceCircle`), `CampInstaller`, `Encounters/RhythmPartyEncounter.cs`, Mira's and Sol's spawn (in `View/Camp`), `Tests/PlayMode/DanceTests.cs`, changes to `Tests/PlayMode/EncounterTests.cs`, `Docs/DANCE_UI.md`, the dance section of `RHYTHM_API.md`. Deletes `View/VillageBuilder.cs`, `VillageLayout.cs`, `WarehouseSet.cs`, `Signs.cs`.

Builds: the dance circle at the reserved plot (fire, four drums, log seats), drums that light with cues, D F J K and touch strips, the ending choice (2.E), Sol the drummer and Mira at the home camp, prehistoric dressing round the home camp's hearth (hide tents, drying racks, a few figures) kept off plots, stock patches, the lattice lines of hex edges and the cells round the corner. Registers the fire and drums with `WalkGrid.AddStaticBlocker`. `CampLayout` gives the spawn cell and heading (the cell 4 steps from the home corner along the Forest spoke, facing the prince's approach) and the prince's start cell.

Assumes: S-0 (`PlotLayout`, `Board.ReservedPlots`, `StartCorners`); P's `WalkGrid`, `PlayerWalker.PlaceExact`, touch strips; V draws every settlement's storehouse and hearth (D only dresses around the home one).

Acceptance: `RhythmTests` unchanged and green; the dance runs a full phrase under the autoplayer with the same judgements as before; drum lighting times equal cue time minus the fall time; ending choice in the window, refused outside; no object named warehouse, cottage, market, bell or square remains (test); dressing never overlaps a plot, a stock patch or a hex-edge line (test); `first_five` becomes `duel_dance` and passes.

### 8.4 (V) World views

Owns: `Assets/World/Game/View/Land/**`, `View/TerrainBuilder.cs`, `Board/BoardSpace.cs`, `LandViewsInstaller`, `Tests/PlayMode/LandViewTests.cs`, `Docs/LAND_UI.md`. Deletes `View/Economy/**` and `View/OutpostBuilder.cs`.

Builds, all reconciled from state after each `GameClock.Ticked` with `RebuildFromState()`: hex tiles and number tokens (posts at the hex centre, lit for 3 s when their number is rolled); stock patches (saplings, lambs, seedlings in three growth stages and mature; clay and stone shown only when mature, as dug piles); settlements (store pile and hearth at the corner, people's colour, city version); empty plots (faint stone ring, only for settlements that exist); buildings and sites per type (primitive collectors look primitive), a builder figure on constructing sites; local paths; main roads and road sites; carriers and shipment porters from `EconomyApi.CarrierPosition`, load in the ware colour; collectors' gatherers walking to the stock patch during a cycle; warriors at their lodge; the rival's war camp while a warning runs and the overrun moment; demons on corners and hex edges; proposal ground markers for open player proposals (top one brighter, matching `FactionApi.ProposalTarget`); the roll effect. Implements `IInspectable` on everything and `IFollowable` on carriers. Registers mountain peaks with `WalkGrid`; keeps props and peaks off plots, stock patches and hex-edge lines.

Assumes: S-0 helpers and events; P's `WalkGrid` and interfaces; D dresses only the home camp.

Acceptance: views equal state after a save load (`RebuildFromState` counts and positions); stage counts on a hex equal `HexStockView`; a carrier's view position equals `CarrierPosition` within 0.05 m; proposal markers equal the open proposals; demons, war camp and retirements appear and disappear with their events; no per-frame scans over all 1350 plot ids or 210 hex edges (only existing settlements, roads and carriers); frame rate on the default seed at 20 minutes of prompt play above 120 fps at 1920x1080 in the editor.

### 8.5 (N) Prince, proposals, conversation, first scene

Owns: `Assets/World/Game/Prince/**`, `Actors/**`, `PrinceInstaller`, `Tests/PlayMode/PrinceTests.cs`, AutoPlay scripts `first_scene` and `prompt_game`, `Docs/PRINCE_API.md`. Deletes `Actors/VillageNpcs.cs`.

Builds: the prince, sawyer and merchant (`Npc` with walking), the pitch loop (the proposal's speaker walks up and pitches the top proposal of the player's people when it changes, then every 40 game seconds while it stays; replies "Show me" (leads, the player follows), "Not this" (`DeclineProposal`; he pitches the next one), "Later" (he waits 60 s); after three pitches without an answer he says a resigned line and keeps to 8 m behind the player for 90 s before pitching again); at the spot he explains and waits for the cast; reactions to `SpellCast`, `SiteCompleted`, `ProposalRemoved`, `OverrunWarningStarted`, `DemonPlaced` (one line each); the first scene of 2.C.5 with the exact lines, and its step saved in the extras key `scene.first` (an int); "follow" on carriers through P. Never pauses the clock.

Assumes: S-0 `FactionApi`; P's `SpeechBubbles`, `PlayerWalker`, `PointerInput`, `IFollowable`; V's proposal markers and carrier views; D's `CampLayout.PrinceStart`.

Acceptance: `first_scene` AutoPlay: the prince reaches the player within 30 s, his three lines and replies are exact, each reply path works, the cast at plot 631 starts the woodcutter's hut, it completes, a carrier takes a load of wood to the storehouse with the player following, 0 console errors; `prompt_game` (every command through the player paths: walking, replies, ground presses) reaches Victory between 10 and 15 game minutes on the default seed; a declined proposal is not pitched again within 60 s; ignoring for 20 minutes ends in Defeat by overrun.

### 8.6 (F) Save, guide, build

Owns: F-0 skeleton (archiving, installer stubs, `WorldBootTests`), `Boot/SaveSystem.cs`, `Boot/SaveLoadScript.cs`, `UI/GuideText.cs`, `Tests/PlayMode/SaveLoadTests.cs`, `AgentScripts/**`, `Archive/Game/**`, `Archive/Docs/**`, the docs listed for F in 7.5; at the end, removal of the fine graph from `BoardTopology` and its tests (coordinated: after every package has merged, only if nothing references it).

Builds: save v4 with extras `walk.i`, `walk.j`, `walk.heading`, `hud`, `scene.first`, `seed` (pose kept in `PlayerPose`); load through the scene reload as now; the pause-menu guide pages (Controls, How to play) from `GuideText` matching the real keys; `PLAY_GUIDE.md`; Windows build; packaged smoke runs.

Assumes: everything else merged.

Acceptance: save with a shipment in flight and the first scene half done, load, the shipment arrives once and the scene resumes at its step; version 3 refused with its sentence; Unity EditMode and PlayMode all green, console 0 errors; Windows build succeeds; packaged `boot_tour`, `first_scene`, `duel_dance`, `prompt_game`, `save_load` each exit 0; `PROGRESS.md` says what ran and what did not.

---

## 9. Open risks

1. **Interface drift between parallel packages.** Five agents code against appendix A and B before S has finished the behaviour. A late rename or a changed meaning (for example a carrier leg's path or a proposal's target point) breaks V, N and P at merge. Mitigation: S-0 lands the surface first with `ApiSurfaceTests`; changes are additive and logged.
2. **Pacing of the prince loop against the rival.** The 10-15 minute win for a prompt player and the overrun of an idle one depend on human walking time, conversation time and the rival's 45 s cast delay. The proof models the human crudely (straight-line walking, fixed talk time); the real game can feel slow (about 20 casts in 12 minutes, one every 36 s) or the rival can win against a reasonable player. The numbers are tuning, but the loop may need a design change after the first playtest.
3. **Per-settlement stock and shipments.** Hub-and-spoke logistics with imports between storehouses is new and can starve or deadlock (a workshop input imported by two settlements, a demon on the only road, carriers busy with shipments). The conservation and cross-link sections catch accounting errors, not bad flow.
4. **Lattice movement feel and travel time.** 5 m steps glided over 0.4 s cross the 800-950 m island in about a minute of holding a key; turning in 60-degree steps with a free pointer may feel stiff. Auto-walk from the map and "follow" carry much of the travel.
5. **Demon frequency.** A 7 comes on one roll in six, so about one demon a minute after the grace period, shared by both peoples; this may dominate the player's time or be trivial, depending on distances.
6. **Shared dice and shared hexes.** Both peoples' home hexes have the same numbers, and two peoples' collectors can compete on one hex; deterministic but possibly felt as unfair.
7. **Archived features re-attach cost.** Cards, the wonder and the military were written against the flag economy; re-attaching them is a port, not a switch.

---

## 10. Choices made by the spec writer

1. Roll every 10 s (`RollIntervalTicks` 100), first roll at 10 s.
2. 7s do nothing during the first 3 minutes (`DemonGraceTicks` 1800), so the first scene is calm.
3. All terrains grow in 75 s (`GrowTicks` 750, a per-terrain table), three visible young stages of equal length; hex cap 12; batch 4.
4. Hexes next to each starting camp begin with 4 mature units.
5. Number dealing: both starting camps get Forest 6, Hills 8, Fields 5; Mountains get {5, 6, 9, 10}; the rest from a fixed 28-number pool; no Catan red-number adjacency rule.
6. Mountain gives Stone (Ore reserved as ware id 10 for a later age); Hills give Clay.
7. Settlements only on interior corners (three board hexes); a Meadow sector gets no primitive collector.
8. Plot layout: three slots per sector (two for a camp, the third for a city): 6 plots per camp, 9 per city, positions at 30 m left/right and 45 m along the spoke.
9. The dance circle takes the home camp's Fields-sector slot 2 (plot 635 on the default board), so the home city has 8 plots.
10. Improved collectors are built on another plot of the same sector, double the yield (2 per unit) and retire the primitive collector when finished.
11. At most one building of each type per settlement.
12. Tier 1 buildings (improved collectors, Sawmill, Kiln) cost raw goods; tier 2 (Bakery, Smokehouse, Stonecutter, Warrior lodge) cost worked goods. The owner's rule names only roads and settlements (raw) and cities and soldiers (worked).
13. Worked goods: Planks, Bricks, Bread, Meat, Cut stone; recipes and times as in 3.2.
14. A new camp costs the Catan set (1 Wood + 1 Clay + 1 Grain + 1 Sheep); each starting camp gets 2 Sheep in its starting stock (4 Wood, 2 Clay, 2 Grain, 2 Sheep), since neither start touches a pasture.
15. City upgrade costs 2 Planks + 2 Bricks + 2 Cut stone + 2 Bread and draws straight from its own storehouse without carriers.
16. Logistics: hub-and-spoke (every output goes to the storehouse, every input comes from it); imports in shipments of up to 4 from the nearest connected camp with spare stock; carriers per camp (3 / 2 / +1 per building / +1 city), 3.3 m/s, always return home; local trip lengths fixed at 8 or 9 steps.
17. Road and settlement sites draw from a source camp chosen at placement (nearest connected by road edges).
18. The builder on a site is a view figure only; the sim has build time, not a builder agent.
19. Strength: camp 1, city 2, fed warrior 1; overrun margin 3; warning 90 s; one warning at a time; no contact or distance condition (with two peoples, the other is always "the neighbour").
20. Win only by overrunning the rival; lose only by being overrun; at 20 minutes the stronger people wins, equal is a draw. No victory points.
21. Warriors train automatically at a lodge (no spell per warrior), max 3 per lodge, eat 1 Bread (else Meat) every 90 s straight from the storehouse, hungry warriors count 0 and are not removed.
22. The rival uses the same brain and auto-casts its top proposal after it has been top for 45 s; it never declines.
23. Demons: one per 7 on a site drawn uniformly from both peoples' camps and built roads; cap 3, outbreak on the 4th to neighbouring sites, no breach track. A demon-held camp's collectors stop and it sends and receives no shipments; workshops go on.
24. Banishing is a spell proposed by the prince (cast on the demon site, removes all demons there); a people can only banish its own sites; no warriors or hero against demons.
25. `CastSpell` takes only a proposal id; the sim does not check the player's position; the game layer allows the press only within 15 m (`CastRangeMetres`, in the sim tuning so both sides read one number). Casts need a proposal: pressing ground without one does nothing.
26. Direct build commands stay in the sim for the F1 tools and tests; the sim does not refuse them in normal play (the game simply does not expose them).
27. The brain proposes only what its people can pay now and plans only while fewer than 3 sites are unfinished; queue of 3; declined proposals are suppressed for 60 s; ids are kept while the same action stays in the queue.
28. Speakers: sawyer for wood collection and the Sawmill (after the opening), merchant for main roads, prince for the rest.
29. Walking lattice: 5 m steps, origin at the board centre, glide 0.4 s per step and 0.25 s per turn, fixed pitch -8 degrees, keys WASD / arrows / numpad (Dungeon Master layout on the numpad), touch strips in the bottom 30% of the screen; settlement corners and active buildings block, empty plots and sites do not; trees never block.
30. The map is read-only; tapping sets an auto-walk target (no teleport); "Go here" fast travel is removed.
31. HUD off at the start.
32. The dance ending choice replaces bar/balcony: "Fire leap" (`Route.Bar`) and "Spirit call" (`Route.Balcony`), cosmetic and not saved; the sim's `Route` enum is not renamed.
33. Factions named "Your people" and "the Ash Clan"; the player-facing word for a settlement is "camp".
34. Strategy and Military code, their tests and views move to `Archive/` (not compiled), rather than being kept compiling and switched off; commit `189466f` holds the working MVP.
35. Save version 4 refuses version 3 saves (no migration).
36. The fine graph stays in `BoardTopology` during the first pass (unused) and is removed by F at the end.
37. Work order S-0, F-0, then P, D, V, N and S in parallel, then F-1, with per-package branches and installer stubs created by F-0.

---

## Appendix A. Simulation interface (binding names)

C# 9, namespace as noted, plain data with public fields. Types not shown in full follow the field lists of 4.3.

### A.1 Enums and constants

```csharp
namespace World.Sim {
  public enum Ware { Wood = 0, Clay = 1, Grain = 2, Sheep = 3, Stone = 4,
                     Planks = 5, Bricks = 6, Bread = 7, Meat = 8, CutStone = 9 }   // 10 = Ore, later
  public static class Wares { Count = 10; RawCount = 5; All; Raw; IsRaw(w); DisplayName(w); Title(w); }
  public static class PlayerIds { Human = 0; Rival = 1; Count = 2; }
  public sealed class Board { int Radius; Terrain[] HexTerrain; int[] HexNumber; int[] StartCorners;
                              int[] SuggestedSettlementCorners; int[] ReservedPlots; BoardTopology Topology /*[JsonIgnore]*/; }
  public static class PlotLayout {
    const int PlotsPerCorner = 9, SlotsPerSector = 3, CitySlot = 2;
    const double PlotRadius = 5, StorehouseRadius = 3, StockPatchRadius = 6, DanceCircleRadius = 7, StepLength = 5;
    int PlotId(int corner, int sector, int slot);  int CornerOf(int plot);  int SectorOf(int plot);  int SlotOf(int plot);
    bool IsSettlementCorner(BoardTopology t, int corner);            // three board hexes
    int SectorHex(BoardTopology t, int corner, int sector);           // adjacent hex ids ascending
    int SectorOfHex(BoardTopology t, int corner, int hex);            // -1 if not adjacent
    int PlotHex(BoardTopology t, int plot);
    double PlotX(BoardTopology t, int plot);  double PlotZ(BoardTopology t, int plot);
    double PlotYaw(BoardTopology t, int plot);                        // degrees, 0 = north, clockwise
    double[] PathPoints(BoardTopology t, int plot);                   // x0,z0,x1,z1,... from the corner
    int LocalSteps(int plot);                                         // 8 (slots 0,1) or 9 (slot 2)
  }
}
namespace World.Sim.Economy {
  public enum BuildingType { LogPile, ClayScrape, GrainBasket, ThornFold, RockHeap,
                             WoodcutterHut, ClayPit, Farm, ShepherdHut, Quarry,
                             Sawmill, Kiln, Bakery, Smokehouse, Stonecutter, WarriorLodge }   // 0..15
  public enum BuildingClass { PrimitiveCollector, ImprovedCollector, Workshop, Lodge }
  public enum BuildingState { Site, Constructing, Active, Retiring, Retired }
  public enum SettlementState { Site, Active }
  public enum UpgradeState { None, Site, Constructing }
  public enum RoadState { Site, Constructing, Built }
  public enum SiteKind { Building = 0, Road = 1, Settlement = 2, City = 3 }
  public enum PlaceKind { Storehouse = 0, Building = 1, RoadSite = 2, SettlementSite = 3 }
  public enum JobState { Waiting, ToPickup, Carrying }
  public enum CarrierState { Idle, ToPickup, Carrying, Returning }
  public sealed class BuildingDef { BuildingType Type; string Name; string Purpose; BuildingClass Class;
     Terrain Terrain /*collectors*/; int[] Cost; int[] Inputs; int[] Outputs; int Yield; int WorkTicks;
     int BuildTicks; bool Placeable; }
  public static class BuildingDefs { int Count = 16; BuildingDef Get(BuildingType t); IReadOnlyList<BuildingDef> All;
     BuildingType PrimitiveFor(Terrain t); BuildingType ImprovedFor(Terrain t); }
  public sealed class EconomyTuning { int RollIntervalTicks = 100, FirstRollTick = 100, BatchSize = 4, HexCap = 12,
     StartMature = 4; int[] GrowTicks /*per Terrain, all 750*/; int CarrierTicksPerStep = 15, ShipmentSize = 4,
     InputBuffer = 2, CollectorStore = 4, MealTicks = 900, MealRetryTicks = 100, WarriorsPerLodge = 3;
     int[] StockTarget; int[] StartStock; int RoadCost[], SettlementCost[], CityCost[], WarriorCost[];
     int RoadBuildTicks = 100, SettlementBuildTicks = 300, CityBuildTicks = 400;
     int StartCarriers = 3, NewSettlementCarriers = 2, CarriersPerBuilding = 1, CarriersPerCity = 1; }
}
namespace World.Sim.Factions {
  public enum ProposalKind { Building = 0, Road = 1, Settlement = 2, City = 3, Banish = 4 }
  public enum GameOutcome { None, Victory, Defeat, Draw }
  public static class OutcomeReasons { Overrun = "overrun"; Time = "time"; }
  public static class Speakers { Prince = "prince"; Sawyer = "sawyer"; Merchant = "merchant"; }
  public sealed class FactionTuning { int StrengthPerSettlement = 1, StrengthPerCity = 2, StrengthPerFedWarrior = 1,
     OverrunMargin = 3, WarningTicks = 900, TimeLimitTicks = 12000, PlanIntervalTicks = 20, QueueLength = 3,
     DeclineCooldownTicks = 600, MaxOpenSites = 3, RivalCastDelayTicks = 450, ExpansionReachEdges = 3;
     double CastRangeMetres = 15; }
}
namespace World.Sim.Demons {
  public enum DemonSiteKind { Corner = 0, HexEdge = 1 }
  public sealed class DemonTuning { int GraceTicks = 1800, Cap = 3, PerSeven = 1; }
}
```

### A.2 Commands (plain public fields; `PlayerId` = people)

```csharp
World.Sim.Factions:  CastSpell { int ProposalId; }   DeclineProposal { int ProposalId; }
World.Sim.Economy:   PlaceBuilding { BuildingType Type; int Plot; }   PlaceRoad { int HexEdge; }
                     FoundSettlement { int Corner; }   UpgradeCity { int Corner; }   CancelSite { SiteKind Kind; int Id; }
World.Sim.Demons:    Banish { DemonSiteKind SiteKind; int SiteId; }
```
Refusal constants: `EconomyRefusals`, `FactionRefusals`, `DemonRefusals` with the codes of 5.2.

### A.3 Read helpers

```csharp
namespace World.Sim.Economy {
  public struct DiceView { int D1, D2, Sum, RollIndex; long Tick; }
  public struct HexStockView { int Hex; Terrain Terrain; Ware? Ware; int Number, Pips, Mature, Young,
     Stage0, Stage1, Stage2, Cap; long NextMatureTick /*-1 none*/; bool YoungVisible /*false for Hills, Mountain*/;
     bool Stopped /*every adjacent finished camp demon-held*/; }
  public struct PlotView { int Plot, Corner, Sector, Slot, Hex; Terrain Terrain; double X, Z, Yaw;
     int SettlementId /*-1*/; PlotUse Use; int BuildingId /*-1*/; }
  public enum PlotUse { NoSettlement, Locked, Reserved, Free, Site, Building }
  public struct BuildingInfo { string Title, Status, RecipeText, CostText; string[] StockLines; }

  public static class EconomyApi {
    DiceView LastRoll(WorldState s);  long TicksToNextRoll(WorldState s);  int Pips(int number);
    HexStockView HexStock(WorldState s, int hex);
    IReadOnlyList<Settlement> Settlements(WorldState s);  Settlement SettlementAt(WorldState s, int corner); // null
    string SettlementName(WorldState s, int settlementId);   // "the home camp", "the camp by the pasture and hills"
    int StockOf(WorldState s, int settlementId, Ware w);     // unreserved storehouse stock
    PlotView Plot(WorldState s, int plot);  int[] PlotsOf(WorldState s, int settlementId);  // usable plots
    BuildingDef Def(BuildingType t);  BuildingInfo DescribeBuilding(WorldState s, int buildingId);
    string WaitingSentence(WorldState s, SiteKind kind, int id);  // null when progressing
    Road RoadOn(WorldState s, int hexEdge);                  // null
    bool CarrierPosition(WorldState s, int carrierId, double tickFraction, out double x, out double z, out double yaw);
    string DescribeCarrier(WorldState s, int carrierId);  string DescribeJob(WorldState s, int jobId);
    CommandResult CheckBuilding(WorldState s, int faction, BuildingType t, int plot);
    CommandResult CheckRoad(WorldState s, int faction, int hexEdge);
    CommandResult CheckSettlement(WorldState s, int faction, int corner);
    CommandResult CheckCity(WorldState s, int faction, int corner);
    string WareName(Ware w);  string WareTitle(Ware w);  string WareColour(Ware w) /*"#RRGGBB"*/;  string CostText(int[] cost);
  }
}
namespace World.Sim.Factions {
  public struct StrengthView { int Faction, Settlements, Cities, FedWarriors, HungryWarriors, Total; }
  public struct WarningView { bool Active; int Attacker, Defender; long TicksLeft; string Sentence; }
  public struct OutcomeView { bool GameOver; GameOutcome Outcome; string Reason; long EndedTick; string Sentence; }
  public static class FactionApi {
    IReadOnlyList<Proposal> Proposals(WorldState s, int faction);   // open, by rank
    Proposal Top(WorldState s, int faction);                        // null
    Proposal GetProposal(WorldState s, int proposalId);             // null when gone
    string ProposalTitle(WorldState s, int proposalId);             // "Woodcutter's hut", "Road to the pasture corner"
    bool ProposalTarget(WorldState s, int proposalId, out double x, out double z, out double pressRadius);
    int ProposalAt(WorldState s, int faction, double x, double z);  // open proposal whose press area holds the point, or -1
    double CastRangeMetres(WorldState s);
    StrengthView Strength(WorldState s, int faction);
    WarningView Warning(WorldState s);  OutcomeView Outcome(WorldState s);  string Headline(WorldState s);
  }
}
namespace World.Sim.Demons {
  public struct DemonSiteView { DemonSiteKind Kind; int Id, Count, Faction; double X, Z; string Effect; }
  public static class DemonApi {
    int DemonsAt(WorldState s, DemonSiteKind kind, int id);
    IReadOnlyList<DemonSiteView> DemonSites(WorldState s);
    string SiteName(WorldState s, DemonSiteKind kind, int id);
  }
}
```

Proposal target points and press areas: Building: plot centre, radius 5. Road: hex-edge midpoint, a 6 m wide strip between the two corners (`ProposalAt` tests the strip; `pressRadius` = 30 for the midpoint). Settlement: corner, 6. City: corner, 6. Banish: the demon site (corner, or hex-edge midpoint with the strip).

### A.4 Modules

`EconomyModule`, `FactionsModule`, `DemonsModule`, each with `InitState(WorldState)` and `Register(WorldSim)`; `WorldSimFactory.CreateNew(seed)` and `SaveCodec.CreateSim(state)` call them in the order Economy, Factions, Demons. Cross-module effects go through `EconomyHooks` (owned by S): `IsCornerBlocked`, `IsEdgeBlocked`, `IsSettlementHalted`, `OnRolled`, `OnSiteCompleted`.

### A.5 API change log

(S appends here: date, old name, new name, why.)

**13 Nov 2026, S-0 (surface landed; pinned by `ApiSurfaceTests`).** No name of A.1-A.4 was renamed or removed. Additions and clarifications:

1. `BuildingState.Cancelled = 5`, `SettlementState.Cancelled = 2`, `RoadState.Cancelled = 3` (new members, appended). Why: `CancelSite` must keep ids stable (list index = id) and free the plot, corner or edge; the enums had no state for a cancelled site. A cancelled road no longer appears in `RoadOnEdge`; `EconomyApi.SettlementAt` and `RoadOn` ignore cancelled sites; `EconomyApi.Settlements` lists every id, so callers check `State`.
2. `SiteKind.City`: the site id is the settlement id (`SitePlaced`, `SiteCompleted`, `SiteCancelled`, `CancelSite`, `WaitingSentence`).
3. `EconomyHooks` signatures: `IsCornerBlocked`, `IsEdgeBlocked`, `IsSettlementHalted` are `List<Func<WorldState, int, bool>>`; `OnRolled` is `List<Action<WorldState, EventBuffer, int d1, int d2>>`; `OnSiteCompleted` is `List<Action<WorldState, EventBuffer, SiteKind, int id>>`. Get them with `EconomyApi.Hooks(WorldSim)` (as before the refactor).
4. `Proposal.Key` is a get-only property marked `[JsonIgnore]` (not saved); `Proposal.MakeKey(...)` builds the same text. Ints throughout, e.g. the opening on the default board is `"0|5|631|-1|-1|0|-1"`. Fields that do not apply are -1 (`DemonSiteKind` then holds `Corner` = 0).
5. `BuildingDefs.PrimitiveFor` / `ImprovedFor` return `(BuildingType)(-1)` for Meadow; `BuildingDefs.Get` returns null for an id out of range (no exceptions).
6. `GameOutcome` is from the player's side (Victory = your people won), also in `GameEnded`.
7. `WareLedger.StockAdded/StockLost/StockTaken` are indexed by ware id with length `Wares.Count` (only raw wares are ever non-zero); `RecipeIn/RecipeOut` are `int[16][10]`. Starting hex stock (`StartMature`) counts as `StockAdded`.
8. `PlaceRoad` with a hex edge id out of range is refused with `not_connected` (5.2 has no "unknown edge" code).
9. `PlayerState.Name` is "Your people" / "the Ash Clan".
10. Helpers added (public, free to use): `SystemOrder.Dice/Growth/Collect/Brain/Strength` (100/150/200/1150/1200; the older slot names stay); `TerrainInfo.Count`, `DisplayName(Terrain)`, `IsMined(Terrain)`; `PlotLayout.PlotCount`, `IsValidPlot`, `Yaw(dx, dz)`, `SideSlotSpoke`, `CitySlotSpoke`, `SideOffset`; `BuildingDefs.IsValid`, `BuildingDef.IsCollector`; `EconomyTuning.GrowFor(Terrain)`; `EconomyApi.CornerTerrains(s, corner)`; `EconomyRefusals.Sentence/Reject`, `FactionRefusals.Sentence/Reject`, `DemonRefusals.Sentence/Reject`; `DemonApi.Effect(kind)`; `FactionApi.PlotPressRadius/CornerPressRadius/RoadStripHalfWidth/RoadPressRadius`; `BoardGenerator.NumberPool`, `MountainNumbers`, `StartForestNumber/StartHillsNumber/StartFieldsNumber`.

---

## Appendix B. Unity-side interface between packages (namespace `World.Game`)

| Name | Owner | Members others use |
|---|---|---|
| `WalkLattice` (static) | P | `Step` 5f; `struct Cell { int I, J; }`; `Vector3 ToWorld(Cell)`; `Cell ToCell(Vector3)`; `Cell Dir(int heading)`; `float Yaw(int heading)`; `int HeadingOf(float yaw)` |
| `WalkGrid` (static) | P | `bool IsBlocked(Cell)`; `void AddStaticBlocker(Vector3 centre, float radius, string owner)`; `void RemoveStaticBlockers(string owner)`; `List<Cell> FindPath(Cell from, Cell to, int maxNodes = 20000)` |
| `PlayerWalker` (MonoBehaviour) | P | `Instance`; `Cell`; `Heading`; `Position`; `EyePosition`; `Yaw`; `IsGliding`; `Forward()`, `Back()`, `TurnLeft()`, `TurnRight()`; `WalkTo(Cell)`; `Follow(IFollowable)`; `StopAuto()`; `IFollowable Following`; `Teleport(Cell, int heading)`; `PlaceExact(Vector3, float yaw, float pitch)` (encounters; snaps back on `RestoreCell()`); `AddLock(string)`, `RemoveLock(string)`; `event Action Stepped` |
| `PointerInput` | P | `event Action<PointerHit> Tapped`; `PointerHit { Vector3 Ground; bool HasGround; Component Target; }`; `Hovered`; `InputEnabled` |
| `IInspectable` (kept) | P | `Title`, `Lines` |
| `IFollowable` | P | `string FollowName { get; }`, `Transform FollowTransform { get; }` |
| `ISpeaker` | P | `string SpeakerId { get; }`, `Transform Head { get; }` |
| `SpeechBubbles` | P | `int Say(ISpeaker who, string text, float seconds)`; `int Ask(ISpeaker who, string text, IList<string> replies, Action<int> onReply, float timeoutSeconds)`; `void Close(int handle)`; `bool IsOpen(int handle)` (keys 1-3 answer the nearest open Ask) |
| `SpellCaster` | P | `CommandResult TryCastAt(Vector3 ground)`; `event Action<int, CommandResult> CastAttempted` (proposal id, result) |
| `MapScreen` | P | `IsOpen`, `Open()`, `Close()` |
| `Hud` | P | `Visible`; `Notice(text, error, seconds)`; `MenuOpen` |
| `TouchStrips` (static) | P | `int StripAt(Vector2 screenPoint)` (1-4, 0 outside the band); `bool Held(int strip)`; `bool Pressed(int strip)` (this frame) |
| `CampLayout` | D | `HomeCorner`; `Hearth`; `DanceCircleCentre`; `DrumPosition(int pad1to4)`; `DancerSpot`, `DancerYaw`; `MiraSpot`; `DrummerSpot`; `Spawn` (Cell), `SpawnHeading`; `PrinceStart` (Cell) |
| `LandViews` | V | `Instance`; `event Action<IReadOnlyList<SimEvent>> Synced`; `Transform CarrierView(int carrierId)`; `Transform BuildingView(int buildingId)`; `Transform ProposalMarker(int proposalId)`; `RebuildFromState()` |
| `PrinceDirector` | N | `Instance`; `Npc Prince`, `Sawyer`, `Merchant`; `int FirstSceneStep`; `RestoreFirstScene(int step)` (F calls it on load) |
| `Npc` (kept, extended) | N | existing members plus `WalkTo(Vector3)`, `Follow(Transform, float distance)`, `IsWalking`; implements `ISpeaker`, `IFollowable` |
| `GuideText` | F | `ControlsLeft`, `ControlsRight`, `HowToPlayLeft(WorldState)`, `HowToPlayRight(WorldState)` |
| Installers | each | `PlayerInstaller`, `DevToolsInstaller` (P), `CampInstaller` (D), `LandViewsInstaller` (V), `PrinceInstaller` (N): `public static void Install(WorldBootstrap boot)`, called in that order: Player, Camp, LandViews, Prince, DevTools |

`GameClock` keeps `Ticked`, `Pause`, `Resume`, `StepOnce`; `PauseReasons.Encounter` and `Menu` (Draft removed). `EncounterHost`, `IEncounter`, `EncounterContext` keep their contracts; `EncounterContext.Avatar` becomes the `PlayerWalker`.

### B.1 Appendix B change log

(F appends here for decisions about the Unity-side interface; the owning package may append for its own names.)

**13 Nov 2026, F-0 (skeleton landed; tag `pre-F0`).** Where appendix B and 7.3 left a gap, F-0 decided as follows.

1. **Files and folders.** Installers: `Game/Player/PlayerInstaller.cs` and `Game/UI/God/DevToolsInstaller.cs` (P), `Game/View/Camp/CampInstaller.cs` (D), `Game/View/Land/LandViewsInstaller.cs` (V), `Game/Prince/PrinceInstaller.cs` (N). Stubs: `Player/WalkLattice.cs`, `WalkGrid.cs`, `PlayerWalker.cs`, `PointerInput.cs` (with `PointerHit`), `SpellCaster.cs`, `TouchStrips.cs`, `PlayerInterfaces.cs` (`IInspectable`, `IInteractable`, `IFollowable`, `ISpeaker`), `UI/SpeechBubbles.cs`, `UI/MapScreen.cs` (P); `View/Camp/CampLayout.cs` (D); `View/Land/LandViews.cs` (V); `Prince/PrinceDirector.cs` (N); `UI/GuideText.cs` (F). `WorldBootstrap.Awake` calls the five installers in the order of 8.0.
2. **Two installers are not empty.** `CampInstaller` spawns Mira at `CampLayout.MiraSpot` so the Ward duel stays startable; `LandViewsInstaller` builds the sky and the terrain through `TerrainBuilder`, so V can change `TerrainBuilder` without editing `WorldBootstrap` (P's). Their owners rewrite them freely.
3. **`Cell`** is the nested struct `WalkLattice.Cell` (`I`, `J`, equality, `+`, `-`, `* int`); files use `using Cell = World.Game.WalkLattice.Cell;`. `WalkLattice` is implemented in full at F-0 (pure geometry of 2.D: `ToWorld`, `ToCell` by cube rounding, `Dir`, `Yaw`, `HeadingOf`); P owns it.
4. **Static or instance.** MonoBehaviours with `Instance`: `PlayerWalker`, `LandViews`, `PrinceDirector` (none is created at F-0: their owners create them in their installers). Static classes: `WalkLattice`, `WalkGrid`, `TouchStrips`, `PointerInput`, `SpeechBubbles`, `SpellCaster` (`TryCastAt`, `CastAttempted` static), `MapScreen`. `Hud` stays a MonoBehaviour; `Hud.Visible` is a new static property (with the existing static `MenuOpen` and `Notice`). `CampLayout` is a plain class built from the board, `new CampLayout(state.Board)` (any package may build one): `int HomeCorner`; `Vector3 Hearth, DanceCircleCentre, DancerSpot, MiraSpot, DrummerSpot`; `float DancerYaw`; `Vector3 DrumPosition(int)`; `Cell Spawn, PrinceStart`; `int SpawnHeading`.
5. **Stub behaviour.** `PlayerWalker` methods only store the pose; `WalkGrid.IsBlocked` knows only the board limit and registered static blockers, `FindPath` returns null; `TouchStrips.StripAt` is real, `Held`/`Pressed` return false; `PointerInput` never raises `Tapped`; `SpeechBubbles` issues and tracks handles but draws nothing and never replies; `SpellCaster.TryCastAt` refuses with code `not_implemented`; `LandViews` lookups return null; `PrinceDirector` has no villagers. `CampLayout` is a working first version (D refines it): spawn 4 steps from the home corner along the Forest spoke, facing the prince's start 4 steps further on (on a hex-edge line); Mira 2 + 2 steps from the corner between the Forest spoke and the next hex edge; dancer on the corner side of the fire, drums on the near half of the 4 m circle.
6. **`Npc` (N)** gained at F-0, so P and D can use them before N lands: `ISpeaker` (`SpeakerId` = `Id`, `Head` = a child 2.15 m up), `IFollowable` (`FollowName` = `DisplayName`, `FollowTransform` = its transform), `WalkTo(Vector3)`, `Follow(Transform, float)`, `IsWalking`, plus `StopWalking()` and a `WalkSpeed` field. The walk is a straight line at 3 m/s with no blockers; N replaces it.
7. **Interim viewpoint.** Until P lands, the player is the old `FirstPersonAvatar` (WASD, mouse look), spawned at `CampLayout.Spawn`; Tab still switches to the old God View camera (no toolbar tools are installed). `EncounterContext.PlaceAvatar(Vector3, float yaw, float pitch)` is new: encounters (the duel, the dance) place the viewpoint only through it or `MoveAvatarToStage`, never through `ctx.Avatar`, so P can switch `EncounterContext.Avatar` to the `PlayerWalker`. P owns that switch in `Encounters/EncounterHost.cs`, and may edit the avatar helpers of `EncounterTestBase` in `Tests/PlayMode/EncounterTests.cs` (`RememberAvatar`, `AssertAvatarRestored`, `Accept`) in the same change; the rest of that file is D's.
8. **Encounters.** `Encounters/EncounterInstaller.cs` (registers the encounters and the `first_five` AutoPlay script) belongs to D (`first_five` becomes `duel_dance`). The rhythm party is unreachable in normal play: no warehouse is built. `WarehouseSet` stays until D deletes it, decoupled from the village layout (`Build(parent, door, yaw)`, `WarehouseSet.Current`); `WarehouseSet.BuildStandalone(boot)` stands it with DJ Sol on the first Meadow hex for the rhythm tests and `first_five`. `EncounterContext.Warehouse` is `WarehouseSet.Current`.
9. **Archived at F-0 rather than deleted later by their packages** (they could not compile without the removed `Board` fields or the old economy): `View/VillageLayout.cs`, `View/VillageBuilder.cs` (D's list), `Actors/VillageNpcs.cs` (N's), `View/Economy/**` and `View/OutpostBuilder.cs` (V's). All are in `Archive/Game/` for reference. `View/Signs.cs` is **not** deleted by D: `TerrainBuilder` and `GodOverlay` use its helpers; it becomes V's.
10. **UI/God.** Archived: `BuildTool`, `RoadTool`, `ProblemsTool`, `WaresTool`, `EconomyGodTools`, `EconomyHud`, `GodCommands`, `CommissionDriver`, `TravelTool`. Kept for P: `GodToolbar` (status strip shows the last roll and the stock of the player's first camp; not installed at F-0), `GodTool`, `GodOverlay`.
11. **Guide text.** `UI/GuideText.cs` (F) holds the pause-menu pages; `Hud` reads `GuideText.ControlsLeft/Right` and `HowToPlayLeft/Right(WorldState)`. The F-0 text describes the interim build.
12. **Save.** Extras `route.bar`, `route.balcony`, `bram.acknowledged` dropped (the warehouse is gone; the dance ending is not saved). The `save_load` AutoPlay script (`Boot/SaveLoadScript.cs`) is archived; F-1 writes the v4 one.

<<<<<<< HEAD
**13 Nov 2026, V (land views, branch `pre/v`).** Additions to V's own names; nothing in appendix A or other packages' names changed. Details in `Docs/LAND_UI.md`.

1. **`LandViews` is created by `LandViewsInstaller`** (`LandViews.Create(boot)`), after the terrain. `Synced` fires after every tick's reconcile and after `Inject`. The appendix B lookups (`CarrierView`, `BuildingView`, `ProposalMarker`) return null for unknown or gone ids; `ProposalMarker` exists only for open proposals of your people (faction 0).
2. **Carrier feed.** `ICarrierFeed { int Count(WorldState); bool TryGet(WorldState, int id, double tickFraction, out CarrierPose); bool Position(WorldState, int id, double tickFraction, out double x, out double z, out double yaw); }`, `struct CarrierPose { X, Z, Yaw, Faction, Home, Moving, HasLoad, Load, LoadCount, Shipment }`, `SimCarrierFeed` (default: `Economy.Carriers` and `EconomyApi.CarrierPosition`; shipment = `LegCorners` not empty or the job `IsShipment`). `static LandViews.CarrierFeed` lets tests drive the carrier views with a fake feed until the real logistics land; `LandViews.Inject(IReadOnlyList<SimEvent>)` sends events through the tick path. A moving carrier's view stands exactly on the feed position; idle carriers (`State == Idle`) wait 4 m from their storehouse corner.
3. **Typed lookups and test support:** `Hex(int)` (`HexLandView`), `Settlement(int)`, `Building(int)`, `Road(int)`, `Carrier(int)`, `Demons(DemonSiteKind, int)`, `Marker(int)` (`ProposalMarkerView`, with `Target`, `PressRadius`, `IsTop`), `WarCamp`, enumerations of each, `Snapshot()`, `RebuildCount`, `HexSyncs`, `TickFraction`, `static Vector3 Eye` (the viewing camera this frame). The camera is `ViewSwitcher.ActiveCamera`, else `Camera.main`.
4. **Picking.** Every view is an `IInspectable` with a trigger collider (nothing blocks). The proposal mark's collider is a 10 cm slab on the ground over the press area (or the 6 m road strip). Camp, building and hex-patch colliders are up to 3 m tall: P's `PointerHit.Ground` should come from a ground raycast that ignores triggers, so a press near a camp (city or banish proposals) still lands on the ground.
5. **Walk blockers.** Mountain crags are registered as `WalkGrid.AddStaticBlocker(centre, radius, "land.peaks")` (six per Mountain hex, 41 m from the centre towards the hex-edge midpoints, radius about 6.5 m; never on a plot, a stock patch or a hex-edge line).
6. **`BoardSpace`** gains `Corner`, `Plot`, `PlotYaw`, `PlotPath`, `HexEdgeDir`. The flag and segment members stay (other packages still call them) until F removes the fine graph.
7. **`Signs`** (V's since F-0) is unchanged; `HexView` (in `TerrainBuilder.cs`) now gives the sentences of `LandText.HexLines`.
=======
**13 Nov 2026, D (dance circle and camp dressing; branch `pre/d`).** D's own names; nothing of P, V or N changed.

1. **`CampLayout` additions** (all existing members kept, same meaning): `Forward`, `Right` (the dancer's view axes: corner to fire, and its right), `ReservedPlot`, `MiraYaw` (Mira faces the spawn), `DrummerYaw` (Sol faces the dancer), `FireLeapMark`, `SpiritCallMark`, `CirclePoint(thetaDeg, radius)`, `SpokeDir(sector)`, `Dressing` (`IReadOnlyList<CampProp>`, `CampProp{Kind, Position, Yaw, Radius}`), `Clearings` (`IReadOnlyList<Vector3>`, circles as (x, z, radius), the format of `TerrainBuilder.Build`'s clearings), `static string Obstruction(Board, Vector3, float radius, bool insideCircle = false)` (null when clear of plots, stock patches, hex-edge lines, home local paths, corners and the dance circle), constants `DrumRadius`, `DancerDistance`, `DrummerDistance`, `DrummerAngle`, `SeatRadius`, `DanceRingRadius`, `FireBlockRadius`, `DrumBlockRadius`.
2. **Changed positions.** `MiraSpot`: 15.5 m from the home corner in the open wedge just beyond the prince's hex edge (out of the forest; F-0 had her in the Forest wedge on the prince's line, behind forest trees). `DrummerSpot`: 5.5 m beyond the fire, 14 degrees right of the line through it (so the flames do not hide Sol). Spawn, `SpawnHeading`, `PrinceStart`, `Hearth`, `DanceCircleCentre`, `DancerSpot`, `DancerYaw`, `DrumPosition` are as at F-0.
3. **Request to V: honour `CampLayout.Clearings`** in the terrain (keep trees, crops, rocks and mounds out of them, e.g. add them to the per-hex clearings next to the camp's plots and paths). They cover the dance circle (8.5 m), every dressing prop, Mira's duel ground, the spawn, the prince's walk and the spawn-to-Mira sightline. Without them, crop rows grow through the dance circle and the camp's tents, and a forest tree stands in front of the spawn. D's captures were taken with the clearings applied to the interim terrain by a capture script.
4. **New D components others may read:** `DanceCircle` (`Current`, `Layout`, `IsLit(pad)`, `DrumGlow(pad)`, `DrumTop(pad)`, `LastEffect` (`None`, `FireLeap`, `SpiritCall`), `ActiveDancers`; static `Approach(cueTime, songTime, fall)`, `DrumScale(pad)`, `ApproachRings`), `CampInstaller.Root` (the "HomeCamp" object), `CampPropInfo` (`IInspectable` on props and drums), `RhythmPartyEncounter.Circle`, `LitAt`, `BassJoined`, `ChoiceName(Route)`, `FireLeapName`, `SpiritCallName`.
5. **Removed:** `WarehouseSet` (and its `World.Game.Route` enum), `EncounterContext.Warehouse` (one field and its initialiser in `Encounters/EncounterHost.cs`, the only edit there), the AutoPlay script `first_five` (now `duel_dance`, `DuelDance.Name`). Sol keeps the NPC id `NpcIds.Host` ("host"); the dance keeps the encounter id `rhythm_party`.
6. **Static blockers:** `WalkGrid.AddStaticBlocker` with owner `"dance_circle"`: the fire (2.5 m) and each drum (1.3 m).
7. **Interim avatar:** the dance and `duel_dance` place the viewpoint only through `EncounterContext.PlaceAvatar` and read keys and `TouchStrips.Pressed/Held`; the ending-choice tap reads `Input.mousePosition` above the touch band (left or right of the fire's screen point), independent of `PointerInput`. `FirstPersonAvatar` pitch is positive downwards; the dance uses 15 (looking down at the drums), which P's `PlaceExact(position, yaw, pitch)` should keep.
>>>>>>> pre/d
