# PRD and MVP: the crystal campaign (Historic age)

Written 14 Nov 2026 on branch `docs/next-ages`, cut from the prehistoric refactor. It builds on the game described in `Docs/REFACTOR_SPEC.md` and follows the owner's answers of 14 Nov 2026. Two sister documents are written at the same time: `Docs/PRD_AGES.md` (Historic economy, later ages, faction and RTS AI) and `Docs/PRD_CHARACTERS.md` (rigged cast and animation). Where this document needs something from them it says so under "Needs from other PRDs" (section 3.9).

When this was written `PRD_CHARACTERS.md` was already in the folder, and its cast numbering, duel staging and phone window sizes are followed here. `PRD_AGES.md` was not yet there, so everything about the ages is an assumption stated in 3.9. That is now settled: `PRD_AGES.md` exists and its section 16 answers N-1 to N-6 (see "Answers" at the end of 3.9 and the changelog at the end of this file).

Reading guide. The owner's read is about 20 minutes: section 1 (summary), section 4 (story and cast), sections 5.1 to 5.6 (the clock, custody, the rival, Mira, consequences), section 7 (what you will see in each slice), then sections 11 and 12 (decisions). The rest is for the builders: section 2 is what exists, 3 the numbered requirements, 5.7 to 5.12 the remaining rules, 6 the data format, generator and validator, 8 the work packages, 9 the tests and the Slice 1 play script, 10 the risks. Appendix A is the simulation interface, B the Unity-side interface, C the authored places, D the route steps.

This is an original game. Every name, place, riddle and line in this document is ours. Nothing is taken from any other game.

---

## 1. Summary

The island has a second story running beside the faction game. Under the island sleeps a power that undoes whatever is made. Four elemental crystals hold it down. Long ago they were carried out of their four shore towers and hidden. Now the Ash Clan's expedition is hunting them for its own altars, and Mira sets out with six companions to bring them home first.

Mira leads. The player is still a floating viewpoint. The player can follow her, leave her, advise her on riddles, and take over her casting when a Ward Break duel starts. The prince runs the camps on his own by then, and the player can go back to him at any time. Both stories share one world, one clock and one save.

Each crystal has a route: a clue, a hidden original site, an Ash altar it may be carried to, and a matching tower. If Mira reaches a crystal first she simply takes it. If the Ash Clan gets there first, the crystal goes to an altar guarded by a keeper, and Mira must win a lethal Ward Break to recover it. A lost lethal duel costs one named companion, who stays behind so the others can get out. Six companions can die. A lethal loss with none left ends the run. When three crystals are restored and Mira reaches the last tower with the fourth, the final enemy stands in the way.

The campaign is data. One versioned definition holds the roles, places, crystals, rival plan, clues, encounters and endings. The authored campaign and generated campaigns use the same format and the same runtime. A seeded offline generator makes new versions that differ in what happens, and a validator refuses any definition that cannot be finished.

Build order, as the owner set it: Slice 1 is Earth end to end. Slice 2 is the other three routes, the race, the finale and the endings. Slice 3 is the generator, the validator and two generated examples.

---

## 2. What exists now that this builds on

Checked in the code and docs of this branch (`wt-docs`, head `32d11ef`) on 14 Nov 2026.

| Thing | State today | Where |
|---|---|---|
| Simulation core | Pure C#, integer state, fixed 10 ticks per second. Commands are validated at once and applied at the next tick. Events are drained after each tick. Modules register systems with an order number. | `Assets/World/Sim/Core` (`WorldSim`, `Command`, `ICommandHandler`, `SimEvent`, `SystemOrder`) |
| World state | One `partial class WorldState`. Each module adds its own fields in its own folder. | `Sim/Core/WorldState.cs`, `Sim/Economy/WorldState.Economy.cs`, `Sim/Encounters/Ward/WorldState.Ward.cs` |
| The island | Fixed: radius 4, 61 hexes, 150 corners, 210 hex edges, 60 m edges. Terrain is the same on every seed; only the dice numbers are dealt by seed. 23 hexes are Meadow, mostly on the rim, with no number and no stock. Only corners where three hexes meet may hold a camp. | `Sim/Board/BoardGenerator.cs`, `PlotLayout.cs` |
| Faction game | Dice, growing stock, camps with plots, main roads, carriers, the prince's brain with a queue of three proposals, spells, the Ash Clan with the same brain, strength and overrun, demons on a roll of 7. The rival's automatic casting is keyed on `Faction.Auto`. Tuning is saved in the state. | `Sim/Economy`, `Sim/Factions`, `Sim/Demons`, `Docs/ECONOMY_API.md`, `Docs/ECONOMY_PROOF.md` |
| Headless proof | `dotnet test DotNet` compiles `Assets/World/Sim` and `Assets/World/Tests/EditMode` outside Unity. The test `ProofReport` writes `Docs/ECONOMY_PROOF.md`. | `DotNet/`, `Tests/EditMode/ProofReport.cs` |
| Walking | A 5 m triangular lattice, 60 degree turns, glided steps. `WalkGrid` blocks cells beyond the board, at camps, at buildings and inside registered static blockers. | `Game/Player/WalkLattice.cs`, `WalkGrid.cs`, `PlayerWalker.cs`, `Docs/PLAYER_API.md` |
| Pointer and bubbles | Free pointer. Speech bubbles with up to three replies (keys 1 to 3 or a tap). An off-screen question shows "<name> calls you" with an arrow. Bubbles are hidden during encounters unless the encounter sets `SpeechBubbles.ShowInEncounter`. | `Game/Player/PointerInput.cs`, `Game/UI/SpeechBubbles.cs` |
| Map, HUD, tools | Tab map of the island (read only, tap sets an auto-walk target). H shows a HUD strip, off at the start. F1 opens developer tools. No on-screen buttons in normal play. Touch strips fill the bottom 30% of the screen and read real touches only. | `Game/UI/MapScreen.cs`, `Hud.cs`, `Game/UI/God` |
| Ward duel | `WardDuelSession` is a self-contained, deterministic, real-time session in integer milliseconds: three loci, ten casts a side, hidden Wards, totals of fractures, echoes and fades only. Outcomes: Victory, Defeat, Stalemate, Clash, Forfeited. The opponent is `WardRival` (a candidate filter). Opponent timing lives in `WardTuning` fields named `Mira*`. | `Sim/Encounters/Ward`, `Docs/WARD_API.md` |
| Duel in the game | `WardDuelEncounter` runs under `EncounterHost`, which pauses the world clock, locks the walker and restores its pose after. The stage spawns an anonymous player wizard facing Mira, 5.5 m apart, camera over the player wizard's shoulder. Mira is the opponent. The result is written by `WardProgressApi.RecordDuel`, the one sanctioned direct write. The duel UI is an IMGUI panel with keys and buttons. `EncounterDirectory.TryStart(id, npc)` takes no parameters for the encounter. | `Game/Encounters`, `Docs/ENCOUNTERS_UI.md` |
| Save | One slot (F5, F9, pause menu). `SaveCodec` writes the whole `WorldState` plus a pose and string extras, version 4. Saving is refused during an encounter and while a command is queued. Loading reloads the scene from the state. | `Sim/Save/SaveCodec.cs`, `Game/Boot/SaveSystem.cs` |
| The prince | On this branch `Game/Prince` is an empty stub. The real package is on branch `pre/n`, not merged: a director that walks up, asks, leads with the player following, waits at the spot, calls and fetches a straggler, and escalates when ignored. | `wt-n/Docs/PRINCE_API.md` |
| Actors | `ActorFactory.Create(id, colour, position, yaw, parent)` returns a placeholder body. `Npc` is a speaker, followable, walks, and can open a dialogue with options that start an encounter. Mira stands at the home camp with a "Practice duel" option. | `Game/Actors`, `Game/View/Camp/CampInstaller.cs` |

Not there today: any campaign state, any interior map, any companion, any opponent other than Mira, any way to pass a setup to an encounter, any automatic save.

This PRD assumes the refactor is finished first (the integration list and package F-1 in `Docs/RESUME.md`, with `pre/n` and `pre/l` merged), as the owner's priority order says.

---

## 3. Requirements

"Sim" means `World.Sim`. "Game" means `World.Game`. Numbers are starting values and are tuning fields.

### 3.1 One world

| Id | Requirement |
|---|---|
| CR-1 | The campaign is a Sim module. Its state is `WorldState.Campaign`. It runs on the world tick clock and is saved in the same save as the faction game. |
| CR-2 | The campaign opens when the prince's handover milestone has been reached and the island is in the Historic age (section 3.9, N-1). A standalone test start opens it at once, so it can be built before the ages work lands. |
| CR-3 | The faction game keeps running. The player can leave Mira for the prince and come back at any time. Time spent anywhere is time the Ash expedition also uses. |

### 3.2 Story and cast

| Id | Requirement |
|---|---|
| CR-4 | Lore, names, places, riddles and lines are original (section 4). No name, text, map or puzzle from any existing game. |
| CR-5 | Six named companions with visible bodies, a small role each, an authored volunteer order, and an optional service that is lost when they die. |
| CR-6 | Mira is counted apart from the six. She leads, casts and restores. The prince leads the faction. |

### 3.3 Campaign structure

| Id | Requirement |
|---|---|
| CR-7 | Four crystals (Earth, Fire, Water, Air). Each is always in exactly one custody state: at its original site, in rival transit, at a rival altar, carried by the party, or restored. |
| CR-8 | Four authored routes. Each has a region, its own clue or puzzle, an original site to discover, a rival altar in an interior, and a matching tower. Earth starts underground. |
| CR-9 | The player can pursue open leads in any order. |
| CR-10 | Every essential clue has a fallback that needs no companion: a core character who cannot die, or something in the world. Companion shortcuts may vanish with their companion. |
| CR-11 | The Ash expedition advances on a documented clock (section 5.1). The table of actions that advance it is shown in the play guide. |
| CR-12 | The player learns of rival moves through reports and world changes. When the last report on a crystal is stale, the journal and Mira say where to look. |
| CR-13 | The rival can reach an original site first and carry the crystal to its altar. A stolen crystal is always recoverable. |
| CR-14 | Restoring a crystal changes its tower and the island, updates what people say, and is recorded in the journal. |
| CR-15 | When three crystals are restored and the party reaches the fourth crystal's tower carrying it, the final confrontation happens there. This works for all 24 restoration orders. |
| CR-16 | Two endings: the restored island (with lines for who lived and who fell) and terminal defeat. Either offers a new campaign. |

### 3.4 Mira

| Id | Requirement |
|---|---|
| CR-17 | Mira keeps a queue of up to three leads and proposes the best one with its reason, as the prince proposes buildings. The player answers in a speech bubble. |
| CR-18 | The player can follow her, leave her, ask what she knows, and advise her at a choice. Her next action never waits on a dialogue for ever. |
| CR-19 | While the player is away Mira scouts, follows clues and answers riddles from what the party knows. She never starts a lethal duel alone: she waits, then withdraws. |
| CR-20 | The map and the journal always say where Mira is, what she found and how to rejoin her. |
| CR-21 | An answer given through the player's advice and an answer Mira chooses herself go through the same Sim action. |
| CR-22 | Mira can send a companion to scout. The scout and what it found show on the map. The party regroups before a lethal duel. |

### 3.5 Ward Break

| Id | Requirement |
|---|---|
| CR-23 | In a campaign duel the player casts as Mira: sets her hidden Ward, builds weaves, triggers casts. Mira and her opponent are both in frame. It is clear whose spell is being shaped. |
| CR-24 | Opponents and encounter tuning are data. The duel rules of `WardDuelSession` do not change. The practice duel still works as it does now. |
| CR-25 | Practice against Mira stays optional and non-lethal. A friendly demonstration with the watchtower wizard is optional and non-lethal. |
| CR-26 | History, sound and effects never reveal which locus matched. |

### 3.6 Consequences

| Id | Requirement |
|---|---|
| CR-27 | The outcome table of section 5.6 is applied by the Sim, never by the Game. |
| CR-28 | Before a lethal duel the game names the companion who will cover the retreat. Walking away before committing is safe. |
| CR-29 | An attempt has an id. Its outcome, the casualty, the retreat and the rival's gain are committed in one step and written to the campaign checkpoint. A second callback, a scene change or a reload cannot apply it twice. |
| CR-30 | With zero companions alive the campaign can still be won. The next lethal defeat ends the run. |
| CR-31 | In the campaign an Ash Clan overrun costs a camp, forces the expedition to retreat and advances the rival. It is not fatal. In standalone skirmish it stays fatal. |

### 3.7 Interiors

| Id | Requirement |
|---|---|
| CR-32 | Interiors are small connected maps on the same step lattice, with the same four moves. They are data: cells, doors, stairs, markers, pedestals, altars. |
| CR-33 | Interiors connect to named places on the island. An opened route stays open across saves. |
| CR-34 | The Tab map shows the interior you are in, with the way out marked. |
| CR-35 | There is always a clear way back to the surface: lit exits, the map, and "Lead me out" from any party member. |

### 3.8 Data, proof, interface

| Id | Requirement |
|---|---|
| CR-36 | One versioned campaign definition with stable ids for roles, locations, connections, crystals, towers, rival plan, clues, leads, choices, encounters, quest events, endings and lines. Authored and generated campaigns use the same format and runtime. |
| CR-37 | Run state is separate from the definition. The save holds the resolved definition, its hash, the seed, the generator version, progress, custody and the roster. A later generator cannot change a run in progress. |
| CR-38 | An offline seeded generator. The same seed, template and generator version give a byte-identical definition. Different seeds differ in what happens, not only in names (section 6.4). |
| CR-39 | A validator runs before any campaign can start and rejects bad content with a reason. It is exercised with intentionally broken fixtures (section 6.5). |
| CR-40 | No line can report an impossible crystal location, and no dead character speaks. Lines are bound to state through tokens and guards. |
| CR-41 | Sim first: pure C#, deterministic, commands in and events out, EditMode tests under `dotnet test DotNet`. The Game reads state, sends commands and animates events. It never writes state. |
| CR-42 | A headless campaign proof, the campaign's equal of the economy proof (section 9.1). |
| CR-43 | Existing tests stay green. The economy, the dance and the practice duel behave as before when no campaign is running. |
| CR-44 | No on-screen buttons while exploring. All conversation is in speech bubbles with at most three replies. The duel keeps its own large touch panel. |
| CR-45 | Designed for touch on a landscape phone. Tested in phone-sized Windows windows of 844 x 390 and 932 x 430 (the sizes `PRD_CHARACTERS.md` uses). Nothing the player must tap sits only inside the bottom 30% touch band. |
| CR-46 | The repository is public. Nothing from the owner's local reference folders is committed. |

### 3.9 Needs from other PRDs

From `PRD_AGES.md`:

| Id | Need | What this PRD does until it exists |
|---|---|---|
| N-1 | A signal that the campaign may start: the prince's handover has happened (assumed rule: the first supply chain has delivered, after which he builds on his own) and the island is in the Historic age. Wanted shape: a Sim read helper `bool AgesApi.CampaignMayStart(WorldState)` and an event when it turns true. | The standalone test start (section 7, Slice 1) starts the campaign at tick 0 on a state where the player's `Faction.Auto` is set true, so the prince's people build by themselves (the automatic casting in `Sim/Factions/Brain.cs` is keyed on that flag; I0 confirms it with a test). |
| N-2 | Campaign-mode faction rules: an overrun of your people takes one camp and does not end the game, and there is no time-limit ending while a campaign runs. Wanted shape: an event `CampOverrun { Attacker, Defender, SettlementId }` and no `GameEnded` from overrun or time while `CampaignApi.IsRunning`. | The test start sets `FactionTuning.OverrunMargin` and `TimeLimitTicks` very high in the state, so neither can fire. The overrun link (CR-31) lands in Slice 2. |
| N-3 | The home camp is never lost in campaign mode, so the expedition always has a base. | If a state ever has no camp, the party's base is the Stonemark Glade. |
| N-4 | Named modifiers the campaign may set when a crystal is restored (section 5.7), or permission to write the listed tuning fields from the campaign module. | Slice 1 writes only `DemonTuning.SevensPerDraw` and one growth figure. |
| N-5 | The age switch does not rename `PlayerIds`, the Ash Clan, corners, hexes or plots. The save version is bumped by one owner; the campaign adds only one nullable field. | - |
| N-6 | What happens to the island after the restored ending (carry-over to the next age). This PRD hands over a `CampaignRecord` (order of restoration, survivors, the fallen and where, seed, definition id). | - |

