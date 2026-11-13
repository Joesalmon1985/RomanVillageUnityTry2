# Unified economy design

Status: IMPLEMENTED in the simulation 13 Nov 2026 (branch `feat/sim-integration`); the headless proof of section 9 is `Docs/ECONOMY_PROOF.md`, written and checked by the test `EconomyProofReport`. The tuned numbers are in section 11 (rebalanced 13 Nov 2026: harvest season, one draft pick per round, the rival builds its cards, economy targets, per-terrain batches; see the rule table there). This replaces PRD sections 5.1 to 5.3 and is binding for the simulation. No economy graphics are built or extended until the owner has read the proof.

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
| Coal | Charcoal kiln | 1 Logs | Brick kiln, smelter, power plant |
| Bricks | Brick kiln | 2 Clay + 1 Coal per 2 bricks | Buildings, roads, cities, wonder |
| Flour | Mill | 1 Wheat | Bread |
| Bread | Bakery | 1 Flour | Rations |
| Meat | Butcher | 1 Sheep | Rations |
| Rations | Tavern | 1 Bread + 1 Meat | Mines, soldiers, founding a city |
| Iron | Smelter (needs power) | 2 Ore + 1 Coal | Weapons, wonder, late cards |
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
- **Round (the player ends it, with a bell):** production draw and a draft pick at the start; the bell can ring only after the harvest season (60 s); at the end the rival takes its turn, then the infection deck is drawn.
- **Age (a fixed number of rounds):** begins with the draft; ends with the military comparison. Three ages, then final scoring.

A batch that is not harvested carries over up to a cap, so ending rounds faster than your buildings can work gains nothing; and the bell cannot ring before the round's harvest season is over (60 s), so every round is a real harvest and every age lasts minutes, not seconds.

## 6. Development cards and the wonder (7 Wonders)

Cards come in the familiar families, each tied to a real hook in the economy:

| Family | Effect in this world |
|---|---|
| Raw and crafted (brown, grey) | Bigger harvest batches on one terrain; faster or doubled output at one workshop type |
| Commerce (yellow) | Better market rate; coin per round; cheaper roads |
| Civic (blue) | Victory points |
| Military (red) | Shields: stronger or faster-trained soldiers, a larger garrison |
| Lore (green) | Hero and ward improvements: extra hero moves, a site holds four demons before it outbreaks, one fewer infection draw |

Draft (as built after the rebalance): four cards each at the start of an age; one pick at the start of each of its three rounds, the rest passed between rounds; the last card is discarded. A picked card can be built in a city (goods or coin), held, or discarded for 3 coin. The rival builds the point and shield cards it drafts (at most two an age, one of them a shield card), so what you pass it becomes its points and shields. The wonder is a building site; each stage needs its goods delivered there.

Military comparison at the end of each age: compare shields (soldiers in the field plus military cards) with each neighbouring power. Win: +1, +3, +5 points by age. Lose: -1. Destroying the rival's outpost outright scores separately. (As built: the comparison is made when the last round of an age ends, before the rival's turn; the rival's shields are its garrison guards plus any military cards it builds; tokens are mirrored for the rival.)

## 7. Demons (Pandemic, and Catan's robber)

- Infection deck of sites. Each round end draws 2, 3 then 4 cards by age; a drawn site gains a demon.
- A site that would gain a fourth demon outbreaks instead: every connected neighbouring site gains one, and the breach track advances. Chains are possible.
- Epidemic: at each age change the bottom site is drawn and filled to three, the draw rate rises, and the discard pile is shuffled back on top.
- Effect on the economy: a demon on a flag blocks carriers through it and stops the building there. A demon on a city stops that city's hexes producing, which is exactly the robber.
- Answers: the hero (a limited number of moves per round, clears a site on arrival), soldiers ordered to a site, and lore cards.
- The breach track is the loss condition.

## 8. Scoring

