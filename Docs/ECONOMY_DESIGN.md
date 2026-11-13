# Unified economy design

Status: IMPLEMENTED in the simulation 13 Nov 2026 (branch `feat/sim-integration`); the headless proof of section 9 is `Docs/ECONOMY_PROOF.md`, written and checked by the test `EconomyProofReport`. The tuned numbers are in section 11. This replaces PRD sections 5.1 to 5.3 and is binding for the simulation. No economy graphics are built or extended until the owner has read the proof.

## 1. One sentence

The land decides what you may take each round (Catan), buildings and roads decide whether you actually get it and turn it into something useful (Widelands), drafted developments and a wonder decide what you spend it on over three ages (7 Wonders), and demons spread through the same roads and towns to stop you (Pandemic).

## 2. Which game owns which question

| Question | Owner | Rule here |
|---|---|---|
| Where can I settle and what does the land give? | Catan | Hex terrain gives one of five raw resources. Cities on hex corners; a city works its three adjacent hexes. |
| How much raw resource this round? | Catan | Each round a production draw picks numbers; matching hexes next to your cities authorise a harvest batch. |
| How does a raw resource become usable? | Widelands | A harvester building on that hex takes the batch; carriers haul it along roads; workshops process it in chains. |
| What do things cost? | Catan pattern, Widelands goods | Costs keep Catan's "every resource matters" pattern but are paid in finished goods that were actually delivered. |
| How do I get stronger over time? | 7 Wonders | Three ages. Each age starts with a pick-and-pass draft. Cards are built with goods or coin, or discarded for coin. A wonder is built in stages. |
| How do I fight? | 7 Wonders + RTS | Soldiers are trained from delivered goods and given delegated orders. Each age ends with a military comparison against each neighbour. |
| What pushes back? | Pandemic | An infection deck places demons each round; a full site outbreaks to its neighbours and advances the breach track. Epidemics intensify it. |
| Who wins? | Catan / 7 Wonders | Victory points from cities, wonder stages, civic cards and military tokens. The breach track is the shared loss. |

## 3. Wares

Five raw resources, exactly Catan's and exactly Gods-and-Wizards': wood, brick (as clay), wheat, sheep, ore. Everything else is made from them.

| Ware | Made by | From | Used for |
|---|---|---|---|
| Logs | Woodcutter (Forest hex; Forester keeps the yield up) | harvest batch | Planks, Coal |
| Clay | Clay pit (Hills hex) | harvest batch | Bricks |
| Wheat | Farm (Fields hex) | harvest batch | Flour |
| Sheep | Sheepfold (Pasture hex) | harvest batch | Meat |
| Ore | Mine (Mountain hex), consumes Rations | harvest batch | Iron |
| Planks | Sawmill | 1 Logs | Buildings, roads, cities, wonder |
| Coal | Charcoal kiln | 2 Logs | Brick kiln, smelter, power plant |
| Bricks | Brick kiln | 1 Clay + 1 Coal per 2 bricks | Buildings, roads, cities, wonder |
| Flour | Mill | 1 Wheat | Bread |
| Bread | Bakery | 1 Flour | Rations |
| Meat | Butcher | 1 Sheep | Rations |
| Rations | Tavern | 1 Bread + 1 Meat | Mines, soldiers, founding a city |
| Iron | Smelter (needs power) | 1 Ore + 1 Coal | Weapons, wonder, late cards |
| Weapons | Smithy | 1 Iron + 1 Planks | Soldiers |
| Coin | not a ware; never carried | market sales, discards, commerce cards | Trade, cards |

Stone is removed: brick takes its place as the masonry material, as in Catan. Power is produced by a Power plant burning Coal and is needed by the Smelter and the Barracks.

## 4. What things cost (Catan's pattern, in finished goods)

| Thing | Catan original | Here |
|---|---|---|
| Road segment | wood + brick | 1 Planks + 1 Bricks |
| New city | wood + brick + wheat + sheep | 3 Planks + 3 Bricks + 2 Rations |
| Ordinary building | | Planks and Bricks by size |
| Development card | wheat + sheep + ore | Age I: Rations or coin. Age II: Rations + Bricks. Age III: Rations + Iron |
| Soldier | (knight card) | 1 Weapons + 1 Rations, trained at a powered Barracks |
| Wonder stage 1, 2 | | Planks + Bricks, then Iron + Rations |
| Bank trade | 4:1 | 4:1 at the market on storehouse stock; commerce cards and a Market building improve it |

Every one of the five resources is therefore needed: wood and clay to build at all, wheat and sheep to feed mines, soldiers and settlers, ore for iron, weapons and the wonder.

## 5. Time: how turn-based and real-time fit together

- **Real time (always running):** harvesting authorised batches, carrying, processing, construction, training, squads walking and fighting.
- **Round (the player ends it, with a bell):** production draw at the start; at the end the rival takes its turn, then the infection deck is drawn.
- **Age (a fixed number of rounds):** begins with the draft; ends with the military comparison. Three ages, then final scoring.