From `PRD_CHARACTERS.md`:

| Id | Need | What this PRD does until it exists |
|---|---|---|
| N-7 | Bodies for these actor ids through `ActorFactory.Create`: `mira`, `companion_garrick`, `companion_ivo`, `companion_petra`, `companion_nell`, `companion_odo`, `companion_tansy`, `keeper_earth`, `keeper_fire`, `keeper_water`, `keeper_air`, `rival_leader`, `wizard`, `ferryman`, `smith`. | Placeholder bodies in distinct colours, as every NPC has today. |
| N-8 | The rival leader uses the Ash champion's body (their cast no. 14) so the first proven opponent and the campaign's leader are one face. | - |
| N-9 | One definition of `DuelStageSpec` (caster actor id, opponent actor id, positions), used by their `DuelDirector` and by this PRD's `WardDuelRequest`. Package I0 creates `Game/Encounters/DuelStageSpec.cs` (it lands first); `PRD_CHARACTERS` WP-9 uses it and changes it only through I0's change log. | I0 creates it. |
| N-10 | One owner per file in `Game/Encounters`, handed over by tag. This PRD's I7 owns `WardDuelEncounter.cs`, `WardDuelStage.cs` and `EncounterInstaller.cs` until tag `cry-I7`; from that tag `PRD_CHARACTERS` WP-9 owns those three files, plus `DuelDirector.cs` (WP-9 only, always) and the result gate. I0 owns `EncounterRequest.cs`, `DuelStageSpec.cs` and the `Request` field in `EncounterHost.cs`. WP-9 starts only after `cry-I7`. Their test id `ward_champion` is a preset request of the campaign duel (mode Friendly), not a second encounter class. | I7 first; WP-9 after `cry-I7`. |
| N-11 | Clips for: Mira's cast, an opponent's hit and break, a companion covering the retreat and falling, walking in a file, pointing, kneeling at a pedestal. Driven by the events of appendix A.4. | The placeholder's `cast`, `cheer`, `surprise` one-shots. |

**Answers from `PRD_AGES.md` section 16 (reconciliation, 14 Nov 2026).** N-1: `AgesApi.CampaignMayStart(WorldState)` and the event `CampaignMayStart`; true from the Historic step on. N-2: event `CampOverrun { Attacker, Defender, SettlementId, Kind }` with Kind `CampLost` or `Looted`; no `GameEnded` from overrun or time while the island is in campaign mode, which `StartCampaign` switches on through `ErasModule.EnterCampaign(WorldState)` (named `Eras.EnterCampaign` before `PRD_AGES` A.9). From H2 on, real captures also raise `CampOverrun`, so this PRD's hooks do not change. N-3: the home camp is never captured in campaign mode; with only the home camp left it is looted, not lost. N-4: the writable fields are listed under 5.7. N-5: the world save version belongs to `PRD_AGES` H1-0 and its successors; this PRD's own state changes inside `CampaignState` are versioned by `CampaignState.FormatVersion`, not by the world save version. N-6: `CampaignRecord` (appendix A.5) is copied whole into `History` by the ages' transition code when `CampaignEnded { Won }` is raised. When `dev.historic` exists (after H1), `-campaignTest earth` and `earth_stolen` are built on it and the fallbacks in the right-hand column above are retired.

---

## 4. Story outline and cast

### 4.1 Names

| Thing | Our name | Note |
|---|---|---|
| The island | **Oskerry** | Both peoples just say "the island". |
| The sealed power | **the Unweaver** | The peoples' title for it is "the Lord of Chaos" (the owner's working title, kept as an in-world title). The demons of the dice are what leaks from it. |
| Earth crystal, tower | **the Deepstone**, **the Rooted Tower** | South-west shore. |
| Fire crystal, tower | **the Kilnheart**, **the Beacon Tower** | East shore. |
| Water crystal, tower | **the Tideglass**, **the Tide Tower** | South shore. |
| Air crystal, tower | **the Skyshard**, **the Whistling Tower** | North shore. |
| The wizard and his watchtower | **Orrin the Watcher**, **Gannet Watch** | On the seaward shoulder of the mountain ridge. Not one of the four towers. |
| The Ash expedition's leader | **Hesk Varr**, the Ember-Sworn | The Ash Clan's champion. Working name; the owner names him. |
| The four altar keepers | **Dolm** the Tally-Keeper (Earth), **Brask** the Forge-Warden (Fire), **Nual** of the Cistern (Water), **Pike** the Signal-Warden (Air) | Ward-bearers of the Ash Clan. |
| Witnesses who cannot die | the sawyer (already in the game), **Hod** the smith, **Old Marl** the ferryman, Orrin | They hold the essential spoken clues. |

### 4.2 The six companions

Volunteer order is authored: on a lethal defeat the first living name in this order covers the retreat.

| Order | Name | Role | One line | Service while alive |
|---|---|---|---|---|
| 1 | **Garrick Holt** | Shield-bearer, an old road guard | "Somebody has to be last out of a bad place. I've had the practice." | A retreat after a clash or stalemate costs half the rival time. |
| 2 | **Ivo Quill** | Lamp-bearer and map-keeper | "I draw the way in so I can find the way out. Mostly for me." | Interior maps show undiscovered markers as "?" and show barred shortcuts. |
| 3 | **Petra Coalhand** | The smith's daughter; reads fire | "Fire tells you what it wants. People are harder." | Knows the kiln's lighting order outright (Fire shortcut). |
| 4 | **Nell Farrow** | Ferry hand; reads water | "The tide keeps its promises. It's the only thing that does." | Reads the flood marker at a glance (Water shortcut) and can work the cistern sluice (an alternate recovery without a duel). |
| 5 | **Odo Birch** | Forester; reads marks on trees and stone | "Every cut on a tree is somebody saying something. I just listen." | Reads the forest marks on sight (Earth shortcut). |
| 6 | **Tansy Reed** | Scout and runner | "I'll be back before you've finished worrying." | Scouting assignments (section 5.5). |

Garrick insists on going first. Tansy is last because Mira always sends her running with word, until there is nobody else to send.

### 4.3 The story in one page

**Before.** The first weavers of the island found something under it that undoes whatever is made: knots slip, walls slump, fires forget to burn. They could not kill it. They tied it down with four knots, each made from a part of the island: stone, fire, tide and wind. The knots are the four crystals, and four towers on the shore were built to hold them. While all four sit in their towers the Unweaver sleeps.

In the lean winters that followed, the two peoples fought over the towers. The last tower-keepers carried the crystals out and hid them, so that no one people could hold the island to ransom: one under the old quarry, one in a kiln, one below the tide line, one on the ridge. Hidden, the crystals still bind, but loosely. What leaks out is what the camps call demons.

**Why now.** The valley people have grown, and the Ash Clan is losing ground. Its champion, Hesk Varr, has begun to dream of the hiding places. He believes that crystals set on altars cut by Ash hands will make the old power serve the Ash Clan. The dreams are sent by the Unweaver. An altar is not a tower: a crystal on an altar is a knot half untied.

**Opening.** Once the prince runs the camp on his own and you have accepted the Historic age (`CampaignMayStart`), Mira comes to the viewpoint at the home camp. She tells the story above in four short bubbles, shows you the six who have agreed to come, and says what it will cost: "If I lose a duel out there, one of them stays behind so the rest of us get out. I won't pretend otherwise." She offers a practice duel, then asks: "Come with me?" Replies: "Lead the way", "Tell me more", "Go on ahead, I'll find you".

**The four routes.** In any order. Earth: forest marks lead to a boundary stone in a glade, a hidden stair, and the old quarry below. Fire: a ruined kiln must be lit in the right order before it shows what it holds. Water: a ferryman's riddle and a flood marker tell when the low causeway can be crossed. Air: the old watcher on the ridge tests Mira before he teaches her to read the wind markers. Wherever the Ash expedition arrived first there is an empty pedestal, a trail, and an altar with a keeper.

**The race.** Reports come in: Ash runners on the south road, smoke at the quarry, a light carried down the cliff. Each crystal Mira sets in its tower lights that tower, thins the demons and changes the land a little. Each crystal on an Ash altar makes the demons come more often. Hesk Varr's reported words grow stranger as the seals close.

**The last tower.** With three towers lit, Mira carries the last crystal to its tower. Hesk Varr is waiting on the steps, alone; his expedition has fled him. The Unweaver, with one knot left loose, speaks through him. This is the final Ward Break.

**Ending one: the Fourfold Seal.** Mira sets the fourth crystal. Four beams meet over the island. The demons are gone and Hesk Varr wakes as himself. At the home fire the survivors gather, and Mira names the fallen in the order they fell and where. If all six lived: "No cairn was needed." If none lived: Mira lights six fires alone.

**Ending two: the Unweaving.** A lethal defeat with no companion left. Mira is thrown from the steps. The lit towers go out one by one and the shore begins to fray. The prince has the last line, watching from the camp. Then: "Begin again".

---

## 5. Rules and state machines

All numbers are fields of `CampaignTuning` (saved with the definition). 10 ticks = 1 second, 600 ticks = 1 minute.

### 5.1 The expedition clock

The Ash expedition has one counter, `RivalProgress`. It rises by 1 every world tick while the campaign runs (the world clock is paused during a duel, the pause menu and nothing else). It also jumps on these actions. This table is the whole list.

| Action | Rival progress |
|---|---|
| World time passing: travel, waiting, talking, time spent with the prince | +1 per tick |
| Lethal defeat and retreat (also a confirmed surrender) | +900 |
| Clash or stalemate and retreat | +600 (+300 while Garrick lives) |
| Wrong answer to a riddle or a wrong puzzle step | +300 |
| One of your camps is overrun by the Ash Clan, or your home camp is looted (`CampOverrun`, either Kind) | +1800 |
| One of the Ash Clan's camps is overrun by your people | the expedition stops for 900 ticks |
| Victory, practice, a friendly demonstration, withdrawing before committing | 0 |
| Time inside a duel | 0 |

### 5.2 Crystal custody

```
AtSite --party takes it--------------------------> Carried --restored at its tower--> Restored
AtSite --rival seizes it--> RivalTransit --> AtRivalAltar --keeper beaten or alternate route--> Carried
```

| Rule | Value |
|---|---|
| Party takes a crystal | The party stands at the pedestal marker and the crystal is `AtSite`. Takes `TakeTicks` = 50. If the rival arrives in the same tick, the party wins the tie. |
| Rival seizes | Its plan step for that crystal completes while the crystal is `AtSite`. |
| Rival transit | `CarryTicks` of rival progress (900 in the authored plan), then `AtRivalAltar`. |
| Recovery | A Victory in that altar's keeper encounter, or the altar's authored alternate route if it has one. |
| Carried | A carried crystal is never lost, even in a retreat. The party may carry several. |
| Restored | Final. The rival cannot touch a carried or restored crystal. |
| Wrong tower | Refused: "This is not its tower." The party learns which tower matches from the pedestal inscription, from the beaten keeper's altar, or by visiting the tower and reading its door sign. |

Every custody change is one Sim step with one event, guarded by the state it starts from, so it cannot happen twice.

Victory points (`PRD_AGES` 6.1): when a crystal becomes `Restored` the player's faction is awarded the key `crystal:<element>`; when it first becomes `AtRivalAltar` the Ash Clan is awarded the same key. Each key is paid once, through the ledger's `LedgerLogic.Award(s, faction, key, 1, ev)` (`PRD_AGES` A.9 item 5), in the same step as the custody change.

### 5.3 The rival expedition

The rival is one party with an authored plan: an ordered list of steps, each `{ crystal, march cost, carry cost, altar }`. It is driven only by `RivalProgress`.

| Phase | Rule |
|---|---|
| Marching | Spends the step's march cost. At the start of the step a report is raised after `ReportDelay` = 300: "Ash runners seen heading for <region>." |
| Arrives, crystal `AtSite` | Seizes it. The site changes (empty pedestal, the trail clue appears). A report follows after `ReportDelay`. |
| Arrives, crystal gone | Spends `SearchCost` = 300, then starts the next step. |
| Carrying | Spends the carry cost, sets the crystal on the altar, then starts the next step. |
| Plan finished | Waits. When three crystals are restored, Hesk Varr goes to the last tower. |