City 2 points each (the home city counts). Wonder stages 2 and 3. Civic cards as printed. Military tokens as above. Outpost destroyed +2. Target to win and breach limit are tuning values set by the headless balance runs (start: 12 points, 6 breaches; tuned: 18 points, 5 breaches); otherwise most points after Age III. The rival scores its plan, its military tokens and the civic cards it builds.

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

## 11. Tuned numbers (headless balance runs, 13 Nov 2026; rebalanced the same day)

Set by simulation with the scripted players (see `Docs/ECONOMY_PROOF.md`, sections C, E and F). Everything is a tuning field in code (`EconomyTuning`, `StrategyTuning`, `MilitaryTuning`), serialised with the game state (save version 3).

**Rule changes of the rebalance** (the design above is unchanged in intent; these make its systems bite):

| Rule | Why |
|---|---|
| **Harvest season**: the bell (EndRound) rings at the earliest `MinRoundTicks` = 600 ticks (60 s) after the round started; earlier it is refused with `round_too_soon` | rounds were ended seconds apart (an age was mostly its last round); now every round is a real harvest |
| **One draft pick per round**: each side is dealt 4 cards at the age start and picks one at the start of each round of the age, passing the rest between rounds; the last card is discarded (`DraftPickPerRound`, `DraftHandSize` 4, `DraftPicks` 3) | a decision every round, three cards a side per age instead of two |
| **The rival builds its cards**: free, at its outpost, the point and shield cards it drafts, at most 2 per age of which 1 a shield card (`RivalMaxBuilt` 9, `RivalBuildsPerAge` 2, `RivalShieldCardsPerAge` 1); it values a card it can still build fully, one it cannot only as denial | what you pass it now becomes its points and shields |
| **Commission reward**: 4 coin when Age I begins (`CommissionCoin`) | coin-cost Age I cards were unaffordable (coin started at 0) |
| **Economy targets** (Widelands): a workshop does not start a cycle while the storehouse holds its target of the output (`StockTarget`: planks 24, bricks 24, coal 8, rations 12, flour, bread, meat, iron and weapons 4); it reports `output_full` naming the ware | logs and clay were turned into planks and coal nobody needed, so logs looked like the bottleneck in every game |
| **Per-terrain batch sizes** (`TerrainBatchSize`) | each raw ware arrives at about the rate the village uses it |
| 6 new cards (24 in all, 8 per age) | four cards a side per age |

**Wares and recipes** (ticks: 10 per second)

| Building | Recipe | Time | Notes |
|---|---|---|---|
| Woodcutter | 1 forest batch unit -> 2 logs | 50 ticks per unit | |
| Clay pit, Farm, Sheepfold, Mine | 1 batch unit -> 1 clay / wheat / sheep / ore | 50 ticks per unit (from 80) | the Mine also eats **1 Rations per 3 Ore** (from 2) |
| Forester | keeps forest batches whole (halved without it) | | |
| Sawmill | 1 logs -> 1 planks | 30 | |
| Charcoal kiln | **1 logs** -> 1 coal (from 2 logs) | 40 | |
| Brick kiln | **2 clay** + 1 coal -> 2 bricks (from 1 clay) | 40 | |
| Mill | 1 wheat -> 1 flour | 30 | |
| Bakery | 1 flour -> **1 bread** (from 2) | 40 | |
| Butcher | 1 sheep -> **1 meat** (from 2) | 40 | |
| Tavern | 1 bread + 1 meat -> 2 rations | 40 | so 1 wheat + 1 sheep -> 2 rations (from 4) |
| Smelter | **2 ore** + 1 coal -> 1 iron (from 1 ore) | 50 | needs power (2) |
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
| Cards | Age I: 1 rations or 2-3 coin; Age II: 1 rations + 1-3 bricks; Age III: 1 rations + 1-2 iron (Great Hall also 2 coin) |
| Soldier | 1 weapons + 1 rations, delivered to the barracks |
| Wonder | stage 1: 4 planks + 4 bricks (2 points); stage 2: 2 iron + 2 rations (3 points) |
| Market | 4 for 1; Market building -1, Merchant Guild -1, best 2 for 1; sell 2 for 1 coin; buy 1 for 3 coin; discard a card for 3 coin |
| Starting stock | 16 planks, 6 bricks, 2 coal, 4 rations; 0 coin, **4 coin when Age I begins** |
| Commission | forester, woodcutter, sawmill, clay pit, charcoal kiln, brick kiln active and road-connected; **18 planks + 18 bricks** in the storehouse (from 10 + 10) |