A batch that is not harvested carries over up to a cap, so ending rounds faster than your buildings can work gains nothing.

## 6. Development cards and the wonder (7 Wonders)

Cards come in the familiar families, each tied to a real hook in the economy:

| Family | Effect in this world |
|---|---|
| Raw and crafted (brown, grey) | Bigger harvest batches on one terrain; faster or doubled output at one workshop type |
| Commerce (yellow) | Better market rate; coin per round; cheaper roads |
| Civic (blue) | Victory points |
| Military (red) | Shields: stronger or faster-trained soldiers, a larger garrison |
| Lore (green) | Hero and ward improvements: extra hero moves, a site holds four demons before it outbreaks, one fewer infection draw |

Draft: three cards each, pick one, pass, pick one. A picked card can be built in a city (goods or coin), held, or discarded for 3 coin. The wonder is a building site; each stage needs its goods delivered there.

Military comparison at the end of each age: compare shields (soldiers in the field plus military cards) with each neighbouring power. Win: +1, +3, +5 points by age. Lose: -1. Destroying the rival's outpost outright scores separately. (As built: the comparison is made when the last round of an age ends, before the rival's turn; the rival's shields are its garrison guards plus any military cards it builds; tokens are mirrored for the rival.)

## 7. Demons (Pandemic, and Catan's robber)

- Infection deck of sites. Each round end draws 2, 3 then 4 cards by age; a drawn site gains a demon.
- A site that would gain a fourth demon outbreaks instead: every connected neighbouring site gains one, and the breach track advances. Chains are possible.
- Epidemic: at each age change the bottom site is drawn and filled to three, the draw rate rises, and the discard pile is shuffled back on top.
- Effect on the economy: a demon on a flag blocks carriers through it and stops the building there. A demon on a city stops that city's hexes producing, which is exactly the robber.
- Answers: the hero (a limited number of moves per round, clears a site on arrival), soldiers ordered to a site, and lore cards.
- The breach track is the loss condition.

## 8. Scoring

City 2 points each (the home city counts). Wonder stages 2 and 3. Civic cards as printed. Military tokens as above. Outpost destroyed +2. Target to win and breach limit are tuning values set by the headless balance runs (start: 12 points, 6 breaches; tuned: 12 points, 5 breaches); otherwise most points after Age III.

## 9. Proof before graphics

The simulation must produce, headless and as plain text, before any economy graphics work resumes:

1. A ware audit: every ware has at least one producer and one consumer, every building has a purpose, no dead ends.
2. A traced chain for each raw resource from the draw to its final use, with tick stamps (for example: round 2 draws 3, Forest batch 6; woodcutter fells; carrier hauls Logs to sawmill; Planks to storehouse; Planks delivered to the wonder site).
3. Conservation over long random play: nothing created or lost outside recipes.
4. Scripted full games on several seeds that reach each ending: points win, breach loss, Age III comparison.
5. A one-page balance table: time to each milestone, how often each resource is the bottleneck.

## 10. What changes from the code as built on 13 Nov 2026

| Area | Today | Needed | Status |
|---|---|---|---|
| Raw resources | Timber, Stone, Grain, Ore | Logs, Clay, Wheat, Sheep, Ore; add Pasture terrain | DONE 13 Nov 2026: `Ware` Logs, Clay, Wheat, Sheep, Ore; Pasture yields Sheep (Sheepfold). |
| Processed | Planks, Flour, Rations, Ingots | Add Coal, Bricks, Bread, Meat, Weapons; Rations need bread and meat; Iron needs coal | DONE: Planks, Coal, Bricks, Flour, Bread, Meat, Rations, Iron, Weapons with the recipes of section 3 (Ingots renamed Iron). |
| Mines | Free | Consume Rations | DONE: 1 Rations per 2 Ore; without rations the mine reports input_missing (rations). |
| Costs | Planks and Stone | Table in section 4 | DONE: buildings in Planks/Bricks by size, roads, cities, cards by age, soldiers, wonder stages, market 4:1. |
| Military | Rations + Ingots; outpost attack only | Weapons + Rations; end-of-age comparison added | DONE: soldiers cost 1 Weapons + 1 Rations delivered to a powered barracks; comparison at each age end, tokens recorded. |
| Demons | Cap of three then breach; no spread | Outbreak to neighbours; epidemic at age change | DONE: outbreak on the 4th demon to the connected neighbouring sites, chains (each site once per draw), epidemic at each age change, demons stop buildings on their flag, a demon on a city stops its hexes. |
| Card families | Mixed list of 18 | Regrouped into the five families | DONE: 18 cards, every family in every age, each with a real hook. |
| Map | 19 hexes, 20 m edges, buildings on hex corners | DONE 13 Nov 2026 (feat/sim-integration): 61 hexes, 60 m hex edges, each split into three 20 m road segments with building flags, city territory 4 segments = 80 m, Pasture terrain added (no ware yet). See CONTRACTS (Board) and MAP_MIGRATION. | DONE (Pasture now yields Sheep). |