Authored plan (starting values): Earth march 4800 (8 minutes from the campaign's start), then Fire, Water and Air at march 3600 each; carry 900 each. A party that goes straight for Earth arrives in about 5 minutes and finds it on its pedestal. A party that does another route first, loses duels, or keeps turning the Earth lead down finds it stolen. Later crystals are meant to be close races, because the party cannot be in two places at once. The proof (section 9.1) reports how many first arrivals each scripted player gets; the target is at least 2 for the direct player and 0 for the reluctant one.

What the player may know of the rival is only what reports and scouts have said (`CampaignApi.RivalView`). The rival's true position and progress are in the state and are shown only by the F1 tools.

### 5.4 Facts, clues, leads and choices

- A **fact** is a named thing the party knows (`earth.glade_known`, `earth.stair_open`, `earth.stolen_seen`). Facts only ever get added.
- A **clue** gives facts. It has a source: a witness, a marker in the world, a companion, a scout, a report or the wizard. An essential clue names its fallback clue, and the fallback's source is never a companion.
- A **lead** is somewhere to go and something to do there, with a one-sentence reason. It is open when the facts it requires are known and its goal is not done.
- A **choice** is a question with up to three answers, one correct, and the name of the fact or marker that reveals the answer.
- A **belief** is what the party thinks about each crystal: custody, place and the tick of the last report. A belief older than `StaleTicks` = 3000 is stale. For a stale belief the journal and Mira give the "where to look" sentence: the last known place, then the scout if Tansy lives, then the wizard.

Mira's brain plans every 20 ticks: it lists open leads, scores them (`Priority`, +30 if the rival is reported marching on that crystal, +20 if the party carries that crystal and the lead is its tower, minus 1 per 20 steps of travel), keeps the best three and keeps ids stable while a lead stays in the queue. This is the prince's proposal queue, reused in shape.

### 5.5 Mira as leader

The Game side is an `ExpeditionDirector` modelled on the prince's director (`pre/n`): approach, ask, lead with the player following, act at the spot, call once if the player lags. The differences: Mira never needs the player's spell, she does not pester, and she goes on alone when not answered.

**Party modes (Sim).**

| Mode | What happens | Leaves when |
|---|---|---|
| Idle | At a place. The top lead is offered. | A lead is accepted, or patience runs out. |
| Travelling | A leg from place to place. Surface legs take lattice step distance x `PartyTicksPerStep` = 7 (about 7 m/s). Interior legs take the shortest path over open cells. | Arrival. |
| Acting | Reading, searching, lighting, taking: `ReadTicks` 50, `SearchTicks` 150, `TakeTicks` 50. | The quest event fires. |
| AskingChoice | A riddle or puzzle step is open. | Answered by the player's advice, or by Mira. |
| AtThreshold | Standing before a lethal encounter. The volunteer is named. | Committed, withdrawn, or the wait runs out. |
| InEncounter | An attempt is pending. | Resolved. |
| WaitingForPlayer | At a tower with its crystal, or withdrawn from a threshold. | The player rejoins, or another open lead exists. |

**How fast Mira looks.** A surface leg moves at about 7 m/s (5 m in 7 ticks). The Game animates her at the leg's speed with the `Run` clip of `PRD_CHARACTERS` (native 7 m/s). The player's own steps are faster (0.4 s per 5 m), so following works. Placeholder bodies simply slide, as they do now.

**What the player says.** All in speech bubbles, at most three replies.

| Moment | Mira says | Replies |
|---|---|---|
| A lead is offered | The lead's reason | "Lead the way" (accept; the player auto-follows her), "Why there?" (the clue behind it, then the other two), "Not that way" (decline: next lead; the same lead waits 600 ticks) |
| Any time, by tapping Mira | "What is it?" | "What do we know?" (journal, crystal by crystal), "Go on without me" (leave), "Practice duel" (when not at a threshold). When rejoining: "I'm with you". |
| A choice | The question | Up to three answers |
| A threshold | "<Keeper> has seen us. If this goes badly, <Volunteer> holds the way out. Do we fight?" | "Fight" (commit), "Not yet" (withdraw, safe), "What happens if we lose?" |
| A tower, carrying its crystal | "This is its tower. Shall I set it?" | "Set it", "Wait" |

**Present and away.** The Sim has one flag, `PlayerPresent`. The Game sets it false when the player says "Go on without me" or stays more than 60 m from the party for 20 seconds, and true when the player taps Mira and says "I'm with you" (or simply answers one of her questions within 15 m).

| Situation | Player present | Player away |
|---|---|---|
| Lead offered | Waits `LeadPatience` = 450 ticks for a reply, then goes anyway and says where. | Decides after 100 ticks. |
| Travel and acting | Same speed. The player auto-follows. | Same speed. |
| A choice | Asks the player. No answer in 300 ticks: she treats it as away. | Goes to the marker that reveals the answer, studies it for `SoloStudyTicks` = 200, then answers correctly. If no such marker is reachable she takes the slower fallback route. She never guesses. |
| Unguarded pedestal with the crystal on it | Takes it. | Takes it. |
| Lethal threshold | Asks. | Waits `ThresholdWait` = 900 ticks, then withdraws to the encounter's safe point and takes another open lead if there is one. She comes back to the threshold when nothing else is open. |
| Tower, carrying its crystal | Asks. | Waits. She does not restore without the player. |
| Scout | Suggests it; the player may also ask. | Sends Tansy herself when a belief is stale. |

**Journal and rejoining.** Every solo action writes a journal entry of one sentence ("Mira read the boundary stone in the Stonemark Glade and found the stair."). The journal is read by asking Mira, and it is the side text of the Tab map. The map shows the party, the scout, discovered sites, towers, the last rival reports with their age, and a marker "Mira is waiting for you here". Tapping it sets the auto-walk target as the map does today. Mira's off-screen questions use the existing "calls you" arrow.

**Scouting.** One scout at a time, Tansy only. She runs to a named place at `ScoutTicksPerStep` = 4, looks for `SearchTicks`, and runs back. Her report sets the belief for that crystal to the truth. A lethal encounter cannot be committed while she is out: Mira says "Wait for Tansy" and the wait is real time.

### 5.6 Encounters and consequences

Encounter kinds: **lethal** (the four keepers and the finale), **friendly** (Orrin's demonstration) and **practice** (Mira, as today). Only lethal encounters go through the attempt machinery below.

**An attempt.**

1. At the threshold the Sim names the volunteer: the first living companion in the authored order, or none.
2. "Fight" sends `CommitEncounter`. The Sim creates the attempt `{ Id, EncounterId, Volunteer, Seed, Restarts }`, and the Game writes the checkpoint and starts the duel from the `EncounterCommitted` event.
3. When the duel ends the Game sends `ResolveEncounter { AttemptId, Outcome }`, steps the clock one tick with `GameClock.StepOnce()` so it is applied at once, and writes the checkpoint again. All of this happens before the result screen can be left, and it happens at the duel's end event, not when the screen closes: the presentation hold of `PRD_CHARACTERS` 7.4 (at least 1.5 s on a final break) delays only the screen.
4. `ResolveEncounter` is accepted only for the pending attempt with that id. Anything else is refused with `already_resolved` or `unknown_attempt`. All effects below happen in that one application.

**Outcome table.**

| Duel result | Campaign consequence |
|---|---|
| Victory | The encounter is complete. Its story effects apply once (for a keeper: the crystal becomes `Carried`). Nobody dies. |
| Defeat with a volunteer | The volunteer dies for good: roster, role and service gone. The party is placed at the encounter's safe point. The enemy stays. What was discovered is kept. Rival +900. |
| Defeat with no companion left | The run ends: `CampaignEnded { Defeat }`, the Unweaving ending, then "Begin again". |
| Clash or stalemate | Nobody dies. The party is placed at the safe point. The encounter stays unresolved. Rival +600 (+300 with Garrick). |
| Surrender after committing (the pause menu's "Leave encounter", confirmed with "<Volunteer> will pay for this. Leave?") | Counts as Defeat. |
| Withdrawal before committing | Safe. No cost. |
| Practice and friendly duels | No attempt, no cost, no casualty. Practice is recorded in `WardProgress` as today. |

A player with zero companions can commit and win any encounter. The death is shown in the scene (the volunteer steps into the gap as the others run), in the roster, in later lines and in the ending.

**If the game is closed during a committed duel.** The checkpoint holds the pending attempt. On Continue the duel is staged again from the Ward setup with a new duel seed (`Restarts` + 1) and no penalty. This is an open decision (section 11, item 3).

**The campaign checkpoint.** A campaign uses the one save slot. The game writes it without being asked at: campaign start, every commit, every resolution, every custody change, every restoration, every ending. F5 still works outside encounters and writes the same slot. So a recorded death cannot be undone by reloading (section 11, item 2).

**Opponent tuning (starting values, to be measured).** The scripted first-time player is the one `WardBalanceTests` already uses.

| Profile | Essences | First cast | Think time | Mistakes | Target win rate for the scripted first-timer |
|---|---|---|---|---|---|
| `practice_mira` (as shipped) | 3, then 4 | 3 to 6 s | 6 to 10 s | 0% | 54% (measured today) |
| The four keepers | 3 | 4 to 7 s | 8 to 12 s | 15% | 65 to 75% |
| `friendly_orrin` | 3 | 5 to 8 s | 9 to 13 s | 25% | not a target |
| `finale_unweaver` | 4 (with Light) | 3 to 6 s | 6 to 10 s | 0% | 50 to 60% |

### 5.7 Restoration

"Set it" sends `ConfirmRestore`. It takes `RestoreTicks` = 100. Then, in one step: custody becomes `Restored`, the order is recorded, the tower's beam lights in its element's colour, and the island changes.

| Effect | Rule |
|---|---|
| Demons | `SevensPerDraw` = max(1, base + restored - crystals on Ash altars). With all four restored, no demon is ever drawn again and those on the board are removed. |
| Earth (the Deepstone) | Hills and Mountain stock grows 20% faster. |
| Fire (the Kilnheart) | Workshops that burn wood (Kiln, Smokehouse) work 20% faster. |
| Water (the Tideglass) | Fields and Pasture stock grows 20% faster. |
| Air (the Skyshard) | Carriers on main roads take one tick less per step. |
| People | Every character's lines switch to the set for the new count of restored crystals. The journal records the restoration. |

Slice 1 builds the demon rule and the Earth boon. The other three boons are Slice 2. The only tuning fields this module may write are (`PRD_AGES` A.2): `DemonTuning.SevensPerDraw`; `EconomyTuning.GrowTicks` (per terrain); `EconomyTuning.CarrierTicksPerStep`; and `EconomyTuning.CycleBonusPermille` (per building type, added by `PRD_AGES` H1-2, default 0) for the Fire boon. Each boon is applied as a change on the saved base value and removed by recomputing from the base, so a boon can never stack twice.

### 5.8 Finale and endings

The finale encounter is not tied to a place in the data. It is bound to "the tower of the fourth crystal". When `Restored` = 3 and the party arrives at the remaining crystal's tower with that crystal `Carried`, the party is at the finale's threshold. Every tower has duel stage markers, so any tower can host it. A survivable defeat retreats to that tower's safe point and the finale stays open. Victory restores the fourth crystal at once and ends the campaign.

`CampaignEnded` carries the ending id and a `CampaignRecord`. After the restored ending the faction game goes on (N-6). After terminal defeat the whole game is over. Both endings finish with one bubble that hangs in front of the player. After terminal defeat the replies are "Begin again" (the same authored tale) and, once a generated campaign exists in the build, "A different telling". After the restored ending the first reply is "Onward, thirty winters on": it submits `AdvanceChapter{ch.modern.harbour}` once `PRD_AGES` has raised `ChapterReady` for it. The bubble keeps at most three replies (Onward, Begin again, and A different telling once it exists).

### 5.9 The faction game and the campaign together

| Matter | Rule |
|---|---|
| When it starts | When `AgesApi.CampaignMayStart` is true: the prince's handover has happened and the player has accepted the Historic age (N-1). `StartCampaign` also switches the island to campaign mode (`ErasModule.EnterCampaign`). |
| The prince | He plans and builds alone. The player may still walk to him, ask what he is doing and advise him (those rules are in `PRD_AGES.md`). Doing so is "away" for Mira. |
| The prince needs you | An overrun warning makes the prince call the player with the existing off-screen arrow. Going is the player's choice. |
| Overrun of one of your camps | The camp is lost, or the home camp is looted when it is the only one left (N-2, `CampOverrun.Kind`). The party drops what it is doing and retreats to the home camp on foot. An uncommitted threshold is abandoned. Rival +1800. Nobody dies. It cannot happen during a duel because the world clock is paused. |
| Your people overrun an Ash camp | The Ash expedition stops for 900 ticks. |
| Demons | Rarer with each restoration, more frequent with each crystal on an altar (5.7). |
| Skirmish | Standalone skirmish has no campaign state and keeps the fatal overrun and the time limit. |
| Clock and save | One tick clock. One save. `WorldState.Campaign` is null when no campaign runs, and then nothing in this document has any effect. |

### 5.10 Interiors

**Definition.** An interior is a small map of lattice cells in its own coordinates. It is written as rows of text, one character per cell, with a legend:

```
"Id": "quarry_upper", "Name": "the Old Quarry",
"Rows": [ "#########",
          "#..P#...#",
          "#.##..#X#",
          "#....#..#",
          "###E#####" ],
"Legend": { "E": { "Kind": "Exit", "To": "glade/stair" }, "P": { "Kind": "Pedestal", "Crystal": "Earth" },
            "X": { "Kind": "Door", "OpenWhen": "earth.ash_cut_open" } }
```

`#` is rock, `.` is floor. Column and row are the lattice i and j, so the six step directions work as on the surface. Marker kinds: `Exit`, `Stairs` (to another interior), `Door` (open when a fact is known), `Pedestal`, `Altar`, `Threshold`, `SafePoint`, `Inscription`, `Witness`, `Lever`, `DuelCaster`, `DuelOpponent`. Size limit: 16 by 16 cells, at most 80 floor cells.

**Connection to the island.** A surface place has an anchor (appendix C) and a few markers on the cells round it. An `Exit` or `Stairs` marker names the marker it leads to. Stepping onto a stair cell moves the viewpoint to the linked cell with a short fade and a quarter-second lock. A stair opened by a quest event stays open: it is a fact in the save.

**In the Game.** Interiors are built far east of the island on the same world lattice, one after another (`InteriorSpace.Origin(index)`), each with its own floor, walls, ceiling and lights. `WalkGrid` gains open regions: a cell beyond the board is walkable only if an interior lists it as floor. The sky and sun are switched off inside. The Sim never uses world positions for interiors; it uses the interior's own cells for paths, travel time and the validator.

**The map.** Inside an interior, Tab shows that interior: floor cells, the exit in green, stairs, discovered markers, the party, the scout and the player. Tapping a cell sets the auto-walk target. A text line at the top switches between the interior and the island. Undiscovered markers are hidden, or shown as "?" while Ivo lives.

**The way back.** Exits and stairs up are always lit and always on the map. Tapping any party member offers "Lead me out": the player auto-walks to the exit. With nobody near, the map tap does the same. Mira says so the first time the party goes underground.

### 5.11 The four authored routes

Step-by-step tables are in appendix D. In short:

| Element | Clue and first route | Fallback with no companion | If the rival was first |
|---|---|---|---|
| Earth | The sawyer explains the foresters' notches. They point to the boundary stone in the Stonemark Glade. Its survey marks open a stair. Below, the Old Quarry forks; wall marks show the way to the Root Chamber and the pedestal. | Marker posts at the forest edge can be read by Mira (slower). Odo reads them on sight. | Empty pedestal and an Ash tally stick. The tally counts the turnings to the Tally Vault on a lower level: the Ash altar and its keeper Dolm. A second way out, the Ash cut, is barred from inside until Dolm is beaten. |
| Fire | Hod the smith tells the order for lighting the Cold Kiln: three steps, asked as a choice of what comes first, then next. The lit kiln shows the pedestal in its hearth. | Notches on the kiln's three draught doors give the order (slower). Petra knows it. | Cold ash and soot-stamped order tablets name the Black Forge near the Ash Clan's land: an interior with the altar and its keeper Brask. |
| Water | Old Marl's riddle and the flood marker tell when the Low Causeway is dry. The tide is real: the causeway is passable 900 ticks in every 2400. The pedestal is in the tide cellar at its far end. | The flood marker's rings can be watched through one tide (slower). Nell reads it at a glance. | A boat manifest at the landing and keel marks on the shore lead to the Old Cistern: the altar and its keeper Nual. With Nell alive the sluice can be opened instead of fighting. |
| Air | Gannet Watch has a door, a stair and Orrin's room. Mira asks what he saw. He sets a knowledge test whose answer is carved over his own door. A right answer teaches the wind markers and gives the lead to the pedestal on the Bare Shoulder. A wrong one sends the party to look round and try again. | Skipping Orrin: the three wind cairns can be searched one by one (slower). | Tansy follows the Ash signal fires to the Cliff Stair: an interior, the altar and its keeper Pike. Without Tansy, ash piles and mirror shards mark the same trail (slower). |

Orrin's answer about the Skyshard is built from the true custody state when he is asked, so it is always current, and it resets the party's belief. He offers a friendly Ward Break demonstration; Mira is still the caster.

### 5.12 The duel when the player casts as Mira

| Matter | Rule |
|---|---|
| Who stands where | Mira's own body stands on the `DuelCaster` marker. The opponent's body stands on `DuelOpponent`, 5.5 m away (the existing `DuelDistance`). The anonymous player wizard is not spawned. The volunteer stands at the way out, in frame. The viewpoint goes over Mira's shoulder with the existing camera rule, replaced by the narrower view of `PRD_CHARACTERS` 7.2 once its WP-9 lands. Staging reads `DuelStageSpec` (with `VolunteerActorId` and `VolunteerPos`); I7 edits `WardDuelStage.cs` for this until `cry-I7`, then the file is WP-9's. |
| Whose spell | The setup title reads "Mira's Ward". As the player adds essences, orbs gather at Mira's hand. Mira animates each accepted cast; the opponent animates its own. |
| Sim change | None to the rules. A new data class `WardOpponentProfile` and `WardTuning.For(profile)` fill the existing `Mira*` timing fields and the essence pool. `WardDuelSession` gets a factory that takes a `WardDuelSpec`. |
| Host change | `EncounterContext` gains a `Request` object, and `EncounterDirectory.TryStart` gains an overload that passes it. With no request, `WardDuelEncounter` behaves exactly as today. |
| Encounter change | `WardDuelEncounter` reads a `WardDuelRequest`: mode, attempt id, profile, pool, seed, stage. In lethal mode there is no rematch; leaving is the confirmed surrender; the result goes out as `ResolveEncounter`, not through `WardProgressApi`. |
| Screen | The duel keeps its own panel as the one place with on-screen controls: essence chips at least 44 points high, the two histories folded to the last cast with a tap to expand, everything inside the phone's safe area at 844 x 390. |

---

## 6. Data format, generator, validator

### 6.1 The definition

A campaign is a **data pack**: a folder of JSON files that the loader merges into one `CampaignDefinition`. Splitting by file lets several writers work at once.

```
Assets/StreamingAssets/world/campaigns/authored_v1/
  campaign.json   format version, id, title, status, roles, companions, crystals, towers, rival plan, tuning
  earth.json      locations, interiors, connections, clues, leads, choices, encounters, quest events (Earth)
  fire.json  water.json  air.json
  finale.json     finale encounter, endings
  lines.json      every spoken line, by id
```

| Part | Stable id | Holds |
|---|---|---|
| Role | `mira`, `prince`, `rival_leader`, `final_enemy`, `wizard` | The actor id bound to each fixed role. |
| Companion | `garrick` ... | Name, role, line, volunteer order, service. |
| Location | `glade`, `tower_earth` ... | Name, kind, anchor on the island, surface markers, safe point. |
| Interior | `quarry_upper` ... | Rows, legend (5.10). |
| Connection | `glade_stair` ... | Two marker references and the fact that opens it. |
| Crystal | `earth` ... | Name, original site and pedestal marker, tower, whether it starts underground. |
| Tower | `tower_earth` ... | Location, door sign, duel stage markers, beam colour. |
| Rival plan | - | Ordered steps: crystal, march cost, carry cost, altar; crystals already stolen at the start. |
| Clue | `earth_sawyer` ... | Source, required facts, facts given, essential flag, fallback clue. |
| Lead | `earth_stone` ... | Goal (place, marker, action), required facts, reason line, priority. |
| Choice | `air_test` ... | Question line, answers, the correct one, the marker that reveals it. |
| Encounter | `keeper_earth`, `finale` ... | Kind, opponent profile, threshold and safe point markers, effects on victory, alternate route. |
| Quest event | `earth_read_stone` ... | Trigger (place, marker, action), required facts, facts set, effects, lines. |
| Ending | `restored`, `unwoven` | Condition, lines, variants by survivors. |
| Line | `mira_open_1` ... | Speaker, text with tokens, guards. |

`Status` is `Complete` or `Partial`. Normal play starts only `Complete` packs. The Slice 1 pack is `Partial` (Earth only) and starts only from the developer start.

**Lines, tokens and guards.** A line's text may hold tokens such as `{crystal.earth.place}`, `{volunteer.name}`, `{count.restored}`. The Sim fills them from state when the line is asked for (`CampaignApi.Line`). A line has guards (`alive: odo`, `custody: earth = AtRivalAltar`, `fact: earth.stolen_seen`). A line whose speaker is dead is never returned. A place name for a crystal may only come from a token, never from fixed text; the validator enforces that.

### 6.2 Run state and the save

`WorldState.Campaign` (appendix A.2) holds the whole resolved definition and its hash, the seed and generator version, the start tick, `RivalProgress` and the rival's phase, the four custody states and beliefs, the party's place, leg and mode, the roster with who fell where and when, the facts, the lead queue, the pending attempt and the list of resolved ones, the scout, the journal, the restoration order and the ending. Because the definition travels inside the save, changing the generator or the shipped pack later cannot change a run in progress.

Game-layer extras (not Sim state): whether the player is inside an interior and which, and the director's scene step, as the prince's first scene is saved today.

### 6.3 The generator

Offline and seeded. It is pure C# in the Sim assembly, so it runs under `dotnet test`, from a command-line tool and inside the game with no network.

```
dotnet run --project DotNet/World.CampaignTool -- generate --seed 7 --template first --out <folder>
dotnet run --project DotNet/World.CampaignTool -- validate <pack folder>
dotnet run --project DotNet/World.CampaignTool -- outline <pack folder>
```

`generate` writes the resolved pack and `outline.md`, a plain-English page: who, where, what the rival plans, each route's clue chain, where a crystal goes if stolen, and the endings. The game starts a generated pack through the same New Campaign path as the authored one.

A **template** is a set of authored building blocks with slots: site modules (an original site with its puzzle), altar modules (an interior with a keeper), the wizard module, clue modules, encounter profiles and lines with tokens. The generator fills the slots under fixed role rules: Mira leads, casts and restores all four; the prince runs the faction; six named companions start alive; the rival must be faced before the fourth restoration; there is a playable wizard consultation; at least one crystal starts underground.

| What a seed may change | Example |
|---|---|
| The rival's motive and plan | Order of targets, march costs, one crystal already on an altar at the start. |
| Who and what holds each clue | The ferryman holds the quarry clue; the kiln order is on a boundary stone. |
| Order and dependencies of leads | Fire needs two witnesses before the forge is known; Water is open from the start. |
| How the rival moves each crystal | Which altar module receives which crystal. |
| Which crystal starts underground | Water in the quarry module, Earth on the ridge. |
| Where the wizard lives and what he gives | A tower, the kiln or the ferry landing; a clue, a scout report or a charm that halves one rival charge. |
| Which interior branches are open | The Ash cut exists or is collapsed. |
| How survivors appear in the ending | Which companion speaks the last line if alive. |

### 6.4 What must differ between seeds

Each definition has a **causal signature** of six parts: (1) the rival's target order, (2) the set of crystals stolen at the start, (3) for each crystal, the shape of its clue chain (how many steps, which source kinds, which dependencies), (4) which element is underground, (5) the wizard's host and gift, (6) the sequence of quest events when the scripted direct player plays it. Two generated examples count as materially different only if at least three parts differ, and one of them is (1) or (3). The proof prints the signatures and the comparison.

### 6.5 The validator

`CampaignValidator.Validate(definition)` returns a list of problems, each with a code and a sentence. Any error blocks the start.

| Group | Checks |
|---|---|
| Form | Known format version. No duplicate ids. Every reference points to something that exists. Every id has a display name. Bubble lines fit the length limit. |
| Roles | Mira, prince, rival leader, final enemy and wizard are bound. Exactly six companions with volunteer orders 1 to 6. |
| Crystals and towers | Exactly four crystals, one per element. Each has one tower; no tower is shared. Every tower has duel stage markers and a safe point. At least one crystal starts underground. |
| Places | Every anchor is a legal place (appendix C), no two share one, none is a start corner. |
| Interiors | Within the size limit. All floor cells connected. Every marker on a floor cell. Stairs and exits linked both ways. From every pedestal, altar, threshold and safe point there is a path to a surface exit using only doors whose facts can be known by then. |
| Clues | From the opening facts, every crystal's original site can be discovered. Every essential clue has a fallback whose source is not a companion. No essential lead requires a companion. |
| Rival | Every plan step names a real crystal and altar. Costs are positive. For every crystal, the stolen path works: the trail from the empty pedestal reaches the altar, and the keeper encounter exists, is lethal and has a threshold and a safe point. |
| Finale and endings | For all 24 restoration orders the finale is reached at the fourth tower. The restored ending and the terminal defeat are both reachable. |
| Simulation | Four scripted players run the definition headless and must finish: direct (first arrivals where the plan allows), reluctant (turns every lead down until its crystal is reported stolen, so every recovery is at an altar), six lethal defeats then all wins, and seven defeats (must end in terminal defeat). |
| Narrative | Every line with a mortal speaker has an "alive" guard. No line gives a crystal's place as fixed text. No essential line is guarded on a companion. |

**Broken fixtures.** Each is a small pack that must be rejected with exactly the code shown.

| Fixture | Code |
|---|---|
| Two locations with one id | `duplicate_id` |
| A lead that points to a missing marker | `dangling_reference` |
| A format version from the future | `bad_version` |
| Five companions | `companion_count` |
| Volunteer orders 1, 2, 2, 4, 5, 6 | `volunteer_order` |
| A crystal with no tower | `missing_tower` |
| Two crystals sharing a tower | `shared_tower` |
| A site anchored on a numbered hex | `bad_anchor` |
| An interior with a walled-off room | `interior_disconnected` |
| A deep level with stairs down and none up | `no_way_out` |
| A pedestal whose only clue is never obtainable | `unreachable_clue` |
| An essential clue held only by a companion | `companion_only_clue` |
| A stolen crystal whose altar no trail reaches | `no_recovery` |
| A tower with no duel stage | `no_finale_stage` |
| A line spoken by a companion with no "alive" guard | `dead_speaker` |
| A line that says "the Deepstone is on its pedestal" as fixed text | `fixed_location_text` |
| A rival plan naming a crystal twice | `rival_plan` |

---

## 7. MVP slices

### Slice 1: Earth, end to end

**What the owner will see and do.**

1. Start the Earth test start. It is night at the home camp. The prince's people are already building on their own.
2. Mira walks up, tells the story, shows the six, offers practice and asks you to come.
3. Follow her to the sawyer, then to the boundary stone in the glade. She reads it; a slab slides back.
4. Step down into the Old Quarry on the same four moves. Open the map: it shows the quarry and the way out.
5. First run: reach the Root Chamber before the Ash expedition. The Deepstone is on its pedestal. Mira takes it.
6. Second run: use the "Ash got there first" test start (or keep answering "Not that way" for 8 minutes). The pedestal is empty. Find the tally stick, follow it down to the Tally Vault, and see Dolm at the Ash altar.
7. Mira names Garrick and asks if you fight. Say yes. Cast as Mira. Lose once: Garrick holds the stair and dies; you are back in the survey gallery. Go again and win: Mira takes the Deepstone from the altar. The Ash cut is now open as a second way up.
8. Walk out. Follow Mira to the Rooted Tower. Say "Set it". The tower lights. Demons come less often. People speak of it.
9. Press F5 at several points, quit, continue. Nothing happens twice.
10. Also try: say "Go on without me" at the start, visit the prince, then find Mira by the map. Try the practice duel: nobody is at risk.

**Acceptance checks.**

| # | Check |
|---|---|
| 1.1 | `dotnet test DotNet` is green, including every test that was green before. |
| 1.2 | The campaign proof sections A to D and F pass on the Slice 1 pack and the skeleton pack (section 9.1). |
| 1.3 | AutoPlay `earth_first` exits 0: opening, follow, stone, descent, pedestal, crystal taken, surface, tower, restored. |
| 1.4 | AutoPlay `earth_stolen` exits 0: the Ash expedition seizes the Deepstone first, empty pedestal, tally, Tally Vault, one scripted loss (Garrick dead, party at the safe point, rival +900), one scripted win, out by the Ash cut, restored. |
| 1.5 | Save and continue at five points (before the stair, underground, at the threshold, right after the loss, carrying the crystal) each resume with the same custody, roster, facts and place. The loss is not applied twice and the crystal is not taken twice. |
| 1.6 | Sending `ResolveEncounter` twice for one attempt changes state once; the second is refused with `already_resolved`. |
| 1.7 | With the player away, Mira reaches the pedestal by herself and takes the crystal if it is there; at the Tally Vault she waits 90 s, then withdraws; the map shows where she waits; "I'm with you" rejoins. |
| 1.8 | From both the Root Chamber and the Tally Vault, "Lead me out" brings the player to the surface. |
| 1.9 | The practice duel against Mira plays and records as before; its PlayMode tests are unchanged and green. |
| 1.10 | In windows of 844 x 390 and 932 x 430 every bubble, reply and duel control is on screen and inside the safe area, and the whole Earth route can be played with pointer taps and the four strips. |
| 1.11 | No write to `WorldState` from `World.Game` other than the existing `WardProgressApi` (checked by search). |

### Slice 2: the other three routes, the race, the finale, the endings

**What the owner will see and do.** The whole campaign from the opening to either ending with ordinary inputs. Light the Cold Kiln in the right order, and once in the wrong order. Answer Old Marl's riddle and wait for the tide. Climb Gannet Watch, answer Orrin correctly, and on another run answer wrongly and find the answer over his door; on a third skip him and search the cairns. Take his friendly demonstration. See reports of Ash runners and race them. Recover at least one crystal from each kind of altar. Restore in an order of your choosing and meet Hesk Varr at whichever tower is last. Lose there and come back. Lose all six and see that you can still win, then lose once more and see the Unweaving. Leave Mira for the prince during an overrun warning and see what it costs.

**Acceptance checks.**

| # | Check |
|---|---|
| 2.1 | The authored pack is `Complete` and passes the validator with no errors. |
| 2.2 | Proof sections A to G pass on the authored pack: all 24 restoration orders reach the finale at the right tower; six deaths then a win; the seventh loss ends the run; clash, stalemate and surrender behave as the table says. |
| 2.3 | AutoPlay `campaign_full` exits 0 from the opening to the restored ending. `campaign_lost` exits 0 at the Unweaving and "Begin again" starts a fresh campaign. |
| 2.4 | The watchtower: a correct answer, a wrong answer with retry, and the skipped route each give the Air lead. Orrin's line matches the true custody in all five custody states. |
| 2.5 | Each route's fallback works with its shortcut companion dead. The cistern sluice route is offered only while Nell lives. |
| 2.6 | A stale belief shows the "where to look" sentence in the journal and from Mira; a scout report clears it. |
| 2.7 | An overrun of one of your camps during the campaign: camp lost, party retreats, rival +1800, nobody dies, no game over. In skirmish the same overrun ends the game. (Needs N-2.) |
| 2.8 | Each restoration lights its tower, changes the demon rule and applies its boon; lines change with the count. |
| 2.9 | The ending names every fallen companion in order with the place, and has the right variant for six, some and no survivors. |
| 2.10 | Save and continue across the finale: before the threshold, after a survivable loss, after the win. No duplicate casualty, no crystal restored twice. |
| 2.11 | Phone-sized windows: checks of 1.10 for the whole campaign. |

### Slice 3: generator, validator, two generated examples

**What the owner will see and do.** Run one command with a seed and read the outline it writes. Run it again and get the same files. Run a second seed and read a different outline: a different rival plan and different clue chains, not just different names. Start either from "A different telling" after an ending, or from the developer start, and play it. Feed the validator a broken pack and read why it was refused.

**Acceptance checks.**

| # | Check |
|---|---|
| 3.1 | The same seed, template and generator version give byte-identical pack files, twice. |
| 3.2 | 20 seeds validate with no errors and complete under the four scripted players (proof section H). |
| 3.3 | Two shipped examples differ in at least three signature parts including the rival plan or a clue chain; the proof prints the comparison. |
| 3.4 | Every broken fixture of 6.5 is rejected with its code. The authored pack is accepted. |
| 3.5 | The first template always includes a playable wizard consultation and one crystal underground; across the 20 seeds the underground element takes at least three different values. |
| 3.6 | A generated pack starts through the same New Campaign path, and a save of it still loads after the generator's version constant is changed. |
| 3.7 | AutoPlay `campaign_generated <seed>` exits 0 on both shipped examples. |
| 3.8 | `Docs/CAMPAIGN_FORMAT.md` is enough for a writer to add a data pack by hand; one hand-written test pack made only from that document validates. |

---

## 8. Work packages

Rules for parallel agents. Each package owns the files listed and edits no others. The interface package of each slice lands first and is tagged; the names in the appendices are binding from then on, and changes are additive and logged. Only one Unity editor may be open per folder, so every package that needs Unity gets its own git worktree unless the table says it runs in sequence in a shared one. Headless packages need no editor; they still use their own worktree when they run beside others. Model tiers: sonnet for mechanical or well-specified work, opus for substantial design-heavy implementation, fable for the hardest integration and judgement.

Package branches are cut from tag `ages-H1-0` (which itself sits on the finished refactor, tag `refactor-done`) into `feat/crystals`, one branch per package (`cry/i0`, `cry/i1`, ...). Worktrees are `wt-cry-<id>`.

### 8.1 Slice 1

| Id | Package and purpose | Owns | May assume | Model | Unity | Depends on | Acceptance |
|---|---|---|---|---|---|---|---|
| I0 | **Interface.** The public surface of appendices A and B as compiling shells, so everyone else can code against it. | `Sim/Campaign/**` (types, commands, events, `CampaignApi`, module shell), `Sim/Board/Lattice.cs`, one registration line in `Sim/Core/WorldSimFactory.cs` (a registration file, see the changelog), one line in `Game/Encounters/EncounterInstaller.cs` registering `ward_campaign`, no edit to `Sim/Save/**` (`PRD_AGES` H1-0 owns it and the save version; `CampaignState.cs` and `WorldState.Campaign` are created by H1-0 and become this package's at tag `ages-H1-0`), `Tests/EditMode/Campaign/CampaignSurfaceTests.cs`, stubs `Game/Expedition/ExpeditionInstaller.cs`, `Game/Interiors/InteriorsInstaller.cs`, `Game/Encounters/EncounterRequest.cs`, `DuelStageSpec.cs`, the `Request` field in `EncounterHost.cs`, two installer calls in `Boot/WorldBootstrap.cs`, `Docs/CAMPAIGN_API.md` | The finished refactor | opus | Yes, once, for a compile check (integration folder) | refactor F-1, `ages-H1-0` | I0 lands in two halves (see the I0 entry of A.6): the Sim half (tag `cry-I0-sim`) and the Unity half (tag `cry-I0`). Reflection test pins every name in appendix A. Unity compiles with 0 errors. All existing tests green. A null campaign changes nothing (state JSON of a 20-minute run is identical to before, apart from the new null field). Tag `cry-I0`. |
| I1 | **Campaign runtime.** Custody, the rival plan and clock, facts, leads, Mira's brain, party movement, choices, attempts and consequences, restoration, journal. | `Sim/Campaign/Runtime/**`, `Tests/EditMode/Campaign/Runtime/**` | I0 names; a sample definition from I0's tests | opus | No | I0 | Unit tests for every row of the tables in 5.1 to 5.7; determinism (two runs, identical state JSON); save and load mid-leg, mid-choice and with a pending attempt continue identically. |
| I2 | **Format, loader, validator v1, packs.** The JSON format, the folder loader, the validator's form, role, crystal, place, interior, clue and rival checks; the authored Earth pack with its lines; the headless skeleton pack; the broken fixtures those checks cover. | `Sim/Campaign/Definition/**`, `Sim/Campaign/Validation/**`, `StreamingAssets/world/campaigns/authored_v1/**`, `Tests/EditMode/Campaign/Packs/**`, `Tests/EditMode/Campaign/Validation/**`, `Docs/CAMPAIGN_FORMAT.md` | I0 types | opus | No | I0 | Authored Earth pack loads and validates as `Partial`. Skeleton pack validates as `Complete`. Fourteen of the broken fixtures are rejected with their codes (all but the finale and narrative ones). Appendix C anchors are pinned by a test. |
| I3 | **Opponent profiles.** `WardOpponentProfile`, `WardTuning.For`, `WardDuelSpec`, the profile file, balance measurements. | `Sim/Encounters/Ward/WardOpponentProfile.cs`, `WardDuelSpec.cs`, additive lines in `WardTuning.cs` and `WardDuelSession.cs`, `StreamingAssets/world/ward_opponents.json`, `Tests/EditMode/WardOpponentTests.cs` | Nothing | sonnet | No | none (can start with I0) | Existing `WardTests` and `WardBalanceTests` unchanged and green. Measured win rates for each profile over 300 seeds are written into `Docs/WARD_API.md`; keepers land in 65 to 75%. |
| I4 | **Campaign proof.** Scripted players and the generated report. | `Tests/EditMode/Campaign/Proof/**`, `Docs/CAMPAIGN_PROOF.md` | I1 and I2 merged. It reports failures to their owners; it does not patch their files. | opus | No | I1, I2 | Section 9.1, sections A to D and F, byte-identical across two runs. |
| I5 | **Interiors in the game.** Open regions in the walk grid, building an interior from its rows, stairs and fades, interior lighting, the interior map, "Lead me out". | `Game/Interiors/**`, the region edits in `Game/Player/WalkGrid.cs` and `PlayerWalker.cs`, the interior mode in `Game/UI/MapScreen.cs`, `Tests/PlayMode/InteriorTests.cs`, `Docs/INTERIORS_UI.md` | I0; a fixture interior until I2's quarry lands | opus | Yes, own worktree | I0 | Every floor cell is walkable and every other cell beyond the board is blocked. Walking down and up a stair returns to the same cell and heading. The map marks the exit; a tap walks there. Existing `PlayerTests` green. |
| I6 | **Expedition director and views.** Mira's loop (offer, lead, act, ask, threshold, wait), the six companions walking in file, present and away, the journal, the map layer, surface site and tower views, the restoration effect, HUD lines. | `Game/Expedition/**`, `Tests/PlayMode/ExpeditionTests.cs`, `Docs/EXPEDITION_UI.md` | I0; `Interiors` API from appendix B; the duel is started through `EncounterDirectory` with a request | fable | Yes, own worktree | I0 (then I1, I2 to run for real) | With a scripted Sim feed: each party mode shows the right bubble and replies; "Go on without me" and "I'm with you" send `SetCompany`; views equal state after a load; no state writes. |
| I7 | **Campaign duel.** `WardDuelEncounter` reads a request; lethal, friendly and practice modes; Mira staged as caster; the stakes and surrender prompts; commit and resolve glue; the phone layout of the duel panel. | `Game/Encounters/WardDuelEncounter.cs`, `WardDuelStage.cs`, `EncounterInstaller.cs` (these three pass to WP-9 at `cry-I7`), `EncounterUi.cs`, `WardBot.cs`, new `Game/Encounters/CampaignDuel.cs`, `Tests/PlayMode/CampaignDuelTests.cs` | I0, I3. Owns `WardDuelStage.cs` and `EncounterInstaller.cs` until `cry-I7`, then they pass to `PRD_CHARACTERS` WP-9 (N-10). | opus | Yes, own worktree | I0, I3 | With no request the practice duel and its tests are unchanged. A lethal request stages Mira and a keeper, has no rematch, and sends exactly one `ResolveEncounter`. The panel fits 844 x 390. |
| I8 | **Integration.** The campaign checkpoint, the developer start, the F1 campaign tool (rival step now, set custody, kill a companion), AutoPlay scripts, phone-size captures, guide pages. | `Game/Boot/SaveSystem.cs`, new `Boot/CampaignStart.cs`, `Boot/CampaignScripts.cs`, `Game/UI/God/CampaignDevTool.cs`, `Game/UI/GuideText.cs`, `Tests/PlayMode/CampaignIntegrationTests.cs`, campaign cases in `Tests/PlayMode/SaveLoadTests.cs`, `Docs/PLAY_GUIDE.md`, `Docs/PROGRESS.md` | Everything above merged | fable | Yes, integration folder | I1 to I7 | All of section 7, Slice 1. |

**Order.** I0 first. Then I1, I2, I3, I5, I6 and I7 in parallel (I1 and I2 can share nothing but I0's types; I5, I6, I7 each in its own Unity worktree). I4 starts when I1 and I2 merge. I8 last. Merge order: I0, I3, I1, I2, I4, I5, I7, I6, I8.

**Package notes added by the reconciliation.**

- `Sim/Board/Lattice.cs` is created by I0 (`CellOf`, `Distance`, and `Step`, `X`, `Z` as `PRD_AGES` A.7 names them). From tag `cry-I0`, `PRD_AGES` M1-0 and M1-1 own it and extend it additively. The surface cells here and in `Game/Player/WalkLattice.cs` are the same cells; an I0 test pins that.
- I5 owns `Game/UI/MapScreen.cs` and `IMapLayer`. Other packages, including the ages ones, add map content through `IMapLayer` and never edit that file. I5's interior lights stay inside the light budget of `PRD_CHARACTERS` 5.8 and keep party faces readable.
- I6 takes over Mira's dialogue at run time through the `Npc` API (it replaces her option list) and registers site clearings through the existing `CampLayout.Clearings` list from its own files. It edits nothing under `Game/View/Camp/**` (that is `PRD_CHARACTERS` WP-16's).
- I8 owns `SaveSystem.cs` and names the slots: `main` (F5 and the campaign checkpoint), `auto.chapter` (written on `ChapterAdvanced`), `ckpt.<mission>.<n>` (mission retries in `PRD_AGES`). Each slot is separate.
- Companion bodies, keepers and the wizard are placeholders in Slices 1 and 2 unless `PRD_CHARACTERS` has delivered them. The four keepers use the champion's body in a different palette when it exists (N-7, N-8).

### 8.2 Slice 2

| Id | Package and purpose | Owns | May assume | Model | Unity | Depends on | Acceptance |
|---|---|---|---|---|---|---|---|
| J0 | **Interface additions.** Finale, endings, scout, reports, restoration effects, overrun hook; `SiteViewRegistry` on the Game side so set-piece packages only add files. | Additive edits in `Sim/Campaign/*.cs` shells and `Game/Expedition/SiteViewRegistry.cs`, change log in `Docs/CAMPAIGN_API.md` | Slice 1 merged | opus | Yes, once (compile) | Slice 1 | Surface test updated; nothing renamed. Tag `cry-J0`. |
| J1 | **Runtime, part two.** The full rival plan, reports and beliefs, scouting, the finale binding, endings and the record, the four boons, the overrun and Ash-camp links. | `Sim/Campaign/Runtime/**` and its tests (same owner as I1) | J0; N-2 and N-4, or the fallbacks of 3.9 | opus | No | J0 | Unit tests for 5.3, 5.5 scouting, 5.7 to 5.9. |
| J2a | **Fire pack.** Kiln, forge, clues, lines. | `campaigns/authored_v1/fire.json`, its lines file | The format doc | opus | No | J0 | Validates; its route proof trace passes. |
| J2b | **Water pack.** Causeway and tide, cistern, sluice route, riddle, lines. | `water.json`, its lines file | The format doc | opus | No | J0 | As J2a. |
| J2c | **Air pack.** Gannet Watch, Orrin, the test, cairns, Cliff Stair, the friendly duel, lines. | `air.json`, its lines file | The format doc | opus | No | J0 | As J2a, plus check 2.4 headless. |
| J2d | **Finale and endings pack.** The finale encounter, both endings with survivor variants, the lines for each count of restored crystals. | `finale.json`, `lines_state.json` | The format doc | opus | No | J0 | Validates; ending text for 6, 3 and 0 survivors reviewed against section 4. |
| J3 | **Proof, part two.** Sections E and G, all four routes in B and C, the finale and narrative validator checks and their fixtures. | `Tests/EditMode/Campaign/Proof/**`, `Sim/Campaign/Validation/**`, fixtures | J1, J2a to J2d merged | opus | No | J1, J2* | Check 2.2; the remaining three broken fixtures rejected. |
| J4 | **Fire and Water set pieces.** The kiln lighting, the tide and causeway, the forge and cistern dressing. | `Game/Expedition/Sites/Fire/**`, `Sites/Water/**`, their PlayMode tests | `SiteViewRegistry`; pack ids from J2a, J2b | opus | Yes, own worktree | J0 | Each site's view follows its facts and custody after a load; the causeway is blocked in the walk grid exactly when the Sim says it is flooded. |
| J5 | **Air set pieces.** Gannet Watch inside and out, Orrin, the wind markers, the Bare Shoulder, the Cliff Stair. | `Game/Expedition/Sites/Air/**`, tests | As J4 | opus | Yes, own worktree | J0 | Check 2.4 in PlayMode. |
| J6 | **Finale, endings, the island's change.** The last-tower staging at any tower, Hesk Varr, both ending scenes, tower beams, the memorial lines. | `Game/Expedition/Finale/**`, `Game/Expedition/Endings/**`, tests | As J4; clips from N-11 or placeholders | fable | Yes, own worktree | J0 | The finale stages correctly at each of the four towers; ending variants for 6, some, 0 survivors; "Begin again" starts clean. |
| J7 | **Integration.** Full-campaign AutoPlay, overrun link in play, phone pass, guide, progress notes. | The files I8 owned | Everything merged | fable | Yes, integration folder | J1 to J6 | All of section 7, Slice 2. |

**Order.** J0 first. Then J1, the four J2 packs, J4, J5 and J6 in parallel. J3 when J1 and the packs merge. J7 last.

### 8.3 Slice 3

| Id | Package and purpose | Owns | May assume | Model | Unity | Depends on | Acceptance |
|---|---|---|---|---|---|---|---|
| G0 | **Interface.** Template format, `CampaignGenerator.Generate` signature, the signature type, the outline writer's signature. | Shells in `Sim/Campaign/Generation/**`, `Sim/Campaign/Outline/**`, change log | Slice 2 merged | opus | No | Slice 2 | Surface test. Tag `cry-G0`. |
| G1 | **Generator and the first template.** Slot filling under the role rules, built from the authored modules. | `Sim/Campaign/Generation/**`, `StreamingAssets/world/campaign_templates/first/**`, `Tests/EditMode/Campaign/Generation/**` | G0; the validator as the acceptance gate | fable | No | G0 | Checks 3.1, 3.5; 20 seeds pass the validator. |
| G2 | **Validator hardening.** The simulation checks over all orders and loss counts as a library call, speed (under 2 s per pack), clear sentences, every fixture. | `Sim/Campaign/Validation/**`, fixtures | G0 | opus | No | G0 | Check 3.4; a pack is validated in under 2 s under `dotnet test`. |
| G3 | **Tool and outline.** The command-line project and the outline writer. | `DotNet/World.CampaignTool/**`, `Sim/Campaign/Outline/**`, the tool section of `Docs/CAMPAIGN_FORMAT.md` | G0 | sonnet | No | G0 (G1 to run for real) | The three commands of 6.3 work; the outline of the authored pack reads correctly against section 4. |
| G4 | **New Campaign in the game.** "A different telling", the developer start by seed or folder, generic site views for modules placed at new anchors. | `Game/Boot/CampaignStart.cs`, `Game/Expedition/NewCampaign.cs`, tests | G0; the Slice 2 site registry | opus | Yes, own worktree | G0 | Checks 3.6, 3.7 on a hand-made variant pack before G1 lands. |
| G5 | **Two examples and proof section H.** Choose two seeds, commit their packs and outlines, extend the proof, play both. | `campaigns/generated/**`, proof section H, `Docs/PROGRESS.md` | G1 to G4 merged | fable | Yes, integration folder | G1 to G4 | All of section 7, Slice 3. |

**Order.** G0 first. G1, G2, G3 and G4 in parallel. G5 last.

---

## 9. Test plan

### 9.1 The campaign proof

The test `CampaignProofReport` writes `Docs/CAMPAIGN_PROOF.md`. It must be byte-identical across two runs. Duel outcomes are injected (the proof sends `ResolveEncounter` with a chosen outcome); the duel itself is covered by the Ward tests and the opponent measurements.

Scripted players: **direct** (accepts every lead at once, answers choices correctly, fights at once), **slow** (waits 60 s before each reply), **reluctant** (answers "Not that way" to each lead until its crystal is reported stolen, then plays directly), **absent** (says "Go on without me" at the start, rejoins only when Mira is waiting), **loser(n)** (loses the first n lethal duels, then wins), **wrong** (gives one wrong answer per choice first).

| Section | Content | Pass |
|---|---|---|
| A. Definition audit | Every id of the pack and how many times each was used across the runs. | Every lead, clue, choice, encounter and ending is used in at least one run. |
| B. Earth, first arrival | A tick-stamped trace from the opening to the Rooted Tower lit, with the direct player. | Custody goes AtSite, Carried, Restored; no lethal duel. |
| C. Earth, stolen and recovered | The same with the reluctant player: seizure, report, empty pedestal, tally, Tally Vault, one loss, one win, the Ash cut, restoration. | Custody goes AtSite, RivalTransit, AtRivalAltar, Carried, Restored; Garrick dead after the loss; rival +900 at the loss. |
| D. Consequences | loser(6) then a full win; loser(7); one clash; one stalemate; one surrender; one withdrawal; one practice duel; every `ResolveEncounter` sent twice; save and load between every pair of steps of one run. | Six named deaths in order, then the restored ending. The seventh loss gives terminal defeat. Doubles are refused. The reloaded run's final state JSON equals the unbroken run's. |
| E. Restoration orders | All 24 orders on the full pack. | Each reaches the finale at the tower of its fourth crystal and then the restored ending. |
| F. Away play | The absent player on each route. | Mira completes every non-lethal step alone, takes unguarded crystals, never commits a lethal encounter, never restores; each wait is in the journal. |
| G. The two games together | An overrun of your camp mid-route; an Ash camp overrun; demons per minute at 0, 2 and 4 restored and with 2 on altars. | The stated differences appear; no game over in campaign mode. |
| H. Generated seeds | 20 seeds: validation result, the four scripted players, the signature table, the comparison of the two shipped examples, two runs of one seed compared byte for byte. | All validate and complete; the examples differ as 6.4 requires. |
| I. Balance | Per scripted player: minutes to each crystal, first arrivals against thefts, lethal duels fought, total length. | Direct: at least 2 first arrivals, 30 to 50 minutes. Reluctant: 0 first arrivals. |
| J. Known weaknesses | An honest list. | Present. |

Slice 1 delivers A to D and F on the Earth pack and the skeleton pack. Slice 2 adds E, G and I and the other routes. Slice 3 adds H.

### 9.2 Other tests

- EditMode (also under `dotnet test`): one test file per table in section 5; the validator's fixtures; the surface test; opponent measurements.
- PlayMode: `InteriorTests`, `ExpeditionTests`, `CampaignDuelTests`, `CampaignIntegrationTests`, campaign cases in `SaveLoadTests`. Existing `EncounterTests`, `PlayerTests`, `LandViewTests`, `DanceTests` stay green.
- AutoPlay: `earth_first`, `earth_stolen`, then `campaign_full`, `campaign_lost`, `campaign_generated <seed>`. Each logs `[AutoPlay] <name> OK` and quits 0.
- Phone-sized windows: the AutoPlay scripts take `-screen-width 844 -screen-height 390` and a capture at every bubble; a test asserts every reply rectangle is on screen, at least 44 points high and above the touch band or reachable by key.
- Every hand-off states which tests and playthroughs actually ran and which did not.

### 9.3 The owner's play script for Slice 1

Start the game with the Earth test start (the hand-off note gives the exact launch line). Use a window of 844 x 390. Keep H off.

| Step | Do | Expect |
|---|---|---|
| 1 | Wait at the home camp. | Mira walks up and tells the story in four bubbles. Six people stand at her fire. |
| 2 | Tap a companion. | Their name, role and one line. |
| 3 | Answer "Tell me more", then "Lead the way". | She explains the cost of a lost duel, then sets off. You follow by yourself. |
| 4 | Follow to the sawyer, then to the glade. | The sawyer explains the notches. At the stone Mira reads the marks and a slab slides back. |
| 5 | Step onto the stair. | A short fade. You are in the Old Quarry. Mira says how to get out. |
| 6 | Press Tab. | The quarry map with the exit in green and the party on it. |
| 7 | Follow Mira left, then right at the forks. | She reads the wall marks. You reach the Root Chamber. The Deepstone glows on its pedestal. |
| 8 | Watch her take it. Tap Odo, choose "Lead me out". | You walk to the stair by yourself and come up in the glade. |
| 9 | Follow to the Rooted Tower. Answer "Set it". | The tower lights green-gold. Mira and the prince each have a new line. |
| 10 | Press F5, close the game, start it, continue. | You are at the tower, the Deepstone restored, all six alive. |
| 11 | Start the "Ash got there first" test start (the Ash expedition begins one step ahead; in the full game this happens when you do another route first). Answer "Go on ahead, I'll find you" and walk to the prince. After about 5 minutes press Tab. | The map's side text says where Mira went and what she found: the stone, the stair, the empty pedestal, the tally. |
| 12 | Tap "Mira is waiting for you here". Close the map. | You walk to the glade and down. She is at the stair above the Tally Vault, or back in the survey gallery if she has waited more than 90 seconds. |
| 13 | Tap Mira, "I'm with you". Follow her down. | Dolm stands at an Ash altar with the Deepstone on it. |
| 14 | Mira asks. Answer "What happens if we lose?", then "Fight". | She names Garrick. The duel starts with Mira in front of you and Dolm beyond. |
| 15 | Set a Ward. Lose on purpose (let the idle timer cast, or press Esc, "Leave encounter", confirm). | Garrick steps into the stair mouth. You are in the survey gallery. The roster shows five. |
| 16 | Close the game during the next step if you like, and continue. | Garrick is still dead. Nothing else changed. |
| 17 | Go down again. "Fight". Win. | Dolm's Ward shatters. Mira lifts the Deepstone from the altar. The barred door at the back opens. |
| 18 | Leave by the Ash cut. Follow to the Rooted Tower. "Set it". | As step 9, and Mira has a line for Garrick. |
| 19 | Tap Mira at the home camp, "Practice duel". | The practice duel as it is today. Nobody is at risk. |

Tell us: was it clear where Mira was and what she wanted; did the quarry feel like a place or a corridor; was the way out ever unclear; did the loss feel fair and did Garrick's death land; was anything too small to tap.

---

## 10. Risks

1. **Three PRDs touching the same files.** `Game/Encounters`, `Sim/Core/WorldSimFactory.cs`, `Sim/Save/SaveCodec.cs`, `Boot/WorldBootstrap.cs`, `Boot/SaveSystem.cs`, `UI/MapScreen.cs` and the walker are all named by other work. Mitigation: the interface packages make the small shared edits first; N-9 and N-10 fix the split with the characters work; the save version has one owner (N-5).
2. **The faction rules the campaign needs do not exist yet.** Non-fatal overrun and the handover are in `PRD_AGES.md`. Mitigation: Slice 1 runs from a test start that switches both off through tuning already in the state, and the link is built in Slice 2.
3. **Pace.** The numbers in 5.3 are guesses. The race may be trivial or hopeless, and the campaign may be 20 minutes or 90. Mitigation: the proof's balance section measures it before any view exists; every number is a tuning field in the pack.
4. **Duel difficulty with death at stake.** At the practice setting a first-time player loses nearly half their duels, which would burn through six companions. Mitigation: softer keeper profiles, measured in I3; the owner's first playthrough decides.
5. **Mira playing by herself.** If she does too much, the player watches; too little, and leaving her is a punishment. Mitigation: the rule "everything except lethal duels and restorations" is one table and can be moved line by line.
6. **Interiors on the lattice.** Five-metre cells with 60 degree turns make wide caves, not corridors, and a small room is seven cells. They may feel empty or confusing. Mitigation: the quarry is built and played in Slice 1 before any other interior is authored.
7. **No buttons and three replies.** Journal, advice, scouting and leaving all share one tap menu on Mira. It may get crowded. Mitigation: paging by a "More" reply; measured in the phone-sized window from Slice 1.
8. **The touch band.** The bottom 30% of the screen is movement on a phone. Low markers in dungeons would be untappable. Mitigation: CR-45 and a test on reply and marker rectangles.
9. **Generator that only renames.** Recombining authored modules can easily give the same story with shuffled labels. Mitigation: the signature rule of 6.4 is an acceptance check, and the first template must vary the rival plan and the clue graph.
10. **Validator cost and trust.** Simulating 24 orders by several loss counts for every pack could be slow, and a validator that passes a dead end is worse than none. Mitigation: the broken fixtures; the skeleton pack; a 2 s budget in G2.
11. **Save without undo.** One automatic checkpoint means a crash or a bug can cost a companion for good. Mitigation: the F1 tool can revive a companion and set custody; the last five checkpoints are kept as numbered files in developer builds.
12. **Scope.** Seven interiors, four routes, five opponents, two endings and a generator is a large build. Mitigation: the owner's order; each slice ends in something playable; Slice 1 alone proves every hard mechanism once.

---

## 11. Open decisions for the owner

Work proceeds on the recommended default unless you say otherwise.

| # | Decision | Recommended default |
|---|---|---|
| 1 | The names: Oskerry, the Unweaver, the Deepstone, Kilnheart, Tideglass and Skyshard, the four towers, the six companions, Hesk Varr, the keepers, Orrin. | As written in section 4. They are data and can change without code. |
| 2 | Can a companion's death be undone by reloading? | No. In a campaign the game writes one checkpoint at every commit and resolution, and F5 writes the same slot. |
| 3 | The game is closed in the middle of a lethal duel. What happens on Continue? | The duel is staged again from the start with a new seed and no penalty. The other choice is to count it as a surrender. |
| 4 | How much may Mira do without you? | Everything except lethal duels and restorations. She takes an unguarded crystal alone. She waits for you at altars and towers. |
| 5 | How hard are the keepers and the final enemy? | A first-time player should win about 7 in 10 keeper duels and a little over half of finales. Tuned after your first playthrough. |
| 6 | How long is the campaign and how hot is the race? | 30 to 50 minutes. The Ash expedition reaches its first crystal 8 minutes in and needs about 7 minutes for each one after. |
| 7 | Does terminal defeat end the whole game, including the faction game? | Yes. "Begin again" starts a new campaign on a new world. |
| 8 | Does restoring a crystal cost the faction anything (goods, an escort)? | No. The two games are linked by time, overruns and demons only. |
| 9 | May your people wipe out the Ash Clan while its expedition is still in the field? | No. In campaign mode an Ash camp can fall, which stops the expedition for 90 seconds, but the Ash home camp stands until the campaign ends. |
| 10 | Are generated campaigns offered in normal play? | Yes, as "A different telling" after the first ending. Before that only through the developer start. |
| 11 | Is "the Lord of Chaos" kept as a title? | Yes, as what the peoples call the Unweaver. His name on screen in the finale is "the Unweaver, in Hesk Varr". |
| 12 | Do crystals pay victory points? | Yes: 1 to the player's side per restored crystal, 1 to the Ash Clan per crystal first set on its altar (`PRD_AGES` 6.1). Both count toward the roster unlock at 10. |
| 13 | After the restored ending, is the step to the Modern age offered at once? | Yes, as the reply "Onward, thirty winters on"; the player may choose "Begin again" instead. |

---

## 12. Choices I made that you might want to overrule

1. **No flood in the backstory.** The brief's opening explains a flood. That is the reference game's backstory, so the story here is our own: a power under the island, tied down by four knots, hidden in the lean winters.
2. **The demons of the dice are the Unweaver leaking.** This ties the campaign to a rule already in the game, and it lets restorations and altars change how often demons come.
3. **The final enemy speaks through the Ash leader.** This satisfies both "the rival must be faced" and "keep the Lord of Chaos for the final confrontation" with one duel and one body.
4. **The rival leader is a man, Hesk Varr, on the Ash champion's body**, to match the characters plan. The owner names him.
5. **Towers stand on the shore, on rim corners; other sites stand on Meadow hex centres.** Rim corners can never hold a camp and Meadow hexes have no stock, so the campaign never collides with plots, roads or stock patches. The island's terrain is fixed, so authored places are fixed too.
6. **One clock.** The rival runs on world ticks plus fixed charges, not on a separate turn counter. Time with the prince is time the rival uses.
7. **There is no "rest" action.** Nothing in the game needs resting, so the brief's example is not built.
8. **A carried crystal is never lost, and the rival cannot take a crystal in transit back.** Recovery is always at an altar.
9. **Arriving first means no lethal duel for that crystal.** The race is what protects the companions. A very fast player meets only the finale.
10. **Volunteer order and services.** Garrick first, Tansy last; each companion has one small mechanical service. The first deaths cost the least.
11. **Interiors are built far to the east on the same lattice**, not under the island, so the walker, the grid and the map need small additions and no new coordinate system.
12. **Solo Mira never guesses a riddle.** She finds the answer in the world first, which costs time, then answers correctly.
13. **Surrender is the pause menu's "Leave encounter" with a confirmation.** There is no separate surrender control.
14. **The duel keeps its on-screen panel.** It is the one exception to "no buttons", because building a weave needs targets to tap.
15. **Speech bubbles keep three replies.** The brief allowed up to eight choices; the built bubbles have three, and longer lists page with "More".
16. **A data pack is a folder of JSON files**, split by route, so several writers can work at once.
17. **The Slice 1 pack is marked Partial** and starts only from the developer start. A headless skeleton pack with four trivial routes proves the state machine (orders, deaths, finale) before the real routes exist.
18. **The generator runs inside the game too**, with no network, so "A different telling" needs no tool. The command-line tool is the authoring path; a Unity editor menu is not planned.
19. **Only one alternate recovery without a duel is authored** (the cistern sluice, with Nell alive). The format allows one per altar.
20. **The campaign record handed to later ages** holds the restoration order, the survivors and the fallen with places. What later ages do with it is for `PRD_AGES.md`.

---

## Appendix A. Simulation interface (binding once package I0 lands)

C# 9, plain data with public fields, namespace `World.Sim.Campaign` unless noted. Lists are kept in a fixed order so the state JSON is deterministic. Changes after I0 are additive and logged in `Docs/CAMPAIGN_API.md`.

### A.1 Definition

```csharp
public enum Element { Earth = 0, Fire = 1, Water = 2, Air = 3 }
public enum Custody { AtSite = 0, RivalTransit = 1, AtRivalAltar = 2, Carried = 3, Restored = 4 }
public enum PackStatus { Partial = 0, Complete = 1 }
public enum LocationKind { Camp, Site, Tower, Watchtower, Shore }
public enum AnchorKind { HomeCamp = 0, MeadowHexCentre = 1, RimCorner = 2 }
public enum MarkerKind { Exit, Stairs, Door, Pedestal, Altar, Threshold, SafePoint, Inscription, Witness, Lever, DuelCaster, DuelOpponent, Plinth }
public enum SourceKind { Witness, Marker, Companion, Scout, Report, Wizard, Opening }
public enum QuestAction { Arrive, Talk, Examine, Search, Open, Light, Take, Answer, Restore }
public enum EncounterKind { Lethal, Friendly, Practice }

public sealed class CampaignDefinition {
  public int FormatVersion = 1;  public string Id, Title;  public PackStatus Status;
  public string Template = "";  public ulong Seed;  public string GeneratorVersion = "";
  public List<RoleDef> Roles;            // { Id, ActorId }
  public List<CompanionDef> Companions;  // { Id, Name, Role, Line, VolunteerOrder, Service, ActorId }
  public List<LocationDef> Locations;    // { Id, Name, Kind, Anchor { Kind, Q, R, Corner }, Markers[], SafePoint }
  public List<InteriorDef> Interiors;    // { Id, Name, Rows[], Legend: char -> MarkerDef }
  public List<ConnectionDef> Connections;// { Id, A: MarkerRef, B: MarkerRef, OpenWhen }
  public List<CrystalDef> Crystals;      // { Id, Element, Name, Site: MarkerRef, Tower, Underground }
  public List<TowerDef> Towers;          // { Id, Location, Element, Sign, BeamColour }
  public RivalPlanDef Rival;             // { LeaderRole, Steps[] { Crystal, MarchCost, CarryCost, Altar: MarkerRef }, StolenAtStart[] }
  public List<ClueDef> Clues;            // { Id, Source { Kind, Ref }, Requires[], Gives[], Essential, Fallback }
  public List<LeadDef> Leads;            // { Id, Title, Goal { At: MarkerRef, Action }, Requires[], DoneWhen, ReasonLine, Priority, Crystal }
  public List<ChoiceDef> Choices;        // { Id, QuestionLine, Answers[] { Line, Correct }, RevealedBy: MarkerRef, WrongLine }
  public List<EncounterDef> Encounters;  // { Id, Kind, Profile, Threshold, SafePoint, Stage, OnVictory[], Alternate, BoundTo ("" | "last_tower") }
  public List<QuestEventDef> Events;     // { Id, At: MarkerRef, Action, Requires[], Sets[], Effects[], Lines[], Once }
  public List<EndingDef> Endings;        // { Id, Outcome, Lines[], Variants[] }
  public List<LineDef> Lines;            // { Id, Speaker, Text, Guards[] }
  public CampaignTuning Tuning;
}
public struct MarkerRef { public string Location, Interior, Marker; }   // Interior "" = on the surface
public sealed class MarkerDef { public string Id; public MarkerKind Kind; public int I, J; public string To, OpenWhen, Crystal; }

public sealed class CampaignTuning {
  public int PartyTicksPerStep = 7, ScoutTicksPerStep = 4, ReadTicks = 50, SearchTicks = 150, TakeTicks = 50, RestoreTicks = 100;
  public int LeadPatience = 450, SoloDecide = 100, ChoicePatience = 300, SoloStudyTicks = 200, ThresholdWait = 900;
  public int DeclineCooldown = 600, PlanInterval = 20, QueueLength = 3, StaleTicks = 3000, ReportDelay = 300, SearchCost = 300;
  public int DefeatCharge = 900, DrawCharge = 600, DrawChargeWithShield = 300, WrongAnswerCharge = 300;
  public int CampOverrunCharge = 1800, AshCampLostPause = 900;
  public int TideCycle = 2400, TideDry = 900, BoonPercent = 20;
}
```

### A.2 Run state

```csharp
public enum CampaignPhase { Running = 0, Won = 1, Lost = 2 }
public enum PartyMode { Idle, Travelling, Acting, AskingChoice, AtThreshold, InEncounter, WaitingForPlayer }
public enum RivalPhase { Marching, Carrying, Searching, Paused, PlanDone, AtLastTower }
public enum DuelResult { Victory = 0, Defeat = 1, Clash = 2, Stalemate = 3, Surrender = 4 }

public sealed class CampaignState {
  public CampaignDefinition Definition;  public string DefinitionHash;
  public CampaignPhase Phase;  public long StartTick;  public string EndingId;
  public long RivalProgress;  public RivalState Rival;      // { Phase, Step, StepProgress, PausedUntilProgress, At: MarkerRef }
  public CrystalState[] Crystals;                           // by Element: { Custody, At: MarkerRef, SinceTick }
  public Belief[] Beliefs;                                  // by Element: { Custody, At, AsOfTick, Source }
  public PartyState Party;                                  // { Mode, At, PlayerPresent, Leg { From, To, Ticks, Progress, Cells[] }, LeadId, ChoiceId, EncounterId, ModeSinceTick }
  public List<CompanionState> Companions;                   // { Id, Alive, FellAt (encounter id), FellTick }
  public List<string> Facts;                                // sorted
  public List<LeadState> Leads;  public int NextLeadId;     // { Id, DefId, Score, Rank, TopSinceTick }
  public List<DeclinedLead> Declined;
  public AttemptState Attempt;  public int NextAttemptId;   // { Id, EncounterId, Volunteer, Seed, Restarts, CommittedTick }
  public List<AttemptRecord> Resolved;                      // { Id, EncounterId, Result, Volunteer, Tick }
  public ScoutState Scout;                                  // { Out, CompanionId, Target, Leg }
  public List<JournalEntry> Journal;                        // { Tick, Text, Place }
  public List<Element> RestoredOrder;
  public List<string> FiredEvents;                          // quest events with Once
  public int BaseSevensPerDraw;  public Rng Rng;
}
namespace World.Sim { public sealed partial class WorldState { public CampaignState Campaign; } }   // null = no campaign
```

### A.3 Commands (`PlayerId` = 0) and refusals

```csharp
StartCampaign   { CampaignDefinition Definition; }       // validated; refused if one is running
SetCompany      { bool Present; }
AcceptLead      { int LeadId; }       DeclineLead { int LeadId; }
AdviseMira      { string ChoiceId; int Answer; }     // named so it cannot clash with PRD_AGES `Advise`
SuggestScout    { string LocationId; }
CommitEncounter { string EncounterId; }                  WithdrawFromEncounter { string EncounterId; }
ResolveEncounter{ int AttemptId; DuelResult Result; }    RestartAttempt { int AttemptId; }   // on Continue with a pending attempt
ConfirmRestore  { Element Crystal; }
DevSetRivalProgress { long Value; }  DevRivalNextStep { }  DevSetCustody { Element Crystal; Custody To; }
DevSetCompanion { string Id; bool Alive; }               // F1 tools and tests only
```

| Code | Sentence |
|---|---|
| `no_campaign` | No expedition is under way. |
| `campaign_running` | An expedition is already under way. |
| `campaign_over` | The expedition is over. |
| `invalid_definition` | That campaign cannot be played: {first problem}. |
| `unknown_lead` | Mira has moved on from that. |
| `not_at_choice` | Nobody is asking that now. |
| `not_at_threshold` | There is no fight to start here. |
| `scout_out` | Wait for {name} to come back first. |
| `no_scout` | There is nobody left to send. |
| `unknown_attempt` | That duel is not the one under way. |
| `already_resolved` | That duel is already settled. |
| `not_carried` | Mira is not carrying that crystal. |
| `wrong_tower` | This is not its tower. |
| `player_needed` | Mira waits for you before she does that. |

### A.4 Events

`CampaignStarted{DefinitionId}`, `LeadAdded{LeadId, DefId, Rank}`, `LeadsReordered{TopLeadId}`, `LeadRemoved{LeadId, Why}`, `PartySetOut{LeadId, From, To, LegTicks}`, `PartyArrived{At}`, `PartyModeChanged{Mode}`, `QuestEventFired{EventId}`, `FactLearned{Fact, Source}`, `ChoiceOffered{ChoiceId}`, `ChoiceAnswered{ChoiceId, Answer, Correct, ByPlayer}`, `CrystalCustodyChanged{Crystal, From, To, At}`, `BeliefChanged{Crystal}`, `RivalStepStarted{Step, Crystal}`, `RivalReported{Crystal, Kind, At}`, `RivalAdvanced{Amount, Why}`, `RivalPaused{Until}`, `EncounterThreshold{EncounterId, Volunteer}`, `EncounterCommitted{AttemptId, EncounterId, Volunteer, Seed, Profile}`, `EncounterResolved{AttemptId, EncounterId, Result}`, `CompanionFell{CompanionId, EncounterId}`, `PartyRetreated{To, Why}`, `CrystalRestored{Crystal, Tower, Count}`, `ScoutSent{CompanionId, Target}`, `ScoutReported{Crystal}`, `ScoutReturned{}`, `JournalAdded{Text}`, `CampaignEnded{Won, EndingId}`.

### A.5 Read helpers and module

```csharp
public static class CampaignApi {
  bool IsRunning(WorldState s);
  CrystalView Crystal(WorldState s, Element e);            // truth: for views of sites, altars, towers
  BeliefView Belief(WorldState s, Element e);              // what the party thinks, with Stale and the "where to look" sentence
  IReadOnlyList<LeadView> Leads(WorldState s);  LeadView TopLead(WorldState s);
  PartyView Party(WorldState s);                           // mode, place, lead, choice, encounter
  bool PartyPosition(WorldState s, double tickFraction, out MarkerRef from, out MarkerRef to, out double t);
  int[] LegCells(WorldState s);                            // interior leg path as i,j pairs; empty on the surface
  IReadOnlyList<CompanionView> Companions(WorldState s);  CompanionView NextVolunteer(WorldState s);   // null when none
  AttemptView Attempt(WorldState s);                       // null when none pending
  RivalKnown RivalView(WorldState s);                      // reports only, never the truth
  IReadOnlyList<JournalEntry> Journal(WorldState s);  string WhereIsMira(WorldState s);
  string Line(WorldState s, string lineId);                // tokens filled, guards applied; null when guarded out
  IReadOnlyList<string> LinesFor(WorldState s, string speaker, string topic);
  bool IsOpen(WorldState s, string connectionId);  bool CausewayDry(WorldState s);
  CommandResult Check(WorldState s, Command c);
  CampaignRecord Record(WorldState s);                     // for later ages: { EndingId, DefinitionId, Seed, RestoredOrder, EverOnAltar[] (elements that were ever on an Ash altar), Survivors[] (ids), Fallen[] { Id, Name, EncounterId, Place, Tick, Cause (Defeat | Surrender) } }
}
public static class CampaignModule { void Register(WorldSim sim); }   // system order 1250, after Strength (`SystemOrder.Campaign`, listed in PRD_AGES A.1)
public static class CampaignLoader { CampaignDefinition LoadFolder(Func<string, string> readFile, IEnumerable<string> files); string Hash(CampaignDefinition d); }
public static class CampaignValidator { IReadOnlyList<Problem> Validate(CampaignDefinition d); }   // Problem { Code, Sentence, Id, IsError }
public static class InteriorMaps { bool IsFloor(InteriorDef m, int i, int j); int[] Path(InteriorDef m, int i0, int j0, int i1, int j1, Func<string, bool> factKnown); }
namespace World.Sim { public static class Lattice { void CellOf(double x, double z, out int i, out int j); int Distance(int i0, int j0, int i1, int j1); } }

namespace World.Sim.Encounters.Ward {
  public sealed class WardOpponentProfile { public string Id, DisplayName; public int FirstCastMinMs, FirstCastMaxMs, ThinkMinMs, ThinkMaxMs, MistakeRatePercent; public Essence[] Pool; }
  public sealed class WardDuelSpec { public WardTuning Tuning; public Essence[] Pool; public ulong Seed; }
  // WardTuning.For(WardOpponentProfile p, WardTuning baseTuning = null); WardDuelSession.Create(WardDuelSpec spec)
}
// Slice 3:
public static class CampaignGenerator { const string Version; CampaignDefinition Generate(ulong seed, TemplateDef template); }
public static class CampaignSignature { Signature Of(CampaignDefinition d); int Differences(Signature a, Signature b); }
public static class CampaignOutline { string Write(CampaignDefinition d); }
```

### A.6 API change log

(Interface packages append here: date, package, change. The full surface and its later changes are in `Docs/CAMPAIGN_API.md`.)

**14 Nov 2026, I0, Sim half (branch `cry/i0`, tag `cry-I0-sim`; pinned by `Tests/EditMode/Campaign/CampaignSurfaceTests.cs`).** Nothing of A.1 to A.5 was renamed or dropped. I0 lands in two halves: this Sim half, and a Unity half (the B rows marked I0: `EncounterRequest.cs`, `DuelStageSpec.cs` with `VolunteerActorId` and `VolunteerPos`, the `Request` field in `EncounterHost.cs`, the `ward_campaign` registration line, the two installer stubs and their calls in `WorldBootstrap.cs`, the Unity compile check), done after the game layer it depends on; that half takes tag `cry-I0`. `DuelStageSpec` uses `Vector3`, so it is Game-layer and not in this half.

1. **Fixes against the code at `ages-H1-0`.** `Eras.EnterCampaign` is `ErasModule.EnterCampaign`, and `Ledger.Award` is `LedgerLogic.Award(s, faction, key, points, ev)` (`PRD_AGES` A.9 items 1 and 5); 3.9, 5.2 and 5.9 are corrected. `WorldSimFactory.CreateNew` builds its sim through `SaveCodec.CreateSim`, so the one registration line in `WorldSimFactory.cs` covers new games only: **a loaded save registers the campaign only once `SaveCodec.CreateSim` also calls `Campaign.CampaignModule.Register(sim)`** (after `LedgerModule`). `Sim/Save/**` belongs to `PRD_AGES`, so that one line is requested of its next interface package; `CampaignModule.Register` is idempotent, so both lines can stay. Until then a test that loads a campaign save calls `CampaignModule.Register(sim)` itself.
2. **Ownership inside `Sim/Campaign`.** The files directly in `Sim/Campaign/` are the binding surface (I0, then J0 and G0, additive only). Every definition and state class is `partial`, so I1 and I2 add fields from their own folders. I0 seeded three files that pass to their owners at `cry-I0-sim`: `Runtime/CampaignRuntime.cs` (I1: the command checks and effects and the tick, which `CampaignModule` forwards to), `Definition/CampaignLoader.cs` (I2), `Validation/CampaignValidator.cs` (I2).
3. **Ward.** I0 created `WardOpponentProfile.cs`, `WardDuelSpec.cs`, `WardTuning.For` and `WardDuelSession.Create` with the A.5 shapes (`For` copies the base, default `Practice()`, and writes the profile into the `Mira*` fields; `Create` is the constructor with practice fallbacks). They pass to I3 at `cry-I0-sim`; I3 adds the profile file and the measurements.
4. **A.1 additions.** The parts the comments named are classes: `AnchorDef`, `RivalStepDef`, `ClueSourceDef`, `GoalDef`, `AnswerDef`, `EffectDef`, `DuelStageDef` (the data stage: `Caster`, `Opponent`, `Volunteer` markers), `EndingVariantDef`. New: `EffectKind` (`SetFact`, `TakeCrystal`, `OfferChoice`, `Journal`, `PlaceParty`, `RivalCharge`, `SetBelief`), `SurvivorBand` (`All`, `Some`, `None`), `CompanionServices` (service ids), `EncounterBindings.LastTower`, `CampaignDefinition.CurrentFormatVersion` and id lookups. Fields added: `MarkerDef.Name`, `RoleDef.Name`, `EncounterDef.Name`, `OpponentActorId`, `OpponentName`, `ChoiceDef.Sets` (facts on a right answer), `QuestEventDef.Guards`, `LineDef.Topic`. `InteriorDef.Legend` is `SortedDictionary<char, MarkerDef>`. `EndingDef.Outcome` is a `CampaignPhase`. `MarkerRef` has a text form: `location/marker` on the surface, `location/interior/marker` inside.
5. **A.2 additions.** `FallCause`, `RivalReportKind`, `RivalReport { Crystal, Kind, At, Tick, DueTick }`; `CampaignState.Reports` (where the delayed reports wait; `RivalView` shows those whose `DueTick` has passed) and `CampaignState.EverOnAltar`; `CompanionState.Cause`. `PartyState.LeadId` is an int (-1 = none).
6. **A.3 additions.** Refusal `unknown_place` "Mira does not know that place." (`SuggestScout`). `scout_out` also refuses `CommitEncounter` while the scout is out (5.5 "Wait for Tansy"). A late `ResolveEncounter` for a settled attempt is `already_resolved` even after the run has ended. `DuelResults.TryFrom(WardOutcome, out DuelResult)` maps `Forfeited` to `Surrender`.
7. **A.4 additions.** `CampaignEnded.Record`; `RivalCharges` (the `Why` codes of `RivalAdvanced`).
8. **A.5 additions.** The view classes (`CrystalView`, `BeliefView`, `LeadView`, `PartyView`, `CompanionView`, `AttemptView`, `RivalKnown`, `FallenRecord`), `Problem` and `ProblemCodes` (the 6.5 codes plus `missing`), `HistoryState.CampaignRecord` (the field the ages code copies the record into), `CampaignGuards` and `CampaignTokens` (the guard and token grammar of 6.1, fixed here so the runtime, validator and writers agree), `CampaignLoader.CanonicalJson`, `InteriorMaps.Markers`, `MarkerAt`, `TryFind`, `FloorCells`, `Steps`, and `Lattice.Step`, `X`, `Z`. `CausewayDry` is true for the first `TideDry` ticks of every `TideCycle`, counted from the campaign's start.
9. **Real now:** every refusal of A.3; `StartCampaign` (run state from the definition, crystals stolen at the start and their points, opening clues, campaign mode, the tower and altar landmarks of appendix C); `SetCompany`; `CommitEncounter` and the attempt; `ResolveEncounter`'s once-only record; `RestartAttempt`; the expedition clock's +1 per tick; the custody step with its ledger points; the dev commands except `DevRivalNextStep`; all read helpers; `InteriorMaps`; `Lattice`; the definition hash. **"I0 STUB"** (accepted, no effect, for I1): `AcceptLead`, `DeclineLead`, `AdviseMira`, `SuggestScout`, `WithdrawFromEncounter`, `ConfirmRestore`, `DevRivalNextStep`, the outcome table after a resolution, and everything else the tick does in 5.1 to 5.9. The loader is a plain merge (I2 replaces it); the validator checks only the version and missing parts (I2).

**14 Nov 2026, I1 (branch `cry/i1`, from `cry-I0-sim`).** The campaign runtime: every "I0 STUB" of item 9 is real (rules in `Docs/CAMPAIGN_API.md`, "Runtime (I1)"). Additive surface changes only, nothing renamed: `CampaignApi.Leads` shows the runtime's own leads (ids starting with `@`: `@take:`, `@recover:`, `@restore:`, `@clue:`, `@choice:`, made only where the pack has no lead with that goal) with a title and `LeadDef.ReasonText` (new, not serialized); `CampaignApi.Record` names the place a companion fell from `CompanionState.FellPlace` (new) when set; new partial run-state fields and the types `RoutePurpose`, `SpentLead`, `OpenChoice` (listed in the API change log). Decisions where this PRD was silent: the threshold wait applies with the player present too; an interior's exit and stair markers are one-way portals (the way in is the surface side's own marker or connection); a lead acted on without result is spent until the party learns or achieves something; seeing a pedestal sets the party's belief about its crystal to the truth; a report never overrides what the party has seen for itself since; alone, Mira waits at each tower once until the player is back, and after withdrawing with nothing else open she holds at the threshold with no time limit; the fourth crystal's `ConfirmRestore` is refused with `not_at_threshold` while the finale holds; withdrawals and the overrun retreat are walked, duel outcomes place the party at once; a crystal in rival transit is "at" its site; the Kilnheart's boon waits for `EconomyTuning.CycleBonusPermille` (`PRD_AGES` H1-2); the tide does not yet gate routes over the causeway (J1 with the Water pack).

---

## Appendix B. Unity-side interface (namespace `World.Game`)

| Name | Owner | Members others use |
|---|---|---|
| `EncounterContext.Request` | I0 | `object Request` set by `EncounterDirectory.TryStart(string id, Npc source, object request)`. Null = behaviour as today. |
| `WardDuelRequest` | I0 | `EncounterKind Mode`; `int AttemptId`; `string EncounterId`; `string ProfileId`; `ulong Seed`; `string VolunteerName`; `string OpponentName`; `DuelStageSpec Stage`. |
| `DuelStageSpec` | I0 (N-9) | `string CasterActorId` (null = the anonymous player wizard), `OpponentActorId`, `VolunteerActorId` (null = none); `Vector3 CasterPos, OpponentPos, VolunteerPos`. |
| `CampaignDuel` (static) | I7 | `bool Start(EncounterCommitted ev)`; `event Action<int, DuelResult> Resolved`; registers the encounter id `ward_campaign`. |
| `InteriorSpace` (static) | I5 | `Cell Origin(int interiorIndex)`; `Vector3 ToWorld(string interiorId, int i, int j)`; `bool TryLocal(Cell world, out string interiorId, out int i, out int j)`. |
| `Interiors` (static) | I5 | `string Current` (null on the surface); `Enter(MarkerRef to)`; `Exit()`; `LeadOut()`; `event Action<string> Changed`; `Build(CampaignDefinition)`; `RebuildFromState()`. |
| `WalkGrid` additions | I5 | `AddOpenRegion(string owner, IEnumerable<Cell> floor, float floorY)`; `RemoveOpenRegions(string owner)`; `SetCellBlocked(string owner, Cell c, bool blocked)` (doors, the flooded causeway). |
| `MapScreen` additions | I5 | `Mode` (`Island`, `Interior`); `AddLayer(IMapLayer layer)`; `IMapLayer { void Draw(MapScreenView view); bool Tap(Vector3 world); }`. |
| `ExpeditionDirector` | I6 | `Instance`; `Npc Mira`; `IReadOnlyList<Npc> Companions`; `Phase`; `Said` (every line: tick, speaker, text); `RestoreScene(int step)`. |
| `SiteViewRegistry` (static) | J0 | `Register(string viewKind, Func<LocationDef, Transform, ISiteView> factory)`; `ISiteView { void Sync(WorldState s); }`. |
| `SaveSystem` additions | I8 | `bool WriteCheckpoint(string why)` (allowed during an encounter once its attempt is resolved); extras `interior`, `scene.expedition`; named slots `main`, `auto.chapter`, `ckpt.<mission>.<n>`. |
| `CampaignStart` (static) | I8 | `Begin(string packFolderOrSeed)`; command line `-campaign <folder>` or `-campaign seed:<n>`; test starts `-campaignTest earth` and `-campaignTest earth_stolen` (the rival's progress starts at its first seizure). |
| Installers | I0 | `InteriorsInstaller.Install(boot)`, `ExpeditionInstaller.Install(boot)`, called after `PrinceInstaller` and before `DevToolsInstaller`. |

Rules kept from the refactor: views rebuild from state after each `GameClock.Ticked`; nothing in `World.Game` writes `WorldState`; people never block the lattice; no text on screen in normal play except speech bubbles and inspect labels.

---

## Appendix C. Authored places

The island's terrain is fixed (`BoardGenerator.Rows`), so the authored campaign uses fixed places. Hex coordinates are (q, r) as in `BoardGenerator`. Every site sits on a Meadow hex centre and keeps within 9 m of it, which stays clear of every plot (the nearest plot edge is 10 m from a hex centre) and of every main road (hex edges are 52 m away). Towers sit on rim corners, where no camp may ever stand. Package I2 pins each anchor with a test (Meadow terrain, a lattice cell, walkable, not shared) and may move one to the nearest legal place, recording the change.

| Place | Anchor | Why there |
|---|---|---|
| Stonemark Glade (Earth entrance) | Meadow hex (0, -1) | An inland meadow between the home forest (1, -1) and the forest at (-1, -1); a short walk from the home camp. |
| The Old Quarry, upper and lower levels | Interiors | Under the glade. |
| The Ash cut (second quarry exit) | Meadow hex (-2, -2) | West of the glade, towards the Ash Clan. |
| The Cold Kiln (Fire site) | Meadow hex (4, -1) | East shore, beside the clay hills at (3, -1). |
| The Black Forge (Fire altar) | The west-rim Meadow hex nearest the Ash Clan's start corner | Ash land. |
| The Low Causeway and tide cellar (Water site) | Meadow hex (1, -4) | South shore. |
| The Old Cistern (Water altar) | Meadow hex (2, -3) | Inland from the south shore. |
| Gannet Watch (the wizard) | Meadow hex (2, 2) | Seaward of the mountain hexes (1, 2) and (2, 1). |
| The Bare Shoulder (Air site) | Meadow hex (3, 1) | Beside the mountain hex (2, 1). |
| The Cliff Stair (Air altar) | Meadow hex (1, 3) | North shore under the mountain hex (1, 2). |
| The Rooted, Beacon, Tide and Whistling Towers | The rim corner nearest the south-west, east, south and north bearing | Four quarters of the shore, visible from far off. |
| Old Marl's landing, Hod's smithy | Meadow hex (0, -4); the home camp | Witnesses near their routes. |

Sites give the terrain builder a clearing, as the camp does through `CampLayout.Clearings`.

**Landmark ids for `PRD_AGES`.** When a campaign starts, `StartCampaign` registers these through `LandmarkApi.Register`, from the places above. In skirmish they do not exist.

| Landmark id | Place |
|---|---|
| `lm.tower.earth`, `.fire`, `.water`, `.air` | The Rooted, Beacon, Tide and Whistling Towers (rim corners) |
| `lm.altar.earth` | The Ash cut hex, entrance to the Tally Vault |
| `lm.altar.fire` | The Black Forge |
| `lm.altar.water` | The Old Cistern |
| `lm.altar.air` | The Cliff Stair |

The location id of a tower is `tower_<element>`; the landmark id is `lm.tower.<element>`. Package I2 adds a test (`LandmarkCollisionTests`, run after `PRD_AGES` H1-2 has merged) that fails if the harbour, relay or district landmark of `PRD_AGES` lands on any of these corners or hexes.

---

## Appendix D. Route steps (authored campaign)

Each row is one lead or quest event. "Fact" is what the party knows afterwards.

**Earth (Slice 1).**

| # | Where | What happens | Fact |
|---|---|---|---|
| 1 | Home camp | Opening. Mira: the Deepstone lies under old workings and the foresters' marks point the way. | `earth.marks_heard` |
| 2 | The sawyer (fallback: marker posts at the forest edge, `SearchTicks` each; Odo: at once) | The notches lead to the stone in the glade between the two woods. | `earth.glade_known` |
| 3 | Stonemark Glade, the boundary stone | Mira reads the survey marks: three cuts and a ring. The ring-marked slab slides back. | `earth.stair_open` |
| 4 | Old Quarry, entry hall | First fork. Left is a collapsed adit (dead end, legible on the map). Right is the survey gallery. | - |
| 5 | Survey gallery, wall marks | Double-cut marks show the branch to the Root Chamber. | `earth.root_way_known` |
| 6a | Root Chamber, crystal present | Mira takes the Deepstone. The pedestal inscription names the Rooted Tower. | custody Carried, `tower.earth_known` |
| 6b | Root Chamber, crystal gone | Empty pedestal. An Ash tally stick lies beside it. | `earth.stolen_seen`, `earth.tally_read` |
| 7 | Stair to the lower level | The tally counts the turnings to the Tally Vault. | `earth.vault_known` |
| 8 | Tally Vault threshold | Dolm at the Ash altar. Lethal encounter `keeper_earth`. Safe point: the survey gallery. | - |
| 9 | Tally Vault, after victory | Mira takes the Deepstone from the altar. Dolm's tally names the tower. The barred door to the Ash cut opens. | custody Carried, `tower.earth_known`, `earth.ash_cut_open` |
| 10 | The Rooted Tower | "Set it". | custody Restored |

Optional: with Tansy alive Mira may send her round the hill; she finds fresh spoil at the Ash cut and it appears on the map as a barred way in.

**Fire, Water, Air (Slice 2).** Same shape. Their puzzles, in our own words:

| Route | The puzzle | The answer and where it is found |
|---|---|---|
| Fire | "A kiln is a throat. What do you give it first?" Three steps, asked one at a time: open the low draught door, lay the charcoal bed, light from the top. | Hod says it. The three draught doors are notched one, two, three. A wrong step smokes the party out (+300). |
| Water | Old Marl: "I come twice a day and never stay. I take the road and give it back. When am I gone?" | "When the third ring is dry." The flood marker by the landing has three rings. |
| Air | Orrin: "Three winds cross this ridge. Name the one the gannets ride home." | Carved over his own door: "Home on the Westerly." A right answer teaches that a wind marker's notch faces where the wind is going. |


---

## Reconciliation, 14 Nov 2026

Made after `PRD_AGES.md` and `PRD_CHARACTERS.md` were written. Minimal edits; no owner decision changed.

| Where | Change | Why |
|---|---|---|
| Header, 3.9 | N-1 to N-6 now carry the answers of `PRD_AGES` section 16; test starts move onto `dev.historic` after H1. | The ages PRD now exists. |
| 3.9 N-9, N-10; 5.12; I0 and I7 rows; appendix B | One owner per file in `Game/Encounters`, handed over by tag `cry-I7` to `PRD_CHARACTERS` WP-9. I0 creates `DuelStageSpec.cs` (plus `VolunteerActorId`, `VolunteerPos`) and registers `ward_campaign`. `ward_champion` is a Friendly preset of the campaign duel. | Both PRDs claimed `WardDuelEncounter.cs`, `WardDuelStage.cs` and `EncounterInstaller.cs`. Mira as caster is stage work, so I7 needs the stage first. |
| I0 row, section 8 intro | I0 no longer edits `Sim/Save/**`; the save version and the `Campaign` slot belong to `PRD_AGES` H1-0. I0 and all branches start from tag `ages-H1-0` on `refactor-done`. `CampaignState` is versioned inside itself. | Two packages owned the save codec and the version. |
| Package notes after 8.1 | `Lattice.cs` hands over to `PRD_AGES` M1 at `cry-I0`; I5 owns `MapScreen.cs` and `IMapLayer`; I6 and Mira's dialogue, clearings; save slots named; keepers and companions are placeholders until the characters work delivers. | Two packages created `Sim/Board/Lattice.cs`; ages map taps and fog need `IMapLayer`; ages needs more than one save slot. |
| A.3 | `Advise` renamed `AdviseMira`. | `PRD_AGES` has a different `Advise` command. |
| 4.3, 5.9 | Mira's opening happens when `CampaignMayStart` is true (Historic accepted), not "the night the prince first manages". Start switches campaign mode on. | The handover (Prehistoric) and the Historic step are two moments. |
| 5.1, 5.9 | A looted home camp costs the same +1800 as a lost camp. | `PRD_AGES` 4.3 loots the home camp instead of capturing it. |
| 5.2 | Restoration pays the player 1 point; the first arrival at an Ash altar pays the Ash Clan 1 point. | `PRD_AGES` 6.1 assumed it; this PRD was silent. Added decision 12. |
| 5.5 | Mira's leg speed (7 m/s) is shown with a `Run` clip. | `PRD_CHARACTERS` clips stopped at 3.2 m/s. |
| 5.6 | Resolution is sent at the duel's end event; the 1.5 s presentation hold delays only the screen. | The two PRDs described the end of a duel differently. |
| 5.7 | The writable tuning fields are listed (adds `CarrierTicksPerStep`, `CycleBonusPermille`) and boons apply from the saved base. | `PRD_AGES` allowed only two fields; the Fire and Air boons need two more. |
| 5.8 | After the restored ending the first reply is "Onward, thirty winters on". | Both PRDs ended the campaign with their own bubble. Added decision 13. |
| Registration files | `Sim/Core/WorldSimFactory.cs`, `Boot/WorldBootstrap.cs`, `Encounters/EncounterInstaller.cs` (until `cry-I7`) and `Game/UI/Hud.cs` take one-line additive registrations from any package; a merge conflict there is resolved by keeping both lines. Nobody rewrites them. | Several packages of all three PRDs need one line each in those files. |
| A.5, appendix C | `CampaignRecord` fields fixed (including `EverOnAltar`); tower and altar landmark ids defined; a collision test with the ages landmarks. | `PRD_AGES` section 16 asked for both. |
