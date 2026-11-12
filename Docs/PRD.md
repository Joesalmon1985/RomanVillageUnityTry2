# Product requirements: the shared village world (MVP)

Status: draft for owner review, 12 Nov 2026. Sources and revisions: `SOURCES.md`. Build order: `BUILD_PLAN.md`. Checks: `TEST_PLAN.md`.

## 1. What this is

A single-player Unity game for Windows. The player walks around a small, working village in first person. Two villagers offer mini-games: Mira offers a Ward duel in the square, and a party host invites the player to a rhythm set in the warehouse. Around them the village runs a real economy the player can watch at street level or manage from a top-down God View. Both views show the same world, and economy, military and city development affect one another.

It combines four of the owner's games: RhythmWarfare (rhythm run), DuelMasterBattle (Ward duel, opening village), logistics-rts-sim (roads, carriers, production, ware-fed military) and Gods-and-Wizards (hex board, cities, demons, development draft).

## 2. Goals and non-goals

**Goals**
1. Within five minutes of pressing Play, a new player has walked, talked, duelled, played one rhythm phrase and seen a carrier deliver a load.
2. One authoritative simulation. First person and God View are two cameras and two control surfaces over it.
3. One connected economy in which a hex production draw, a road network, a powered military queue and drafted developments all depend on the same carried wares.
4. Everything that is blocked says why, in a sentence.
5. The owner can play a packaged Windows build end to end and give feedback against a written script.

**Non-goals for this release**
Multiplayer, phone support, more than one map, more than one rival, campaigns, story beyond greetings, tick-for-tick parity with the Godot sims, final art or audio, Sims 2 characters in any distributed build.

## 3. Decisions already made

| Decision | Choice | Made by |
|---|---|---|
| Characters | Original placeholder figures by default; Sims 2 animated bodies optional and local-only behind `ICharacterActor` | Owner |
| Project | Standalone `RhytmDrama\WorldMVP`, seeded from RomanVillageUnity | Owner |
| Economy port | Fresh C# sim using source data and test vectors; no parity requirement | Owner |
| Priority if time runs short | Walkable village, both encounters and the road economy are solid first | Owner |
| Ward duel essences | Flame, Frost, Vine, then Light, as the plan states. The source's Mira duel uses Flame, Frost, Stone; the story duels use Vine. | Plan |
| Mira's invitation | She accepts the first challenge. In the source she refuses twice first. | Plan |
| Rhythm final block | A failed final block keeps the player on the floor for a retry. In the source a failed block still routes, with an energy penalty. | Plan |
| Military supplies | Carried to the barracks flag. The source hybrid sim deducts from headquarters stock, against its own requirement. | Plan |
| Hex terrain | Each hex has one terrain and one resource. The source gives every hex a chance for all five resources. | Plan |

## 4. Player experience

### 4.1 The first five minutes

| # | Beat | Requirement |
|---|---|---|
| F1 | Spawn | Start in first person in the village square. WASD and mouse look work at once. Mira and the party host are within 25 m and in view; the warehouse door shows coloured light and plays muffled music that gets louder on approach. |
| F2 | Greetings | Each NPC turns to face the player within 4 m and shows a one-line greeting. E opens a short dialogue with a clear accept and leave. |
| F3 | Duel | Accepting Mira starts a practice Ward duel in the square with both wizards visible. After the result: rematch or return to walking at the same spot and facing. |
| F4 | Party | Accepting the host at the decks starts one 64-beat phrase. The final block offers bar or balcony. On success the chosen area lights up and can be walked into. After the result: retry at once or roam. |
| F5 | Carrier | From the square the player can see the storehouse, a road, and a carrier completing a first delivery within the first 60 seconds. The quartermaster asks for planks and stone and points at the buildings and road involved. Starter stock is enough to place a first building immediately. |
| F6 | God View | Tab toggles at any time. Returning restores the avatar's exact position and facing. During an encounter Tab changes only the camera; encounter controls and progress are kept. |

### 4.2 Ward duel (Mira)

| ID | Requirement | Starting value |
|---|---|---|
| W1 | Each side secretly sets a Ward of three loci from the essence pool; repeats allowed | 3 loci |
| W2 | Essence pool is Flame, Frost, Vine; Light is added from the player's fourth completed duel against Mira (after three completed) | unlock after 3 |
| W3 | Each side has ten casts. Sides cast independently, not in turns | 10 casts |
| W4 | After a cast the player must wait before the next cast | 2.0 s, tunable |
| W5 | Mira casts on her own cadence; her first cast comes shortly after the duel starts | think 2.5 to 6.0 s, first cast 1.5 to 4.0 s, tunable |
| W6 | Feedback per cast is three totals only: fractures (right essence, right locus), echoes (right essence, wrong locus, each Ward locus matched once), fades (the rest). Positions are never shown. Both sides' histories are visible | source `score_attack` |
| W7 | A cast that fractures all three loci breaks the Ward and ends the duel | |
| W8 | Outcomes are explicit: victory, defeat, both exhausted (stalemate), player exhausted first (Mira keeps casting until she breaks or exhausts), and two breaking casts in the same tick (clash, shown as a draw) | |
| W9 | Both Wards are revealed on completion | |
| W10 | Mira is seeded. She keeps every Ward consistent with her feedback so far, opens with two essences split across the loci, then casts a random remaining candidate; if none remain she resets | candidate filter, mistake rate 0, tunable |
| W11 | If the player does not cast for 60 s the duel casts a random legal weave for them | 60 s |
| W12 | The village clock pauses for the duel and resumes on exit. Leaving mid-duel counts as a forfeit, with no penalty in practice duels | |
| W13 | Completed-duel count against Mira is saved | |

