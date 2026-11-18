# PRD and MVP: the god board, the plague and the prehistoric war

Written 18 Nov 2026 on `historic/main` (`328c07e`) from a design discussion with the owner. It changes no code. The owner answered every open question the same day (section 13). This document is binding for agents working on the god board.

**What this replaces.** Where it differs, this document wins over:

- `REFACTOR_SPEC.md`: visible dice on a clock, growing hex stock, the prince's build proposals and spells for prehistoric building, strength, warriors, overrun and the 20-minute limit.
- `PRD_AGES.md`: the handover milestone as the way into Historic, and the prince building alone in Historic.
- `PRD_CRYSTALS.md`: the campaign opening only in Historic (CR-2), the crystal boons (5.7), crystal victory points, and the overrun rows (CR-31, 5.1, 5.9).

**Sources of authority, in order:** the owner's answers of 18 Nov 2026 (recorded in this document), the limits in `NEXT_START_HERE.md` section 4 (original names and data, AI from technique not code, nothing from the source repos), then the existing specs where this document is silent.

**Inspiration, mechanics only.** The prehistoric board follows the well-known hex-trading board-game model: resource hexes, settlements on corners, roads on edges, cities, 10 points. The plague follows the cooperative outbreak board-game model: an infection deck, a rate track, epidemics and outbreaks. The war and the Historic economy follow the Widelands model. In the game, every name, piece, card, look and line is ours. The game never names those games.

Reading guide: sections 1 to 3 are the plan in plain words. Sections 4 to 9 are the requirements and rules. Section 10 lists the new views and objects. Sections 11 and 12 are the build plan (slices, work packages, agents) and the proof. Sections 13 and 14 are the owner's open decisions and the risks. The appendix sketches the simulation interface.

---

## 1. Summary

The prehistoric age becomes a board game that the player plays from a god's-eye view, while the world below acts it out.

- **Four peoples** share the island: the player's people, the Ash Clan, the Elves and the Orcs. Each starts with one camp and has its own temple.
- **A fresh board every game.** The island's outline and Meadow rim stay fixed, but the resource hexes and their numbers are dealt fresh each game, as in a normal hex-trading setup, then checked so the four starting camps are fair.
- **The god board.** At the temple in the home camp the player sacrifices a lamb. The view rises to the whole island seen from above. The player places their second settlement, then each of the three AI peoples places theirs. From then on the peoples take turns. Each turn makes a hidden production roll, then the people trades and builds roads, settlements and cities. The first people to **10 points** ends the prehistoric age.
- **No dice anywhere.** A hex's chance of producing is shown by how rich it looks: thick old forest against a few thin trees, a towering mountain against a stony hump. When a hex produces, the settlements beside it get a card, and their villagers walk out and carry the goods home.
- **The turn timer** is an hourglass. It runs slowly while the player acts and fast while they idle. AI turns take a few seconds. **Leaving the god board pauses the turns.** The world keeps living: carriers finish hauls, armies march, and Mira's expedition and the Ash race keep running.
- **The AIs trade.** They offer trades to anyone who is not leading, starting with the worst-placed people. They trade towards their next build, for goods they cannot produce, and to shed surplus. The player receives their offers.
- **The plague.** Every roll of 7 is an epidemic (and anyone holding more than 7 cards discards half). Four kinds of demon (Earth, Fire, Water, Air) hold four regions of the island. The plague has its own map: 48 nodes on the resource hexes (some hexes carry two), joined by trails, with only a few passes (and one tunnel) between regions. Demons spread round by round along the trails, a hex with demons on any of its nodes produces nothing, and outbreaks chain. A plague overlay shows all of it. At 8 outbreaks the island falls and everyone loses.
- **The war.** Each settlement turns the goods its villagers bring home into processed goods, made from the pairs of raw goods it can reach. These feed the people's war lodge, which trains soldiers whose strengths depend on the goods they got. Armies act on their own. Soldiers travel only along the plague trails, so passes are the island's front lines. They hunt demons first. They attack a people only when it leads by 2 points or more. When two or more peoples are tied for the lead, those peoples' armies fight each other. An enemy soldier that reaches an unguarded settlement burns it.
- **Mira's crystal campaign** can be started from the first minute by talking to Mira. The Ash expedition races her on the world clock, including while the player is at the god board. Each crystal is the cure for one kind of demon. Whoever gets a crystal (Mira by restoring it, the Ash Clan by setting it on an altar) gets a free city, and a crystal spire rises there. The spire becomes a stage for the rhythm game, with a new track for each later spire.
- **Historic** begins when any people reaches 10 points. The temples are torn down, the god board closes for good, and the plague ends. Every people raises a castle at its most productive settlement, in wood, then brick, then stone. Raw goods now work the Widelands way: trees are felled and only regrow with a forester. Historic is played by the AIs. The player walks the world, joins the council and the rhythm game, and can carry on (or start) the crystal campaign. The prehistoric war rules stay until a new Historic military replaces them (a later PRD).

