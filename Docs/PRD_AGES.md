# PRD: the later ages (Historic growth, Modern, Future)

Written 14 Nov 2026 on branch `docs/next-ages`, cut from the prehistoric refactor (`32d11ef`). This document plans the later ages. It changes no code. It is binding for agents only once the owner has read it. Until then, where it differs from `REFACTOR_SPEC.md`, the spec wins.

Sources of authority, in order: the owner's answers (14 Nov 2026), the orchestrator's limits (original content only; no copied names, rosters or data tables), `REFACTOR_SPEC.md` for everything already built, then the owner's brief for the modern and future ages. The two sibling documents are `PRD_CRYSTALS.md` (the four-crystal campaign in the Historic age, led by Mira) and `PRD_CHARACTERS.md` (rigged cast and animation). Where this document needs something from them, it says so in section 16.

Reading guide: sections 1 to 4 are the plan in plain words (about 10 minutes). Sections 5 to 9 are the rules and tables. Sections 10 to 12 are how it gets built and proven. Sections 13 to 15 are risks and the owner's decisions. The appendix is the interface sketch for the agents.

---

## 1. Summary

One island, four ages. The prehistoric camp (built) grows into a Roman village (Historic), then a harbour town at war (Modern), then a damaged industrial district (Future). The same seeded board, the same faction ids, the same landmarks and the same simulation carry through.

- **Prehistoric to Historic** is not a time jump. It happens when the prince's people first deliver a workshop-made good to a building site (the handover milestone, section 4.2). From then on the prince builds on his own and the player advises. The prince then invites the player to the Historic age. Historic unlocks the full faction tree and the first building upgrades.
- **Historic** holds the crystal campaign (`PRD_CRYSTALS.md`). In parallel, the faction grows into one complete original tribe, the **Valley people**: 43 wares, 26 kinds of worker, 40 building types, soldiers with three trainable skills, four military buildings, two training sites. Every building can be upgraded once in Historic (paid in goods) and once in Modern (paid in credits), each upgrade measurably better at its job.
- **Victory points and credits** live in one ledger per faction. Points come from permanent milestones (camps, cities, towns, first iron, a trained soldier, a first sale, captures, crystals). At 10 points a faction unlocks the **Modern roster**: 17 original structures by function (command post, power, refinery and harvester, storage, foundry, machine shop, infantry hall, motor works, signal tower, service bay, research institute, two defences, two walls) and 10 units. Building them costs delivered goods and credits. Their queues need physical goods.
- **Historic to Modern** and **Modern to Future** are time jumps, each one saveable operation with a small transfer table (what carries, what becomes history).
- **Modern mission: the harbour blockade.** Build a powered base from delivered goods, train a squad and a tank, scout, then break the blockade by a frontal push or a flank on the relay. Win when the convoy arrives. The route you open changes the Future start.
- **Future mission: bring the core online.** In one district hex, a local tile grid holds a drill, directional belts with finite buffers and backpressure, a splitter or junction choice, a power network with a battery and brownouts, turrets that need physical ammunition, and an assembler that makes a repair drone. Survive five announced waves.
- **AI.** The faction AI grows from today's prince brain into a needs-based economy and military planner, built on the technique of the Widelands computer player. The Modern side gets a modular base and army AI built on the technique of the OpenRA modular bot. Both are fresh C#, deterministic, and proven headless before any view work.
- **Player.** Still a viewpoint, no build menu. Leaders propose and act; the player advises, vetoes, and gives delegated orders from the map. The direct-build tools stay behind F1.

Honest scale: Slice H1 is about a week of agent work. H2 (the complete faction with military and AI) and M1 (the Modern proof) are each several weeks. F1 is two to four weeks. The finished game (more factions, air, sea, more sectors) is beyond this plan and is tracked in the coverage table.

---

## 2. What exists now, and what can be reused

Checked against the code at `32d11ef` and the archive, not just the docs.