### 4.3 Rhythm party (warehouse)

| ID | Requirement | Starting value |
|---|---|---|
| R1 | Four pads on D, F, J, K | |
| R2 | 174 BPM; one phrase is 64 beats in four blocks of 16; cues fall in beats 0 to 12 of a block; the block is judged at beat 12.5 | from `run.json` |
| R3 | Judgement windows around the cue: perfect within 45 ms, great within 90 ms, accepted within 140 ms; all three count as a hit. A press with no cue in reach is a stray and costs nothing | 45 / 90 / 140 |
| R4 | Holds score by coverage; released within 0.15 s of the tail counts as complete | 0.15 s |
| R5 | A block passes when mean cue credit is at least 0.6 | 0.6 |
| R6 | Mix stages: drums alone, bass enters, drums return, bass strongest. A pass moves up one stage; a miss holds the stage. Drum blocks use pads 1 and 4; bass blocks use all four | stage gains from `run.json` |
| R7 | Note timing and stems run on the audio DSP clock; stems stay sample-aligned; a 2 s count-in precedes beat 0; a calibration offset is adjustable | lead-in 1.2 s fall time |
| R8 | The bar or balcony choice opens at beat 48 and can change until beat 60.5. Choose with the left and right arrow keys or by looking at the lit doorway and pressing E. No choice means the bar | |
| R9 | Party success is three or more passing blocks. If the final block passes, the chosen route opens on the downbeat at beat 64, lights up, and becomes walkable. If it fails, the player stays on the floor and can retry | |
| R10 | Music: the owner's own Lv1 Warehouse loops are imported when present. Otherwise an original synthesised drum and bass loop plays with a hand-aligned fallback chart | |
| R11 | The village clock pauses for the phrase and resumes on exit | |

### 4.4 Two views

| ID | Requirement |
|---|---|
| V1 | First person: walk, run (Shift), look, E to interact, collision with buildings and terrain. |
| V2 | God View: orthographic overhead camera. Pan with WASD or middle-drag, zoom with the wheel, click to select buildings, roads, flags, cities, squads and the hero. The avatar is shown as a marker and does not move. |
| V3 | God View controls: place and remove roads, place and cancel building plans, set production priority, enable or disable a building, found a city, trade, end the round, move the hero, draft and build cards, build wonder stages, queue units, give and cancel squad orders. |
| V4 | First person reaches the same commands through people and signs: the quartermaster (building and roads), a sign at each building (priority, enable, status), the market keeper (trade), the town bell (end round), the captain (units and orders), the scribe (cards and wonder). |
| V5 | Selecting anything in either view shows the same inspect panel: what it is, its stock, what it is waiting for and why. |

## 5. The economy

### 5.1 World

One settlement on a small hex region (radius 2: 19 hexes). Cities sit on hex vertices, roads on hex edges, and every building stands at a vertex flag. Carriers walk the same road edges the player places. The avatar walks freely on the terrain.

Terrain and raw goods: forest gives timber, hills give stone, fields give grain, mountain gives ore, meadow gives nothing. Coin is the only good that is not carried.

### 5.2 Buildings and chains

| Building | Needs nearby | Recipe | Notes |
|---|---|---|---|
| Storehouse (home city) | | stores everything | Start building; starter stock here |
| Forester | forest hex | plants trees | Keeps a forest hex productive |
| Woodcutter | forest hex | timber batch to timber | Works only against an authorised batch |
| Sawmill | | 1 timber to 1 planks | |
| Quarry | hills hex | stone batch to stone | |
| Farm | fields hex | grain batch to grain | |
| Grain mill | | 1 grain to 1 flour | |
| Bakery | | 1 flour to 1 rations | |
| Ore pit | mountain hex | ore batch to ore | Mountain is reachable only from a second city |
| Refinery | power | 1 ore to 1 ingots | Stalls without power |
| Power plant | | burns timber, supplies power | |
| Barracks | power | rations and ingots to infantry | Supplies are carried to its flag |
| Market | | trade | At the home city |
| Wonder site | | two stages paid in delivered goods | Victory points |

All names are original working titles; no data tables are copied from reference games.

### 5.3 Rules