**MVP** = Slices 1 and 2 (section 11): four peoples, the temple and god board, hidden production, building, bank and AI trading, collecting villagers, the 10-point switch into Historic as a stub, the plague with its overlay, and the campaign rewired (Mira from minute one, crystals as cures and free cities, the first crystal spire with today's rhythm game). Slice 3 adds processing and the war. Slice 4 adds the full Historic switch and the Widelands-style resources.

Honest scale: Slice 0 is about a week of agent work. Slices 1 and 3 are each two to three weeks. Slice 2 is one to two weeks. Slice 4 is two to three weeks. Art (people looks, soldiers, castles) is the largest single cost and runs beside the sim work. These are guesses, not measurements.

---

## 2. What exists now, and what is reused

Checked in the code on `historic/main` at `328c07e`.

| Thing | State today | Fate |
|---|---|---|
| Board | Radius 4: 61 hexes, 150 corners, 210 edges. 23 hexes are Meadow (mostly the rim) with no number; 38 carry numbers. Terrain is fixed, numbers are dealt by seed. Camps sit only where three hexes meet. | **Kept.** 38 numbered hexes is roomy for 4 peoples and enough for 6. The Meadow rim takes the ports. |
| Players | `PlayerIds.Human = 0`, `Rival = 1`, `Count = 2`, used in about 290 places (prince, dev tools, tests, faction logic). | **Changed first** (Slice 0) to a list of peoples. |
| Dice and stock | Dice every 100 ticks (`EconomyTuning.RollIntervalTicks`), young stock grows 75 s, hex caps. | **Removed** from prehistoric. The roll moves into the turn engine and is never shown. |
| Carriers, storehouses, workshops, shipments, grades, towns | A full Widelands-like logistics sim with a ware ledger and a headless proof. | **Reused.** In prehistoric they carry collected goods to processing huts and the war lodge. In Historic they become the main economy. |
| The prince's brain (`Factions/Brain.cs`) | Proposal queue, site scoring (`BoardGenerator` corner value: terrain variety, Mountain bonus). | **Reused** for AI build choice, and as the player's adviser highlight at the board. Proposals and spells for building are retired in prehistoric. |
| Strength, warriors, overrun, 20-minute limit | Live. | **Removed.** |
| Demons (`Sim/Demons`) | On corners and road edges; cap 3, outbreaks, banish spell; a draw on every second 7 after a grace period. | **Replaced** by the plague (section 7). The banish spell stays as a player action in the world. |
| Ledger (`Sim/Ledger`) | Points per faction, keys paid once (`LedgerLogic.Award`). | **Reused** for board points. Crystal keys stop paying points. |
| Eras (`Sim/Eras`) | Prehistoric → Historic step, chapter ids, landmarks. | **Reused**; the trigger becomes "a people reaches 10 points". |
| Crystal campaign (`Sim/Campaign`, `Game/Expedition`, interiors) | Earth route finished (tag `earth-slice-1`). | **Kept**, rewired (section 8). |
| Rhythm game (`Sim/Encounters/Rhythm`, `RhythmPartyEncounter`, `DanceCircle`) | Charts load from `pack.json` (`PackJson.Parse`); fallback chart is the owner's Lv1 Warehouse loop at 174 BPM. | **Moved** to the crystal spires. Later spires load new packs. |
| Historic looks (`ages/h1-5`, merged at `b0e58c3`) | Historic models for 16 building types, upgrades in progress, towns. | **Reused** for castle stages and the Historic world. |
| Council (`council/main`) | Finished up to `6a83425`, gestures WIP, not merged. | Joins Historic once merged (section 9). |
| Archived military (`Archive/Sim/Military`) | Squads, orders, combat, raids. | **Reference only** for the data shape. |

---

## 3. The player's day, start to finish

1. Night start in the home camp, as today. The prince greets the player and takes them to Mira, then points at the temple. (He no longer pesters about builds.)
2. **Mira** offers the expedition at any time. Accepting starts the campaign and the Ash race.
3. **The temple.** Casting at the altar sacrifices a lamb. The camera rises into the god board. The first time, the setup round runs: the player places a settlement and a road, then the three AIs do the same. Each people takes its starting cards from its second settlement.
4. **Turns.** Production, trading and building, with the hourglass. Walking away from the temple pauses the turns. Coming back resumes them.
5. **In the world:** villagers collect after each roll; huts process; the lodge trains soldiers; armies hunt demons and, when someone leads by 2, march on them; settlements may burn. The player can walk anywhere, banish demons, follow Mira, and dance at a spire.
6. **10 points**, by anyone: the Historic age begins (section 9). The player keeps walking, the council and rhythm game stay open, and the campaign carries on.

---

## 4. Requirements: peoples and the god board

"Sim" means `World.Sim`, "Game" means `World.Game`. Numbers are starting values and are tuning fields saved in the state.

### 4.0 The board deal (owner, D-14)

| Id | Requirement |
|---|---|
| BD-1 | The island's outline and its 23 Meadow hexes stay fixed (they hold the campaign places, the towers and the plague footprint). The 38 resource hexes are dealt fresh each game from the seed, as in a normal hex-trading setup. |
| BD-2 | Tile mix (twice the classic set, rounded up to 38): 8 Forest, 8 Pasture, 8 Fields, 7 Hills, 7 Mountain (the classic 4, 4, 4, 3, 3 doubled, plus one Hills and one Mountain). **Seven hexes are pinned** (owner, Q-1) because the home camp and the campaign name them: the home camp's (0, 0) Fields, (1, -1) Forest and (1, 0) Hills; the glade's other wood (-1, -1) Forest; the Cold Kiln's clay hills (3, -1) Hills; and the Air route's mountains (2, 1) and (1, 2) Mountain. The other 31 are dealt from what is left: 6 Forest, 8 Pasture, 7 Fields, 5 Hills, 5 Mountain. Numbers are dealt on all 38. |
| BD-3 | Numbers: twice the classic set plus one 4 and one 10 (two 2s and two 12s, four each of 3, 5, 6, 8, 9, 11, five each of 4 and 10), shuffled onto the 38 resource hexes. A 6 or 8 may never sit next to another 6 or 8; the deal reshuffles until it holds. |
| BD-4 | **Fair starts.** The four first camps stay on fixed corners (home 70, Ash Clan 97, Elves 57, Orcs 82). The deal is repeated with the next sub-seed until every first camp touches three different raw terrains and the four camps' summed pips are within ±1 of each other, at most `DealTries` = 500 times; after that the fairest deal tried is kept. |
| BD-5 | Ports: nine fixed spots on the Meadow rim (chosen in G0-1, spread evenly round the shore, none on a tower corner or campaign place); which spot is 3:1 or which 2:1 good is shuffled each game (owner, Q-5). |
| BD-6 | The old fixed terrain table and fixed start numbers (`BoardGenerator.Rows`, `StartForestNumber` etc.) remain only for the legacy ruleset (G0-2). |

### 4.1 Peoples

| Id | Requirement |
|---|---|
| PE-1 | The sim supports N peoples (N = 4 now, up to 6 later). Each has an id, a name, a colour, a banner and a building palette. The human is people 0. |
| PE-2 | Start corners are fixed (home 70, Ash Clan 97, Elves 57, Orcs 82) and the deal makes them fair (BD-4). |
| PE-3 | Every per-faction system (economy, ledger, brain, campaign hooks, save) works for any N. No code outside tests may assume 2. |
| PE-4 | Elves and Orcs need their own look. For now (owner, D-12) they use the shared building set tinted to the people's palette with its banner; distinct sets are later art packages. |

### 4.2 Temple and god board

| Id | Requirement |
|---|---|
| GB-1 | **Every people has a temple** with an altar in its home camp (owner, D-10). Casting at an altar ("Sacrifice a lamb") opens the god board. It costs nothing. A short sequence plays (lamb, smoke, the camera rising). The god board only ever gives the player control of their own people: the AI peoples always play themselves. Smoke rises from a people's temple while that people takes its turn. Only the player's own temple opens the board; rival temples are scenery (owner, Q-3). |
| GB-2 | The god board shows the whole island from above, with each people's pieces, legal spots, piece counts, points, hands (counts only for AIs), the turn order and an event feed. |
| GB-3 | This is the one place in normal play with on-screen controls. They must work in a landscape phone-sized window (844 × 390) by touch and by mouse. |
| GB-4 | Leaving (a "Return to the world" control, or the pause key) puts the player back at the altar. Turns are paused while the player is not at the board, and the peoples' turn state is saved. |
| GB-5 | The first opening runs the setup round. Later openings resume the current turn. |
| GB-6 | Before the first opening there are no rolls and no turns. The world is quiet apart from Mira's campaign, if started. |
| GB-7 | The prince guides the player at the start (to Mira, then to the temple) and advises at the board (owner, D-11): his brain highlights where it would build next. The player can turn the hint off. |

### 4.3 Hidden production and abundance

| Id | Requirement |
|---|---|
| AB-1 | No die face, number token, roll total or odds appears in normal play: not on the board, the map, the HUD or in speech. F1 tools may show them. |
| AB-2 | Each numbered hex is dressed by its pip value (2/12 = 1, 3/11 = 2, 4/10 = 3, 5/9 = 4, 6/8 = 5). Higher pips mean visibly more: tree count and height, cliff size, field extent, flock size, mountain height. Five levels per terrain, readable from the god board and on the ground. |
| AB-3 | On a production roll, the hexes that produce pulse on the god board, and the settlements beside them send villagers out (CL-1). A roll of 7 plays the epidemic signs instead (PL-6). |
| AB-4 | Ports on the Meadow rim show as a jetty with a trader's boat: four 3:1 and five 2:1 (one per raw good), placed by BD-5. |
| AB-5 | Because the board is dealt fresh each game (BD-1), the hex dressing is built at load from the dealt terrain and pips, never baked into the scene. |

### 4.4 Turns and building

| Id | Requirement |
|---|---|
| TU-1 | Turn order is fixed by seed after the setup round. A turn is: roll (hidden), production, then trading and building in any order, then end of turn. |
| TU-2 | Production: each settlement next to a hex whose number was rolled gets 1 card of that hex's good; a city gets 2. A hex with any demon on any of its plague nodes produces nothing (PL-9). Meadow produces nothing. |
| TU-3 | Costs: road = Wood + Clay; settlement = Wood + Clay + Grain + Sheep; city = 2 Grain + 3 Stone. |
| TU-4 | Placement: a settlement needs an empty corner with no settlement on the three neighbouring corners (the distance rule) and, after setup, a connection by the people's own road. Roads join the people's own road or settlement. |
| TU-5 | Piece limits per people: 15 roads, 5 settlements, 4 cities (a city returns its settlement piece). |
| TU-6 | Points: settlement 1, city 2, longest road 2 (at least 5 joined roads; ties keep the holder). Points are computed from the board, never banked, so a burned settlement loses its points at once. |
| TU-7 | When a people's points reach 10 during any step, the sim raises `AgeEnded{Prehistoric, winner}` at the end of that step (section 9). |
| TU-8 | No development cards and no robber (owner, D-3). |
| TU-10 | **Discards on a 7** (owner, D-3): when a 7 is rolled, every people holding more than 7 cards discards half of them, rounded down, before the epidemic resolves. AIs choose with their brain (keeping the cards for their target build). The player chooses on the board within `DiscardTicks` = 150; if time runs out, the adviser's choice is used. Discards touch only cards, never the villagers' physical goods (TU-9). |
| TU-9 | Cards are a hand per people (one count per raw good). They are separate from the physical goods the villagers carry (CL-2). |

### 4.5 The hourglass

| Id | Requirement |
|---|---|
| HG-0 | The values below are the starting values; they are tuned from play (owner, D-2). |
| HG-1 | Each human turn starts with `TurnBaseTicks` = 450 (45 s) of sand, capped at `TurnCapTicks` = 900. |
| HG-2 | Sand falls at 1× while the player has acted in the last `IdleAfterTicks` = 40, and at `IdleRate` = 3× after that. |
| HG-3 | Each accepted action (a build, a trade, an offer answered) adds `ActionRefillTicks` = 50, up to the cap. |
| HG-4 | Tapping the hourglass ends the turn. When the sand runs out the turn ends. Open offers expire. |
| HG-5 | AI turns take `AiTurnTicks` = 30 plus 10 per action, so the player can follow them in the feed. |
| HG-6 | Leaving the board freezes the sand mid-turn. |

### 4.6 Trading

| Id | Requirement |
|---|---|
| TR-1 | Bank trade 4:1 any time on your turn; 3:1 or 2:1 at your ports. |
| TR-2 | On your turn you may offer a trade to one people or to all. AIs answer within `AiAnswerTicks` = 20. |
| TR-3 | AIs make offers to the player and to each other on their own turns, and the player sees offers addressed to them as cards they can accept, decline or counter. An offer lasts `OfferTicks` = 150 or until the turn ends. |
| TR-4 | **Who an AI trades with:** never a people that is leading. "Leading" = holding the highest points, ties included. An AI that is itself leading still trades, but only with peoples that are not. Among eligible partners it prefers the lowest points, then the lowest summed pips. |
| TR-5 | **What an AI wants, in order:** (1) the cards missing for its target build (the brain's chosen next road, settlement or city); (2) goods it has no settlement next to; (3) to shed surplus: cards beyond its target build's needs, most of all when holding more than 7. |
| TR-6 | **What an AI accepts:** a trade that does not lower its progress to its target build, and that gives it at least as much as it gives (counted in cards), except that it pays up to 2-for-1 for the last card of its target build. |
| TR-7 | Trades between AIs show in the feed as one line. The feed also says why the leader is shut out ("The Elves will not trade with the leader"). |

---

## 5. Requirements: collecting and processing

| Id | Requirement |
|---|---|
| CL-1 | On a production roll, each producing settlement sends one villager per card (at most `CollectWalkersCap` = 3 per settlement) to the hex and back. Cards reach the hand at once. The walk is visual. |
| CL-2 | Each returning villager also delivers one physical unit of the raw good to its settlement's store. These goods never become cards and cards never become goods. |
| PR-1 | Each settlement gets one processing hut for each pair of distinct raw goods among its neighbouring hexes (up to 3). Huts build themselves when the settlement is founded (`HutBuildTicks` = 300) and need no cards. |
| PR-2 | A hut turns 1 of each of its pair into 1 processed good every `HutCycleTicks` = 200, when both are in store. |
| PR-3 | Carriers (the existing logistics) take processed goods along the people's roads to the people's war lodge. A settlement with no road link to the lodge keeps its goods in store. |
| PR-4 | The ten processed goods, their pairs and what they give a soldier: |

| Pair | Processed good | Soldier stat |
|---|---|---|
| Wood + Clay | Fire-hardened spear | Attack |
| Wood + Stone | Hafted stone axe | Attack |
| Sheep + Stone | Flint knife on sinew | Attack |
| Wood + Sheep | Hide shield | Defence |
| Clay + Stone | Clay-plate vest | Defence |
| Wood + Grain | Hearth cake | Health |
| Grain + Sheep | Cheese and dried meat | Health |
| Grain + Stone | Millstone bread | Health |
| Clay + Sheep | Ochre and tallow paint | Evade |
| Clay + Grain | Straw-and-mud cloak | Evade |

Names are placeholders the owner may change.

---

## 6. Requirements: the war

### 6.1 Lodges and soldiers

| Id | Requirement |
|---|---|
| WA-1 | Each people has one war lodge at its home camp. It trains a soldier from `GoodsPerSoldier` = 3 processed goods every `TrainTicks` = 600. Each good raises its stat by one level (max 3 per stat). |
| WA-2 | Stats (integers): Health 100 + 30 per level; Attack 12–16 + 4 per level; Defence 0% + 8% per level (damage reduction); Evade 20% + 8% per level. |
| WA-3 | A soldier's look follows its highest stat: Brute (Health), Raider (Attack), Shieldbearer (Defence), Skirmisher (Evade); Warrior when no stat leads. Gear pieces show levels. |
| WA-4 | Every settlement starts with 1 garrison soldier (base stats). Becoming a city adds 1 more. |
| WA-5 | **The garrison rule:** a soldier may leave a settlement only if at least one other soldier stays there. |
| WA-6 | Soldiers trained at the lodge are field soldiers. They also obey WA-5 at the lodge's settlement. |

### 6.2 Fighting

| Id | Requirement |
|---|---|
| WA-7 | Close combat only. Two hostile soldiers within `EngageRadius` = 6 m stop and duel one-on-one. Others queue to fight the winner (the Widelands door-duel model). |
| WA-8 | A duel is integer and seeded: alternate swings; a swing hits unless the target evades (Evade %); damage = Attack × (100 − Defence) / 100. At 0 health the soldier dies. |
| WA-9 | **No health bars.** Wear must be readable from animation: stagger on a hit, a limp under half health, a dropped shield, a slower swing. |
| WA-10 | Soldiers at a plague node with demons fight the demons: each soldier removes one demon of any kind per `PurgeTicks` = 150. Demons do not fight back in MVP. |

### 6.3 Who fights whom

| Id | Requirement |
|---|---|
| WA-11 | **War state**, recomputed whenever points change: if the top people leads the next by **2 or more**, every other people is at war with it. If two or more peoples share the top score, those peoples are at war with each other. Otherwise nobody is at war. A lead of exactly 1 is peace. |
| WA-12 | Soldiers of peoples not at war pass each other without fighting, on any land. |
| WA-13 | **Army priorities** (each people's army brain, every `ArmyPlanTicks` = 50): (1) demon nodes on hexes that touch the people's own settlements; (2) demon nodes on hexes that touch any non-leading people's settlements, nearest by trail first; (3) if at war, the enemy's weakest reachable settlement (fewest garrison soldiers, then nearest by trail). Demons blocking a crossing on the army's route count as priority (1). A demon node whose hex touches only the leading people's settlements is ignored by the others' armies. The leader's own army treats its own demons as priority (1). |
| WA-14 | An enemy soldier that reaches a settlement corner with no soldiers present burns it: the settlement or city and its huts are destroyed, its points are lost at once (TU-6), its stored goods are lost, and a ruin remains. After `RuinTicks` = 1800 the corner is free for anyone under the normal rules. Roads stay. |
| WA-15 | **Home camps are looted, never burned:** they lose their stored goods and the attacker withdraws. The temple, Mira and the interior entrances are never lost. |
| WA-16 | Burned settlements and losses appear in the feed and as world events, with the cause. |
| WA-17 | **Soldiers move only along plague trails** (PL-3), node to node, crossings and the tunnel included. A soldier steps from a node to a settlement, and back, when the settlement touches that node's hex. So a settlement is reachable from every node on its hexes, and war between two peoples runs through the crossings between their regions. |
| WA-18 | A node holding 3 demons cannot be walked through (soldiers must purge it first). Fewer demons do not block movement. |

---

## 7. Requirements: the plague

| Id | Requirement |
|---|---|
The plague runs on its own map: a graph of **nodes** joined by **trails**, laid over the board but not the same as it. The graph is drawn smaller than the island: it covers only the 38 resource hexes, and busy hexes carry two nodes. The data is in Appendix B.

| Id | Requirement |
|---|---|
| PL-1 | **Nodes.** 48 plague nodes, 12 per element (Earth, Fire, Water, Air). Every node sits on a resource hex; Meadow hexes carry none. Each resource hex carries one node, except 10 fixed hexes that carry two, so that every region has 12 (listed in Appendix B: mostly the home camp's and the Orcs' hexes and the south). The board is dealt fresh each game (BD-1) but the graph never changes between games. A node's demon kind is its element. |
| PL-2 | **Regions** (owner, Q-2) are fixed groups of resource hexes, one per element, each round its element's tower and its crystal route: Earth the west (10 hexes), Water the south (8), Fire the east (8), Air the north and the mountain block (12), so the Air site, the wizard and the Air altar stand in Air country. Each region has 12 nodes. A Meadow place belongs to the region of the resource hexes beside it. |
| PL-3 | **Trails.** 73 trails join the nodes: 66 inside regions and 7 crossings between them. A node has 1 to 5 trails (7 dead ends with one trail). Trails are how outbreaks spread and how soldiers move (WA-17). Roads, settlements, carriers and building ignore trails completely. |
| PL-4 | **Crossings.** Every pair of regions is joined, but only through few crossings: Fire–Water 1 pass (south-east of home), Water–Earth 2 passes (south, and the island centre), Earth–Air 1 pass (by the Ash Clan), Fire–Air 1 pass (onto the mountain shoulder in the east), Water–Air 1 pass (island centre), and Earth–Fire 1 **tunnel** through the Old Quarry under the home camp. Crossings are ordinary trails in the rules; the tunnel is drawn underground. |
| PL-5 | **The infection deck** holds one card per node (48). **Once per round** (after the last people's turn) the sim draws as many cards as the current rate and puts 1 demon on each node drawn, then discards them. |
| PL-6 | **Every production roll of 7 is an epidemic** (after `GraceRounds` = 6): the rate moves one step along the track, the bottom card of the deck gets 3 demons, then the discards are shuffled and put on top. The god board darkens, thunder rolls and the feed says "The gods are angry." No 7 is ever shown. |
| PL-7 | Rate track: 1, 1, 2, 2, 2, 3, 3, 3, 4. |
| PL-8 | A node holds at most 3 demons. A fourth causes an **outbreak**: every node one trail away gets 1 demon of the source's kind (chaining, each node at most once per chain). The outbreak counter goes up by 1 per outbreaking node. |
| PL-9 | **A hex produces nothing while any node on it holds demons** (TU-2). So a two-node hex is easier to block (owner, D-13). Demons do not block roads or carriers. |
| PL-10 | **8 outbreaks: the island falls** and the game is lost for every people, the player's included: shared doom (owner, D-1). The game ends with a "The island falls" ending and offers a new game. |
| PL-11 | **The banish spell** stays: the player walks to a node with demons and casts; it removes one demon, with a cooldown of `BanishCooldownTicks` = 300. |
| PL-12 | **The overlay:** a toggle on the god board and in the world (key and touch strip). It shows the four regions, the nodes and trails (crossings marked, the tunnel dashed), demons by kind on each node, the rate track, the outbreak counter, and the top of the discard pile. Outbreak chains animate along the trails. On the ground, nodes are small shrines or cairns; trails are faint paths. |
| PL-13 | The graph is data: `Docs/godboard_plague_graph.json`, made by `Tools/godboard/plague_graph.py` (Appendix B). G2-1 turns it into a sim data table (the sim never reads `Docs/`). The trails were generated; before release it is hand-tuned to the terrain art (rivers, ridges, valleys), keeping 12 nodes per region and the crossings of PL-4. |

---

## 8. Requirements: the crystal campaign, rewired

| Id | Requirement |
|---|---|
| CC-1 | Mira offers "Set out for the crystals" at the home camp from the first minute. Accepting is `StartCampaign`. The handover/Historic gate (`AgesApi.CampaignMayStart`, CR-2) is removed. Practice duels stay. |
| CC-2 | The Ash expedition's clock (`RivalProgress`, PRD_CRYSTALS 5.1) starts at `StartCampaign` and runs on the world clock, including while the player is at the god board. Only duels and the pause menu stop it. March times are retuned for this (section 12). |
| CC-3 | **Crystals cure demons.** A Restored crystal cures its element: no new demons of that kind are placed (draws of that kind's nodes are discarded with no effect) and outbreaks of that kind do not chain. When no demon of a cured kind is left on the island, the kind is eradicated. A crystal on an Ash altar strengthens its kind: each draw of that kind places 2 demons. Curing has no effect in Historic, where the plague has ended (HI-8). |
| CC-4 | **Crystal reward:** whichever people gets a crystal (the player's people on restoring it; the Ash Clan on first setting it on an altar) gets a free city at its most productive settlement (highest summed pips). If it has no settlement it can upgrade (none left, or no city pieces left), it gets a free settlement at its best legal corner instead, placed automatically (owner, D-4). If neither is possible it gets nothing. |
| CC-5 | **The crystal spire.** A spire rises at the rewarded settlement, coloured by element. It survives burning. It is the stage for the rhythm game (section 8.1). |
| CC-6 | Crystal ledger keys stop paying points. Points come only from the board. |
| CC-7 | **Trouble at home:** when one of the player's settlements burns, or an outbreak reaches a node on one of the home camp's three hexes, the party drops what it is doing and walks home, and `RivalProgress` gains `HomeTroubleCharge` = 900. These replace the overrun rows of PRD_CRYSTALS 5.1 and 5.9. |
| CC-8 | The campaign continues across the switch into Historic. It can also be started for the first time in Historic. A crystal gained in Historic gives its city or settlement automatically (CC-4). |
| CC-9 | Terminal defeat (a lethal loss with no companion left) ends the campaign, not the game. |
| CC-10 | The campaign's single-step custody rules, attempt ids and checkpoints (PRD_CRYSTALS 5.2, 5.6) are unchanged. |
| CC-11 | **The Black Forge (Fire altar) moves into the Fire region** (owner, D-15): from the north-west shore to the Meadow hex (4, -3) on the south-east shore, beside the Fire region's hexes (3, -3) and (3, -2), two hexes south of the Cold Kiln. Its interior and keeper are unchanged; the Ash expedition's Fire carry leg is re-measured. |
| CC-12 | The campaign's terrain lines stay true on every deal because the hexes they name are pinned (BD-2). A new authored place must sit beside pinned hexes, or its lines must not name terrain. |

### 8.1 Spires and the rhythm game

| Id | Requirement |
|---|---|
| SP-1 | The first spire hosts the rhythm game as it plays today (the Lv1 Warehouse chart, 174 BPM). The home camp's dance circle stays until the first spire rises, then is retired (owner, D-6). |
| SP-2 | The second, third and fourth spires each load their own pack (`pack.json` + stems) with a new track, chart, tempo and stage dressing. |
| SP-3 | The tracks for spires 2 to 4 are the owner's own, from earlier projects (owner, D-9). Found on this machine: charts in `..\A1-before\songs\packs\` (`lv2_alien_spaceship`, 174 BPM; seventeen `gospel_*` packs, 124 BPM) and the matching Beat Shaper stems in `..\MusicGame\`. The packs use the same `pack.json` shape the game already reads (`PackJson.Parse`). Spire 2 plays `lv2_alien_spaceship`; spires 3 and 4 play two `gospel_*` packs (default `gospel_temple` and `gospel_storm`; the owner may swap them). The stems stay local, outside the repo, like the character meshes: the game looks for them in a local folder and a build without them falls back to spire 1's track (owner, Q-4). Nothing from third parties. |
| SP-4 | The player may play at any spire, including a rival's (owner, D-5). |

---

## 9. Requirements: the switch into Historic

| Id | Requirement |
|---|---|
| HI-1 | On `AgeEnded{Prehistoric, winner}`: the god board closes (if open, the player is returned to the altar with a short "The age has turned" moment), turns stop for good, and the era moves to Historic. A human win and an AI win both switch. The feed and the prince say who won. |
| HI-2 | Every temple is torn down (dust, collapse, cleared ground). The altar and the sacrifice are gone. |
| HI-3 | Every people starts a castle at its most productive settlement, in three stages: wood, then brick, then stone. Each stage is a construction site fed by the people's carriers (the existing site mechanics). The building upgrades of `ages/h1-5` apply. |
| HI-4 | **Historic is AI-only for now.** Every people, the player's included, is run by its brain. The player walks, talks, joins the council, plays the rhythm game and follows the campaign. |
| HI-5 | **Widelands-style raw goods:** production rolls stop. Woodcutters fell individual trees, which leave stumps that rot; trees regrow only where a forester plants. Quarries use up rocks. Fields are sown and harvested. Sheep need a farm. Clay pits dig. Each resource is a finite or renewable object in the world, not a hex number. |
| HI-6 | The brains plan Historic economies on the Widelands computer player's technique: keep foresters ahead of woodcutters, replace exhausted quarries, keep the castle supplied. |
| HI-7 | The prehistoric war rules (section 6) stay in force in Historic until the Historic military PRD replaces them. Garrisons and field armies carry over. |
| HI-8 | **The plague ends** at the switch (owner, D-7): every demon is cleared with a last banishing sequence, the infection deck and rate track stop, the overlay is retired, and 7s no longer exist (there are no rolls). Soldiers' demon duties end; armies keep only the war rules. |
| HI-9 | The council election and the rhythm spires are reachable in Historic. |
| HI-10 | A new alternative to the crystal quest for Historic is out of scope here. It gets its own PRD. |

---

## 10. New views and objects

| Area | What has to be made | Where |
|---|---|---|
| Peoples | 4 colours and banners; shared building set tinted per people (D-12) | Art, `Game/View` |
| Temple | One temple per people (4), altar, lamb, smoke while a people takes its turn; sacrifice sequence; camera rise to the board and back; demolition at Historic | `Game/View/Camp`, new `Game/Board/Temple` |
| God board | Top-down island camera; corner and edge hit targets; legal-spot highlights; settlement, city and road pieces per people; hand of cards; cost card; trade panel; offer cards; VP strip; turn ribbon; hourglass; event feed; adviser highlight | New `Game/Board/GodBoard` (UI Toolkit, phone layout) |
| Abundance | Procedural dressing per terrain, 5 levels: trees, cliffs, fields, flocks, mountains; produce pulse | `Game/Board` hex builder |
| Ports | 9 jetties with trader boats and trade-rate signs (pictures, not numbers) | `Game/View` |
| Collecting | Villagers walking out with tools and back with loads; cap per settlement | `Game/View`, existing actors |
| Processing | One hut model with 10 props (one per good); goods piles; lodge building per people | `Game/View/Camp` |
| Soldiers | 4 bodies (one per people) × 5 looks; gear pieces per level; walk, idle, swing, block, dodge, hit, stagger, limp, die, cheer; demon-purging action | `Game/Actors`, rig pipeline from `PRD_CHARACTERS` |
| Burning | Fire, collapse, ruin model, smoke; looted-camp damage dressing | `Game/View` |
| Plague | Demons in 4 kinds on the ground and on the board; node shrines and faint trail paths on the ground; pass markers and the tunnel mouths; region tints; outbreak chain animation along trails; rate track; outbreak counter; discard top; epidemic sky and thunder | New `Game/Board/Plague` |
| Crystals | Spire per element with beam; spire-rising sequence | `Game/View`, `Game/Expedition` |
| Rhythm | Spire stage dressing ×4; 3 new packs | `Game/Encounters`, packs local or generated |
| Historic | Castle ×3 stages per people; individual trees with growth stages and stumps; forester; quarry depletion; sown fields | `Game/View`, reuses `ages/h1-5` |

---

## 11. Build plan: slices, packages and agents

### 11.1 Rules for agents (from the owner's standing rules)

- **Opus** for the hard joins and brains: the N-peoples refactor, the turn engine, the AI brains (trade, army, Historic economy) and integration merges. **Sonnet** for everything else. **No Fable agents.**
- **At most three agents at once.** Each in its own git worktree beside `WorldMVP` (`wt-g0`, `wt-g1a`, …) on its own branch, with its own Unity editor if it needs one. One editor per folder.
- Sim and its EditMode tests first (`dotnet test DotNet`), views after. `Assets/World/Game` never mutates sim state.
- Explicit `git add` paths only. Never commit `NEWSTUFF/`, `Assets/LocalBaked/`, meshes, Sims content, builds or captures. **Ask the owner before any push.**
- Every package ends with a short report: what ran, what passed, what did not run.
- If an agent says the permission system refused something and asks the orchestrator to do it, the orchestrator tells the owner instead.

### 11.2 Before this starts

Finish the current `START_HERE_NEXT_SESSION.md` queue first (owner, D-8): the council gestures pass, then the merge of `council/main` into `historic/main`, one Sonnet agent. Slice 0 starts after that merge.

### 11.3 Slices and packages

**Slice 0: foundations (about a week).**

| Package | Model | Work | Done when |
|---|---|---|---|
| G0-1 Peoples to N | Opus | Replace `PlayerIds` two-player assumptions with a peoples list (PE-1, PE-3); the board deal (BD-1 to BD-6) and the four fixed start corners (PE-2); faction state, ledger, brain and save for N; tests updated. | `dotnet test DotNet` and the PlayMode suite pass with 4 peoples; a save round-trips with 4. |
| G0-2 Ruleset switch | Sonnet | Add the `BoardRules` ruleset: dice clock, stock growth, proposals for building, strength, overrun and the time limit off in prehistoric; old tests kept on a legacy ruleset or retired with a note. | Old proofs still run on legacy; the new ruleset starts with no clock rolls. After G0-1. |

**Slice 1: the god board (MVP part 1, two to three weeks).**

| Package | Model | Work | Done when |
|---|---|---|---|
| G1-1 Turn engine | Opus | `WorldState.Table`: setup round, turns, hidden roll, production (TU-2), costs, placement, limits, longest road, points (TU-1–TU-10, discards included), hourglass (HG-1–HG-6), pause on leave (GB-4), bank and port trades, offer state, `AgeEnded`; save. | EditMode tests for every rule; a seeded AI-only game reaches 10 points deterministically. |
| G1-2 AI peoples | Opus | Build brain on `Brain.cs` scoring; trade brain (TR-4–TR-7); answering offers; offers to the player. | Headless: AI-only games finish; trades obey TR-4 in 100% of logged trades. After G1-1. |
| G1-3 Board proof | Sonnet | `Docs/GODBOARD_PROOF.md` generator: virtual players (section 12) over many seeds. | Proof report written; targets checked. After G1-2. |
| G1-4 Abundance hexes | Sonnet | AB-2, AB-3, AB-5: dressing built at load from the dealt board, and pulse; ports (AB-4). | Captures at 844 × 390: five levels per terrain tell apart. Can run beside G1-1. |
| G1-5 Temple and board view | Sonnet | GB-1, GB-2, GB-4, GB-7; camera; pieces; hit targets. | Play: sacrifice, place, leave, return. After G1-1 interface. |
| G1-6 Board UI | Sonnet | Hand, costs, trade panel, offers, VP strip, turn ribbon, hourglass, feed (GB-3). | Touch and mouse at 844 × 390; AutoPlay script `board_first_round`. After G1-5. |
| G1-7 Collecting villagers | Sonnet | CL-1, CL-2 (goods to store). | Visible walk on a roll; ware ledger balances. After G1-1. |
| G1-8 Historic stub | Sonnet | HI-1, HI-2 (temple torn down), era switch, board closed. Castles and resources wait for Slice 4. | AutoPlay `board_to_historic` (dev start near 10 points). |
| G1-9 Integration | Opus | Merge G1 branches; full suites; build `Builds\Next`; owner play script. | Everything in 12.3 ran or is reported as not run. |

**Slice 2: plague, campaign, spires (MVP part 2, one to two weeks).**

| Package | Model | Work | Done when |
|---|---|---|---|
| G2-1 Plague sim | Sonnet | Replace `Demons` with the plague graph and rules (PL-1–PL-11, PL-13), with the graph from `Docs/godboard_plague_graph.json` as a sim data table; banish spell kept. | EditMode tests; plague figures in the board proof. After G1-1. |
| G2-2 Plague views | Sonnet | Overlay, node shrines, trails and world demons (PL-12), epidemic signs (PL-6). | Captures on board and ground; overlay readable at phone size. |
| G2-3 Campaign rewiring | Opus | CC-1–CC-10; retune race; campaign proof updated. | `CAMPAIGN_PROOF` regenerated; AutoPlay `earth_first` and `earth_stolen` pass from minute one. |
| G2-4 Spires and rhythm | Sonnet | CC-5, SP-1–SP-4: spire, stage, per-spire pack loading. | First spire plays today's chart; spire 2 loads the owner's spaceship pack. Spires 3 and 4 load the two gospel packs from the local stems folder; with the folder missing, they fall back to spire 1's track. |
| G2-5 Integration | Opus | As G1-9 for Slice 2. MVP build. | MVP play script run end to end. |

**Slice 3: processing and the war (two to three weeks).**

| Package | Model | Work | Done when |
|---|---|---|---|
| G3-1 Huts and lodge | Sonnet | PR-1–PR-4 on the existing logistics; lodge training (WA-1–WA-4). | Ware ledger balances; huts appear per pair. |
| G3-2 Soldiers and army brain | Opus | Garrisons, duels, purging, war state, priorities, burning, looting, trail movement (WA-5–WA-18). Needs G2-1's graph. | Headless: war only when the lead is 2 or more; no settlement left unguarded by its own soldiers; proof figures in 12.2. |
| G3-3 Soldier and fire views | Sonnet | Bodies, looks, gear, animations (WA-9), burning and ruins. | Captures; wear readable without bars (owner judges). |
| G3-4 Integration | Opus | As G1-9. | Play script for the war. |

**Slice 4: Historic (two to three weeks).**

| Package | Model | Work | Done when |
|---|---|---|---|
| G4-1 Castles | Sonnet | HI-3 castle stages on the site mechanics; temple-demolition polish. | AI-only Historic raises three stages for every people. |
| G4-2 Finite resources and Historic brain | Opus | HI-5, HI-6: trees, stumps, foresters, quarries, fields, farms; the brain keeps them in balance. | Headless 60-minute AI-only Historic with no people out of wood for more than 5 minutes. |
| G4-3 Historic views | Sonnet | Trees with growth stages and stumps (instanced), forester, quarry depletion. | Frame time within budget on the test machine at phone size. |
| G4-4 Historic player | Sonnet | HI-4, HI-7, HI-9; council and spires reachable. | AutoPlay `historic_walk`. |
| G4-5 Integration | Opus | As G1-9. | Play script for Historic. |

### 11.4 Order with at most three agents at once

| Wave | Agents |
|---|---|
| 1 | G0-1 (Opus) · G1-4 (Sonnet, views only, no sim) |
| 2 | G0-2 (Sonnet) · G1-1 (Opus) once G0-1 lands |
| 3 | G1-2 (Opus) · G1-5 (Sonnet) · G1-7 (Sonnet) |
| 4 | G1-3 (Sonnet) · G1-6 (Sonnet) · G2-1 (Sonnet) |
| 5 | G1-8 (Sonnet) · G2-2 (Sonnet) · G2-3 (Opus) |
| 6 | G2-4 (Sonnet) · G1-9 then G2-5 (Opus) → **MVP build, owner plays** |
| 7+ | Slice 3, then Slice 4, three at a time in the order above |

---

## 12. Proof and tests

### 12.1 Virtual players

Run over at least 50 seeds each, headless:

- **Quick**: acts at once, accepts every fair offer, ends turns early.
- **Slow**: uses most of every hourglass.
- **Absent**: opens the board, then never acts (the hourglass ends every turn).
- **AI seat**: the human seat played by the AI brain (also the shape of Historic).

### 12.2 Targets (starting values, to be tuned)

| Measure | Target |
|---|---|
| Rounds to 10 points, AI seat | 25–40 rounds |
| Quick player wins | 30–50% of seeds (4 peoples, so 25% is even) |
| Absent player wins | under 5% |
| Island falls (8 outbreaks) | 5–20% of games with no banishing; under 5% with an army brain active |
| Points lost to burning | median under 2 per game; no game where a people falls from 9+ to under 6 more than twice |
| Peace share | war state active under 30% of rounds |
| Trades | 100% obey TR-4; at least one AI-to-player offer every 3 rounds for a non-leading player |
| Wares | every physical good accounted for (ledger balance), every card accounted for |
| Determinism | same seed and inputs, same game, bit for bit, including save and load at any step |
| Campaign | a direct player reaches at least 2 crystals first; a reluctant one 0 (as today), with the board played in between |

### 12.3 Other tests

- EditMode tests for every rule table in sections 4 to 9.
- Board deal over 10,000 seeds: every deal obeys BD-2 and BD-3, at least 99% meet BD-4 within `DealTries`, and the same seed always gives the same board.
- PlayMode: board open, place, trade, leave and resume; overlay; spire stage; Historic switch.
- AutoPlay scripts: `board_first_round`, `board_to_historic`, `plague_epidemic`, `war_burn`, `earth_first`, `earth_stolen`, `historic_walk`, plus the existing ones.
- The full PlayMode suite takes about 50 minutes; run it at each integration package, not every package.

### 12.4 The owner's play script for the MVP

1. Start a new game. Meet Mira; accept the expedition.
2. Go to the temple and sacrifice. Place the second settlement and road. Watch the AIs place theirs.
3. Play five rounds: build a road, accept and refuse an offer, make a bank trade, let the hourglass run out once.
4. Leave the board mid-turn. Walk to a settlement and watch villagers collect. Return; the turn resumes where it stopped.
5. Wait for a 7: see the angry sky, then the overlay. Banish a demon on foot.
6. Follow Mira to the Earth crystal and restore it. See the free city and the spire. Dance at the spire.
7. Use the dev start near 10 points; reach 10; watch the temple fall.

---

## 13. Owner's decisions and open questions

### 13.1 Decided (18 Nov 2026)

| Id | Question | Owner's answer | Where |
|---|---|---|---|
| D-1 | At 8 outbreaks, shared or private loss? | Shared doom: everyone loses. | PL-10 |
| D-2 | Hourglass values | Start as written, tune from play. | HG-0 |
| D-3 | Development cards, robber, discards on a 7 | Discards on a 7; no development cards, no robber. | TU-8, TU-10 |
| D-4 | Free settlement from a crystal | Placed automatically. | CC-4 |
| D-5 | Dance at a rival's spire | Yes. | SP-4 |
| D-6 | Home dance circle | Kept until the first spire rises. | SP-1 |
| D-7 | Plague in Historic | Ends. | HI-8 |
| D-8 | Council queue | Finished before Slice 0. | 11.2 |
| D-9 | Music for spires 2 to 4 | The owner's own tracks from earlier projects (spaceship, gospel levels). | SP-3 |
| D-10 | Temples | One per people; the player only ever controls their own people. | GB-1 |
| D-11 | The prince | Guide at the start, adviser at the board. | GB-7 |
| D-12 | Elf and Orc art | Tinted shared set for now. | PE-4 |
| D-13 | Two-node hexes | Blocked when any of its nodes holds demons. | PL-9 |
| D-14 | Board and fair starts | Resource hex types and numbers dealt fresh each game, as in a normal setup. | BD-1 to BD-6 |
| D-15 | Black Forge | Moved into the Fire region. | CC-11 |

### 13.2 Second round (18 Nov 2026, owner accepted the recommendations)

| Id | Question | Owner's answer | Where |
|---|---|---|---|
| Q-1 | The campaign names terrain at fixed hexes; a fresh deal could change them | Pin the 7 named hexes; deal the other 31 | BD-2, CC-12 |
| Q-2 | The Air route stood in the Fire plague region | Move the border: the north-east mountains belong to Air; graph regenerated (Appendix B); the Black Forge moves to (4, -3) | PL-2, PL-4, CC-11 |
| Q-3 | Can a rival's altar open the board? | No: only the player's own temple | GB-1 |
| Q-4 | Spire tracks and where the stems live | Spaceship for spire 2, two gospel packs for 3 and 4; stems stay local | SP-3 |
| Q-5 | Port placement | Fixed spots, kinds shuffled each game | BD-5 |

No open questions remain. New ones found during the build go to the owner, not into the code.

---

## 14. Risks

| Risk | Mitigation |
|---|---|
| Two catch-up rules (no trades with the leader, war on a 2-point lead) stall games near 9 points. | Proof measures rounds to 10 and points lost; tune the war threshold, `RuinTicks` and training speed first. |
| 10–13 epidemics per game (a 7 is 1 roll in 6) overwhelm the board. | Long gentle rate track; grace rounds; armies purge; tuning in the proof. |
| The N-peoples refactor ripples through ~290 references and breaks tests. | Opus agent, alone on those files, first. |
| The god board is the first heavy UI and must work on a phone. | Phone layout from the first capture; one UI framework (UI Toolkit). |
| Soldier art and animation for four peoples is the largest cost. | Shared body with per-people dressing; gear pieces instead of separate models. |
| Individual trees in Historic cost frame time. | Instancing; a tree cap per hex; measured in G4-3. |
| Spire music comes from the owner's earlier projects, which may not be cleared for the public repo. | Q-4; packs are data, so stems can stay local and the build falls back to spire 1 if they are missing. |
| The campaign race tuned for Historic now starts at minute one. | G2-3 retunes and reruns the campaign proof. |

---

## Appendix A. Simulation interface sketch (not binding until G0-1 and G1-1 land)

**State.** `WorldState.Peoples` (list: id, name, colour, faction ref). `WorldState.Table` (turn order, current people, phase, hourglass ticks, hands, pieces, ports, offers, points cache, longest-road holder, round, roll RNG). `WorldState.Plague` (graph from Appendix B: node hex, region, trails; deck, discards, rate index, demons per node, outbreaks, cured, eradicated). `WorldState.War` (soldiers: id, people, stats, role, node; war state; ruins). `WorldState.Processing` (huts per settlement, lodge queues).

**Commands** (`PlayerId` = the acting people): `Sacrifice`, `LeaveTable`, `PlaceSettlement{corner}`, `PlaceRoad{edge}`, `UpgradeCity{corner}`, `BankTrade{give, get}`, `OfferTrade{to, give, get}`, `AnswerOffer{id, accept|decline|counter}`, `EndTurn`, `Banish{hex}`.

**Events:** `TurnStarted`, `Produced{hexes}` (no roll value), `Epidemic`, `Infected`, `Outbreak`, `IslandFell`, `Built`, `Traded`, `OfferMade`, `OfferAnswered`, `PointsChanged`, `WarStateChanged`, `DuelStarted`, `SoldierDied`, `SettlementBurned`, `CampLooted`, `CrystalRewarded`, `SpireRaised`, `AgeEnded`.

**Refusals** (plain sentences, as today): `not_your_turn`, `cannot_afford`, `too_close`, `not_connected`, `no_pieces`, `offer_closed`, `leader_refuses`, `table_closed`.

## Appendix B. The plague graph (owner's Q-2 regions, 18 Nov 2026; hand-tune before release, PL-13)

Generated by `Tools/godboard/plague_graph.py`, which also writes `Docs/godboard_plague_graph.json` (the file the sim loads). Regions, two-node hexes and crossings are authored tables in that script; only the trails inside regions are generated. Node positions are world metres (X east, Z north, island centre at 0, 0), on the hex given as (q, r). Ids are 1-based. Two nodes on one hex sit 20 m either side of its centre.

**Regions (resource hexes):** Earth (west, 10): (-3, -1), (-2, -1), (-1, -1), (-1, -2), (-3, 0), (-2, 0), (-1, 0), (-3, 1), (-3, 2), (-3, 3). Fire (east, 8): (3, -3), (3, -2), (3, -1), (2, -1), (3, 0), (2, 0), (1, 0), (0, 1). Water (south, 8): (2, -4), (0, -3), (1, -3), (0, -2), (1, -2), (2, -2), (1, -1), (0, 0). Air (north and the mountains, 12): (-2, 1), (-1, 1), (1, 1), (2, 1), (-2, 2), (-1, 2), (0, 2), (1, 2), (-2, 3), (-1, 3), (0, 3), (-2, 4).

| Node | Element | Hex | X | Z | Node | Element | Hex | X | Z |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Earth | (-3, -1) | -363.7 | -90.0 | 25 | Water | (2, -4) | 0.0 | -360.0 |
| 2 | Earth | (-2, -1) | -259.8 | -90.0 | 26 | Water | (0, -3) | -155.9 | -270.0 |
| 3 | Earth | (-1, -1) | -165.9 | -72.7 | 27 | Water | (1, -3) | -52.0 | -270.0 |
| 4 | Earth | (-1, -1) | -145.9 | -107.3 | 28 | Water | (0, -2) | -121.2 | -170.0 |
| 5 | Earth | (-1, -2) | -207.8 | -180.0 | 29 | Water | (0, -2) | -86.6 | -190.0 |
| 6 | Earth | (-3, 0) | -311.8 | 20.0 | 30 | Water | (1, -2) | -20.0 | -180.0 |
| 7 | Earth | (-3, 0) | -311.8 | -20.0 | 31 | Water | (1, -2) | 20.0 | -180.0 |
| 8 | Earth | (-2, 0) | -207.8 | 0.0 | 32 | Water | (2, -2) | 103.9 | -180.0 |
| 9 | Earth | (-1, 0) | -103.9 | 0.0 | 33 | Water | (1, -1) | 34.6 | -100.0 |
| 10 | Earth | (-3, 1) | -259.8 | 90.0 | 34 | Water | (1, -1) | 69.3 | -80.0 |
| 11 | Earth | (-3, 2) | -207.8 | 180.0 | 35 | Water | (0, 0) | -20.0 | 0.0 |
| 12 | Earth | (-3, 3) | -155.9 | 270.0 | 36 | Water | (0, 0) | 20.0 | 0.0 |
| 13 | Fire | (3, -3) | 155.9 | -270.0 | 37 | Air | (-2, 1) | -155.9 | 90.0 |
| 14 | Fire | (3, -2) | 207.8 | -180.0 | 38 | Air | (-1, 1) | -52.0 | 90.0 |
| 15 | Fire | (3, -1) | 259.8 | -90.0 | 39 | Air | (1, 1) | 155.9 | 90.0 |
| 16 | Fire | (2, -1) | 145.9 | -107.3 | 40 | Air | (2, 1) | 259.8 | 90.0 |
| 17 | Fire | (2, -1) | 165.9 | -72.7 | 41 | Air | (-2, 2) | -103.9 | 180.0 |
| 18 | Fire | (3, 0) | 311.8 | -20.0 | 42 | Air | (-1, 2) | 0.0 | 180.0 |
| 19 | Fire | (3, 0) | 311.8 | 20.0 | 43 | Air | (0, 2) | 103.9 | 180.0 |
| 20 | Fire | (2, 0) | 207.8 | -20.0 | 44 | Air | (1, 2) | 207.8 | 180.0 |
| 21 | Fire | (2, 0) | 207.8 | 20.0 | 45 | Air | (-2, 3) | -52.0 | 270.0 |
| 22 | Fire | (1, 0) | 103.9 | -20.0 | 46 | Air | (-1, 3) | 52.0 | 270.0 |
| 23 | Fire | (1, 0) | 103.9 | 20.0 | 47 | Air | (0, 3) | 155.9 | 270.0 |
| 24 | Fire | (0, 1) | 52.0 | 90.0 | 48 | Air | (-2, 4) | 0.0 | 360.0 |

**Trails inside regions (66):** 1-7, 2-3, 2-5, 2-7, 2-8, 3-4, 3-8, 3-9, 4-5, 4-9, 6-7, 6-10, 7-8, 8-10, 10-11, 11-12, 13-14, 14-15, 14-16, 15-17, 15-18, 15-20, 16-17, 16-22, 16-23, 17-20, 17-22, 18-19, 20-21, 20-23, 22-23, 23-24, 25-27, 26-29, 27-28, 27-29, 27-30, 28-29, 29-30, 30-31, 30-33, 31-32, 31-33, 31-34, 33-34, 33-36, 34-36, 35-36, 37-38, 37-41, 38-41, 39-43, 39-44, 40-44, 41-42, 41-45, 42-43, 42-45, 42-46, 43-44, 43-47, 44-47, 45-46, 45-48, 46-47, 46-48.

**Crossings (7):**

- Fire–Water pass: node 16 on (2, -1) to node 32 on (2, -2).
- Water–Earth pass: node 28 on (0, -2) to node 5 on (-1, -2).
- Water–Earth pass: node 35 on (0, 0) to node 9 on (-1, 0).
- Earth–Air pass: node 8 on (-2, 0) to node 37 on (-2, 1).
- Fire–Air pass: node 19 on (3, 0) to node 40 on (2, 1).
- Water–Air pass: node 35 on (0, 0) to node 38 on (-1, 1).
- Earth–Fire tunnel (Old Quarry): node 4 on (-1, -1) to node 22 on (1, 0).

Hexes with two nodes: (1, 0), (2, 0), (3, 0), (2, -1), (0, 0), (1, -1), (1, -2), (0, -2), (-1, -1), (-3, 0).

Dead ends: 7.