## 11. Tuned numbers (headless balance runs, 13 Nov 2026)

Set by simulation with the scripted players (see `Docs/ECONOMY_PROOF.md`, sections E and F). Everything is a tuning field in code (`EconomyTuning`, `StrategyTuning`, `MilitaryTuning`), serialised with the game state.

**Wares and recipes** (ticks: 10 per second)

| Building | Recipe | Time | Notes |
|---|---|---|---|
| Woodcutter | 1 forest batch unit -> **2 logs** | 80 ticks per unit | logs feed both planks and coal, so a forest unit yields 2 |
| Clay pit, Farm, Sheepfold, Mine | 1 batch unit -> 1 clay / wheat / sheep / ore | 80 ticks per unit | the Mine also eats **1 Rations per 2 Ore** |
| Forester | keeps forest batches whole (halved without it) | | |
| Sawmill | 1 logs -> 1 planks | 30 | |
| Charcoal kiln | 2 logs -> 1 coal | 40 | |
| Brick kiln | 1 clay + 1 coal -> 2 bricks | 40 | |
| Mill | 1 wheat -> 1 flour | 30 | |
| Bakery | 1 flour -> **2 bread** | 40 | |
| Butcher | 1 sheep -> **2 meat** | 40 | |
| Tavern | 1 bread + 1 meat -> **2 rations** | 40 | so 1 wheat + 1 sheep -> 4 rations |
| Smelter | 1 ore + 1 coal -> 1 iron | 50 | needs power (2) |
| Smithy | 1 iron + 1 planks -> 1 weapons | 50 | |
| Power plant | burns 1 coal per 600 ticks | | 4 power; lights coal only while something needs power |
| Barracks | trains a soldier from 1 weapons + 1 rations delivered | 150 | needs power (2) while a unit is queued |

**Costs**

| Thing | Cost |
|---|---|
| Building, small (forester, woodcutter, clay pit, farm, sheepfold) | 1 planks |
| Building, medium (sawmill, charcoal kiln, brick kiln, mill, bakery, butcher, tavern, mine) | 1 planks + 1 bricks |
| Building, large (smelter, smithy, power plant, market) | 2 planks + 1 bricks |
| Building, huge (barracks, wonder site) | 2 planks + 2 bricks |
| Road | 1 planks + 1 bricks per 3 segments (one hex edge, 60 m) |
| New city | 3 planks + 3 bricks + 2 rations |
| Cards | Age I: 1 rations or 2-3 coin; Age II: 1 rations + 1-3 bricks; Age III: 1 rations + 1 iron (Great Hall also 2 coin) |
| Soldier | 1 weapons + 1 rations, delivered to the barracks |
| Wonder | stage 1: 4 planks + 4 bricks (2 points); stage 2: 2 iron + 2 rations (3 points) |
| Market | 4 for 1; Market building -1, Merchant Guild -1, best 2 for 1; sell 2 for 1 coin; buy 1 for 3 coin; discard a card for 3 coin |
| Starting stock | 16 planks, 6 bricks, 2 coal, 4 rations |
| Commission | forester, woodcutter, sawmill, clay pit, charcoal kiln, brick kiln active and road-connected; 10 planks + 10 bricks in the storehouse |

**Land, rounds, carriers**

| Number | Value |
|---|---|
| Batch per drawn hex | 12 units (+2 per age at your cities, Age I +2, II +4, III +6), carry-over cap 24 |
| Numbers drawn per round | 3 of 4 (prologue: the starter sequence, home forest every round plus hills and fields in turn, until the commission) |
| Ages | 3 ages of 3 rounds; Age I starts at the first round after the commission (at the latest round 9) |
| Carriers | 8 at the start, +2 per finished building, +2 per city; 30 ticks per 20 m segment; a carrier waits 150 ticks at its last flag before walking home |
| Deliveries | highest priority first, then the consumer with the least on hand, then the nearest; stockpiling into the storehouse last |
| Building time | 100 ticks; output store 8 |

**Demons, military, score**

| Number | Value |
|---|---|
| Infection deck | the flags within 2 segments of home (the village core), home from Age II, every founded city's corner |
| Draws per round end | 2, 3, 4 by age (Beacon Tower -1) |
| Demons per site | 3; the 4th outbreaks (Ward Stones: 4, the 5th outbreaks) |
| Connected neighbouring sites | from the site, walk along each of its road segments (built or not) in a straight line to the first flag that can hold demons (a corner, or a flag with a building, road end, city or demons): a corner has up to 3 neighbours, an intermediate flag 2 |
| Epidemic | at each age change: the bottom card's site filled to 3 (an outbreak if it held demons), discard shuffled on top |
| Breach limit | 5 (from 6) |
| Hero | 4 moves per round, one hex edge each, clears the flags it passes |
| Military comparison | shields: your soldiers in the field + military cards; rival: its garrison guards (1, 2, 3 by age) + its cards (it builds none by default) |
| Rival plan | +2 points at the end of each age (rounds 3, 6, 9), stops when its outpost falls |
| Win | 12 points (unchanged) |