| ID | Requirement |
|---|---|
| E1 | Roads: a road segment joins two adjacent flags, must connect to the player's network, and costs goods. Removing a road stalls routes that used it. |
| E2 | Construction: a plan becomes a site that requests its materials one ware at a time; builders start when everything has arrived. |
| E3 | Hauling: each ware moves as a named load carried between flags. Wares are reserved at the source, leave it on pickup, and count at the destination only on arrival. Goods in transit cannot be spent. |
| E4 | Production: a building works only with its worker present, inputs on hand, and output space free. Inputs are consumed once per cycle. |
| E5 | Diagnostics: any job, site, building or queue that is not progressing shows one reason: no road, no carrier, waiting for a ware (named), demon on the route, output full, input missing (named), no power, no batch this round, invalid site, no action left this round, switched off. |
| E6 | Starter commission: connect a forester, woodcutter, sawmill and quarry to the storehouse and hold 10 planks and 10 stone there. The opening map and seeded draws make this achievable without trading. |
| E7 | Strategic rounds: the player ends a round when ready. At the start of each round a seeded production value is drawn; each matching hex next to one of the player's cities authorises one harvest batch, once. Workers fulfil it and carriers haul it. |
| E8 | Cities: a new city needs a connected road, an unoccupied vertex, and no city on any adjacent vertex. Each city is worth 2 victory points and brings its adjacent hexes into production. |
| E9 | Market: trade storehouse stock at a fixed rate (starting 4 for 1), or sell for coin. |
| E10 | Round end: the rival takes its turn, then infection is drawn. A drawn flag gains a demon; a flag already holding three adds one breach instead. A demon on a flag blocks deliveries through it and stops production at a city on it. |
| E11 | Hero: moves along edges with a limited number of moves per round and clears all demons on a flag by arriving. |
| E12 | Ages and draft: three ages of two rounds. At the start of each age the player and the rival are each dealt three cards, pick one, pass the rest, and pick one more. A drafted card can be built at a city for goods or coin, kept, or discarded for coin. |
| E13 | Cards change a later decision: a timber card enlarges eligible forest batches, a market card improves the exchange rate, a military card shortens training once supplies have arrived, a ward card slows breaches, a monument gives points. Nine cards in the first release. |
| E14 | Wonder: two stages built from delivered planks and stone (then rations and ingots), each worth victory points. |
| E15 | Power and military: the refinery and the barracks queue need power; with too little, the queue stalls and resumes when supply returns. One infantry type, trained from delivered rations and ingots. |
| E16 | Orders: the player gives a squad a delegated order (defend a flag or road, attack the rival outpost) and watches it move and fight. No direct unit control. A squad arriving on a demon flag clears it. |
| E17 | Rival: one visible, reachable outpost. Each round the rival gains points on a simple seeded plan, drafts by a scoring rule, and reinforces or raids. Destroying the outpost removes its raids and scores points. |
| E18 | Ending: first to 8 victory points wins; 4 breaches loses; otherwise points are compared at the end of Age III. A full economy playthrough takes 10 to 15 minutes. |
| E19 | The settlement keeps running while the player walks or manages. Round actions resolve at a tick boundary. |

### 5.4 The proof chain

A forest draw authorises a timber batch. The woodcutter fells it at the forest flag, a carrier hauls it to the sawmill, and a second delivery brings planks to the storehouse. Those planks go on to build the barracks or a wonder stage. A built timber card changes the next eligible batch. Food and ore chains run in the same world, so a barracks short of rations or ingots shows which delivery is holding up the squad.

## 6. Saving

One save slot, available between encounters (F5 to save, F9 to load, and on quit). It stores the board, stocks, shipments in flight, queues, round and age, draft state, random state, NPC progress and the player's position and facing. A shipment in flight when saved arrives exactly once after loading.

## 7. MVP scope

**Must be solid (owner's priority):** F1 to F6, W1 to W13, R1 to R11, V1, V2, V5, E1 to E6, and the building, road and priority parts of V3 and V4.

**Should be present, depth as time allows, in this order:** E7 to E9; E10 and E11; E12 to E14 with at least six cards; E15; E16 and E17; E18; saving; Windows build.

**Deliverables:** an openable Unity project with its startup scene registered; a Windows build if the build succeeds; `PLAY_GUIDE.md`; `SOURCES.md` kept current; `PROGRESS.md` listing which tests and playthroughs actually ran.

**Out:** everything in the non-goals list, plus rhythm phrases beyond the first, duel opponents beyond Mira, building upgrades, fog of war, more than one infantry type.

## 8. Quality bar

- 60 frames per second at 1080p on the owner's PC in both views.
- No console errors during the scripted playthrough.
- Text readable at 1080p from a normal seating distance; every panel fits one screen.
- Rhythm hits feel on time at default calibration with wired headphones or speakers.
- Deterministic: the same seed and commands give the same result.

## 9. Open questions for after the first playthrough

1. Should Mira keep her source behaviour (refusing twice, then duelling in earnest)?
2. Should rounds stay player-ended, or run on a visible timer?
3. Which of the owner's other loops should become rhythm phrases two and three?
4. Is IMGUI good enough for the next milestone, or should the God View move to UI Toolkit first?
5. DuelMasterBattle is published as GPL-3.0-or-later. The duel rules here are a fresh C# implementation of the owner's own design; the owner should confirm the licence wanted for this project.