**Cards added** (24 in all; every family in every age): Age I Village Green (civic, 1 point, 1 rations), Watch Post (military, 1 shield, 1 rations); Age II Aqueduct (civic, 2 points, 1 rations + 2 bricks), Stockade (military, 1 shield, 1 rations + 2 bricks); Age III Forum (civic, 4 points, 1 rations + 2 iron), Siege Works (military, 1 shield and +1 damage, 1 rations + 1 iron).

**Land, rounds, carriers**

| Number | Value |
|---|---|
| Batch per drawn hex | forest 12, hills 10, fields 8, pasture 8, mountain 5 (`TerrainBatchSize`; `BatchSize` 10 for anything unset), +1 per age at your cities (Age I +1, II +2, III +3; from +2), carry-over cap 24 |
| Numbers drawn per round | 3 of 4 (prologue: the starter sequence, home forest every round plus hills and fields in turn, until the commission, at most 6 rounds) |
| Rounds | at least 60 s each (the harvest season); the scripted player rings the bell when the round's harvest is in: 60-90 s, mean 68 s |
| Ages | 3 ages of 3 rounds (3.0-4.1 minutes each); Age I starts at the first round after the commission (at the latest round 7) |
| Draft | 4 cards each at the age start, one pick per round, 3 picks per age, the last card discarded |
| Carriers | 8 at the start, +2 per finished building, +2 per city; 30 ticks per 20 m segment; a carrier waits 150 ticks at its last flag before walking home (1 per building was tried: too few) |
| Deliveries | highest priority first, then the consumer with the least on hand, then the nearest; stockpiling into the storehouse last; workshops pause at the economy targets |
| Building time | 100 ticks; output store 8 |

**Demons, military, score**

| Number | Value |
|---|---|
| Infection deck | the flags within 2 segments of home (the village core), home from Age II, every founded city's corner |
| Draws per round end | 2, 3, 4 by age (Beacon Tower -1) |
| Demons per site | 3; the 4th outbreaks (Ward Stones: 4, the 5th outbreaks) |
| Connected neighbouring sites | from the site, walk along each of its road segments (built or not) in a straight line to the first flag that can hold demons (a corner, or a flag with a building, road end, city or demons): a corner has up to 3 neighbours, an intermediate flag 2 |
| Epidemic | at each age change: the bottom card's site filled to 3 (an outbreak if it held demons), discard shuffled on top |
| Breach limit | 5 |
| Hero | **6 moves per round** (from 4), one hex edge each, clears the flags it passes |
| Military comparison | shields: your soldiers in the field + military cards; rival: its garrison guards (**a flat 2**, from 1/2/3 by age) + the military cards it built |
| Rival plan | **+1** point (from 2) at the end of each age (rounds 3, 6, 9), stops when its outpost falls; plus the civic cards it builds |
| Win | **18 points** (from 12); otherwise most points after Age III |

Measured with these numbers (11 seeds, `Docs/ECONOMY_PROOF.md`): the competent player wins 11 of 11, 10 of 11 between 10 and 15 minutes (full player 10 of 11), commission at 3.2 minutes, every age 3-4 minutes, cards from 3-4 families in every game, wonder stage 2 in 11 of 11, the binding ware is rations 5, planks 4, logs 1, ore 1, under 0.5% of any raw ware is lost to the cap; careless play loses 11 of 11 (8 by breach), passive 11 of 11.