| Source | What is there | Reuse for this PRD |
|---|---|---|
| `Assets/World/Sim/Economy` (live) | 10 wares (`Ware` 0-9, id 10 reserved for Ore), 16 building types (`BuildingDefs.cs`), per-camp storehouses, plots, carriers, jobs, shipments, city upgrade, warriors and meals, ware ledger and invariants | The base everything grows from. New wares and buildings are added to the same tables; upgrades reuse the site and delivery flow; the city upgrade is the model for every grade upgrade. |
| `Assets/World/Sim/Factions` (live) | `Brain.cs` (654 lines): proposal queue of 3, candidate scoring, expansion target, scarcest-ware bonus, workshop and lodge stages, rival auto-cast per spell pace; `FactionsState` with tuning; strength, overrun warning, endings | The faction AI is an extension of this brain (section 8.2). The proposal queue and speakers stay the player-facing surface. |
| `Assets/World/Sim/Demons`, `Board`, `Save` | Demons, board radius 4 (61 hexes, 150 corners, 210 edges), `PlotLayout`, save v4 (`SaveCodec`, older refused) | Kept. Board gains landmarks and mountain veins; `PlotLayout` gains town plots; saves gain versions with migration from v4. |
| `Assets/World/Sim/Core/WorldSim.cs` `SystemOrder` | Slots 100-1300, plus unused older names `Power` 700, `Barracks` 800, `Squads` 900, `Combat` 1000 | New systems take those slots (appendix A.1). |
| `Assets/World/Sim/Core/Reasons.cs` | Closed stall list; `no_power` kept but unused | Reused for power stalls. |
| Tests: `GameRunner`, `ScriptedPlayer`, `ProofReport`, `ConservationTests`, `ApiSurfaceTests` | Headless whole-game runs, scripted players, byte-identical proof report, invariant checks, reflection surface checks | Same pattern for every new proof (`AGES_PROOF.md`). |
| `Archive/Sim/Military` | Power summary (supply per fuelled plant, demand per building), barracks queue (max 8, ware-fed: 1 Weapons + 1 Rations), squads, outpost combat with HP, damage and cooldown at fixed ticks, raids | Power accounting and the ware-fed queue shape for Modern; the fixed-tick combat loop for soldiers and units. Written on the old flag graph: a port, not a switch. |
| `Archive/Sim/Economy` | Earlier economy: 14 wares, 20 buildings including Mine, Smelter, Power plant, Barracks, Market, Charcoal kiln, Smithy, Tavern | Recipe ideas (ore plus coal to iron, coin from a market). No direct code reuse (flag graph). |
| `Archive/Sim/Strategy` | Age I-III draft counter, cards, wonder stages, end-of-age comparison | Not reused. Our chapter ids are deliberately separate from its `Age` counter (AG-4). The wonder's staged site is a model for multi-step builds. |
| `Archive/Tests/EditMode/StrategyPlayer.cs`, `EconomyProofReport.cs` | The competent scripted player the brain was ported from; the older proof | Patterns for scripted opponents (passive, aggressive). |
| `_sources/logistics-rts-sim` (owner's own) | Hybrid requirements: no instant production, logistics-fed construction, production and repair, power gating, radar as an economy-built structure, delegated military control (stances, rally, guard, delegated attack on visible targets only), `input_missing` stalls; heuristic economy and military bots | The control model for the player in Modern (section 8.4) and the logistics-fed queue rules (section 7.4). Ideas only; it is GDScript. |
| `_sources/gods-and-wizards` (owner's own) | Two views of one state (first person and overhead), threat spread, card gating of technology | The map as the place for delegated orders; visible pressure on the island. |
| DuelMasterBattle `1b9513dd` `wizard_rts/vp.py` (owner's own) | Points written once under a key, so saves and replays never double count | The ledger's idempotent milestone keys (section 6.1). |
| DuelMasterBattle `industry/hybrid.py`, era audit | Industrial blocks that make old wares faster; one shared store between roads and belts; audit found 66 dead-end wares when tools had no use | Rule AG-14: every ware has a producer and a consumer, and tools are consumed as worker equipment. The transfer station boundary (section 9.2). Some donor tables derive from GPL sources: no data copied. |
| Widelands computer player, OpenRA modular bot (public source) | Techniques, summarised in section 8.1 | Technique only. GPL: no code or tables copied. |

Not present and needed: any notion of age or chapter in the live sim, a points or credits ledger, building grades, soldiers with levels, fog of war, sim-side lattice pathing (the walking lattice exists only in the game layer, `Game/Player`), units, orders, power in the live sim, belts, scenarios and transitions.

---

## 3. Requirements

Ids are stable. "Test" names the proof that closes each one (section 12).

### Ages and transitions

| Id | Requirement | Test |
|---|---|---|
| AG-1 | One island, four eras: Prehistoric, Historic, Modern, Future. Same seed, board, corner, hex-edge and plot ids, faction ids (0 player, 1 Ash Clan), landmark ids. | `EraTests` |
| AG-2 | The handover milestone (4.2) is detected per faction, fires `PrinceHandover` once, and is saved. | `HandoverTests` |
| AG-3 | After handover the player's faction is autonomous: it executes its own top proposal after an announce window. Casting still works and skips the wait. | `AutonomyTests` |
| AG-4 | Chapter ids (`ch.prehistoric`, `ch.historic`, `ch.modern.harbour`, `ch.future.core`) and the `Era` enum are separate from the archived strategy `Age` counter and from any archived id. | `EraTests` |
| AG-5 | Each step is one command, `AdvanceChapter`, applied atomically in one tick, refused with a reason when its conditions do not hold, and saveable on both sides. | `TransitionTests` |
| AG-6 | Each time jump applies its authored transfer table (4.4, 4.5) and guarantees a minimum starting kit, so every valid earlier run can continue. | `TransitionTests` |
| AG-7 | Standalone development starts: `dev.historic`, `dev.modern.harbour`, `dev.modern.skirmish`, `dev.future.core` (with a chosen Modern outcome). They are ordinary scenario starts, reachable from F1 and the command line. | `StartTests` |
| AG-8 | The Prehistoric 20-minute skirmish is unchanged and stays the default new game until the campaign entry exists. | existing proof |
| AG-9 | In the campaign, an overrun costs a camp (never the home camp) or, with only the home left, loots it and forces a retreat into it; nothing ends the game (4.3). In standalone skirmish it stays fatal. | `CampOverrunTests` |

### The faction and upgrades

| Id | Requirement | Test |
|---|---|---|
| AG-10 | The Valley people's full tree (section 5) is data in code tables, one row per ware, worker, building and soldier skill. | `TreeAuditTests` |
| AG-11 | Every building has grades: Prehistoric (0), Historic (1), Modern (2). A building born in Historic starts at grade 1. | `UpgradeTests` |
| AG-12 | Grade 0 to 1 is paid in delivered goods and allowed only from the Historic era; grade 1 to 2 is paid in credits and allowed only from the Modern era. | `UpgradeTests` |
| AG-13 | An upgrade keeps the building's id, plot, stock, reservations and valid incoming deliveries. It pauses work while upgrading. Cancelling returns delivered goods and credits. Each upgrade's benefit is measured in the building's role (5.6) by a test that runs the building before and after. | `UpgradeBenefitTests` |
| AG-14 | Every ware has at least one producer and one consumer; every tool is consumed as worker equipment. | `TreeAuditTests` |
| AG-15 | A coverage table (5.7) tracks every ware, worker, building, recipe, soldier skill, roster structure and unit to sim, test, proof, view and AI use. It is generated by a test from the code tables. | `CoverageReport` |
| AG-16 | Soldiers are trained, garrisoned, fed, sent to attack, defend, and capture, all in the deterministic sim. In Historic, real attacks replace the abstract overrun. | `MilitaryTests` |

### Points, credits, unlock

| Id | Requirement | Test |
|---|---|---|
| AG-20 | One ledger per faction holds victory points, credits, reserved credits, the roster unlock flag and its tick, and the milestone keys already paid. | `LedgerTests` |
| AG-21 | Points are permanent milestones written once under a key; saves, loads and repeated events never pay twice. | `LedgerTests` |
| AG-22 | A faction can earn its first 10 points through economy and military play alone (6.1). | proof section V |
| AG-23 | The roster unlocks permanently for a faction when it first holds 10 points. The check runs on every roster construction and production command for the commanding faction only. Player, prince and rivals use the same check. | `UnlockTests` |
| AG-24 | Credits come first from selling delivered goods at a Trading post, then from the harvester and refinery loop. | `CreditTests` |
| AG-25 | Roster construction needs delivered goods and credits. Credits are reserved when the site is placed, spent when it completes, refunded on cancel. Goods follow the existing site flow. | `MixedCostTests` |
| AG-26 | Roster production queues need physical goods delivered to the building and a per-item credit charge, taken when the item starts and refunded on cancel. | `QueueTests` |
| AG-27 | The HUD (H) and the leader's lines show points, the threshold, credits, and both cost types before anything is committed. | PlayMode `LedgerHudTests` |

### Modern and Future

| Id | Requirement | Test |
|---|---|---|
| AG-30 | Units move on a sim-side copy of the walking lattice (5 m cells) with deterministic pathing. | `LatticeTests` |
| AG-31 | Fog of war per faction on a 20 m sight grid: unseen, remembered, visible. AI target choice uses only what its faction has seen. | `FogTests` |
| AG-32 | Power per faction: supply against draw. Under 100%, powered production slows in proportion and powered defences and the signal tower go offline. | `PowerTests` |
| AG-33 | The harbour blockade mission plays end to end with two approaches, a tracked convoy win, defeat and retry, and a visible effect on the Future start. | `HarbourMissionTests` |
| AG-34 | The Future district has one inventory boundary (the transfer station) between roads and belts; every item is owned by exactly one place each tick. | `IndustryConservationTests` |
| AG-35 | Belts are directional with finite buffers and backpressure; a splitter and a junction exist; power has a battery and brownouts; turrets consume physical ammunition; one assembled drone joins the army. | `IndustryTests` |
| AG-36 | Five telegraphed waves; losing the core offers a retry from a checkpoint. | `CoreMissionTests` |

### AI and player

| Id | Requirement | Test |
|---|---|---|
| AG-40 | The faction AI plays the full faction from needs and state: build order, expansion, worker and tool supply, training, garrisons, attacks, all deterministic. | proof section F |
| AG-41 | The RTS AI builds a base, manages power and income, produces a unit mix, forms squads, scouts, attacks the enemy base, defends and retreats. | proof section R |
| AG-42 | The player advises (priority, favour, veto, ask) and gives delegated orders from the map. Normal play shows no buttons and no build menu; F1 keeps direct tools. | PlayMode `AdviceTests` |
| AG-43 | Same rules and same AI for player-side leaders and rivals; rivals never read hidden information for military choices. | `AiFairnessTests` |

### Architecture

| Id | Requirement | Test |
|---|---|---|
| AG-50 | Pure C# deterministic sim, integer state, commands in, events out. `World.Game` never writes state (grep test). | existing grep test, `DeterminismTests` |
| AG-51 | Headless proof first (`dotnet test DotNet`), then views. | gate per slice |
| AG-52 | Save versions 5 to 8, each reading every older version from 4 up by migration; version 3 still refused. | `SaveMigrationTests` |
| AG-53 | Landscape phone layout is the design target; tested in phone-sized Windows windows of 844 x 390 and 932 x 430 (the sizes the other two PRDs use). | PlayMode `LayoutTests` |

---

## 4. Age model and transitions

### 4.1 Eras, chapters and landmarks

| Era | Chapter id | What the island is | Who leads the player's people |
|---|---|---|---|
| Prehistoric (0) | `ch.prehistoric` | Camps on hex corners, the dance circle | The prince proposes; the player casts every construction |
| Historic (1) | `ch.historic` | A Roman-styled village and the crystal campaign | The prince, autonomous after handover; the player advises |
| Modern (2) | `ch.modern.harbour` | A harbour town under blockade | The prince's heir, Commander Ilse Teague, with Quartermaster Tobin Hale (working names) |
| Future (3) | `ch.future.core` | An industrial district cut off by hostile machines | Engineer Rhea Okonkwo-Dahl (working name); Mira recurs through the crystal link |

Names are working names, original to this game, open for the owner to change. `PRD_CHARACTERS.md` decides who gets a rig.

Landmarks are stable string ids in `Board.Landmarks` (new), each with a corner, hex or hex edge:

| Id | Where (default board) | Kept as |
|---|---|---|
| `lm.home_hearth` | corner 70 (the player's start camp) | the village square in Historic, the town hall in Modern, the district gate in Future |
| `lm.ash_hearth` | corner 97 | the Ash Clan's seat in every era |
| `lm.dance_circle` | plot 635 | the festival ground (Historic), a memorial square (Modern, Future) |
| `lm.harbour` | a rim corner chosen by the generator: the rim corner nearest corner 70 by hex-edge distance, ties by lower id, skipping the four rim corners that `PRD_CRYSTALS` Appendix C gives to the towers | quay in Historic, the blockaded harbour in Modern, the evacuation dock in Future |
| `lm.tower.earth`, `.water`, `.air`, `.fire`, and `lm.altar.<element>` | placed by `PRD_CRYSTALS.md` (its Appendix C): the campaign module registers them with `LandmarkApi.Register` when a campaign starts, so they are absent in skirmish | ruins, then restored towers; power conduits in Modern (7.2, R2) |
| `lm.relay` | an interior corner between the harbour and the Ash Clan seat (generator rule in 9.1) | a signal relay in Modern |
| `lm.district` | the hex the Future mission converts (rule in 9.2) | the industrial district |

The board has no sea terrain. The harbour sits on the rim: the sea is outside the board (a view), roads may end at rim corners, settlements may not (as now).

### 4.2 Prehistoric to Historic: the handover

**The exact milestone.** A faction reaches handover at the first tick on which a carrier delivers a worked ware (Planks, Bricks, Bread, Meat or Cut stone, ids 5-9) to a construction site of that faction (building, road or camp site), or a city upgrade of that faction moves a worked ware from stock into its upgrade. Starting stock holds no worked goods, so such a ware was always made by that faction's own workshop: the first supply chain has delivered.

Detection is in the sim (the economy raises an internal hook on `WareDelivered` and on the city upgrade's stock move). On that tick:

1. `Faction.HandoverTick` = now; event `PrinceHandover{Faction}` (once per faction; the rival gets it too, for the record).
2. For the player's faction, `Faction.Auto` becomes true with the announce rule (AG-3, 8.3).
3. For the player's faction, the chapter becomes eligible to advance (`ChapterReady{ch.historic}`).

**The invitation.** The prince says so in the world ("The sawmill fed the builders. I can run the camp now. Come and see what it can become."). Replies: "Show me" (submits `AdvanceChapter{To = ch.historic}`), "Not yet" (asked again after 3 minutes), walking away (asked again on his next pitch). In a scripted or AutoPlay run, the advance can be submitted directly.

**What the step does.** Nothing is transferred: it is the same moment. `Eras.Current` = Historic. The Historic content unlocks (section 5): new building types, grade-1 upgrades, the city to town upgrade, Historic soldiers. Each faction's home storehouse receives the **smith's chest** (starting kit, 5.5): one of each tool and 4 Iron, so the tool chain can bootstrap. Views re-dress camps over time by grade, not in one swap.

**Standalone skirmish.** The 20-minute Prehistoric skirmish never advances. The handover still fires there (the prince becomes autonomous), because the owner's rule says so for the faction. Open decision 14.2 asks whether that is wanted in skirmish.

Expected timing from the existing proof data: a prompt player has its first workshop goods at about 5 to 8 minutes. Proof section H measures it.

### 4.3 Losing in the campaign (owner decision 4)

In the campaign (`Eras.Mode` = Campaign), the overrun rule of the refactor changes outcome:

- Prehistoric and early Historic (before H2 military): when an overrun completes, the attacker captures the defender's settlement nearest the attacker's war camp, **never the home settlement** (all its buildings and stock change owner; its jobs are cancelled with goods returned home where possible). Event `CampOverrun{Attacker, Defender, SettlementId, Kind = CampLost}` (the shape `PRD_CRYSTALS.md` asks for, its N-2).
- If the defender has only its home settlement left, the people **retreat** into it: the home camp is kept (crystals PRD N-3), half of its stock is lost (looted, ledger `Looted`), and its plot buildings stop for 1800 ticks. Event `CampOverrun{..., Kind = Looted}`.
- The crystal campaign uses `CampOverrun` to give the rival expedition time (its clock is owned by `PRD_CRYSTALS.md`).
- After an overrun, no new overrun warning can start for `SetbackGraceTicks` = 3000 (5 minutes). While a campaign runs, neither overrun nor the time limit ends the game (`GameEnded` is not raised).
- From H2 on, overrun is replaced by real attacks (5.4). In the campaign, the home settlement cannot be captured: an attack that empties its defences loots it as above. A capture of a campaign settlement raises `CampOverrun` too (Kind `CampLost`, or `Looted` for a home settlement), whoever the attacker is, so the crystal campaign's hooks (its N-2) are unchanged by H2.
- `StartCampaign` (crystals) switches `Eras.Mode` to Campaign through `Eras.EnterCampaign(WorldState)`; `AgesApi.IsCampaign` reads only that mode. The standalone `dev.historic` start has mode DevStart until a campaign starts.
- Standalone skirmish keeps overrun fatal and the time limit as built.

### 4.4 Historic to Modern (a time jump)

**Trigger.** The crystal campaign's ending (`PRD_CRYSTALS.md`: the fourth restoration after the final confrontation) raises `ChapterReady{ch.modern.harbour}` (the Eras code listens for `CampaignEnded{Won}`, copies `CampaignApi.Record` into `History` and raises it; the crystal campaign offers the step as its reply "Onward, thirty winters on"). Mira and the prince's heir invite the player to the harbour ("Thirty winters on..."). `AdvanceChapter` then applies the transfer. Until the crystal ending exists, a test fixture raises the same event (reported as fixture, not as integration).

**Transfer table (authored, data in `Eras/TransferTables.cs`).**

| Thing | What happens |
|---|---|
| Landmarks | All kept, with state (tower restored or ruined, crystal held by whom). |
| Roads | All roads of both factions stay roads. Road sites are cancelled; their delivered goods go home. |
| Settlements | All kept with grade and owner. Settlement sites are cancelled. |
| Buildings | All finished buildings kept with grade, id, plot. Sites and upgrades in progress are cancelled with refunds. |
| Shipments and carriers in flight | Settled at the jump tick: each load arrives at its destination if that still exists, else goes back to its source. Ledger line `Settled`. |
| Stock | Each settlement keeps half of each ware (rounded down). Then the **harbour kit** tops up the faction's home storehouse to at least: 12 Planks, 10 Bricks, 8 Cut stone, 8 Iron, 6 Rations, 4 Coal, 2 Cloth. Ledger lines `JumpLoss` and `JumpKit`. |
| Soldiers | Historic soldiers do not carry. Their count and best levels go into `History` ("the old guard"). |
| Victory points, credits, unlock | Carry unchanged. Credits above the Modern cap are kept (the cap only limits new income). |
| Faction relations | Carry (`Relations[a,b]`: War, Truce). The Ash Clan stays at war in the default story. |
| Crystal outcomes, companions | Carry as the `CampaignRecord` handed over by `PRD_CRYSTALS.md` (order of restoration, survivors, the fallen and where, seed, definition id), stored whole in `History`: restored towers enable the Ward conduit (R2) at those towers; each element in `CampaignRecord.EverOnAltar` gives the Ash Clan one free Ward conduit at that element's altar landmark (at the Won ending every crystal is restored, so the record, not the final custody, says which were stolen). |
| Casualties | The named casualty list carries to `History.Casualties` and is shown on the memorial square. |
| Mira | Recurs (the crystal connection keeps her out of time; open decision 14.6). |

### 4.5 Modern to Future (a time jump)

**Trigger.** The harbour mission's win raises `ChapterReady{ch.future.core}` with the route outcome (`RouteA`, `RouteB` or both). The engineer invites the player.

| Thing | What happens |
|---|---|
| Landmarks, roads, settlements outside the district | Kept, owner kept. |
| The district hex (`lm.district`) | Its plots' buildings become ruins and are removed; their stock moves to the transfer station inventory (`Settled`). The hex becomes the industrial tile area. |
| Roster structures | Removed (decades pass), except one damaged Command post per faction, which carries at 40% HP. |
| Units | Up to 4 infantry and 2 vehicles of the player's faction carry, best first by cost; the rest go to `History`. |
| Stock | Half kept per settlement; the **district kit** tops up the transfer station to at least 12 Steel, 8 Parts, 6 Shells, 4 Coal. |
| Outcome effect | Route A (harbour cleared): the transfer station starts with +20 Plate and +10 Slug. Route B (relay held): the battery starts full and the first generator gives +5 power. |
| Points, credits, unlock, relations, history | Carry. |

### 4.6 The saveable one-shot transition

`AdvanceChapter{To}` is validated in `Eras` (slot 50, before dice). If valid, it runs the transfer in the same tick, in a fixed order (cancel sites and queues, settle carriers, move stock, apply kits, convert the district, set the era, write `History`), then emits `ChapterAdvanced{From, To, Tick}`. The game layer writes the slot `auto.chapter` on that event (slot names are owned by the `SaveSystem` of `PRD_CRYSTALS` I8: `main`, `auto.chapter`, `ckpt.<mission>.<n>`). A save just before and just after loads correctly; replaying the same save and command gives byte-identical state JSON. A refused advance changes nothing.

### 4.7 Standalone starts

`ScenarioStarts.Create(startId, seed, options)` builds a full `WorldState` for a chapter without playing earlier ones. Each start is the end state of a short scripted run, then frozen as authored data, so it is a legal state (not granted cards):

| Start | Contents |
|---|---|
| `dev.historic` | Tick 0 of Historic after handover: home city, 2 camps, Sawmill, Kiln, Bakery, Smokehouse, improved collectors, smith's chest; rival similar. |
| `dev.modern.harbour` | Player: home town plus 3 camps; Historic smelting and tool chain running; 8 points, 600 credits, harbour kit. Rival base at the harbour approach (9.1). |
| `dev.modern.skirmish` | Two mirrored towns, 10 points each, 1000 credits, no mission objects. For the RTS AI proof. |
| `dev.future.core` | Option `modernOutcome` = A, B or none; district kit; 2 rifle squads, 1 light tank. |

---

## 5. The faction: the Valley people

Design goals: comparable depth to a classic settlers-style tribe; every ware has a use; each age playable on its own; original names; fits the camp, plot and road rules already built. The Ash Clan uses the same tree through M1 with its own names and look; its own distinct tree is finished-game scope (5.8).

### 5.1 Wares (43)

Ids 0-9 exist and are binding. New ids continue after them. `Wares.IsRaw` becomes a table lookup (ids 0-4 and 10-15).

| Id | Ware | Kind | Made by | Used by |
|---|---|---|---|---|
| 0 | Wood | raw | Log pile, Woodcutter's hut | roads, camps, buildings, Sawmill, Kiln, Smokehouse, Charcoal kiln |
| 1 | Clay | raw | Clay scrape, Clay pit | roads, camps, buildings, Kiln |
| 2 | Grain | raw | Gleaners' basket, Farm | camps, Bakery, Brewery, Ox pen |
| 3 | Sheep | raw | Thorn fold, Shepherd's hut | camps, Smokehouse |
| 4 | Stone | raw | Rock heap, Quarry | Stonecutter, Barricade |
| 5 | Planks | worked | Sawmill | buildings, city, town, Toolsmith, Weaponsmith, Armourer, roster |
| 6 | Bricks | worked | Kiln | buildings, city, town, roster |
| 7 | Bread | food | Bakery | city, town, meals, Tavern |
| 8 | Meat | food | Smokehouse | meals, Tavern, Warrior lodge |
| 9 | Cut stone | worked | Stonecutter | city, town, military buildings, roster |
| 10 | Ore | raw (H) | Ore mine | Smelter, Foundry |
| 11 | Coal | raw (H) | Coal mine, Charcoal kiln | Smelter, Weaponsmith, Generator fuel, Foundry, Machine shop, Recon buggy |
| 12 | Gold | raw (H) | Gold mine | Smelter |
| 13 | Game | raw (H) | Hunter's lodge | Smokehouse (grade 1) |
| 14 | Flax | raw (H) | Flax field | Weaver |
| 15 | Water | raw (H) | Well | Brewery, Ox pen |
| 16 | Cloth | worked (H) | Weaver | Tailor, Armourer, Signal tower, Research institute, Scout |
| 17 | Iron | metal (H) | Smelter | tools, weapons, armour, military buildings, roster |
| 18 | Gilt | metal (H) | Smelter | Weaponsmith (Blade), Citadel, Research institute, sale |
| 19 | Rations | food (H) | Tavern | mines, Scout hut, Barracks, training, infantry, meals |
| 20 | Ale | food (H) | Brewery | Gold mine, top training steps |
| 21 | Tunic | garment (H) | Tailor | Barracks (recruits) |
| 22 | Axe | tool (H) | Toolsmith | Woodcutter's hut grade 1 |
| 23 | Pick | tool (H) | Toolsmith | Clay pit and Quarry grade 1, mines, Well |
| 24 | Hammer | tool (H) | Toolsmith | Stonecutter grade 1, Toolsmith, town upgrade, military buildings, Engineer |
| 25 | Saw | tool (H) | Toolsmith | Sawmill grade 1 |
| 26 | Sickle | tool (H) | Toolsmith | Farm grade 1, Flax field |
| 27 | Knife | tool (H) | Toolsmith | Shepherd's hut and Smokehouse grade 1, Hunter's lodge |
| 28 | Tongs | tool (H) | Toolsmith | Kiln grade 1, Smelter, Weaponsmith, Armourer |
| 29 | Spear | weapon (H) | Weaponsmith | Barracks (recruits) |
| 30 | Sword | weapon (H) | Weaponsmith | Drill yard (attack 1) |
| 31 | Blade | weapon (H) | Weaponsmith | Drill yard (attack 2) |
| 32 | Helm | armour (H) | Armourer | Proving ground (health 1) |
| 33 | Mail | armour (H) | Armourer | Proving ground (health 2) |
| 34 | Shield | armour (H) | Armourer | Drill yard (defence 1 and 2) |
| 35 | Steel | metal (M) | Foundry | Machine shop, roster, units, Future builds |
| 36 | Parts | component (M) | Machine shop | vehicles, Service bay, Engineer, Future builds |
| 37 | Rifle | weapon (M) | Machine shop | infantry |
| 38 | Shells | ammunition (M) | Machine shop | Launcher trooper, tanks, Field gun |
| 39 | Plate | belt item (F) | Smelter block | Press, Fabricator |
| 40 | Circuit | belt item (F) | Press | Fabricator, Assembler |
| 41 | Slug | belt item (F) | Press | Turret ammunition |
| 42 | Frame | belt item (F) | Fabricator | Assembler |

Counts: 11 raw, 12 worked and food, 7 tools, 6 weapons and armour, 4 Modern, 4 Future (some Modern and Future wares also feed roster costs).

**Where the new raw goods come from.** No new hex-stock kinds except veins, so the dice and growth rules stay as built:
- Hunter's lodge takes 1 mature Forest unit and yields Game. Flax field takes 1 mature Fields unit and yields Flax. They compete with woodcutters and farms on the same hexes: a real choice.
- Each of the 4 Mountain hexes gets a **vein** at board generation (`Board.HexVein`, seeded order of {Ore, Coal, Ore, Gold}). A mine may stand only in a sector of a Mountain hex with its vein; it takes 1 mature Mountain unit per cycle. Quarry and rock heap work on any Mountain and compete for the same stock.
- The Well is a **source**: no hex stock, any plot, makes Water on a cycle (ledger column `Sourced`).

### 5.2 Workers (26)

The refactor has implied workers (a building works when finished). The full tree keeps that rule and adds tools: a grade-1 building, and every building born in Historic, takes its worker's tool as part of its construction or upgrade cost. The tool is consumed into the worker, who keeps it. This gives every tool a real consumer without a separate population economy (choice 15.4).

| Worker | Tool | Works at |
|---|---|---|
| Carrier | none | storehouse (count rule as now) |
| Builder | Hammer from Historic town upgrade on | construction sites (as now) |
| Gatherer | none | primitive collectors |
| Woodcutter | Axe at grade 1 | Woodcutter's hut |
| Digger | Pick at grade 1 | Clay pit, Quarry, Well |
| Farmer | Sickle at grade 1 | Farm, Flax field |
| Shepherd | Knife at grade 1 | Shepherd's hut |
| Hunter | Knife | Hunter's lodge |
| Miner | Pick | Ore, Coal and Gold mines |
| Sawyer | Saw at grade 1 | Sawmill |
| Burner | Tongs at grade 1 (Kiln); none (Charcoal kiln) | Kiln, Charcoal kiln |
| Baker | none | Bakery |
| Smoker | Knife at grade 1 | Smokehouse |
| Mason | Hammer at grade 1 | Stonecutter |
| Brewer | none | Brewery |
| Innkeeper | none | Tavern |
| Weaver | none | Weaver |
| Tailor | none | Tailor |
| Smelter | Tongs | Smelter |
| Smith | Hammer (Toolsmith) or Tongs (Weaponsmith, Armourer) | Toolsmith, Weaponsmith, Armourer |
| Herder | none | Ox pen |
| Trader | none | Trading post |
| Scout | none | Scout hut |
| Trainer | none (a soldier of total level 3 or more, assigned) | Drill yard, Proving ground |
| Soldier | Tunic and Spear (recruit) | Barracks, military buildings |
| Ox | bred, not a person | doubles shipment size on one road |

### 5.3 Buildings (40)

Columns: grade born (P or H); placement; recipe per cycle and ticks; construction cost (goods, plus the tool). Ids 0-15 exist and keep their recipes and costs at grade 0. All Historic-born buildings are allowed only from the Historic era.

| Id | Building | Class | Born | Place | Recipe (ticks) | Cost |
|---|---|---|---|---|---|---|
| 0 | Log pile | primitive collector | P | Forest sector | 1 unit -> 1 Wood (100) | comes with camp |
| 1 | Clay scrape | primitive collector | P | Hills | 1 -> 1 Clay (100) | comes with camp |
| 2 | Gleaners' basket | primitive collector | P | Fields | 1 -> 1 Grain (100) | comes with camp |
| 3 | Thorn fold | primitive collector | P | Pasture | 1 -> 1 Sheep (100) | comes with camp |
| 4 | Rock heap | primitive collector | P | Mountain | 1 -> 1 Stone (100) | comes with camp |
| 5 | Woodcutter's hut | improved collector | P | Forest | 1 -> 2 Wood (60) | 2 Wood |
| 6 | Clay pit | improved collector | P | Hills | 1 -> 2 Clay (60) | 2 Wood |
| 7 | Farm | improved collector | P | Fields | 1 -> 2 Grain (60) | 2 Wood, 1 Clay |
| 8 | Shepherd's hut | improved collector | P | Pasture | 1 -> 2 Sheep (60) | 2 Wood, 1 Clay |
| 9 | Quarry | improved collector | P | Mountain | 1 -> 2 Stone (60) | 2 Wood, 1 Clay |
| 10 | Sawmill | workshop | P | any | 1 Wood -> 1 Planks (100) | 2 Wood, 1 Clay |
| 11 | Kiln | workshop | P | any | 2 Clay + 1 Wood -> 2 Bricks (150) | 1 Wood, 2 Clay |
| 12 | Bakery | workshop | P | any | 1 Grain -> 1 Bread (100) | 2 Planks, 1 Bricks |
| 13 | Smokehouse | workshop | P | any | 1 Sheep + 1 Wood -> 2 Meat (150); grade 1 also 1 Game + 1 Wood -> 2 Meat | 2 Planks, 1 Bricks |
| 14 | Stonecutter | workshop | P | any | 1 Stone -> 1 Cut stone (150) | 2 Planks, 1 Bricks |
| 15 | Warrior lodge / Barracks | recruitment | P | any | grade 0: 1 Bread + 1 Meat -> 1 warrior (300, max 3). Grade 1 (Barracks): 1 Tunic + 1 Spear + 1 Rations -> 1 soldier (200) | 2 Planks, 2 Bricks |
| 16 | Ore mine | mine | H | Mountain with Ore vein | 1 unit + 1 Rations -> 3 Ore (120) | 3 Planks, 2 Cut stone, Pick |
| 17 | Coal mine | mine | H | Mountain with Coal vein | 1 unit + 1 Rations -> 3 Coal (120) | 3 Planks, 2 Cut stone, Pick |
| 18 | Gold mine | mine | H | Mountain with Gold vein | 1 unit + 1 Rations + 1 Ale -> 2 Gold (150) | 3 Planks, 3 Cut stone, 1 Iron, Pick |
| 19 | Hunter's lodge | collector | H | Forest | 1 unit -> 1 Game (80) | 2 Planks, Knife |
| 20 | Flax field | collector | H | Fields | 1 unit -> 2 Flax (80) | 2 Planks, 1 Bricks, Sickle |
| 21 | Well | source | H | any | -> 1 Water (100) | 1 Planks, 1 Bricks, Pick |
| 22 | Charcoal kiln | workshop | H | any | 3 Wood -> 1 Coal (200) | 2 Planks, 2 Bricks |
| 23 | Weaver | workshop | H | any | 2 Flax -> 1 Cloth (120) | 2 Planks, 1 Bricks |
| 24 | Tailor | workshop | H | any | 1 Cloth -> 1 Tunic (100) | 2 Planks, 1 Bricks |
| 25 | Brewery | workshop | H | any | 1 Grain + 1 Water -> 1 Ale (120) | 2 Planks, 2 Bricks |
| 26 | Tavern | workshop | H | any | 1 Bread + 1 Meat -> 2 Rations (120) | 3 Planks, 2 Bricks |
| 27 | Smelter | workshop | H | any | 1 Ore + 1 Coal -> 1 Iron (150); or 1 Gold + 1 Coal -> 1 Gilt (200), by demand | 2 Planks, 3 Bricks, 1 Cut stone, Tongs |
| 28 | Toolsmith | workshop | H | any | 1 Iron + 1 Planks -> 1 tool, the most demanded (ties by ware id) (150) | 2 Planks, 2 Bricks, Hammer |
| 29 | Weaponsmith | workshop | H | any | Spear: 1 Iron + 1 Planks; Sword: 2 Iron + 1 Coal; Blade: 1 Iron + 1 Gilt + 1 Coal (150 each), by demand | 3 Planks, 2 Bricks, 1 Cut stone, Tongs |
| 30 | Armourer | workshop | H | any | Helm: 1 Iron; Mail: 1 Iron + 1 Cloth; Shield: 1 Iron + 1 Planks (150 each), by demand | 3 Planks, 2 Bricks, 1 Cut stone, Tongs |
| 31 | Ox pen | workshop | H | any | 2 Grain + 1 Water -> 1 Ox (600), up to one per road edge in use | 3 Planks, 1 Bricks |
| 32 | Trading post | trade | H | any | sells 1 marked unit -> credits at the ware's price (100) | 3 Planks, 2 Bricks, 1 Cloth |
| 33 | Scout hut | scouting | H | any | 1 Rations -> reveals one unseen hex within 3 edges for 600 ticks (300) | 2 Planks |
| 34 | Drill yard | training site | H | city or town | attack and defence steps (5.4) (200 per step) | 4 Planks, 3 Bricks, 2 Cut stone, 1 Iron |
| 35 | Proving ground | training site | H | city or town | health steps (5.4) (200 per step) | 4 Planks, 3 Bricks, 2 Cut stone, 1 Iron |
| 36 | Watch post | military | H | any | garrison 1, sight 2 hex edges | 2 Planks, 1 Cut stone, Hammer |
| 37 | Stockade | military | H | any | garrison 3, sight 2 | 3 Planks, 2 Bricks, 1 Cut stone, Hammer |
| 38 | Hillfort | military | H | slot 2 (city) or a town plot | garrison 6, sight 3 | 4 Planks, 4 Bricks, 3 Cut stone, 1 Iron, Hammer |
| 39 | Citadel | military | H | town plot, one per faction | garrison 10, sight 3; soldiers inside heal twice as fast | 6 Planks, 6 Bricks, 6 Cut stone, 2 Iron, 1 Gilt, Hammer |

Counts by function (40): extraction 16 (5 primitive collectors, 5 improved collectors, 3 mines, Hunter's lodge, Flax field, Well); processing 7 (Sawmill, Kiln, Stonecutter, Charcoal kiln, Weaver, Tailor, Smelter); food 4 (Bakery, Smokehouse, Brewery, Tavern); tools and equipment 3; recruitment 1; training 2; military 4; logistics and trade 2 (Ox pen, Trading post); scouting 1.

**Settlement grades** (a settlement is not a plot building, but it follows the same upgrade rule):

| Grade | Name | Paid with | Cost | Ticks | Gives |
|---|---|---|---|---|---|
| P (exists) | Camp to City | goods | 2 Planks, 2 Bricks, 2 Cut stone, 2 Bread | 400 | third plot per sector, strength 2 |
| H | City to Town | goods | 4 Planks, 4 Bricks, 4 Cut stone, 2 Bread, Hammer | 600 | 6 outer plots, +2 carriers, shipment size 4 to 6 |
| M | Town to District | credits | 400 credits | 400 | +2 carriers, shipment size 8, counts as a roster supply corner |

**Town plots.** `PlotLayout` gains outer slots 3 and 4 per sector: 45 m along the spoke and 17 m to the left and right. Checked by hand against the spec's rules: 17.2 m from slots 0 and 1, 17 m from slot 2, 30.5 m from the hex edges, 22.7 m from the hex centre. The one conflict: when two settlements share a hex from corners two edges apart, their facing outer slots come within about 9 m. Rule: such an outer slot opens only for the settlement that became a town first; the other stays locked (refusal `plot_blocked_by_neighbour`). Outer plot ids continue after the existing range: `1350 + corner * 6 + sector * 2 + (slot - 3)`. Existing ids do not change. `PlotLayoutTests` must prove every spacing rule.

**One building of each type per settlement** stays. A full economy therefore needs several towns, which is the expansion pressure the AI plans for.

### 5.4 Soldiers and the military

**Soldier record.** `Soldier{Id, Faction, Home (building), Attack 0-2, Health 0-2, Defence 0-2, Hp, State (Garrison | Reserve | Marching | Fighting | Healing | Training), Fed}`. Prehistoric warriors become soldiers with all skills 0 at the Historic step (same strength rules until H2 lands).

| Rule | Starting value |
|---|---|
| Hit points | 13 + 5 per Health level |
| Damage per hit | 3 + 2 per Attack level, minus 1 per defender Defence level, at least 1 |
| Hit chance | 65%, minus 5 per defender Defence level; rolled with `Rng.Fork("military.duel")` |
| Duel round | every 10 ticks, one-on-one |
| Healing | 1 HP per 50 ticks in a military building (Citadel: per 25) |
| Meals | 1 Rations, else Bread, else Meat, per 900 ticks from the home settlement's storehouse; a hungry soldier fights at half damage |
| Recruits | Barracks (lodge grade 1): 1 Tunic + 1 Spear + 1 Rations. Stops when soldiers reach capacity: the sum of garrison sizes plus 4 per Barracks |

**Training** (one step per visit, the soldier returns to garrison after):

| Site | Step | Inputs |
|---|---|---|
| Drill yard | Attack 0 to 1 | 1 Sword, 1 Rations |
| Drill yard | Attack 1 to 2 | 1 Blade, 1 Rations, 1 Ale |
| Drill yard | Defence 0 to 1 | 1 Shield, 1 Rations |
| Drill yard | Defence 1 to 2 | 1 Shield, 1 Rations, 1 Ale |
| Proving ground | Health 0 to 1 | 1 Helm, 1 Rations |
| Proving ground | Health 1 to 2 | 1 Mail, 1 Rations, 1 Ale |

**Garrisons.** A military building holds up to its garrison size. A settlement is **protected** while it, or another settlement of the same faction within 2 hex edges, has a military building with at least one soldier at home.

**Attacks.** An attack order names a visible enemy military building or settlement within 4 hex edges (by any edges, not only roads) of one of the attacker's military buildings. Up to (garrison - 1) soldiers per sending building march along hex edges at 120 ticks per edge. Defenders come out from the target and from the defender's military buildings within 2 edges, one defender per attacker, best first. Duels until one side has no fighters at the target. Outcomes:
- Enemy military building emptied: captured if the attacker has a settlement within 2 edges of it, else razed. Its plot then belongs to the attacker's settlement only if captured.
- Enemy settlement with no protection left: captured with all its buildings and stock; jobs cancelled. A home settlement captured: skirmish defeat. In the campaign the home settlement is looted instead (4.3).
- Survivors walk home to heal.

**Strength** keeps its role as a quick measure for the HUD and the AI: settlements 1, cities 2, towns 3, plus each fed soldier `1 + (total level) / 2`. In Historic, the overrun warning is retired; an attack is how a faction wins.

**Fog of war (Historic on).** Per faction, on the 20 m sight grid of AG-31. Sight: a settlement corner 90 m, a military building its sight in hex edges times 60 m, a soldier 40 m, a Scout hut's revealed hex for its duration. Remembered cells keep the last seen buildings.

### 5.5 Bootstrapping the Historic tree

Tools need Iron, Iron needs mines, mines need Picks. The **smith's chest** at the Historic step breaks the circle: 2 Pick, 1 Axe, 1 Hammer, 1 Saw, 1 Sickle, 1 Knife, 1 Tongs, 4 Iron in each faction's home storehouse. The Toolsmith needs a Hammer, the Smelter Tongs, the Ore mine a Pick, the Charcoal kiln nothing. The proof (section H) must show that from the chest every seed reaches a working Toolsmith without help, and that a faction that spends its tools badly can still recover (Charcoal kiln plus the remaining Pick plus the 4 Iron).

### 5.6 The upgrade rule

- Grade 0 to 1: goods (and the worker's tool), allowed in Historic and later. Grade 1 to 2: credits, allowed in Modern and later.
- Command `UpgradeBuilding{BuildingId}`. The building enters `Upgrading`: it stops starting cycles, finishes nothing new, keeps its `Stock`, `Reserved` and `Incoming`. Deliveries already on the way still arrive into its stock. Upgrade goods are delivered by carriers into `UpgradeDelivered` (as for a site). Credits are reserved at the command and spent when it completes. Build ticks run once everything has arrived. On completion: `Grade++`, event `BuildingUpgraded{BuildingId, Grade}`.
- `CancelUpgrade` returns delivered goods to the storehouse and releases reserved credits.
- Upgrades go through the same proposal and spell path as any construction (the brain proposes them, Prehistoric players would cast them, autonomous leaders execute them).

Per-building grades. "Cycle" means the recipe's ticks. Each benefit has a measurement test in its role.

| Building(s) | Grade 1 cost | Grade 1 benefit | Grade 2 credits | Grade 2 benefit | Measured as |
|---|---|---|---|---|---|
| Primitive collectors (0-4) | 1 Planks | cycle 100 to 70 | 30 | cycle 50 | units out per minute on a full hex |
| Improved collectors (5-9) | 2 Planks, 1 Bricks, tool | yield 2 to 3 per stock unit | 120 | cycle 60 to 40 | wares per stock unit; per minute |
| Sawmill | 2 Planks, 2 Bricks, Saw | cycle 100 to 65; input buffer 3 cycles | 150 | cycle 50 | Planks per minute with inputs stocked |
| Kiln | 2 Planks, 2 Cut stone, Tongs | cycle 150 to 100 | 150 | cycle 75 | Bricks per minute |
| Bakery | 2 Planks, 2 Bricks | cycle 100 to 65 | 120 | cycle 50 | Bread per minute |
| Smokehouse | 2 Planks, 1 Bricks, Knife | cycle 150 to 100; accepts Game | 120 | cycle 75 | Meat per minute |
| Stonecutter | 2 Planks, 1 Bricks, Hammer | cycle 150 to 100 | 150 | cycle 75 | Cut stone per minute |
| Warrior lodge | 3 Planks, 3 Bricks, 2 Cut stone | becomes Barracks: trainable soldiers, no cap of 3, 200 ticks | 250 | 120 ticks, 2 recruits at once | soldiers per 10 minutes |
| Mines (16-18) | born H | - | 200 | yield +1 per cycle | ore per Rations |
| Hunter's lodge, Flax field | born H | - | 100 | cycle -30% | per minute |
| Well | born H | - | 80 | cycle 50 | Water per minute |
| Workshops 22-30 | born H | - | 150-250 (Smelter 250, smiths 200, others 150) | cycle -40% | output per minute |
| Ox pen | born H | - | 120 | breeding 600 to 350 | oxen per 10 minutes |
| Trading post | born H | - | 200 | 2 units per cycle, prices +25% | credits per minute |
| Scout hut | born H | - | 80 | reveal 2 hexes per trip | hexes seen per minute |
| Drill yard, Proving ground | born H | - | 300 | step 200 to 120 ticks | steps per 10 minutes |
| Watch post, Stockade, Hillfort, Citadel | born H | - | 100, 200, 300, 500 | garrison x 1.5 (rounded up), sight +1 edge | garrison capacity; seen cells |

A Prehistoric-born building pays its grade 1 in Historic; a Historic-born building has only the grade 2 step.

### 5.7 Coverage table (template, generated)

`CoverageReport` (a test) writes `Docs/COVERAGE.md` from the code tables and the proof results. One row per item. Status words: `planned`, `data` (in tables), `sim` (behaviour implemented), `tested` (unit test), `proven` (appears in a headless whole game), `viewed` (has a view), `ai` (the AI builds or uses it).

| Kind | Id | Name | Era | Faction | Data | Sim | Unit test | Proven in game | View | AI uses | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ware | 17 | Iron | H | Valley | | | | | | | |
| worker | - | Miner | H | Valley | | | | | | | tool Pick |
| building | 27 | Smelter | H | Valley | | | | | | | grade 2 in M |
| recipe | 27.b | Gold + Coal -> Gilt | H | Valley | | | | | | | |
| soldier step | D2 | Defence 1 to 2 | H | Valley | | | | | | | |
| upgrade | 10.g1 | Sawmill grade 1 | H | Valley | | | | | | | benefit measured |
| roster | R9 | Motor works | M | Valley | | | | | | | power -6 |
| unit | U6 | Light tank | M | Valley | | | | | | | |
| industry | F5 | Splitter | F | Valley | | | | | | | |
| campaign | R2 | Ward conduit | M | campaign only | | | | | | | needs a restored tower |

Finished-game scope listed in the same table with status `planned`:
- **Ash Clan tree** (its own wares and buildings by function: a herding and fire-working people; distinct soldier skills).
- **A third faction** (a coastal people; needs sea terrain).
- **Future machines faction** as a full faction (in F1 it is only a wave script).
- Sea and harbour economy (coast terrain, fishers, ships, ports), air units and airfields, naval units and dockyards, worker experience levels, a second training-site tier, liquids, programmable logic, more Future sectors.

### 5.8 The Ash Clan

Through M1 the Ash Clan uses the Valley tree, the same AI, its own display names for buildings (data table of names only) and its own look. Its own tree is finished-game scope.

---

## 6. Points, credits and the roster unlock

### 6.1 Victory points

`FactionLedger.Points` is the sum of milestone values, each written once under a key in `FactionLedger.Paid` (a sorted set). Points never go down; the unlock is permanent.

| Key | When | Points | Cap counted |
|---|---|---|---|
| `camp:<settlementId>` | a settlement founded after the start (or captured) | 1 | 4 |
| `city:<settlementId>` | a city finished | 1 | 2 |
| `town:<settlementId>` | a town finished | 1 | 2 |
| `first:iron` | first Iron smelted | 1 | 1 |
| `first:soldier2` | first soldier reaching total level 2 | 1 | 1 |
| `first:sale` | first Trading post sale | 1 | 1 |
| `capture:<kind>:<id>` | capturing an enemy military building or settlement | 1 | 3 |
| `crystal:<element>` | (campaign) a crystal restored by the player's side, or carried to its altar by the Ash Clan | 1 | 4 |
| `mission:<id>:<objective>` | authored mission objectives (9.1) | 1 | as authored |

The economy path to 10 without war: 4 camps, 2 cities, 1 town, first iron, a level-2 soldier, a first sale. Proof section V measures when each faction gets there.

Expected timing (to be tuned in H2): an active faction reaches 10 in late Historic, around 45 to 60 minutes of Historic play. In the campaign, the crystal chapter may be shorter than that, so many players reach Modern with 6 to 9 points and see the unlock in the harbour mission. A faction that does reach 10 in Historic has the roster open, by the owner's rule (open decision 14.1).

### 6.2 Credits

- `FactionLedger.Credits` and `ReservedCredits`. Available = Credits - ReservedCredits. Credits exist from Historic.
- **Trade first.** The Trading post sells marked wares. The default mark (the AI's and the player's unless advised) is any ware above twice its stock target. Prices per unit: raw goods 1; Planks, Bricks 3; Bread, Meat 2; Cut stone 4; Cloth 4; Iron 8; Gilt 20; Rations 4; Ale 3; tools 10; weapons and armour 12; Steel 12; Parts 15.
- **Then the harvester and refinery loop** (Modern roster): a Harvester drives to a hex with mature Mountain or Hills stock within 300 m of its Refinery, takes up to 8 units (20 ticks per unit), returns and unloads: 15 credits per Mountain unit, 10 per Hills unit. It competes with quarries, mines and clay pits for the same stock (choice 15.9).
- **Cap.** Income stops at 2000 + 1000 per Refinery + 1000 per Depot (`credits_full` reason on the Trading post and Refinery).
- Spending: grade 2 upgrades, the Town to District upgrade, roster construction, per-item unit charges.

### 6.3 The unlock

- `FactionLedger.RosterUnlocked` becomes true at the first tick `Points >= 10`, with `RosterUnlockedTick`; event `RosterUnlocked{Faction}`. It never becomes false.
- Every roster command (`PlaceStructure`, `QueueUnit`) checks the commanding faction's flag first: refusal `roster_locked` "Our people need 10 victory points to build that ({points} of 10)."
- The same commands for the prince, the commander, the rival and scripted players. The rival's flag is its own.
- Visible: the H HUD shows "Points 8 of 10" and credits; the leader's pitch for a roster structure states both costs ("A generator: 150 credits, 3 bricks and 2 iron. We have the credits; the bricks are on the way.").

### 6.4 Mixed costs and refusals

| Situation | Result |
|---|---|
| Goods present, credits short | `PlaceStructure` refused `no_credits` "We need {n} more credits." Nothing reserved. |
| Credits present, goods short | Accepted. Credits reserved. The site waits (`waiting_ware`) and does not finish until goods arrive. |
| Retry after a refusal | The same command succeeds once the condition holds; nothing left over from the refused try. |
| Two identical commands in one tick | The second is refused (`cells_taken`). |
| Cancel | Reserved credits released; delivered goods back to the supply storehouse; ledger shows no spend. |
| Save and load mid-site | Reserved credits and delivered goods persist; the site completes once; credits are spent once. |
| Queue item cancelled | Its per-item credits refunded; delivered inputs stay in the building's stock for the next item. |

---

## 7. The Modern roster and its rules

### 7.1 Placement and supply

- Roster structures stand on the sim lattice (AG-30), not on plots. Footprints: 1 cell (walls, Gun nest), 3 cells (a small triangle: Generator, Depot, Cannon post, Signal tower, Ward conduit), 7 cells (a hexagon of a centre and six neighbours: everything else).
- A footprint must avoid plots of any settlement, the 3 m storehouse area at corners, stock patches, peaks, roads' edge lines and other footprints.
- Every structure except the Command post must lie within 150 m of a finished Command post of the same faction.
- Each Command post has a **supply corner**: the nearest Town or District of its faction within 2 hex edges by road, chosen at placement. Carriers from that storehouse deliver to roster sites and queues on lattice legs (`CarrierTicksPerStep` per lattice step). A finished Depot becomes an extra place goods can be imported to (a storehouse for the base).

### 7.2 Structures (17)

Costs are credits plus goods. Power: + supply, - draw. Build ticks after everything has arrived.

| Id | Name | Function | Credits | Goods | Ticks | Power | HP | Needs |
|---|---|---|---|---|---|---|---|---|
| R0 | Command post | construction yard; +5 power; its builders work roster sites | 400 | 6 Planks, 6 Bricks, 4 Cut stone, 2 Iron | 600 | +5 | 800 | 10 points; a Town or District within 2 edges |
| R1 | Generator | power; burns 1 Coal per 300 ticks, else gives 0 | 150 | 3 Bricks, 2 Iron | 200 | +10 | 300 | R0 |
| R2 | Ward conduit | power from a restored crystal tower; no fuel (campaign only) | 200 | 2 Iron, 2 Cut stone | 250 | +15 | 300 | R0; within 60 m of a restored tower landmark |
| R3 | Turbine hall | advanced power; 2 Coal per 300 ticks | 300 | 4 Steel, 2 Bricks | 450 | +25 | 500 | R12 |
| R4 | Refinery | turns harvested stock into credits; comes with one Harvester | 400 | 4 Bricks, 3 Iron, 2 Planks | 400 | -3 | 600 | R1 or R2 |
| R5 | Depot | storage: an import point for the base, +1000 credit cap | 100 | 3 Planks, 2 Bricks | 150 | 0 | 300 | R0 |
| R6 | Foundry | 1 Ore + 1 Coal -> 2 Steel (150) | 250 | 4 Bricks, 2 Iron | 300 | -4 | 400 | R1 or R2 |
| R7 | Machine shop | Steel -> 2 Parts; Steel + Coal -> 3 Shells; Steel + Planks -> 2 Rifles (120 each, by demand) | 250 | 2 Steel, 3 Bricks | 300 | -3 | 400 | R6 |
| R8 | Infantry hall | infantry queue (5 slots) | 200 | 3 Planks, 3 Bricks, 1 Iron | 250 | -2 | 400 | R1 or R2 |
| R9 | Motor works | vehicle queue (5 slots) | 500 | 6 Bricks, 4 Iron, 2 Cut stone | 500 | -6 | 700 | R4 |
| R10 | Signal tower | radar: sight 300 m, shows units on the map | 300 | 2 Iron, 2 Planks, 1 Cloth | 300 | -3 | 350 | R4 |
| R11 | Service bay | repairs vehicles in reach: 1 Parts per 20 HP | 250 | 2 Steel, 2 Bricks | 300 | -2 | 400 | R9 |
| R12 | Research institute | tech: unlocks Heavy tank, Field gun, Turbine hall | 800 | 4 Steel, 4 Bricks, 2 Gilt, 2 Cloth | 600 | -5 | 500 | R9, R10 |
| R13 | Gun nest | anti-infantry defence; works without power | 150 | 2 Bricks, 1 Iron, 2 Cut stone | 150 | 0 | 300 | R8 |
| R14 | Cannon post | anti-armour defence; offline under brownout | 350 | 2 Steel, 3 Cut stone | 250 | -2 | 400 | R9 |
| R15 | Barricade | wall, per cell | 10 | 1 Stone | 20 | 0 | 80 | R0 |
| R16 | Rampart | strong wall, per cell | 25 | 1 Cut stone, 1 Steel | 40 | 0 | 200 | R6 |

Scoped out explicitly for this plan: aircraft and airfields, anti-air defences, naval units and dockyards, mobile construction (a deployable Command post), support powers, capture of neutral tech buildings, mines and minelayers. All are listed in the coverage table as finished-game scope.

### 7.3 Units (10 Modern, 2 Future)

Armour classes: Infantry, Vehicle, Structure. Damage is per shot against each class. Speed is ticks per 5 m lattice step. Range and sight in metres.

| Id | Unit | Built at | Credits | Physical inputs | Ticks | HP | Class | Damage I / V / S | Range | Cooldown | Speed | Sight | Needs |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| U0 | Harvester | R9 (one free with R4) | 300 | 2 Steel, 2 Parts | 450 | 200 | V | - | - | - | 8 | 40 | R4 |
| U1 | Rifleman | R8 | 50 | 1 Rifle, 1 Rations | 150 | 25 | I | 4 / 1 / 1 | 25 | 10 | 10 | 40 | - |
| U2 | Launcher trooper | R8 | 100 | 1 Rifle, 2 Shells, 1 Rations | 200 | 25 | I | 2 / 10 / 6 | 30 | 25 | 10 | 40 | R7 |
| U3 | Engineer | R8 | 150 | 1 Rations, 1 Hammer, 1 Parts | 200 | 20 | I | - | - | - | 10 | 40 | - |
| U4 | Scout | R8 | 40 | 1 Rations, 1 Cloth | 120 | 20 | I | 2 / 0 / 0 | 20 | 10 | 7 | 80 | - |
| U5 | Recon buggy | R9 | 200 | 1 Steel, 1 Parts, 1 Coal | 250 | 60 | V | 3 / 2 / 1 | 25 | 10 | 4 | 70 | - |
| U6 | Light tank | R9 | 350 | 3 Steel, 2 Parts, 2 Shells | 400 | 120 | V | 3 / 8 / 6 | 35 | 20 | 6 | 45 | - |
| U7 | Heavy tank | R9 | 700 | 6 Steel, 4 Parts, 4 Shells | 700 | 220 | V | 4 / 14 / 10 | 40 | 25 | 9 | 45 | R12 |
| U8 | Field gun | R9 | 500 | 3 Steel, 2 Parts, 6 Shells | 600 | 70 | V | 8 / 6 / 16 | 90 (min 20) | 50 | 9 | 35 | R12 |
| U9 | Supply truck | R9 | 150 | 2 Steel, 1 Parts | 250 | 80 | V | - | - | - | 6 | 40 | - |
| U10 | Repair drone | Assembler (Future) | - | 1 Frame, 2 Circuit (belt items) | 300 | 40 | V | - | - | - | 5 | 50 | Future |
| U11 | Breacher mech | (finished scope) | - | - | - | - | - | - | - | - | - | - | - |

Defences: Gun nest 6 / 1 / 1 at 30 m, cooldown 10. Cannon post 2 / 12 / 4 at 40 m, cooldown 20. Historic soldiers fight Modern units as Infantry with their duel damage at 5 m range.

Special abilities: the Engineer captures an enemy structure it reaches when the structure is below 50% HP or undefended within 20 m (capture takes 50 ticks), and repairs own structures (10 HP per 10 ticks, 1 Parts per 50 HP). The Supply truck carries 6 goods units between storehouses, the Depot and queues; the convoy is made of Supply trucks.

### 7.4 Production queues

- Each production structure has up to 5 queued items. An item starts when its credits are available (charged at start) and its physical inputs are in the building's stock. Inputs are requested through the normal job system as soon as the item is queued (the building keeps one item's inputs on hand and the next item's on the way).
- Progress per tick = 1 x the faction's power ratio (per mille) when power is short. Stall reasons: `input_missing`, `no_power`, `no_credits`, `disabled`.
- Finished units appear at the structure's exit cell and walk to its rally point (default: 15 m in front).
- `CancelQueueItem` refunds credits; delivered inputs stay for the next item.

### 7.5 Power

Per faction, one network for all roster structures (Modern). Supply = sum of fuelled or fuel-free plants. Draw = sum of finished, enabled consumers. Ratio = min(1000, supply x 1000 / draw). Under 1000: production slows in proportion; the Signal tower and Cannon posts go offline. `SetPowered{StructureId, On}` lets a leader switch a consumer off to save power. Generator fuel is a physical input (Coal) delivered like any input; an unfuelled generator supplies 0 (`input_missing`).

### 7.6 Fog of war

Per faction, a 20 m sight grid over the island (about 50 x 50 cells). States: Unseen, Remembered (last seen structures kept), Visible (recomputed every 10 ticks from settlements, military buildings, roster structures, soldiers and units). The Signal tower adds a 300 m circle and makes units in it show on the map. AI target choice reads only Visible and Remembered cells of its own faction.

### 7.7 Orders

| Order | Fields | Who issues it in normal play |
|---|---|---|
| `Move` | units, cell | leaders (AI) |
| `AttackMove` | units, cell | leaders |
| `Attack` | units, target id | leaders |
| `Guard` | units, target id | leaders |
| `Hold` / stance | units, Aggressive, Defensive, Hold fire | leaders |
| `Stop` | units | leaders |
| `Capture` | engineer, structure | leaders |
| `Repair` | engineer, structure | leaders |
| `Harvest` | harvester, hex | harvester module |
| `Escort` | units, convoy id | leaders |
| `SetRally` | structure, cell | leaders |

The player does not issue unit orders in normal play. The player gives **delegated orders** to the commander (8.4); under F1 every order above is available directly.

---

## 8. AI design

### 8.1 Research summary (technique only, in our words)

Sources read on 14 Nov 2026 (public GitHub source, master and bleed branches). The project wiki pages were blocked to automated reading; details from them are marked unverified in the notes and not relied on.

**Widelands computer player** (`src/ai`: [defaultai.cc](https://raw.githubusercontent.com/widelands/widelands/master/src/ai/defaultai.cc), [defaultai_warfare.cc](https://raw.githubusercontent.com/widelands/widelands/master/src/ai/defaultai_warfare.cc), [defaultai_seafaring.cc](https://raw.githubusercontent.com/widelands/widelands/master/src/ai/defaultai_seafaring.cc), [ai_help_structs.h](https://raw.githubusercontent.com/widelands/widelands/master/src/ai/ai_help_structs.h), [ai_hints.h](https://raw.githubusercontent.com/widelands/widelands/master/src/ai/ai_hints.h), [defaultai.h](https://raw.githubusercontent.com/widelands/widelands/master/src/ai/defaultai.h)):

- **A task scheduler.** The AI thinks about twice a second but runs only one or a few due jobs each time. Each job type has its own interval: construct a building every few seconds, check production sites and mines every 15 s a handful at a time, military sites every few seconds, enemy sites about every 20 s, training sites every 15 s, economy targets every 15 minutes. This keeps it cheap and steady.
- **Cached knowledge per field and per building type.** For each buildable spot it keeps what is nearby (resources, free land, enemy and own military, border nearness). For each building type it keeps counts (built, under construction, unoccupied, unconnected), inputs, outputs, and data hints (needs water, takes space, expands territory, is a basic-economy building, limits).
- **A basic economy first.** A data list says which buildings and how many make a working start. Until it is complete, the AI concentrates on it.
- **Needs, then score.** Each building type first gets a verdict (forced, needed, allowed, not needed, forbidden) from stock against targets of its outputs, whether its inputs and workers exist, the expansion mode, and how long since that type was last built. Then every candidate spot is scored for every needed type using what is around the spot (trees for woodcutters, rocks for quarries, ground water for wells, closeness to the consumer for simple chains, distance from the border for vulnerable buildings). The single best pair is built. Few sites at once (two non-military), a minimum gap between decisions of one type, and recently tried spots are blocked for a while.
- **Expansion modes.** From yes/no facts about economy, strength, land and enemies it picks a mode (grow for resources, grow for space, consolidate the economy, or both) and a threshold a new military building must beat.
- **Running the economy.** Production sites are reviewed in rotation. Unproductive ones are dismantled by rules per type (a woodcutter with no trees left, a quarry with no rocks, a building with inputs whose type is no longer needed and that performs below average). Upgrades wait until a site is old enough, connected, staffed and performing well. Economy targets are rescaled with the number of producers.
- **Soldiers.** It compares open soldier slots with free soldiers (full, enough, shortage, bad shortage). That level sets garrison sizes: border sites an enemy can reach get full garrisons and the best soldiers; interior sites keep fewer and the weakest, and can be dismantled. Training sites open only when their inputs are not short, and equipment buffers are kept small.
- **Attacks.** Only visible enemy sites are attackable; seen sites are remembered and forgotten after a while. For each, it compares the defenders' strength (from their levels) with the strength of its own soldiers that could reach it, plus enemies nearby. The ratio must beat a threshold that depends on difficulty. Many weighted inputs (trends in land and power, recent attacks, mines nearby) form a score; the best positive one is attacked with a capped number of healthy soldiers; the same site gets a growing cooldown.
- **Personality.** Weights are evolved offline across many games (a "DNA" of numbers and yes/no rule masks); a game does not learn while running. Difficulty levels are the same AI with tighter limits and a higher attack threshold.
- **Visibility and determinism.** Attacks need visibility; economic analysis reads the map directly. It uses an unsynchronised random generator (only the host runs it), so its runs are not repeatable.

**OpenRA modular bot** ([ModularBot.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/Player/ModularBot.cs), [BaseBuilderBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/BaseBuilderBotModule.cs), [BaseBuilderQueueManager.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/BotModuleLogic/BaseBuilderQueueManager.cs), [UnitBuilderBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/UnitBuilderBotModule.cs), [HarvesterBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/HarvesterBotModule.cs), [SquadManagerBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs), [Squad.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/Squads/Squad.cs), [AttackOrFleeFuzzy.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/Squads/AttackOrFleeFuzzy.cs), [GroundStates.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/Squads/States/GroundStates.cs), [ProtectionStates.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/Squads/States/ProtectionStates.cs), [PowerDownBotManager.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/PowerDownBotManager.cs), [BuildingRepairBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/BuildingRepairBotModule.cs), [McvManagerBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/McvManagerBotModule.cs), [CaptureManagerBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/CaptureManagerBotModule.cs), [SupportPowerBotModule.cs](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/BotModules/SupportPowerBotModule.cs), [ai.yaml](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/mods/ra/rules/ai.yaml)):

- **Independent modules on one bot.** Base builder, unit builder, harvester manager, squad manager, power-down manager, repair, capture, support powers. Each ticks on its own interval, often with a random start offset; orders are queued and released a few per tick. Bot personalities (rush, normal, turtle, naval) are the same modules with different parameter sets.
- **Base builder priorities, in order:** a power plant when spare power is below a target that grows with the base (clamped between a minimum and a maximum); a refinery while below the minimum count (power first if the refinery would cause a deficit); a new production building, by chance, once cash is above a threshold; naval production if water is near; storage when stored resources are near capacity; otherwise a **fractions table**: shuffle the list and build the first type that is below its target share of all buildings, below its count limit and past its time delay. Nothing is started below a small cash floor. Placement: a random valid cell in a ring around the base centre; refineries near resources; defences at a defence centre facing the nearest enemy, which moves to where the base was last hit. Repeated placement failure cancels the item.
- **Unit builder.** Every so often, one unit per queue: the first in a shuffled list below its share of the army, within limits and delays. It pauses when enough idle units sit at home or while refineries are short.
- **Harvesters.** Idle harvesters are sent to the nearest reachable resource, with path costs raised near enemies; a slower pass rebalances them to under-used regions; more are requested while below a minimum or the refinery count.
- **Squads.** New ground units join an idle pool. When the pool reaches the squad size (plus a random extra), it becomes an assault squad. Periodically the bot checks whether the enemy's base is lightly defended and launches a rush squad. When an own building is hit, nearby squads retarget and a protection squad forms from idle units, chasing the attacker for a while, then returning. Targets are visible enemies the squad can reach and hit; the closest wins. A squad picks a slow leader, regroups stragglers, attack-moves, treats itself as stuck after a while without progress, and flees to own buildings and disbands when it is losing.
- **Attack or flee.** A small fuzzy rule set takes own and enemy health, relative damage per second and relative speed, each in three overlapping bands, and outputs a single value; below a cut-off the squad attacks. Healthier squads accept more risk.
- **Power down and repair.** When power is negative, switch off listed consumers from the smallest draw up; switch them back from the largest when there is surplus. Repair buildings that fall past light damage.
- **No dedicated scouting**; exploration is a side effect of moving squads and harvesters. Squads and capture use visible actors; the bot's orders are part of the shared deterministic simulation.

What we take: the scheduler with per-job intervals; a basic-economy list; needs-then-score building choice; expansion modes; soldier-status-driven garrisons; visible-only, threshold-and-score attacks with cooldowns; module split; power-first build priority; fractions with limits and delays; the idle pool into squads; protection squads; attack-or-flee with retreat. What we change: no evolved weights (hand-set personalities, tuned by headless proofs); every random choice from a forked sim `Rng`, so everything replays; an explicit scout module; the economy AI and the RTS AI share one ware-target interface so Modern queues pull real goods through the Valley economy.

### 8.2 The faction economy AI (extends the prince's brain)

Lives in `Sim/Factions/Ai/` and replaces the candidate scoring inside `Brain.Plan` from Historic on. The proposal queue, keys, speakers, decline cooldown and the rival's auto-cast stay as built. Prehistoric play keeps today's brain unchanged (so the existing proof stays valid).

**Scheduler.** Per faction, a list of jobs with due ticks; each Brain tick (slot 1150) runs the first due job (ties by job order). Player faction offset 0, rival offset by half an interval.

| Job | Interval (ticks) | Does |
|---|---|---|
| `ReviewNeeds` | 100 | recompute ware needs and building verdicts |
| `Construct` | 60 | refill the proposal queue (best three by score) |
| `CheckSites` | 150 | one building in rotation: stall diagnosis, upgrade candidate, dismantle candidate |
| `CheckMilitary` | 40 | one military building in rotation: garrison target, swap soldiers |
| `CheckEnemies` | 190 | update seen enemy sites from fog; forget after 12000 ticks unseen |
| `ConsiderAttack` | 300 to 1800 (adaptive) | attack evaluation (below) |
| `CheckTraining` | 150 | open or close training steps |
| `Trade` | 300 | mark wares for sale |
| `ReviewTargets` | 9000 | rescale ware targets by producer counts |

**Needs.** For each ware: `Need = Target + Demand(sites, upgrades, queues, recruits) - Stock - Incoming` over the faction's connected network. Targets are data (Planks 8, Bricks 8, Cut stone 6, Bread 6, Meat 6, Iron 6, Rations 8, tools 1 each, weapons and armour 2 each, Coal 6, Steel 6, Parts 4, Shells 6, Rifles 4). The RTS AI raises targets for its queue inputs (8.3), which is how Modern production pulls the Valley economy.

**Basic economy list** (data, in order): Woodcutter's hut, Clay pit, Sawmill, Kiln, Farm, Shepherd's hut, Bakery, Smokehouse, Quarry, Stonecutter, then (Historic) Charcoal kiln or Coal mine, Ore mine, Smelter, Toolsmith, Well, Tavern, Watch post at the camp nearest the enemy. Until complete, a building on this list gets +40.

**Verdict per type:** forbidden (era, plot, or one-per-settlement rule), not needed (every output above target and no demand), needed (an output need above 0), forced (on the basic list and missing). A type whose inputs have no producer in the faction gets -30 unless its input producer is itself needed (chains are pulled in order).

**Score** (integers; data weights):
`Score = 10 x needed-output weight (preciousness x need, capped) + basic bonus + chain bonus (consumer of a ware in surplus) + site quality - cost pressure - recency penalty + advice bonus + mode bonus`
- Site quality: for collectors the pips of the hex and its free mature stock; for mines the vein and pips; for workshops +5 if the first input is collected in that settlement; for military buildings, from the expansion mode and border distance (below).
- Cost pressure: -2 per unit of the cost not affordable now.
- Recency: -15 if the same type was proposed in the last 600 ticks.
- At most `MaxOpenSites` (3, tunable to 4 in Historic) non-military sites open at once; military sites count separately (max 2).

**Expansion modes** (from facts: free plots left, wares never collected, enemy seen within 4 edges, soldier status, strength lead):

| Mode | When | Effect |
|---|---|---|
| Resources | a raw or vein ware is not collected and a corner with it is in reach | +20 roads and camps toward it |
| Space | fewer than 2 free plots across the faction | +20 camps, +15 town upgrades |
| Economy | needs are high and free plots exist | -20 roads and camps |
| Military | enemy seen within 4 edges and soldier status shortage or worse | +25 military buildings at border camps, +20 Barracks inputs, training paused |

**Upgrades.** A building becomes an upgrade candidate when it is at least 3000 ticks old, has worked at least 60% of its last 1800 ticks, and its output need is above 0. Score 30 + need.

**Dismantle.** A collector on a hex whose stock stayed at 0 for 3000 ticks while another collector of its ware exists; a workshop whose outputs have all been above twice the target for 6000 ticks, if at least two of its type exist. (Dismantle returns half the cost.)

**Soldiers.** Status from open garrison slots against free soldiers: Full, Enough, Shortage, BadShortage. Garrison target per military building: border (an enemy site seen within 4 edges) full size and highest-level soldiers; interior: one third, lowest-level soldiers. Training opens a step when its inputs are in stock and status is Enough or Full; equipment targets stay at 2 to avoid hoarding.

**Attack decision.** For each seen enemy military building or settlement within reach: `Defenders` = sum of soldier power `(13 + 5H) x (3 + 2A) x (10 + D)` of soldiers at it and within 2 edges (remembered counts if not visible now). `Attackers` = the same sum for soldiers that could be sent (all but one per sending building, HP above 60%). Attack if `Attackers x 100 >= Defenders x Threshold`, where Threshold is the personality's (below). Among qualifying targets: score = ratio + 20 if it is a settlement + 15 if it holds a mine or a vein sector + 10 if it attacked us in the last 3000 ticks - 25 per previous attack on it in the last 6000 ticks. Best positive target wins. Interval shortens to 300 after a successful attack and lengthens to 1800 after a failed one. Never attacks before `PeaceTicks` (personality) or during a truce.

**Personalities** (data; the rival's default is Balanced; scripted test opponents use the others):

| Field | Passive | Balanced | Aggressive |
|---|---|---|---|
| Attack threshold (%) | never attacks | 130 | 90 |
| Peace ticks | - | 9000 | 3000 |
| Military mode trigger | enemy within 2 edges | 4 edges | always after peace |
| Basic list extra | +1 Watch post | as listed | Barracks and Drill yard moved up |
| Max military sites open | 1 | 2 | 3 |

**Determinism.** All ties by id; any random pick from `Rng.Fork("ai.faction." + id)` held in state. The AI is a pure function of state plus its own saved scheduler state.

**What the player sees.** The AI still expresses every action as a proposal with a one-sentence reason and a speaker. Autonomous leaders execute; the player can watch, ask and advise (8.4).

**Headless proof** (section F of `AGES_PROOF.md`; 11 seeds each, from `dev.historic`, 90 game minutes, Balanced against):
- itself: both factions build at least 35 of the 40 types; at least one soldier of total level 6 in at least 8 of 11 seeds; at least one attack and one capture in at least 8 of 11; no deadlock (no 10-minute window without a finished construction while a need is open); conservation zero drift.
- Passive: Balanced captures the Passive home settlement in at least 9 of 11 seeds, median under 75 minutes.
- Aggressive: Balanced survives (keeps its home settlement) in at least 8 of 11 seeds to 90 minutes, and wins or holds the strength lead in at least 5.
- Every placeable type built in at least one game; every recipe run; every training step done.

### 8.3 The RTS AI (Modern)

Lives in `Sim/Rts/Ai/`, one instance per autonomous faction from Modern on, ticking in slot 1160, after the faction AI. Modules and intervals:

| Module | Interval | Does |
|---|---|---|
| `BaseBuilder` | 25 while building, 125 idle (+0-10 offset from the forked Rng) | one structure decision (priorities below) |
| `PowerManager` | 150 | switch consumers off smallest-draw first on deficit, back on largest first on surplus |
| `HarvesterManager` | 50 | idle harvesters to the best hex (mature stock, distance, enemy nearness x 20 cost) |
| `UnitBuilder` | 30 | one item per queue by shares |
| `SquadManager` | 50 (squads updated every 75) | pool, assault, rush check every 600, protection |
| `Scout` | 300 | send one Scout or Recon buggy to the oldest unseen cell group |
| `Repair` | 100 | queue repairs below 75% HP; Engineers to damaged structures |
| `DefencePlacer` | inside `BaseBuilder` | defence centre faces the nearest seen enemy; moves to the last attack point |

**Base build priorities** (first that applies):
1. Command post if none (and unlocked).
2. Power: if spare power < clamp(10% of draw, 5, 15), a Generator (or Ward conduit if a restored tower is in reach).
3. Refinery while fewer than 1, or fewer than 2 once a Motor works exists.
4. Infantry hall, then Motor works, if missing.
5. Depot when credits are above 80% of the cap.
6. Fractions with limits and delays (data per personality):

| Structure | Normal share | Limit | Earliest tick |
|---|---|---|---|
| Generator | 20% | - | 0 |
| Refinery | 10% | 3 | 0 |
| Foundry | 8% | 2 | 600 |
| Machine shop | 8% | 2 | 900 |
| Infantry hall | 8% | 2 | 0 |
| Motor works | 8% | 2 | 1200 |
| Signal tower | 4% | 1 | 1800 |
| Service bay | 4% | 1 | 2400 |
| Research institute | 4% | 1 | 3600 |
| Gun nest | 10% | 6 | 0 |
| Cannon post | 10% | 4 | 1800 |
| Barricade / Rampart | rings round defences | 24 cells | 1200 |

The list is shuffled with the forked `Rng`; the first type under its share and limit and past its delay is placed. Nothing starts with fewer than 150 available credits. A structure is placed only if its goods are affordable from the supply storehouse plus incoming, or its need is raised so the faction AI makes them (the ware-target link). Placement: random valid cells in a ring 20-150 m from the Command post; refinery nearest to high-stock Mountain or Hills hexes; defences at the defence centre.

**Unit mix** (shares, Normal): Rifleman 35%, Launcher trooper 15%, Light tank 25%, Heavy tank 10%, Field gun 5%, Recon buggy 5%, Engineer 5%; Harvesters kept at max(2, refineries). Limits: Engineers 3, Scouts 2. A unit is queued only if its credits are available and its physical inputs are in stock or incoming; otherwise the module raises the targets of its inputs and skips it (no blocked queues).

**Squads.**
- New units join the idle pool at the rally point (in front of the Command post, toward the enemy).
- Assault: when the pool holds `SquadSize` (Normal 10, Rush 6, Turtle 14) plus 0-3 extra, the pool becomes a squad.
- Target: the closest visible or remembered enemy structure by path, preferring Command post, production, then power when two are within 10% of the same distance. If no enemy structure is known, the squad attack-moves to the Scout's oldest unseen area toward the enemy start.
- States: Idle, Gather (regroup on a slow leader until 80% are within 30 m), AttackMove, Attack, Flee. Stuck after 60 ticks without progress: back to Idle.
- Rush: every 600 ticks, if the enemy has no defence structure and fewer than 3 units within 100 m of its Command post, idle units form a rush squad.
- Protection: when an own structure or harvester is hit, nearby squads retarget the attacker and idle units form a protection squad; it chases for up to 300 ticks out of sight, then returns and rejoins the pool.

**Attack or flee** (integers, our own rule table):
`Own = sum(HP) x sum(damage per 10 ticks against the enemy's main class)`, `Enemy` the same for visible enemies within 60 m of the squad leader (including defences).
- Attack if `Own x 100 >= Enemy x 120` (Normal), `>= Enemy x 90` (Rush), `>= Enemy x 150` (Turtle).
- Flee if the squad lost more than 50% of its starting HP and `Own < Enemy`. Fleeing squads go to the nearest Service bay or the Command post and rejoin the pool when repaired.

**Determinism.** All random choices from `Rng.Fork("ai.rts." + faction)`; orders are ordinary commands submitted through the sim's command path in the same tick order.

**Headless proof** (section R; 11 seeds, `dev.modern.skirmish`, 40 game minutes):
- Normal against Normal: both build a Command post, power, a refinery, an Infantry hall and a Motor works; both field a mixed army; at least one Command post destroyed in at least 6 of 11 seeds.
- Normal against Passive (scripted: builds defences and never attacks): Normal destroys the Passive Command post in at least 9 of 11, median under 30 minutes.
- Normal against Aggressive (Rush): Normal survives the first rush in at least 8 of 11 and wins at least 5.
- Never more than 60 s with power below 100% while credits are available; never a queue blocked on missing inputs for more than 120 s while those inputs are producible.
- Conservation of goods and credits zero drift.

### 8.4 How the player interacts

**Leaders propose and execute.** From handover on, the prince (Historic), the commander (Modern) and the engineer (Future) run their side with the AI above. Each announces its next action as its top proposal: a speech line, and on the Tab map a marker. It executes after `AnnounceTicks` = 150 (15 s) unless the player answers. The player may still cast (press the ground at the marker) to make it happen at once.

**Advice** (command `Advise{Faction, Kind, Target}`), always through speech replies or a map tap, never a button:

| Advice | Effect | Lasts |
|---|---|---|
| Priority: economy, expansion, military, defence, trade | +40 to that class of candidates | 3000 ticks |
| Favour this (on a proposal) | executes now | once |
| Not this (on a proposal) | declines it (existing cooldown) | 600 ticks |
| Focus on a ware | +30 to producers and inputs of that ware | 3000 ticks |
| Attack there (map tap on a seen enemy site) | the AI evaluates that target first; if it would fail the threshold, the leader says why ("They have six soldiers there; we can send three.") and does not go | once |
| Hold there / defend there (map tap) | sets the defence centre or the garrison priority there | 3000 ticks |
| Scout there (map tap) | the Scout module goes there next | once |

**Asking.** "What are you doing?" and "Why?" are read helpers (`FactionApi.Explain`) that return the current top three with reasons and the blocking reason, if any ("Waiting for iron: the smelter has no coal.").

**Map as the command surface.** Tab opens the map (read-only today, with tap-to-walk). With a leader near the player (within 30 m) or in a mission, a tap on the map shows the leader's options as speech replies (Attack there, Hold there, Scout there, Walk there). With no leader near, a tap stays an auto-walk target. No new buttons. The map is `Game/UI/MapScreen.cs`, owned by `PRD_CRYSTALS` I5; the taps here are an `IMapLayer` of ours, not an edit to that file.

**F1.** The God View tools keep direct build, cast, upgrade, place structure, queue unit, and direct unit orders for testing and AutoPlay.

---

## 9. Missions

Missions are scenario data (`Sim/Scenario/`): sites, objectives, enemy plans, kits, dialogue line ids, checkpoints, transfer outcomes. The runtime is the same sim; objectives are checked by a Scenario system (slot 1280) that reads state and emits `ObjectiveChanged`, `MissionWon`, `MissionLost`.

### 9.1 Modern: the harbour blockade (`ch.modern.harbour`)

**Setting.** Thirty winters after the crystals. The Ash Clan holds the harbour approach with a fortified command point and a gun line; supply ships wait offshore. Commander Ilse Teague (the prince's heir) needs the route open. Quartermaster Tobin Hale counts what is left.

**Map objects** (placed by rule on the seeded board; the generator check in C1 validates them):
- `lm.harbour` (rim corner) and the **quay** (the two rim hex edges either side).
- **Ash command point**: a 7-cell Ash structure (HP 1200) one hex in from the harbour, with 2 Gun nests, 1 Cannon post, 1 Generator, 6 Riflemen, 2 Light tanks on Defensive stance.
- **Relay** (`lm.relay`): an interior corner 2-3 hex edges from both the harbour and the Ash seat, holding the Ash Clan's relay mast (structure, HP 400, guarded by 4 Riflemen and 1 Gun nest). While the Ash Clan holds it, their structures near the harbour draw power through it; if it is captured, Ash power at the harbour drops to 0 (their Cannon post and Gun nests near the harbour go offline: Gun nests at 50% rate, Cannon posts off).
- **Route A** (coast road): main roads along the shortest hex-edge path from the quay to `lm.home_hearth`, passing the command point.
- **Route B** (inland road): the shortest path from the quay to the home that passes through `lm.relay` and avoids the command point's hex.

**Flow and objectives** (each a step the commander pitches; every action uses ordinary commands):

| Step | Objective | Points |
|---|---|---|
| 1 | Talk to the commander on the docks (`mission.talk`) | - |
| 2 | Found a camp at the harbour approach corner (supplied road) | `camp:` milestone, usually point 9 |
| 3 | First sale at the harbour Trading post | `first:sale` if not paid, else `mission:harbour:trade` = 1; points reach 10: the roster unlocks (shown in the world: the commander's line, the HUD, the ledger) |
| 4 | Build a Command post with a supplied road from the harbour camp or home | - |
| 5 | Bring a Generator online; build an Infantry hall or Motor works (powered) | - |
| 6 | Produce from physical goods: 1 rifle squad (4 Riflemen), 1 Light tank; ore mined or hauled, refined into Steel at a Foundry; Shells and Rifles from a Machine shop | - |
| 7 | Scout: a Scout or Recon buggy reveals the command point and the relay | - |
| 8 | One static defence on the supply road (Gun nest or Cannon post) | - |
| 9 | Choose the approach (commander asks; both stay possible) | - |
| 9A | Frontal: destroy or capture the command point (Engineer capture below 50% HP) | `mission:harbour:front` = 1 |
| 9B | Flank: capture the relay with an Engineer (infantry hold the ground for 50 ticks) | `mission:harbour:relay` = 1 |
| 10 | Convoy: the quartermaster dispatches 3 Supply trucks from the quay along an open route; escort them | - |
| Win | at least one truck of the convoy reaches `lm.home_hearth` (`MissionWon{Route}`) | `mission:harbour:convoy` = 1 |

**Rival pressure.** The Ash Clan's RTS AI plays the Turtle personality at the harbour, plus authored raids: from minute 8, every 4 minutes, a raid of 2-4 units against the supply road or the relay, announced 30 s before by the quartermaster ("Ash trucks on the coast road, two minutes out."). Raid size grows by one each time, capped at 4.

**Defeat.** The player's Command post destroyed, or all convoy trucks destroyed (`MissionLost{Reason}`). Retry offers the last checkpoint: mission start, after step 5 (base running), after step 9 (approach chosen). Checkpoints are ordinary saves in the slots `ckpt.harbour.<n>` (`ckpt.core.<n>` in the Future).

**Shortages are inspectable.** Every structure and queue answers "why is this waiting?" with the stall reason and the ware ("The motor works waits for 2 parts: the machine shop has no steel."), in the inspect bubble and from the commander.

**Effect on the Future start.** Route A opened: +20 Plate and +10 Slug in the transfer station. Route B opened: battery full and +5 on the first generator. Both: both.

**Standalone start.** `dev.modern.harbour` (4.7) puts the player at step 1 with 8 points.

### 9.2 Future: bring the core online (`ch.future.core`)

**Setting.** Generations later. Machines left from the old wars hold the outer island. In the district, the surviving community keeps a dormant core. Engineer Rhea takes the player there: reconnect the core before the next assault and reopen the evacuation dock.

**The district** (`lm.district`): the Meadow or Fields hex adjacent to `lm.home_hearth` with the most free area, never a hex that holds a campaign site (`PRD_CRYSTALS` Appendix C) (rule in the generator, ties by lower id). It becomes a **local industrial tile area**: a 40 x 40 grid of 2.5 m tiles centred on the hex centre, clipped to the hex (tiles outside the hex are void). Tile coordinates are integers; the tile grid is aligned to the world X and Z axes.

**Inventory boundary.** The **transfer station** stands on the district edge tile nearest the home corner. It is a storehouse place for the road side (`PlaceKind.Transfer`, served by carriers and shipments exactly like a storehouse) and the only belt port. Its inventory is one stock array. It has up to 4 belt ports: an export port feeds one chosen ware onto an adjacent belt when the belt's first slot is free; an import port takes any item arriving at it into the inventory. An item is in exactly one place at any tick: a storehouse, a carrier, the transfer station, a belt slot, a machine's buffer, a turret's magazine, or a unit's cargo. The ledger counts belt moves as transfers, never as production.

**Blocks** (placed by the engineer's blueprints, cast by pressing the ground as in the Prehistoric spell; F1 places them directly):

| Id | Block | Size | Cost (from delivered Modern stock) | Power | Behaviour |
|---|---|---|---|---|---|
| F1 | Drill | 2 x 2 | 4 Steel, 2 Parts | -2 | must cover at least 2 deposit tiles; 1 item per 40 ticks (Ore or Coal, by deposit), pushed to the belt it faces |
| F2 | Belt | 1 x 1, directional | 1 Steel per 3 tiles | 0 | 3 item slots per tile; an item advances one slot every 8 ticks only if the next slot is free (backpressure) |
| F3 | Splitter | 1 x 1 | 1 Steel, 1 Parts | 0 | one input, two outputs, alternates; if one output is blocked, sends to the other |
| F4 | Junction | 1 x 1 | 1 Steel, 1 Parts | 0 | two belts cross without mixing |
| F5 | Generator block | 2 x 2 | 3 Steel, 2 Parts | +6 | burns 1 Coal item per 100 ticks from its input buffer (holds 3) |
| F6 | Battery | 1 x 2 | 3 Steel, 2 Parts | stores | 600 energy units; charges from surplus, discharges on deficit |
| F7 | Smelter block | 2 x 2 | 4 Steel, 1 Bricks | -3 | 2 Ore -> 1 Plate (60 ticks); input buffer 4, output buffer 2 |
| F8 | Press | 2 x 2 | 3 Steel, 2 Parts | -3 | 1 Plate + 1 Coal -> 1 Circuit (80), or 1 Plate -> 3 Slug (40), by the recipe set on it |
| F9 | Fabricator | 2 x 2 | 4 Steel, 2 Parts | -3 | 2 Plate + 1 Circuit -> 1 Frame (120) |
| F10 | Assembler | 3 x 3 | 4 Steel, 4 Parts | -4 | 1 Frame + 2 Circuit -> 1 Repair drone (300), which joins the army |
| F11 | Turret | 2 x 2 | 3 Steel, 1 Parts | -1 (-2 firing) | magazine of 10 Slugs; 1 Slug per 2 shots; 6 / 6 / 2 damage at 45 m; no ammo, no fire |
| F12 | Core | 3 x 3 (exists) | - | -4 to wake | HP 500; online once powered for 300 continuous ticks with a Plate delivery of 10 |

Belt item buffers: belts 3 per tile; machines as listed; the transfer station unlimited (it is a storehouse).

**Power network** (local to the district; separate from the Modern faction network): supply from Generator blocks, the battery covers deficit, draw from blocks. Brownout: when supply plus battery output is below draw, blocks are served in priority order (default: Turrets, Core, Drill, Smelter, Press, Fabricator, Assembler; the player can move one class to the front through the engineer) and the rest pause (`no_power`). The battery and generation, draw and deficit are shown on the engineer's board in the world and in the H HUD.

**Three readable failures, each repairable:**

| Failure | What happens | Repair |
|---|---|---|
| Cut belt (enemy fire breaks a tile) | the tile's items are destroyed (ledger `Destroyed`); upstream backs up; downstream drains | the drone repairs it, or the engineer pitches a repair (1 Steel), or a reroute |
| Power loss (generator out of coal) | brownout by priority; battery drains, then blocks stop | route Coal to the generator through the splitter, or switch a block off |
| Depleted input (turret out of Slugs) | the turret stops firing | route Slugs to it; the press's recipe set to Slug |

**Removal and age change.** Removing a belt tile or block returns its items to the transfer station inventory (ledger `Reclaimed`). At any later age change, items on belts and in buffers are reclaimed the same way.

**The routing choice.** The engineer proposes, as two replies, a Splitter (share the coal line between the generator and the press) or a Junction (cross the ore line over the coal line and keep them separate). Both work; they make different shortages likely.

**The engineer's blueprints.** Each proposal shows deposit, destination, footprint, inputs and expected output per minute (`IndustryApi.Blueprint`), drawn as a ghost in the world and on the map. The player answers by speech reply and presses the ground to place. Priority choice and repair or reroute come the same way.

**Waves.** Five waves of hostile machines (faction id 1's automated remnant; a wave script, not a full faction in F1), each announced 60 s ahead with its entry edge and size; the first starts when the player tells the engineer "We're ready", or after a 4-minute budget that the engineer counts down. Wave sizes 4, 6, 8, 10, 12 crawlers (fast, 20 HP, 3 damage to blocks), walkers from wave 3 (60 HP), one breaker in wave 5 (200 HP, targets belts). Modern units carried over and the drone fight in the same combat system.

**Win.** The core is online and wave 5 is cleared: the evacuation dock opens; a final convoy (2 Supply trucks) reaches `lm.harbour`. That ends this plan's campaign (`MissionWon`, ending scene owned by the game layer).

**Loss.** Core HP 0: `MissionLost{core_lost}`; retry from the checkpoint before wave 1 or before the wave that broke it.

**Standalone start.** `dev.future.core` with option A, B or none.

---

## 10. MVP slices (the owner's order)

### Slice H1: Historic growth and the prince's handover

Small on purpose. It is needed before the crystal campaign's first slice.

**What the crystal campaign needs from H1** (and nothing more):
1. `Era.Historic` and `ch.historic`, reachable by the handover and `AdvanceChapter`, and by `dev.historic`.
2. The player's faction autonomous after handover, so the player can leave with Mira while the prince builds.
3. `Advise` and `FactionApi.Explain`, so the player can talk to the prince when back.
4. The campaign overrun rule (4.3): `CampOverrun` events, the home camp never lost, no `GameEnded` from overrun or time while the campaign runs. Plus `AgesApi.CampaignMayStart(WorldState)` and the `CampaignMayStart` event (true from the Historic step on).
5. The landmark registry (`Board.Landmarks`, `LandmarkApi`) with the four tower ids reserved for `PRD_CRYSTALS.md` to place.
6. The ledger skeleton with `crystal:<element>` keys accepted.
7. Save version 5 with an `Eras` state, a `History` record (casualties list included) and an empty `Campaign` slot the crystals package owns.

**In H1:** handover detection, autonomy with the announce window, advice and explain, the Historic step (smith's chest delivered but not yet usable: no Historic types yet), grade 1 upgrades of the 16 existing types, City to Town with outer plots, campaign overrun, save v5 with migration from v4.

**Deliberately left out of H1:** all new wares and buildings, soldiers, fog, credits, points beyond the skeleton.

**What the owner sees and does.** From a new game: plays the Prehistoric opening as now; when the first planks reach a site, the prince says he can run the camp; the player says "Show me"; the camp starts to look like a village as buildings upgrade; the prince builds without spells, announces each action, and answers "What are you doing?"; the player can say "Not this" or "Focus on stone". Or: start `dev.historic` from F1.

**Acceptance.**
- `dotnet test DotNet` green, including new `HandoverTests`, `AutonomyTests`, `UpgradeTests`, `UpgradeBenefitTests` (16 types and the town), `PlotLayoutTests` (outer plots), `CampOverrunTests`, `SaveMigrationTests` (a v4 save loads as a Prehistoric skirmish and plays on identically).
- Proof section H: handover tick per seed for the prompt, 45 s and ignored players (an ignored player never hands over; prompt median 5-9 minutes); after handover with no player input at all, the faction keeps building for 20 minutes and is not overrun in at least 8 of 11 seeds; the Prehistoric skirmish proof unchanged.
- PlayMode: the handover conversation and advice replies work on keys and taps; a phone-sized window shows the lines without overlap.

### Slice H2: the complete faction, its military and its AI

**In H2:** wares 10-34, buildings 16-39, workers and tools, veins, the Well source, the smith's chest made usable, Ox pen, Trading post and credits, the Scout hut, soldiers with training, garrisons, attacks, capture, fog, the points ledger with its milestones, the full faction AI with personalities, the coverage report.

**What the owner sees and does.** Starts `dev.historic` (or plays on from H1), watches the prince grow a full economy, sees miners, smiths and a drill yard, sees soldiers train, sees the Ash Clan attack a border camp and the prince defend it, advises "Attack there" on the map and sees the answer, watches points climb on the H HUD.

**Acceptance.**
- Unit tests for every recipe, every training step, every grade 2 cost check (refused before Modern), the bootstrapping chest, attack resolution cases (capture, raze, retreat), fog states.
- Proof sections A (ware audit: every ware made and used in games), F (faction AI against itself, Passive, Aggressive, as in 8.2), V (points timeline per faction), D (conservation zero drift, 10 seeds, 90 minutes).
- `COVERAGE.md` generated; every H item at least `proven`.
- PlayMode: placeholder views for every new building by grade, soldiers walking and duelling, the fog shading on the map, no buttons.

### Slice M1: the Modern proof

**In M1:** the ledger unlock and mixed costs, sim lattice and passability, units and orders, combat with armour classes, fog for units, power, the 17 structures, queues with physical inputs, harvester and refinery, Foundry and Machine shop chains, grade 2 upgrades and Town to District, the RTS AI, the Historic to Modern transition with its transfer table, the harbour mission with both approaches and retry, `dev.modern.harbour` and `dev.modern.skirmish`.

**What the owner sees and does.** Starts `dev.modern.harbour` (or arrives from the campaign fixture); walks the docks; talks to the commander and quartermaster; sees the points reach 10 and the roster unlock; watches the base rise from delivered goods; asks why the motor works waits; chooses an approach; taps targets on the map; escorts the convoy; loses once on purpose and retries.

**Acceptance.**
- `LedgerTests`, `UnlockTests` (9 against 10 points for two factions independently: faction 0 at 10 can build, faction 1 at 9 is refused, then 1 reaches 10 and can; neither affects the other), `MixedCostTests` (goods without credits refused; credits without goods waits; retry; cancel refunds; save and load mid-site spends once), `QueueTests`, `PowerTests` (brownout slows production by ratio; defences offline), `LatticeTests`, `FogTests`, `CombatTests`, `HarvesterTests`, `TransitionTests` (H to M on a fixture of a finished Historic game).
- `HarbourMissionTests`: scripted runs of Route A and Route B each win; a run that loses the Command post loses and its retry from each checkpoint wins; a shortage run (no coal) stalls the generator and shows the reason; a power-failure run slows the queue.
- Proof section R (RTS AI, 8.3) and a section showing the faction AI and RTS AI together on `dev.modern.skirmish`.
- Both upgrade steps shown on one building in one run (grade 0 to 1 in goods in Historic, 1 to 2 in credits in Modern).
- PlayMode: the harbour flow through ordinary inputs (AutoPlay script `harbour_front`, `harbour_flank`), phone-sized window check.

### Slice F1: the Future proof

**In F1:** the district tile area, transfer station, blocks F1-F12, belts with backpressure, splitter and junction, power network with battery and brownouts, turret ammunition, fabricator and assembler, the repair drone joining the army, waves, core and retry, the Modern to Future transition with its table, `dev.future.core`.

**What the owner sees and does.** Starts `dev.future.core` (A, B or none) or arrives from a Modern win; follows the engineer; places a drill on ore, lays belts to the smelter, picks splitter or junction, watches items queue and back up, builds a generator and battery, feeds a turret, sees a brownout, repairs a cut belt, builds the drone, says "We're ready" and holds five waves.

**Acceptance.**
- `IndustryTests` (backpressure stops a drill and it resumes; splitter alternates and falls back; junction keeps lines apart; battery charges and discharges; brownout order; turret stops at 0 Slugs and resumes), `IndustryConservationTests` (every item in exactly one place every tick, 10 seeds, with removals and cuts), `CoreMissionTests` (win; loss and retry from both checkpoints; the three failures each detected and repaired by a scripted fix), `TransitionTests` (M to F with each outcome).
- PlayMode: AutoPlay `core_online`, phone-sized window check.

### Slice C1: campaign proof across transitions

**In C1:** the whole chain through ordinary player commands from a new campaign (with the crystal campaign's ending, or its fixture if not ready, reported as such); save and load on both sides of each transition with a road shipment, a queue and belt items in flight; a seeded variation of each mission (raid plan, relay and harbour corner, deposit layout, opening stock) validated for reachability and a constructible first chain.

**Acceptance.**
- `CampaignChainTests`: Prehistoric to Future end, 3 seeds, scripted player through ordinary commands; conservation across every transition (ledger lines `Settled`, `JumpLoss`, `JumpKit`, `Reclaimed` balance); casualty history identical on every side; no milestone paid twice; points and unlock flags consistent; a post-loss retry valid in each mission.
- `ScenarioValidationTests`: 20 generated variations per mission pass reachability, first-chain and kit checks.
- Existing smoke paths still green: Prehistoric proof, dance, Ward duel, save and load.
- A Windows build; packaged AutoPlay `chain_short` exits 0; `PROGRESS.md` says what ran.

---

## 11. Work packages

Rules for all packages: each package owns its files and folders and no other package edits them. Interfaces land first in a small interface package per slice (appendix A); later name changes are additive and logged at the end of appendix A. One Unity editor per folder: every package marked "Unity" works in its own git worktree (named below) on its own branch. Branches are `ages/<id>` (for example `ages/h1-0`); the slice lane `ages/h1` already exists and takes the merges. Interface packages tag their merge: `ages-H1-0`, `ages-H2-0`, `ages-M1-0`, `ages-F1-0`. Every package starts from tag `refactor-done` or a later tag of this lane. Sim packages run without Unity (`dotnet test DotNet`) and can share the integration checkout only if they own disjoint folders; the default is one worktree per package anyway.

Model tiers: "sonnet" for mechanical or well-specified work, "opus" for substantial design-heavy implementation, "fable" for the hardest integration and judgement work.

### 11.1 Slice H1

| Package | Purpose | Owns | Model | Unity | Worktree | Depends on |
|---|---|---|---|---|---|---|
| H1-0 | Interfaces: `Eras`, `History`, `Landmarks`, ledger skeleton, `AdvanceChapter`, `Advise`, `UpgradeBuilding`, grade fields, outer plot ids, save v5 and migration framework, `ApiSurfaceTests` additions | `Sim/Eras/**` (stubs), `Sim/Ledger/**` (stubs), `Sim/Save/**` (and the save version), `Sim/Core/WorldSim.cs` (`SystemOrder`), its lines in `Sim/Core/WorldSimFactory.cs` (a registration file), the shell `Sim/Campaign/CampaignState.cs` with `WorldState.Campaign` (handed to `PRD_CRYSTALS` I0 at tag `ages-H1-0`), `Tests/EditMode/ApiSurfaceAgesTests.cs`, `Tests/EditMode/SaveMigrationTests.cs` | opus | no | `wt-h1-0` | tag `refactor-done`; tags `ages-H1-0` when merged |
| H1-1 | Handover, autonomy with announce window, advice and explain, campaign overrun and setback, the Historic step and smith's chest | `Sim/Factions/**`, `Sim/Eras/**` (after H1-0), `Tests/EditMode/HandoverTests.cs`, `AutonomyTests.cs`, `CampOverrunTests.cs`, `EraTests.cs` | opus | no | `wt-h1-1` | H1-0 |
| H1-2 | Grade 1 upgrades of types 0-15, City to Town, outer plots, `EconomyTuning.CycleBonusPermille` (per building type, default 0, for the crystal campaign's Fire boon) | `Sim/Economy/**`, `Sim/Board/PlotLayout.cs`, `Sim/Board/Board.cs`, `Sim/Board/BoardGenerator.cs`, `Tests/EditMode/UpgradeTests.cs`, `UpgradeBenefitTests.cs`, `PlotLayoutTests.cs` | sonnet | no | `wt-h1-2` | H1-0 |
| H1-3 | Proof section H, `AGES_PROOF.md` generator scaffold | `Tests/EditMode/AgesProof*.cs`, `Docs/AGES_PROOF.md` | sonnet | no | `wt-h1-3` | H1-1, H1-2 |
| H1-4 | Prince handover scene, invitation, advice and ask replies (map taps move to H2-7) | `Game/Prince/**` except `PrinceActorBinding.cs` (`PRD_CHARACTERS` WP-8) and, once H2-7 starts, `Advice/`; `Tests/PlayMode/HandoverPlayTests.cs` | opus | yes | `wt-h1-4` | H1-0 (code against stubs), H1-1 to finish |
| H1-5 | Grade visuals for the 16 types and the town, outer plot rings, Historic dressing by grade | `Game/View/Land/Grades/**`, `Tests/PlayMode/GradeViewTests.cs` | sonnet | yes | `wt-h1-5` | H1-0, H1-2 to finish |

Order: H1-0, then H1-1, H1-2, H1-4, H1-5 in parallel, then H1-3. Merge order: 0, 2, 1, 3, 5, 4.

### 11.2 Slice H2

| Package | Purpose | Owns | Model | Unity | Worktree | Depends on |
|---|---|---|---|---|---|---|
| H2-0 | Interfaces: ware ids 10-34, building ids 16-39, def table shape, worker table, soldier record, military commands and events, fog API, ledger milestone API, AI personality data shape | `Sim/Core/Ware.cs`, `Sim/Military/**` (stubs), `Sim/Vision/**` (stubs), `Sim/Factions/Ai/**` (stubs), `Tests/EditMode/ApiSurfaceH2Tests.cs` | opus | no | `wt-h2-0` | H1 merged |
| H2-1 | The economy tree: wares, buildings, recipes, veins, Well, tools in costs, Ox pen, Trading post and credits income, Scout hut, grade 2 data | `Sim/Economy/**`, `Sim/Board/BoardGenerator.cs`, `Tests/EditMode/TreeTests*.cs` | opus | no | `wt-h2-1` | H2-0 |
| H2-2 | Military: soldiers, Barracks recruits, training sites, garrisons, meals, attacks, duels, capture, raze, retreat; fog grid | `Sim/Military/**`, `Sim/Vision/**`, `Tests/EditMode/MilitaryTests*.cs`, `FogTests.cs` | opus | no | `wt-h2-2` | H2-0 |
| H2-3 | Points ledger: milestones, keys, caps, events | `Sim/Ledger/**`, `Tests/EditMode/LedgerTests.cs` | sonnet | no | `wt-h2-3` | H2-0 |
| H2-4 | Faction AI: scheduler, needs, verdicts, scores, modes, upgrades, dismantle, soldiers, attacks, personalities; integration with `Brain` | `Sim/Factions/**`, `Tests/EditMode/FactionAiTests.cs` | fable | no | `wt-h2-4` | H2-0; behaviour of H2-1 and H2-2 to finish |
| H2-5 | Proof sections A, F, V, D; `CoverageReport` | `Tests/EditMode/AgesProof*.cs`, `CoverageReport.cs`, `Docs/AGES_PROOF.md`, `Docs/COVERAGE.md` | opus | no | `wt-h2-5` | H2-1 to H2-4 |
| H2-6 | Views: 24 new buildings by grade (original placeholder models), mines on veins, soldiers, duels, garrison flags, fog on the map through an `IMapLayer` (`MapScreen.cs` belongs to `PRD_CRYSTALS` I5) | `Game/View/Land/Historic/**`, `Game/View/Military/**`, `Tests/PlayMode/HistoricViewTests.cs` | opus | yes | `wt-h2-6` | H2-0; H2-1 and H2-2 to finish |
| H2-7 | Interaction: military advice, attack-there and the other map taps (as an `IMapLayer`; `Game/UI/Hud.cs` hooks go in as registration lines), H HUD points and credits lines, inspect sentences for the new stalls | `Game/Prince/Advice/**`, `Game/UI/Hud/LedgerLines.cs`, `Tests/PlayMode/AdviceTests.cs` | sonnet | yes | `wt-h2-7` | H2-0, H1-4, `cry-I5` merged (for `IMapLayer`) |

Order: H2-0; then H2-1, H2-2, H2-3, H2-6, H2-7 in parallel; H2-4 starts on the interfaces at the same time and integrates last; H2-5 last. H2-4 and H2-1 both touch the factions-economy seam only through `EconomyApi` read helpers (owned by H2-1); H2-4 never edits economy files.

### 11.3 Slice M1

| Package | Purpose | Owns | Model | Unity | Worktree | Depends on |
|---|---|---|---|---|---|---|
| M1-0 | Interfaces: lattice, passability, unit and structure records, orders, queues, power, roster defs, scenario and objective shapes, transfer table shape | `Sim/Board/Lattice.cs` (created by `PRD_CRYSTALS` I0 with `CellOf` and `Distance`; from tag `cry-I0` it is this package's, extended additively), `Sim/Rts/**` (stubs), `Sim/Scenario/**` (stubs), `Tests/EditMode/ApiSurfaceM1Tests.cs` | opus | no | `wt-m1-0` | H2 merged |
| M1-1 | Units: lattice pathing, movement, orders, stances, combat with armour classes, unit fog, engineer capture and repair | `Sim/Rts/Units/**`, `Sim/Board/Lattice.cs` (after M1-0), `Tests/EditMode/LatticeTests.cs`, `CombatTests.cs`, `UnitOrderTests.cs` | opus | no | `wt-m1-1` | M1-0 |
| M1-2 | Base: unlock check, roster placement and supply corner, mixed costs and reservations, power, queues with physical inputs, harvester and refinery, Foundry and Machine shop, grade 2 upgrades and District | `Sim/Rts/Base/**`, `Sim/Ledger/**`, `Tests/EditMode/UnlockTests.cs`, `MixedCostTests.cs`, `QueueTests.cs`, `PowerTests.cs`, `HarvesterTests.cs` | opus | no | `wt-m1-2` | M1-0 |
| M1-3 | RTS AI modules and personalities | `Sim/Rts/Ai/**`, `Tests/EditMode/RtsAiTests.cs` | fable | no | `wt-m1-3` | M1-0; M1-1 and M1-2 to finish |
| M1-4 | Transitions H to M, the harbour mission data and objectives, raids, checkpoints, standalone starts | `Sim/Eras/Transfer*.cs`, `Sim/Scenario/**`, `Tests/EditMode/TransitionTests.cs`, `HarbourMissionTests.cs`, `StartTests.cs` | opus | no | `wt-m1-4` | M1-0 |
| M1-5 | Proof section R and Modern sections | `Tests/EditMode/AgesProofModern*.cs` | opus | no | `wt-m1-5` | M1-1 to M1-4 |
| M1-6 | Views: roster structures, units, combat effects, power state, fog overlay, convoy | `Game/View/Rts/**`, `Tests/PlayMode/RtsViewTests.cs` | opus | yes | `wt-m1-6` | M1-0 |
| M1-7 | The harbour in play: commander and quartermaster, docks, delegated orders from the map, mission lines, retry flow, AutoPlay `harbour_front` and `harbour_flank` | `Game/Mission/Harbour/**`, `Game/Actors/Modern/**`, `Tests/PlayMode/HarbourPlayTests.cs` | opus | yes | `wt-m1-7` | M1-0, M1-4; rigs from `PRD_CHARACTERS.md` or placeholders |

### 11.4 Slice F1

| Package | Purpose | Owns | Model | Unity | Worktree | Depends on |
|---|---|---|---|---|---|---|
| F1-0 | Interfaces: tile area, blocks, belt slots, transfer place kind, district power, blueprint shape, wave script shape | `Sim/Industry/**` (stubs), `Tests/EditMode/ApiSurfaceF1Tests.cs` | opus | no | `wt-f1-0` | M1 merged |
| F1-1 | Industry: tiles, belts and backpressure, splitter, junction, drill, machines, transfer station boundary, district power, battery, brownout, turret ammunition, assembler and the drone joining the army | `Sim/Industry/**`, `Tests/EditMode/IndustryTests.cs`, `IndustryConservationTests.cs` | opus | no | `wt-f1-1` | F1-0 |
| F1-2 | Future mission: transition M to F, district conversion, waves, core, checkpoints, `dev.future.core`, the engineer's blueprint proposals | `Sim/Scenario/Future/**`, `Sim/Eras/TransferFuture.cs`, `Tests/EditMode/CoreMissionTests.cs` | opus | no | `wt-f1-2` | F1-0 |
| F1-3 | Proof sections for Future | `Tests/EditMode/AgesProofFuture*.cs` | sonnet | no | `wt-f1-3` | F1-1, F1-2 |
| F1-4 | Views and play: tile area, belts with moving items, blocks, power board, ghosts for blueprints, waves, engineer, AutoPlay `core_online` | `Game/View/Industry/**`, `Game/Mission/Core/**`, `Tests/PlayMode/CorePlayTests.cs` | opus | yes | `wt-f1-4` | F1-0 |

### 11.5 Slice C1

| Package | Purpose | Owns | Model | Unity | Worktree | Depends on |
|---|---|---|---|---|---|---|
| C1-1 | Campaign chain, save matrix across transitions, cross-transition conservation, seeded variations and validation | `Tests/EditMode/CampaignChainTests.cs`, `ScenarioValidationTests.cs`, `Sim/Scenario/Variation/**` | fable | no | `wt-c1-1` | F1 merged; crystal ending or fixture |
| C1-2 | AutoPlay `chain_short`, packaged runs, phone layout checks, Windows build | `Game/Boot/AutoPlayScripts/Chain*.cs`, `AgentScripts/Chain*.cs` | sonnet | yes | `wt-c1-2` | C1-1 |
| C1-3 | Docs: start-here note for the ages, `COVERAGE.md` refresh, `PROGRESS.md` | a refresh of `Docs/NEXT_START_HERE.md` (the one start-here page, instead of a separate ages note), `Docs/PROGRESS.md` (append-only) | sonnet | no | `wt-c1-3` | C1-1, C1-2 |

### 11.6 Summary table

| Package | Model | Unity | Depends on |
|---|---|---|---|
| H1-0 Interfaces | opus | no | - |
| H1-1 Handover and autonomy | opus | no | H1-0 |
| H1-2 Upgrades and towns | sonnet | no | H1-0 |
| H1-3 Proof H | sonnet | no | H1-1, H1-2 |
| H1-4 Prince scene | opus | yes | H1-0, H1-1 |
| H1-5 Grade visuals | sonnet | yes | H1-0, H1-2 |
| H2-0 Interfaces | opus | no | H1 |
| H2-1 Economy tree | opus | no | H2-0 |
| H2-2 Military and fog | opus | no | H2-0 |
| H2-3 Points ledger | sonnet | no | H2-0 |
| H2-4 Faction AI | fable | no | H2-0, H2-1, H2-2 |
| H2-5 Proof and coverage | opus | no | H2-1 to H2-4 |
| H2-6 Historic views | opus | yes | H2-0, H2-1, H2-2 |
| H2-7 Advice and HUD | sonnet | yes | H2-0, H1-4 |
| M1-0 Interfaces | opus | no | H2 |
| M1-1 Units and combat | opus | no | M1-0 |
| M1-2 Base, power, queues, unlock | opus | no | M1-0 |
| M1-3 RTS AI | fable | no | M1-0, M1-1, M1-2 |
| M1-4 Transition and harbour mission | opus | no | M1-0 |
| M1-5 Modern proof | opus | no | M1-1 to M1-4 |
| M1-6 RTS views | opus | yes | M1-0 |
| M1-7 Harbour in play | opus | yes | M1-0, M1-4 |
| F1-0 Interfaces | opus | no | M1 |
| F1-1 Industry sim | opus | no | F1-0 |
| F1-2 Future mission | opus | no | F1-0 |
| F1-3 Future proof | sonnet | no | F1-1, F1-2 |
| F1-4 Industry views and play | opus | yes | F1-0 |
| C1-1 Campaign chain | fable | no | F1, crystal ending |
| C1-2 Campaign AutoPlay and build | sonnet | yes | C1-1 |
| C1-3 Docs | sonnet | no | C1-1, C1-2 |

---

## 12. Test and proof plan

- **Fast harness first.** Every sim package adds EditMode tests under `Assets/World/Tests/EditMode`, compiled by `DotNet/World.Sim.Tests` through its existing glob, and runs `dotnet test DotNet` green before any merge. Unity EditMode runs the same sources.
- **Determinism.** Two runs of each standalone start for 20 game minutes give identical state JSON. Each AI proof is generated twice and must be byte-identical (as `ProofReport` today).
- **Invariants every 100 ticks** in every proof game: the spec 4.4 ware balance extended with new sinks and sources (`Sourced`, `Sold`, `Settled`, `JumpLoss`, `JumpKit`, `Reclaimed`, `Destroyed`, unit inputs); credits balance (`income = spent + held + reserved`); every item in one place (Future).
- **`AGES_PROOF.md`** (generated), sections: H handover; A ware audit for the full tree; F faction AI matchups; V points timelines; D conservation; R RTS AI matchups; M missions (both harbour routes, losses and retries; core online, failures and repairs); T transitions (before and after state digests); G known weaknesses.
- **Ledger and unlock tests** exactly as listed in M1 acceptance (two factions, 9 against 10, goods without credits, credits without goods, retry, cancel, save and load without double spend).
- **Save matrix.** v4 skirmish, v5 Historic, v7 Modern mid-mission with a queue item and a shipment in flight, v8 Future mid-wave with belt items: save, load, run 600 ticks, compare against a run without the save.
- **PlayMode.** Each Unity package adds its own test file; AutoPlay scripts drive ordinary inputs (walking, replies, ground presses, map taps), never F1 tools, for the acceptance runs.
- **Phone layout.** A PlayMode test sets the Game view to 844 x 390 and 932 x 430 and checks that speech bubbles and HUD lines stay inside the safe area and do not overlap; a capture is inspected by the agent. Device testing stays pending (owner decision 18).
- **Reporting.** Every package reports which tests and playthroughs ran and which did not (repo rule).

---

## 13. Risks

1. **Scope.** This is the big one. The full faction plus military plus AI (H2) is several weeks of agent work, as is the Modern proof (M1). The first slices deliberately leave out: other factions, the Ash Clan's own tree, air, sea, naval units, worker experience, a deployable command post, support powers, liquids, programmable logistics, extra Future sectors, generated whole new conflicts, and any rhythm or duel content in the later ages. H1 is small on purpose so the crystal campaign is not blocked.
2. **Plot and space limits.** One building per type per settlement, 9 plots per city and 15 per town: a full economy needs 5 to 7 towns per faction on a board with room for perhaps 20 settlements in total. If the AI cannot find space, it stalls. Mitigation: the Space expansion mode, proof deadlock check; fallback: allow a second building of a type in towns (tuning switch).
3. **Hex-stock competition.** Hunters, flax fields, mines and harvesters all draw on the same mature stock as the Prehistoric collectors. It can starve chains. Mitigation: the proof's per-ware loss and wait figures; fallback: separate cohorts per ware.
4. **The handover may be too early.** The first worked good reaches a site at about 5-9 minutes, before the player has learnt the spell loop well. The owner asked for exactly this milestone; the proof reports the time, and the open decision 14.2 offers a later milestone.
5. **Two AIs in one faction in Modern.** The faction AI and the RTS AI share goods. Without the ware-target link, queues block. Mitigation: queues only start when inputs exist; the RTS AI raises targets rather than queueing blind; a proof criterion on blocked queues.
6. **Lattice pathing cost.** About 30,000 lattice cells across the island; dozens of units pathing every few seconds is fine in C#, but naive A* per unit per tick is not. Mitigation: path on order, replan on block, cache by destination; a performance test with 60 units.
7. **Combat feel at first-person scale.** RTS fights seen from a walking viewpoint can be unreadable. The map is the command surface; the first-person view is for being there. Needs the owner's eyes after M1.
8. **Time-jump believability.** Carrying half the stock and all buildings across decades is a game rule, not realism. It is authored data and can be changed per jump.
9. **The crystal campaign's ending is a dependency** for the real H to M seam. A fixture stands in; C1 reports integration status honestly.
10. **Archived code is a port.** Power, queues and combat in `Archive/Sim/Military` were written on the flag graph; reuse is design and structure, not drop-in.
11. **GPL contamination.** Agents must not open or copy Widelands or OpenRA code into the repository. The research summary here is our own words; packages work from this document.

---

## 14. Open decisions for the owner

Each has a recommended default, so work can proceed without waiting.

1. **Can a faction open the Modern roster during the Historic crystal campaign if it reaches 10 points?** Your rule says the unlock follows the points. Default: yes, points only; the Historic point table is tuned so this normally happens late, and the Historic AI personalities do not build roster structures until the Modern era (a tuning switch, not a rule). Alternative: also require the Modern era.
2. **Does the handover also happen in the 20-minute Prehistoric skirmish?** Default: yes for the faction's autonomy (the prince starts building on his own after the first workshop goods reach a site), with no age step. Alternative: skirmish keeps the spell for every construction.
3. **Is the Prehistoric to Historic step automatic or by invitation?** Default: by invitation (the prince asks, the player says "Show me"); asked again every 3 minutes.
4. **Real attacks replace the abstract overrun from Historic on.** Default: yes; strength stays as a HUD and AI measure.
5. **Credits before Modern.** Default: the Trading post earns credits from Historic on, so a careful player arrives in Modern with a reserve.
6. **Same named people across the jumps.** Default: Mira recurs (crystal link); the prince is succeeded by his heir in Modern; the engineer in Future is a descendant; the prince and named companions appear in records and the memorial.
7. **What carries across the jumps.** Default: ownership of settlements and roads carries for both factions; half of the stock; buildings with grades; soldiers and Modern structures do not.
8. **Future scope.** Default: defend one district; extra sectors are finished-game scope.
9. **A later optional Mira duel.** Default: non-lethal outside the crystal campaign (no new permanent deaths).
10. **The Ash Clan's look and names in the later ages.** Default: the same faction id with era names: Ash Clan (Historic), the Ash Directorate (Modern), the Ash engines (Future, machines left from its wars).

---

## 15. Choices made in this document that the owner might overrule

1. **All names**: the Valley people, every ware, building, structure and unit name, Commander Ilse Teague, Quartermaster Tobin Hale, Engineer Rhea Okonkwo-Dahl.
2. **The handover milestone**: the first workshop-made good delivered to a construction (including a city upgrade), detected per faction.
3. **Outer town plots** at 45 m along the spoke and 17 m to the side, with the first-town-wins rule for the one facing conflict.
4. **Tools are consumed as worker equipment** in building and upgrade costs, instead of a population of workers who pick up tools. No worker experience.
5. **New raw goods draw on existing hex stock** (Hunter on Forest, Flax on Fields, mines on Mountain with a dealt vein) rather than new stock kinds.
6. **One building per type per settlement** kept from the refactor.
7. **Soldier model**: three skills, 0-2 each; the numbers in 5.4; attacks over any hex edges within 4 edges of a military building; capture of a whole settlement when unprotected.
8. **Recruit cap** by garrison capacity plus 4 per Barracks, instead of a population count.
9. **Harvesters take Mountain and Hills stock**, competing with mines, quarries and clay pits.
10. **Credit cap** of 2000 plus 1000 per Refinery and Depot.
11. **Generators burn Coal** (a physical input) and the Ward conduit is the campaign's fuel-free power from restored towers.
12. **Roster structures stand on the lattice**, not on plots, within 150 m of a Command post, supplied from a Town or District corner.
13. **Fog on a 20 m grid**; the AI uses only what its faction has seen for military choices but reads its own economy fully.
14. **Hand-set AI personalities** (Passive, Balanced, Aggressive; Rush, Normal, Turtle) instead of evolved weights.
15. **Leaders announce and act after 15 s**; casting skips the wait.
16. **Delegated orders only** in normal play; direct unit orders under F1.
17. **The district tile grid**: 40 x 40 tiles of 2.5 m on one hex.
18. **Items on removed belts return to the transfer station; items on a cut belt are destroyed.**
19. **Point values and caps** in 6.1, including 1 point per crystal and mission objectives.
20. **Save version per slice** (5 H1, 6 H2, 7 M1, 8 F1), each reading back to v4.
21. **Air and naval scoped out** of this plan entirely.

---

## 16. Needs from other PRDs

`PRD_CRYSTALS.md` was written in parallel and lists its own needs from this document (its section 3.9, N-1 to N-6). This document answers them as follows: N-1 `AgesApi.CampaignMayStart` and the `CampaignMayStart` event (A.2); N-2 `CampOverrun` and no game end while the campaign runs (4.3); N-3 the home camp is never lost in the campaign (4.3); N-4 the named modifiers `DemonTuning.SevensPerDraw` and `EconomyTuning.GrowTicks` (A.2); N-5 no ids renamed, the save version owned by this document's interface packages, the campaign's one nullable field is the `Campaign` slot; N-6 the `CampaignRecord` is stored whole in `History` and read by the H to M transfer (4.4).

What this document still needs from it:
1. Landmark ids and positions for the four towers and the four Ash altars (this document reserves `lm.tower.earth`, `.water`, `.air`, `.fire`; the crystals PRD places them and names the altar ids, for example `lm.altar.earth`).
2. The `CampaignRecord` shape, with per element: restored or on an altar at the ending, and the named fallen (id, name, place, cause). H to M reads it (Ward conduits; the Ash Clan's conduit; the memorial).
3. The ending event that raises `ChapterReady{ch.modern.harbour}`, and a fixture with the same shape until it exists.
4. Confirmation that the rival expedition clock (owned by the crystals PRD) advances on `CampOverrun`.
5. Whether restoring a crystal pays the player's faction a point (`crystal:<element>`); this document assumes yes, and that a crystal set on an Ash altar pays the Ash Clan one.
6. Confirmation that the crystal campaign can run from `dev.historic` once H1 lands, instead of its own test start.

**Answered by `PRD_CRYSTALS.md` (reconciliation, 14 Nov 2026).** 1: its Appendix C now lists `lm.tower.*` and `lm.altar.*` with places; the campaign module registers them with `LandmarkApi.Register`. 2: `CampaignRecord` is defined in its A.5 (restored order, `EverOnAltar`, survivors, the fallen with place, tick and cause). 3: `CampaignEnded{Won}` is the event; the Eras code raises `ChapterReady{ch.modern.harbour}`; until the crystal ending exists a fixture raises it. 4: confirmed (+1800; a looted home camp counts too). 5: yes, its 5.2 awards `crystal:<element>` to the player on restoration and to the Ash Clan when a crystal first reaches an altar. 6: yes; after H1 its test starts are built on `dev.historic`.

### Given to `PRD_CHARACTERS.md`
The age switch hook its WP-18 and gate C1 wait for: the game layer reads `AgesApi.Current(state)` at load and listens for `ChapterAdvanced{From, To}` to swap costumes. Building grades (`BuildingUpgraded`) are the hook for village dressing, not for characters.

### From `PRD_CHARACTERS.md`
1. Rigs or placeholders for: the prince's heir (Commander Ilse Teague), Quartermaster Tobin Hale, Engineer Rhea Okonkwo-Dahl, and Mira's later look; Historic costumes for the prince.
2. The `Npc` walking and speech interface the later-age leaders use (assumed unchanged from `PRINCE_API.md`).
3. Whether soldiers and Modern infantry use the shared rig at low detail or a simpler figure; this PRD assumes a simple figure until the characters PRD says otherwise.

---

## Appendix A. Interface sketch

Names and shapes the interface packages land first (H1-0, H2-0, M1-0, F1-0). All in `World.Sim`, plain data, integer state. Additive changes only after landing, logged in A.9.

### A.1 System order (additions)

```csharp
public static class SystemOrder
{
    public const int Eras = 50;          // AdvanceChapter, transfers (H1)
    // Dice 100, Growth 150, Collect 200, Production 300, Construction 400 (incl. upgrades), Jobs 500, Carriers 600 (existing)
    public const int Industry = 650;     // belts, blocks, district power (F1)
    public const int Power = 700;        // existing name: faction power (M1)
    public const int Barracks = 800;     // existing name: recruits, training, roster queues (H2, M1)
    public const int Squads = 900;       // existing name: soldier marches, unit movement and orders (H2, M1)
    public const int Combat = 1000;      // existing name: duels, unit combat, capture (H2, M1)
    // Demons 1100, Brain 1150 (faction AI from H2)
    public const int RtsAi = 1160;       // (M1)
    // Strength 1200
    public const int Campaign = 1250;    // crystal campaign module (PRD_CRYSTALS); H1-0 adds the constant
    public const int Vision = 1270;      // fog (H2)
    public const int Scenario = 1280;    // objectives, waves (M1, F1)
    // Diagnostics 1300
}
```

### A.2 Eras, chapters, history, landmarks (H1-0)

```csharp
public enum Era { Prehistoric = 0, Historic = 1, Modern = 2, Future = 3 }
public enum PlayMode { Skirmish = 0, Campaign = 1, DevStart = 2 }

public static class ChapterIds
{
    public const string Prehistoric = "ch.prehistoric";
    public const string Historic = "ch.historic";
    public const string ModernHarbour = "ch.modern.harbour";
    public const string FutureCore = "ch.future.core";
}

public sealed class ErasState
{
    public Era Current;
    public PlayMode Mode;
    public string Chapter;
    public List<string> ReadyChapters = new List<string>();   // raised by ChapterReady
    public long ChapterStartTick;
    public string StartId;                                     // dev start id, or null
}

public sealed class HistoryState
{
    public List<ChapterRecord> Chapters = new List<ChapterRecord>();   // From, To, Tick, outcome strings
    public List<CasualtyRecord> Casualties = new List<CasualtyRecord>(); // Id, Name, Chapter, Cause, Tick
    public List<string> Notes = new List<string>();                      // "the old guard: 14 soldiers, best level 6"
    public SortedDictionary<string, string> Outcomes = new SortedDictionary<string, string>(StringComparer.Ordinal); // "harbour.route" -> "A"
}

public sealed class Landmark { public string Id; public int Corner = -1, Hex = -1, HexEdge = -1, Plot = -1; public string State; }
// Board.Landmarks: List<Landmark>, sorted by Id.

public sealed class AdvanceChapter : Command { public string To; }
// Events: ChapterReady{Chapter}, ChapterAdvanced{From, To}, PrinceHandover{Faction}, CampaignMayStart{},
//         CampOverrun{Attacker, Defender, SettlementId, Kind (CampLost | Looted)}
// Refusals: chapter_not_ready "That time has not come yet.", chapter_unknown "There is no such chapter."

public static class AgesApi
{
    public static Era Current(WorldState s);
    public static string Chapter(WorldState s);
    public static bool IsReady(WorldState s, string chapter);
    public static Landmark Landmark(WorldState s, string id);
    public static long HandoverTick(WorldState s, int faction); // -1 if not yet
    public static bool CampaignMayStart(WorldState s);           // crystals PRD N-1: Historic era and the player's handover done
    public static bool IsCampaign(WorldState s);                 // Eras.Mode == Campaign
}

public static class Eras { public static void EnterCampaign(WorldState s); }                  // called by the crystals' StartCampaign handler only
public static class LandmarkApi { public static void Register(WorldState s, Landmark lm); }   // campaign module only; keeps Board.Landmarks sorted by Id

// Crystals PRD N-4: the campaign module may write these named modifiers (and nothing else in faction or economy tuning):
// DemonTuning.SevensPerDraw, EconomyTuning.GrowTicks (per terrain), EconomyTuning.CarrierTicksPerStep,
// EconomyTuning.CycleBonusPermille (new, per building type, default 0, added by H1-2). Any further one is added to this list first.

public static class ScenarioStarts
{
    public static WorldSim Create(string startId, ulong seed, IDictionary<string, string> options = null);
    public static IReadOnlyList<string> Ids { get; }   // dev.historic, dev.modern.harbour, dev.modern.skirmish, dev.future.core
}
```

### A.3 Ledger (H1-0 skeleton, H2-3 milestones, M1-2 unlock and costs)

```csharp
public sealed class FactionLedger
{
    public int Faction;
    public int Points;
    public SortedSet<string> Paid = new SortedSet<string>(StringComparer.Ordinal);
    public long Credits;
    public long ReservedCredits;
    public long CreditCap;
    public bool RosterUnlocked;
    public long RosterUnlockedTick = -1;
    public long CreditsEarnedTrade, CreditsEarnedRefinery, CreditsSpent;   // for the balance invariant
}

public static class LedgerApi
{
    public const int RosterThreshold = 10;
    public static FactionLedger Of(WorldState s, int faction);
    public static long AvailableCredits(WorldState s, int faction);
    public static bool IsPaid(WorldState s, int faction, string key);
}
// Internal: Ledger.Award(s, faction, key, points, cap group, ev) -> bool (false if already paid or capped)
// Events: PointsAwarded{Faction, Key, Points, Total}, RosterUnlocked{Faction}, CreditsChanged{Faction, Delta, Reason}
// Refusals: roster_locked, no_credits
```

### A.4 Grades and upgrades (H1-0)

```csharp
public enum Grade { Prehistoric = 0, Historic = 1, Modern = 2 }
// Building gains: Grade Grade; bool Upgrading; int[] UpgradeDelivered; int[] UpgradeIncoming; long UpgradeProgress; long UpgradeCredits;
// Settlement gains: bool IsTown; bool IsDistrict;

public sealed class UpgradeBuilding : Command { public int BuildingId; }
public sealed class CancelUpgrade : Command { public int BuildingId; }
public sealed class UpgradeSettlement : Command { public int Corner; }   // City->Town (H), Town->District (M); existing UpgradeCity stays for Camp->City

public sealed class GradeDef { public Grade To; public int[] Goods; public int Credits; public int Ticks; public string Benefit; public int CycleTicks; public int YieldPerUnit; public int InputBufferCycles; public int Garrison; public int Sight; }
// BuildingDef gains: Grade BornGrade; GradeDef[] Grades; int ToolWare (-1 if none); Era Era;
// Events: BuildingUpgraded{BuildingId, Grade}, UpgradeCancelled{BuildingId}
// Refusals: wrong_era "That needs the {era} age.", max_grade "It cannot be improved further.", upgrade_in_progress (exists)

public static class PlotLayoutOuter
{
    public const int OuterBase = 1350;
    public static int OuterPlotId(int corner, int sector, int slot);   // slot 3 or 4
    public static bool IsOuter(int plot);
}
```

### A.5 Faction autonomy and advice (H1-0)

```csharp
// Faction gains: long HandoverTick = -1; int AnnounceTicks; string Personality; List<AdviceRecord> Advice;
public enum AdviceKind { Priority = 0, Favour = 1, Veto = 2, FocusWare = 3, AttackTarget = 4, HoldTarget = 5, ScoutTarget = 6 }
public enum PriorityClass { Economy = 0, Expansion = 1, Military = 2, Defence = 3, Trade = 4 }

public sealed class Advise : Command
{
    public int Faction; public AdviceKind Kind; public PriorityClass Priority;
    public int ProposalId = -1; public int Ware = -1; public int TargetKind = -1; public int TargetId = -1; public int Cell = -1;
}
public sealed class AdviceRecord { public AdviceKind Kind; public PriorityClass Priority; public int Ware, TargetKind, TargetId, Cell; public long UntilTick; }

public struct Explanation { public string Doing; public string Why; public string Blocked; public int[] TopProposalIds; }
public static partial class FactionApi { public static Explanation Explain(WorldState s, int faction); }
// Events: AdviceTaken{Faction, Kind}, AdviceDeclined{Faction, Kind, Sentence}, ActionAnnounced{Faction, ProposalId, ExecuteTick}
```

### A.6 Military and fog (H2-0)

```csharp
public enum SoldierState { Reserve = 0, Garrison = 1, Marching = 2, Fighting = 3, Healing = 4, Training = 5 }
public sealed class Soldier { public int Id, Faction, Home, Settlement; public int Attack, Health, Defence; public int Hp; public SoldierState State; public bool Fed; public long NextMealTick; public int AttackId = -1; public int Edge = -1; public int Progress; }
public sealed class AttackState { public int Id, Faction, TargetKind, TargetId; public List<int> Soldiers; public long StartTick; public string Result; }
public sealed class MilitaryState { public MilitaryTuning Tuning; public List<Soldier> Soldiers; public List<AttackState> Attacks; public int NextSoldierId, NextAttackId; }

public sealed class OrderAttack : Command { public int TargetKind; public int TargetId; public int MaxSoldiers; }   // TargetKind: 0 settlement, 1 building
public sealed class SetGarrison : Command { public int BuildingId; public int Target; public bool PreferStrong; }
public sealed class SetTraining : Command { public int BuildingId; public int Step; public bool Open; }
// Events: SoldierRecruited, SoldierTrained{SoldierId, Skill, Level}, SoldierFed, SoldierHungry, AttackStarted, DuelRound{A, B, Damage}, SoldierDied, BuildingCaptured, BuildingRazed, SettlementCaptured
// Refusals: no_soldiers, target_not_visible, target_out_of_reach, peace

public enum Seen : byte { Unseen = 0, Remembered = 1, Visible = 2 }
public static class VisionApi
{
    public const int CellMetres = 20;
    public static Seen At(WorldState s, int faction, int cellX, int cellZ);
    public static Seen AtWorld(WorldState s, int faction, double x, double z);
}
```

### A.7 Modern (M1-0)

```csharp
public static class Lattice
{
    public const double Step = 5.0;
    public static double X(int i, int j); public static double Z(int i, int j);
    public static int Cell(int i, int j); public static void Coords(int cell, out int i, out int j);
    public static bool Passable(WorldState s, int cell, MoveClass c);   // Infantry, Vehicle
    public static int[] Path(WorldState s, int fromCell, int toCell, MoveClass c);   // deterministic A*, ties by cell id
}

public enum ArmourClass { Infantry = 0, Vehicle = 1, Structure = 2 }
public enum Stance { Aggressive = 0, Defensive = 1, HoldFire = 2 }
public sealed class Unit { public int Id, Faction, Type, Cell, Hp; public Stance Stance; public int OrderKind, OrderTarget, OrderCell; public int[] Path; public int PathIndex; public long CooldownUntil; public int[] Cargo; public int SquadId = -1; }
public sealed class Structure { public int Id, Faction, Type, Cell, Hp; public int State; public int SupplySettlement; public int[] Delivered, Incoming, Stock; public long BuildProgress; public long ReservedCredits; public bool PoweredOn = true; public List<QueueItem> Queue; public int RallyCell; }
public sealed class QueueItem { public int UnitType; public bool Started; public long Progress; public long CreditsCharged; }
public sealed class RtsState { public RtsTuning Tuning; public List<Unit> Units; public List<Structure> Structures; public int[] PowerSupply, PowerDraw; public int NextUnitId, NextStructureId; }

public sealed class PlaceStructure : Command { public int Type; public int Cell; }
public sealed class CancelStructure : Command { public int StructureId; }
public sealed class QueueUnit : Command { public int StructureId; public int UnitType; }
public sealed class CancelQueueItem : Command { public int StructureId; public int Index; }
public sealed class SetPowered : Command { public int StructureId; public bool On; }
public sealed class SetRally : Command { public int StructureId; public int Cell; }
public sealed class OrderUnits : Command { public int[] Units; public int Kind; public int TargetId = -1; public int Cell = -1; public Stance Stance; }
// Kind: Move, AttackMove, Attack, Guard, Hold, Stop, Capture, Repair, Harvest, Escort
// Events: StructurePlaced, StructureCompleted, StructureDestroyed, StructureCaptured, UnitProduced, UnitDied, ShotFired{From, To, Damage}, PowerChanged{Faction, Supply, Draw}, QueueStalled{StructureId, Reason, Ware}, HarvesterUnloaded{UnitId, Credits}
// Refusals: roster_locked, no_credits, missing_prerequisite "{name} needs a {prereq} first.", cells_taken, out_of_base "Too far from a command post.", no_supply_corner, queue_full, not_visible

public sealed class ScenarioDef { public string Id; public int Version; public List<SiteDef> Sites; public List<ObjectiveDef> Objectives; public List<RaidDef> Raids; public List<CheckpointDef> Checkpoints; public Dictionary<string, int[]> Kits; }
public sealed class ObjectiveState { public string Id; public int State; public long Tick; }   // 0 open, 1 done, 2 failed
// Events: ObjectiveChanged{Id, State}, MissionWon{Mission, Outcome}, MissionLost{Mission, Reason}, RaidAnnounced{Id, Tick, Size}
```

### A.8 Future (F1-0)

```csharp
public enum BlockType { Drill = 1, Belt = 2, Splitter = 3, Junction = 4, Generator = 5, Battery = 6, Smelter = 7, Press = 8, Fabricator = 9, Assembler = 10, Turret = 11, Core = 12, Transfer = 13 }
public enum Dir { E = 0, N = 1, W = 2, S = 3 }
public sealed class TileArea { public int Hex; public int Size = 40; public double TileMetres = 2.5; public byte[] Deposit; public bool[] Void; }
public sealed class Block { public int Id, Type, X, Y, W, H; public Dir Facing; public int Hp; public int[] InBuf, OutBuf; public int Recipe; public long Progress; public bool Broken; public int Priority; }
public sealed class BeltSlot { public int Ware = -1; public int Age; }
public sealed class IndustryState { public TileArea Area; public List<Block> Blocks; public BeltSlot[][] Belts; public int TransferBlockId; public int Supply, Draw, Battery, BatteryCap; public int[] PowerPriority; public int NextBlockId; }

public sealed class PlaceBlock : Command { public int Type; public int X, Y; public Dir Facing; public int Recipe; }
public sealed class RemoveBlock : Command { public int BlockId; }
public sealed class RepairBlock : Command { public int BlockId; }
public sealed class SetPortWare : Command { public int Port; public int Ware; }
public sealed class SetPowerPriority : Command { public int BlockType; }
// PlaceKind gains Transfer = 4 for road-side carriers and shipments.
// Events: ItemMoved (debug only, off by default), BlockStalled{BlockId, Reason}, Brownout{Supply, Draw, Battery}, BeltCut{BlockId, ItemsLost}, AmmoEmpty{BlockId}, DroneAssembled{UnitId}, WaveAnnounced{Index, Tick, Edge, Size}, WaveStarted, WaveCleared, CoreOnline, CoreLost

public struct BlueprintView { public string Title; public int[] Footprint; public int DepositWare; public string Destination; public int[] Inputs; public int OutputPerMinute; }
public static class IndustryApi { public static BlueprintView Blueprint(WorldState s, int proposalId); public static int ItemsAt(WorldState s, int x, int y); }
```

### A.9 API change log

(Interface packages append here: date, package, change, old name kept.)

**14 Nov 2026, H1-0 (slice H1 surface landed on `ages/h1`, tag `ages-H1-0`; pinned by `ApiSurfaceAgesTests`, save by `SaveMigrationTests`).** Nothing of A.1 to A.5 was renamed or dropped except item 1. Marked "H1-0 STUB" in code: the tail of the three grade commands, `EconomySeams.TransferSettlement`, `ErasLogic.ApplyStepContent`, the contents of `dev.historic`, and the H2 advice kinds. Everything else below is real.

1. **Fix: `Eras.EnterCampaign` is `ErasModule.EnterCampaign(WorldState)`.** A static class `Eras` cannot sit beside the namespace `World.Sim.Eras` (the module folder's namespace, as `World.Sim.Economy`), and `Eras.X` from other namespaces resolves to the namespace. Same contract: the crystals' `StartCampaign` handler calls it; it sets `Eras.Mode = Campaign`; `AgesApi.IsCampaign` reads only the mode.
2. Namespaces and files: `World.Sim.Eras` (`Sim/Eras/`: `Era`, `PlayMode`, `ChapterIds`, `ErasState`, `ErasTuning`, `HistoryState` (partial, so the crystals add their whole `CampaignRecord` from their own folder, N-6), `ChapterRecord` (+ `Outcomes`), `CasualtyRecord` (+ `Place`, "the fallen and where"), `Landmark`, `LandmarkIds`, `LandmarkApi`, `AgesApi`, `ScenarioStarts`, `AdvanceChapter`, `ErasRefusals`, events, `ErasModule`); `World.Sim.Ledger` (`Sim/Ledger/`); `World.Sim.Campaign.CampaignState` (shell, `Sim/Campaign/CampaignState.cs`, one field `FormatVersion` = 1, partial; with `WorldState.Campaign`, null = none; handed to crystals I0 at `ages-H1-0`). Grades, commands and events in `World.Sim.Economy` (`Sim/Economy/Grades.cs`); advice in `World.Sim.Factions` (`Sim/Factions/Autonomy.cs`); outer plots in `World.Sim.PlotLayoutOuter` (`Sim/Board/PlotLayoutOuter.cs`). Name clash to know: the crystals' `World.Sim.Campaign.Advise` (answer a choice) and our `World.Sim.Factions.Advise` are different commands; a file using both namespaces qualifies one.
3. Additions to A.2: `AgesApi.Mode`, `ReadyChapters`, `AtLeast(s, era)`, `EraName(era)` ("Historic", for "That needs the {era} age."); `ErasState.Tuning` (`ErasTuning.InviteRepeatTicks` 1800; the smith's chest as `SmithsChestWares` / `SmithsChestCounts` by future ware id), `ErasState.CampaignMayStartTick` (-1 until the event fired); `ErasModule.InitState / EnsureState / Register / EnterCampaign`; `ChapterIds.All`, `IsKnown`, `EraOf`; `ErasRefusals.GameOver`. `LandmarkApi.Get`, `All`, `Register` (A.2 of the reconciliation), `ShoreCorner(t, bearing)` and `TowerCorners(t)` (the Appendix C rule of `PRD_CRYSTALS`, "the rim corner whose bearing from the board centre is nearest" SW / E / S / N, ties by lower id: corners 17, 74, 2, 147 on the default board; shared so the towers and the harbour agree), `HarbourCorner` (corner 16 on the default board); `LandmarkIds.Altar*`. A new game seeds `lm.home_hearth` (70), `lm.ash_hearth` (97), `lm.dance_circle` (plot 635), `lm.harbour`; towers and altars only when the campaign registers them. `ScenarioStarts.Ids` lists only starts that exist now (`dev.historic`); `Create` returns null for the others. Internal for H1-1: `ErasLogic.MarkReady(s, chapter, ev)` (raises `ChapterReady` once), `LandmarkLogic.Set`.
4. Fix: **the smith's chest cannot be delivered in H1** (its wares 17 and 22-28 do not exist until H2-0 grows `Ware`). It is data (`ErasTuning`) and H1-1 delivers the units whose ware id is below `Wares.Count`, i.e. none in H1; H2-1 makes it real. Likewise every **tool in a cost** (`BuildingDef.ToolWare`, `EconomyTuning.TownToolWare`) is a future ware id and is charged only once that ware exists.
5. A.3 additions: `LedgerApi.Points`, `RosterUnlocked`, `CapGroup(key)` (text before the first ':', a `first:*` key is its own group), `CapOf(group)` (6.1 caps; "mission" and unknown groups unlimited), `PaidInGroup`, `BaseCreditCap` 2000 (a new ledger's `CreditCap`); `LedgerRefusals.Sentence(code, n)`; `LedgerModule`. The internal award is `LedgerLogic.Award(s, faction, key, points, ev, cap = -1)` (not `Ledger.Award`: a class may not share the namespace's name); it is real (idempotent, capped, `RosterUnlocked` at 10). `Paid` and the history `Outcomes` keep their ordinal comparer through a load.
6. A.4 additions: `SiteKind.BuildingUpgrade` = 4 and `PlaceKind.BuildingUpgrade` = 4 (carrier legs to a building's upgrade goods; `Logistics` already routes them to `UpgradeDelivered` / `UpgradeIncoming`). **Consequence for A.8: F1-0's `PlaceKind.Transfer` becomes 5.** Events `UpgradeStarted{BuildingId, Grade}`, `TownUpgraded` and `DistrictUpgraded{SettlementId, Corner, Faction}`. Refusals with sentences: `wrong_era` "That needs the {era} age.", `max_grade` "It cannot be improved further.", `unknown_building` "There is no building there.", `not_active` "It must be finished first.", `not_upgrading` "Nothing is being improved there.", `not_a_city` "This camp must become a city first.", `plot_blocked_by_neighbour` "A neighbouring town already uses that ground."; `upgrade_in_progress` keeps its code and takes a subject ("building": "That building is already being improved."; "town": "This city is already being raised to a town."). `GradeDefs` (the 5.6 table for types 0-15 as data, `GradeDefs.ToolOf`, tool id constants); a grade's upgrade `Ticks` are the type's `BuildTicks`, at least 100 (5.6 gave none). `EconomyTuning`: `TownCost` (4 Planks, 4 Bricks, 4 Cut stone, 2 Bread), `TownToolWare` 24, `TownBuildTicks` 600, `CarriersPerTown` 2, `TownShipmentSize` 6, `DistrictCredits` 400, `DistrictBuildTicks` 400, `CarriersPerDistrict` 2, `DistrictShipmentSize` 8. City to Town and Town to District sites use `SiteKind.City` (id = settlement), like Camp to City. `PlotLayoutOuter`: `OuterPlotId`, `IsOuter`, `CornerOf`, `SectorOf`, `SlotOf`, `PlotCount`, `IsValidOuterPlot`, `Centre`, `Yaw` (slot 3 left, slot 4 right; facing the spoke point at 45 m). `EconomyTuning.CycleBonusPermille` is reserved for H1-2.
7. A.5 additions: `Faction.SetbackUntilTick` (-1); `FactionsState.Halts` (`SettlementHalt{SettlementId, UntilTick}`, read through `EconomyHooks.IsSettlementHalted`, so a looted home stops as a demon-held camp does); `FactionTuning`: `AnnounceTicks` 150, `SetbackGraceTicks` 3000, `LootHaltTicks` 1800, `AdvicePriorityBonus` 40 / `AdvicePriorityTicks` 3000, `AdviceFocusBonus` 30 / `AdviceFocusTicks` 3000, `AdviceVetoTicks` 600, `AdviceHoldTicks` 3000, `StrengthPerTown` 3; `Personalities` (passive, balanced, aggressive); `FactionApi` is now `partial`, plus `FactionApi.AdviceInForce`; refusals `not_owner`, `unknown_advice` "I do not understand that advice.", `unknown_ware` "There is no such ware.", `advice_not_yet` "We cannot do that yet." (AttackTarget, HoldTarget, ScoutTarget until H2).
8. Seams for H1-1 and H1-2: `EconomyHooks.OnConstructionDelivery` (state, events, faction, site kind, site id, ware, count), raised on every carrier delivery to a building, road or settlement site or a building upgrade and on every city or town upgrade stock move (the handover detection listens here); `EconomySeams.LootSettlement` (real: half of each unreserved ware, ledger `WareLedger.Looted`, also counted in `Consumed`) and `EconomySeams.TransferSettlement` (H1-0 STUB, returns false; H1-2 implements it). `CampaignRules` (Factions) applies 4.3 when `IsCampaign`: overrun target nearest the attacker's home, capture through `TransferSettlement` else loot the home, halt, setback grace, `CampOverrun`; no `GameEnded` from overrun or time.
9. Save: version 5, `SaveMigrations` (one step per version, `Upgrade(SaveModel)`, `StepVersions`), `SaveModel.LoadedVersion` (not saved). v4 loads as a Prehistoric skirmish with an empty ledger per people, the starting landmarks and no campaign, and plays on identically; v3 refused. `SystemOrder` gained `Eras` 50, `Industry` 650, `RtsAi` 1160, `Campaign` 1250, `Vision` 1270, `Scenario` 1280. `WorldSimFactory` and `SaveCodec.CreateSim` run Eras then Ledger after Demons.


---

## Reconciliation, 14 Nov 2026

Made after `PRD_CRYSTALS.md` and `PRD_CHARACTERS.md` were finished. Minimal edits; no owner decision changed.

| Where | Change | Why |
|---|---|---|
| AG-53, section 12 | Phone windows are 844 x 390 and 932 x 430. | The other two PRDs and the owner's phone-window answer use those; this PRD had two other sizes. |
| 4.1, 9.2 | Harbour skips the four tower corners; the district never takes a hex with a campaign site; tower and altar landmarks are registered by the campaign module. | Rim corners and Meadow hexes were claimed by both PRDs. |
| 4.3 | H2 captures also raise `CampOverrun`; `StartCampaign` switches campaign mode through `Eras.EnterCampaign`. | The crystal hooks listen to `CampOverrun` only; "campaign" meant two different flags. |
| 4.4 | The Eras code reacts to `CampaignEnded{Won}`; the Ash conduit uses `CampaignRecord.EverOnAltar`; the crystal ending offers the step as "Onward". | At the Won ending every crystal is restored, so the old rule could never fire; both PRDs had their own ending bubble. |
| 4.6, 9.1 | Save slots named (`auto.chapter`, `ckpt.harbour.<n>`); names are owned by the crystal I8 `SaveSystem`. | The crystals PRD says one slot; this PRD wanted several. |
| A.1, A.2 | `SystemOrder.Campaign` 1250; `Eras.EnterCampaign`; `LandmarkApi.Register`; the N-4 list adds `CarrierTicksPerStep` and `CycleBonusPermille`. | The crystals PRD uses all of them; two boons had no allowed field. |
| 11 rules, H1-0 | H1-0 owns `Sim/Save/**`, the version, `SystemOrder`, and the `CampaignState` shell, handed to crystals I0 at `ages-H1-0`. Branch and tag naming added. | Both PRDs edited the save codec; branches were unnamed. |
| H1-2 | Adds `EconomyTuning.CycleBonusPermille`. | Fire boon. |
| H1-4, H2-6, H2-7 | `Game/Prince/**` carve-outs (WP-8's binding, `Advice/` for H2-7); map content through `IMapLayer`; map taps moved from H1-4 to H2-7; PlayMode test renamed `HandoverPlayTests`. | Overlapping folder claims; `MapScreen.cs` is the crystals I5 file. |
| M1-0 | `Sim/Board/Lattice.cs` is created by crystals I0 and extended here. | Both PRDs created it. |
| C1-2, C1-3, section 10 | AutoPlay renamed `chain_short`; start-here note refreshed in `NEXT_START_HERE.md`. | `campaign_short` looked like the crystals scripts; two start-here pages. |
| Registration files | `Sim/Core/WorldSimFactory.cs`, `Boot/WorldBootstrap.cs` and `Game/UI/Hud.cs` take one-line additive registrations from any package; a merge conflict there is resolved by keeping both lines. Nobody rewrites them. | Ages packages need installer and module lines and no PRD listed them. |
| Section 16 | Answers from the crystals PRD recorded. | Closes the loop. |
