# Campaign data format (version 1)

How to write a crystal campaign as data (`Docs/PRD_CRYSTALS.md` sections 5 and 6). One pack, read by `CampaignLoader`, checked by `CampaignValidator`, played by the campaign runtime. Authored and generated campaigns use this same format. Written by package I2 on 14 Nov 2026; the C# shapes are `Assets/World/Sim/Campaign/CampaignDefinition.cs` (appendix A.1) plus `Definition/DefinitionFormat.cs`.

Everything in a pack is original: names, places, riddles, lines. Lines are plain and short, and every conversation fits a speech bubble with at most three replies.

## 1. A pack is a folder of JSON files

```
Assets/StreamingAssets/world/campaigns/<pack id>/
  campaign.json      identity, roles, companions, crystals, towers, rival plan, tuning
  home.json          the home camp
  towers.json        the four tower locations
  earth.json         Earth: locations, interiors, connections, clues, leads, encounters, events
  lines.json         shared lines (opening, replies, companions, tower signs, the prince)
  lines_earth.json   Earth's lines
  fire.json, lines_fire.json, ...  one pair per route (Slice 2)
  finale.json, lines_state.json    finale, endings, lines by restored count (Slice 2)
```

- Each file is one JSON object. Its keys are top-level parts of the definition: `FormatVersion`, `Id`, `Title`, `Status`, `Template`, `Seed`, `GeneratorVersion`, `Roles`, `Companions`, `Locations`, `Interiors`, `Connections`, `Crystals`, `Towers`, `Rival`, `Clues`, `Leads`, `Choices`, `Encounters`, `Events`, `Endings`, `Lines`, `Tuning`. Spell them exactly so.
- **List parts** (`Roles` ... `Lines`) may appear in any number of files; the lists are appended in file order. That is what lets several writers work at once: each route has its own files.
- **Single parts** (`Id`, `Title`, `Status`, `Rival`, `Tuning`, ...) may be set by one file only, normally `campaign.json`.
- **File order**: `campaign.json` first, then every other `.json` file by name (ordinal). Unity `.meta` files are ignored. `CampaignLoader.PackOrder` gives the order and `CampaignLoader.LoadDirectory(folder)` reads a folder on disk; `CampaignLoader.LoadFolder(readFile, files)` reads through any file reader (Android's StreamingAssets).
- **Strict reading.** An unknown key, a wrong type, an enum by number, an unknown enum name, a duplicate key or a bad marker reference stops the load with a `FormatException` whose message reads `file at path (line n, column m): reason`, for example `earth.json at Locations[0].Kind (line 3, column 37): Error converting value "Glade" ...`. `CampaignLoadError.Of(exception)` returns the parts (`File`, `JsonPath`, `Line`, `Column`, `Reason`). Comments (`//`) are allowed.
- Enums are written by name: `"Kind": "Site"`. Fields left out keep their starting values (so `"Tuning": { "StaleTicks": 2400 }` changes one number).
- The **definition hash** (`CampaignLoader.Hash`) is SHA-256 of the canonical JSON of the merged definition. The same files always give the same hash; the run saves the whole definition and its hash, so a later edit of the pack never changes a run in progress.

## 2. Identity and status

```json
{ "FormatVersion": 1, "Id": "authored_v1", "Title": "The Four Knots of Oskerry", "Status": "Partial",
  "Template": "", "Seed": 0, "GeneratorVersion": "" }
```

`Status` is `Complete` or `Partial`. Normal play starts only `Complete` packs. A `Partial` pack is under construction: it starts from the developer start, and the validator's story checks (section 10, "Routes") only warn on it. Every other check is an error on every pack.

## 3. Ids and references

Every part has a stable `Id` (lower case, `a-z`, digits, `_`; facts and ids may use `.`). Ids are unique within their list; roles and companions share one namespace because a line's `Speaker` names either. Markers are unique within their location or interior.

A **marker reference** is text:

| Form | Means |
|---|---|
| `glade/stone` | the marker `stone` on the surface round the location `glade` |
| `glade/quarry_upper/pedestal` | the marker `pedestal` inside the interior `quarry_upper`, filed under the location `glade` |
| `""` | nothing (only where a field may be empty) |

The object form `{ "Location": "glade", "Interior": "", "Marker": "stone" }` is also read. An interior reference names the interior's own `Location` (section 5); the validator warns (`interior_location`) when it names another, so every reference to one marker is the same text.

A **fact** is a free name such as `earth.stair_open`. Facts are only ever added. Convention: `<crystal>.<what>` for a route's facts, `tower.<crystal>_known` when the party knows the right tower.

## 4. People

```json
"Roles": [ { "Id": "mira", "ActorId": "mira", "Name": "Mira" }, ... ],
"Companions": [ { "Id": "garrick", "Name": "Garrick Holt", "Role": "Shield-bearer, an old road guard",
                  "Line": "companion_garrick_line", "VolunteerOrder": 1, "Service": "shield", "ActorId": "companion_garrick" }, ... ]
```

- **Roles.** Five are required and bound to an actor: `mira`, `prince`, `rival_leader`, `final_enemy`, `wizard`. Any other speaker who cannot die is a role too: witnesses (`sawyer`, `smith`, `ferryman`) and the keepers (`keeper_earth` ...). `Name` is what tokens and the map print.
- **Companions.** Exactly six, volunteer orders 1 to 6 each once. `Line` is their introduction line (spoken by them, guarded `alive`). `Service` is one of `shield`, `lamp`, `fire`, `water`, `earth`, `scout` or `""` (`CompanionServices`). Companions are the only mortal speakers.

## 5. Places

### Locations on the island

```json
{ "Id": "glade", "Name": "the Stonemark Glade", "Kind": "Site",
  "Anchor": { "Kind": "MeadowHexCentre", "Q": 0, "R": -1 }, "SafePoint": "safe",
  "Markers": [ { "Id": "stone", "Kind": "Inscription", "Name": "the boundary stone", "I": 0, "J": 0 },
               { "Id": "stair", "Kind": "Stairs", "Name": "the hidden stair", "I": 0, "J": 1,
                 "To": "glade/quarry_upper/exit", "OpenWhen": "earth.stair_open" } ] }
```

- `Kind`: `Camp`, `Site`, `Tower`, `Watchtower`, `Shore`.
- **Anchors** (appendix C; `CampaignPlaces` computes them):
  - `HomeCamp`: the player's start corner. Exactly one location (Kind `Camp`) has it; it is the party's base. Its markers' `I, J` are hints; the game's camp layout places the people there.
  - `MeadowHexCentre` with `Q, R`: the centre of a Meadow hex (no number, no stock, the same on every seed). One location per hex. Every marker keeps within 9 m of the centre: offsets `(0,0)`, the six neighbours (5 m) and `(1,1) (2,-1) (1,-2) (-1,-1) (-2,1) (-1,2)` (8.7 m).
  - `RimCorner` with `Corner`: a shore corner (fewer than three hexes), never a start corner, one location per corner. `Corner: -1` means the appendix C bearing rule for the tower standing there. Markers should stand on the island (off-shore is a warning).
- **Marker offsets** `I, J` are steps of the 5 m lattice from the anchor cell. The six steps are `(0,1)` north, `(1,0)` north-east, `(1,-1)` south-east, `(0,-1)` south, `(-1,0)` south-west, `(-1,1)` north-west. No marker may stand on a plot (the validator checks every plot of the island) or share a hex or corner with a landmark of the ages (harbour, hearths, dance circle).
- `SafePoint` names the marker where a retreat puts the party.

### Authored places (appendix C, pinned by `AppendixCPlacesTests`)

| Place | Location id | Anchor |
|---|---|---|
| The home camp | `home` | HomeCamp (corner 70) |
| Stonemark Glade | `glade` | Meadow (0, -1) |
| The Ash cut | `ash_cut` | Meadow (-2, -2) |
| The Cold Kiln | `kiln` | Meadow (4, -1) |
| The Black Forge | `forge` | Meadow (-4, 2) (the west-rim Meadow hex nearest the Ash start; ties by hex id) |
| The Low Causeway | `causeway` | Meadow (1, -4) |
| The Old Cistern | `cistern` | Meadow (2, -3) |
| The Bare Shoulder | `shoulder` | Meadow (3, 1) |
| The Cliff Stair | `cliff` | Meadow (1, 3) |
| Gannet Watch, Old Marl's landing | (Slice 2) | Meadow (2, 2), (0, -4) |
| The Rooted, Beacon, Tide, Whistling Towers | `tower_earth` ... `tower_air` | RimCorner 17, 74, 2, 147 |

Each tower corner touches one hex, and the line from the corner to that hex's centre is a lattice axis: earth `(1,0)`, fire `(-1,1)`, water `(0,1)`, air `(0,-1)`. The tower markers stand along it: `door` 1 step in, `opponent` (DuelOpponent, the steps) 2, `caster` (DuelCaster) 3, `safe` 6.

### Interiors

```json
{ "Id": "tally_vault", "Name": "the Tally Vault", "Location": "ash_cut",
  "Rows": [ "############",
            "#U.#########",
            ...
            "##H.C.######",
            "##..K.BX####",
            "##..A.######",
            "############" ],
  "Legend": { "U": { "Id": "stair_up", "Kind": "Stairs", "Name": "the stair up", "To": "glade/quarry_upper/stair_down" },
              "B": { "Id": "bar", "Kind": "Door", "Name": "the barred door", "OpenWhen": "earth.ash_cut_open" }, ... } }
```

- One character per lattice cell: `#` rock, `.` floor, a legend character is a floor cell holding that marker (each character used once; the loader fills the marker's `I, J`). Column is `i`, row is `j`, so in the text a cell's neighbours are up, down, left, right, **up-right and down-left** (not up-left or down-right). Two cells touching only up-right/down-left are joined; mind that when walling rooms apart.
- At most 16 by 16 cells and 80 floor cells. All floor connected (doors counted open).
- `Location` is the location the interior is filed under; every reference into it uses it. For an altar's interior this is the location of the altar's way in, because the altar landmark `lm.altar.<element>` is placed at that location's hex.
- Marker kinds: `Exit` (to the surface), `Stairs` (to another interior or the surface), `Door` (a cell passable once `OpenWhen` is known; with `To` it is also a way through), `Pedestal`, `Altar` (both with `Crystal`), `Threshold`, `SafePoint`, `Inscription`, `Witness`, `Lever`, `DuelCaster`, `DuelOpponent`, `Plinth`.
- **Links.** An `Exit`, `Stairs` (or `Door` with `To`) marker leads to the marker its `To` names, and that marker must lead back (`To` naming the first). A link is open when every `OpenWhen` of its two markers and of any `Connection` joining them is known. Stepping onto a link cell takes the party (and the player, with a fade) to the other end.

### Connections

```json
{ "Id": "glade_stair", "A": "glade/stair", "B": "glade/quarry_upper/exit", "OpenWhen": "earth.stair_open" }
```

A connection names a link (its two markers must lead to each other) so the game and `CampaignApi.IsOpen` can ask about it by id, and may add the fact that opens it. An opened link stays open: it is a fact in the save.

## 6. Crystals, towers and the rival

```json
"Crystals": [ { "Id": "earth", "Element": "Earth", "Name": "the Deepstone", "Site": "glade/quarry_upper/pedestal",
                "Tower": "tower_earth", "Underground": true } ],
"Towers": [ { "Id": "tower_earth", "Location": "tower_earth", "Element": "Earth", "Sign": "tower_earth_sign", "BeamColour": "#A8B83C" } ],
"Rival": { "LeaderRole": "rival_leader", "StolenAtStart": [], "OpenWith": [ "earth" ],
           "Steps": [ { "Crystal": "earth", "MarchCost": 2250, "MarchSpread": 600, "CarryCost": 900, "Altar": "ash_cut/tally_vault/altar" } ] }
```

- Four crystals, one per element. `Site` is a `Pedestal` marker. `Underground` is true exactly when the site is inside an interior; at least one crystal is.
- Each crystal has its own tower of the same element. A tower's location (Kind `Tower`) has a `DuelCaster`, a `DuelOpponent` and a `SafePoint` marker, so any tower can stage the finale. `Sign` is the door-sign line.
- The rival plan is an ordered list of steps; each names a crystal once, positive costs, and an `Altar` marker for that crystal. `StolenAtStart` crystals begin on their step's altar.
- **The per-run draw (race tuning, 14 Nov 2026; both optional).** `MarchSpread` (default 0) makes a step's march vary by seed, drawn uniformly in `MarchCost` - `MarchSpread` to `MarchCost` + `MarchSpread`; it must be smaller than `MarchCost`. `OpenWith` (default empty = the plan's own order) lists crystals the expedition may open with: one is drawn per seed and its step moved to the front, while the march costs and spreads stay with the positions (the first position's march is always the opening march). Every `OpenWith` crystal needs a step. The authored pack: Earth 2250 +- 600 (2:45 to 4:45, was 4800), the others 3000 +- 600 (was 3600), carry 900, `OpenWith` `[ "earth" ]` (the other routes are not written yet).

## 7. The story: clues, leads, choices, events, encounters

### How the runtime and the validator read a story

- **Mira goes only where a lead sends her.** A lead is open when its `Requires` facts are known and its `DoneWhen` fact is not. At the goal marker she does the goal's `Action` (`Arrive`, `Talk`, `Examine`, `Search`, `Open`, `Light`, `Take`, `Answer`, `Restore`). Every marker the story needs (a witness, a pedestal, a threshold, a tower door) must be the goal of some lead.
- **Quest events** fire when the party does the event's action at its marker (an `Arrive` event also fires when the party passes through or arrives at the marker by any leg), its `Requires` facts are known and its `Guards` hold. They set `Sets` facts, apply `Effects`, and speak `Lines` in order. `Once` (default true) fires an event at most once.
- **Clues** give facts. A clue is given when its `Requires` are known and its source is at hand: `Opening` at the start; `Witness`, `Marker` or `Wizard` (a marker reference) when the party visits that marker (a role id as `Ref` = that person's knowledge, given at once); `Companion` at once while that companion lives; `Scout` when the scout returns from that place; `Report` when the rival's theft of that crystal is reported.
- **Taking a crystal** is an event at the pedestal with Action `Take`, guard `custody: <id> = AtSite` and effect `TakeCrystal`. Its stolen twin is an event at the same marker and action with guard `custody: <id> = AtRivalAltar` (and another for `RivalTransit`) that sets the trail's first fact.
- **A keeper** is a `Lethal` encounter whose `OnVictory` takes the crystal (`TakeCrystal`). The party reaches its `Threshold` through a lead; the threshold is live while the crystal is on the altar. An alternate recovery is an event that takes the crystal under `custody: <id> = AtRivalAltar` (name it in the encounter's `Alternate`).
- **Restoring** is a lead to the tower's door with Action `Restore` (it needs the party to carry the crystal and to know the tower), and an event there with Action `Restore` that sets `<crystal>.restored`.

### Clues

```json
{ "Id": "earth_sawyer", "Source": { "Kind": "Witness", "Ref": "home/sawyer" }, "Requires": [ "earth.marks_heard" ],
  "Gives": [ "earth.glade_known" ], "Essential": true, "Fallback": "earth_posts" }
```

An **essential** clue whose source is a companion must name a fallback whose source is not; an essential clue's fallback is never a companion's. Beyond that, every crystal must be findable, and recoverable, with no companion alive (section 10).

### Leads

```json
{ "Id": "earth_stone", "Title": "The boundary stone", "Goal": { "At": "glade/stone", "Action": "Examine" },
  "Requires": [ "earth.glade_known" ], "DoneWhen": "earth.stair_open", "ReasonLine": "mira_reason_stone",
  "Priority": 60, "Crystal": "earth" }
```

`ReasonLine` is Mira's one-sentence reason (5.5). `Priority` is the base score of 5.4. Leads have no guards: when a lead must depend on a living companion, give it a fact that only that companion's clue gives.

### Choices

```json
{ "Id": "air_test", "QuestionLine": "orrin_question", "RevealedBy": "watch/door", "WrongLine": "orrin_wrong",
  "Sets": [ "air.markers_known" ], "Answers": [ { "Line": "answer_east" }, { "Line": "answer_west", "Correct": true } ] }
```

One to three answers, exactly one correct; answers are reply lines of at most 32 characters. An event offers a choice with the effect `OfferChoice`. `RevealedBy` is the marker Mira studies when the player is away.

### Encounters

```json
{ "Id": "keeper_earth", "Name": "Dolm at the Ash altar", "Kind": "Lethal", "Profile": "keeper_earth",
  "OpponentActorId": "keeper_earth", "OpponentName": "Dolm the Tally-Keeper",
  "Threshold": "ash_cut/tally_vault/threshold", "SafePoint": "glade/quarry_upper/safe",
  "Stage": { "Caster": "ash_cut/tally_vault/caster", "Opponent": "ash_cut/tally_vault/keeper", "Volunteer": "ash_cut/tally_vault/threshold" },
  "OnVictory": [ { "Kind": "TakeCrystal", "Crystal": "earth" }, { "Kind": "SetFact", "Ref": "earth.ash_cut_open" } ] }
```

- `Kind`: `Lethal`, `Friendly`, `Practice`. `Profile` is a Ward opponent profile id (`StreamingAssets/world/ward_opponents.json`, package I3).
- A lethal encounter has a threshold, a safe point and a stage (caster and opponent markers about one step apart; the volunteer stands at the way out).
- The finale has `"BoundTo": "last_tower"` and no place: it is staged at whichever tower is last, on that tower's `DuelCaster` and `DuelOpponent`. There is one finale.

### Quest events and effects

```json
{ "Id": "earth_take", "At": "glade/quarry_upper/pedestal", "Action": "Take", "Guards": [ "custody: earth = AtSite" ],
  "Sets": [ "earth.site_seen", "earth.in_hand", "tower.earth_known" ], "Lines": [ "mira_take_1", "mira_inscription" ],
  "Effects": [ { "Kind": "TakeCrystal", "Crystal": "earth" }, { "Kind": "Journal", "Ref": "journal_earth_taken" } ] }
```

Effects (`EffectKind`): `SetFact` (`Ref`), `TakeCrystal` (`Crystal`), `OfferChoice` (`Ref` = choice), `Journal` (`Ref` = line written to the journal), `PlaceParty` (`At`), `RivalCharge` (`Amount` > 0), `SetBelief` (`Crystal`: the party's belief becomes the truth).

### Endings

```json
{ "Id": "restored", "Outcome": "Won", "Lines": [ "end_restored" ],
  "Variants": [ { "Survivors": "All", "Lines": [ "end_all" ] }, { "Survivors": "Some", "Lines": [ "end_some" ] }, { "Survivors": "None", "Lines": [ "end_none" ] } ] }
```

A complete pack has an ending with `Outcome` `Won` (the restored island) and one with `Lost` (terminal defeat).

## 8. Lines, guards and tokens

```json
{ "Id": "mira_threshold_earth", "Speaker": "mira", "Topic": "threshold",
  "Text": "{role.keeper_earth.name} has seen us. If this goes badly, {volunteer.name} holds the way out. Do we fight?" }
```

- `Speaker` is a role id, a companion id, or `""` (narration, inscriptions, replies, journal entries). `Topic` groups lines for `CampaignApi.LinesFor(state, speaker, topic)`; the authored pack uses `opening`, `reply`, `menu`, `lead`, `intro`, `journal`, `threshold`, `volunteer`, `memorial`, `state`, `sign`, `restore`, `underground`, and one per route and keeper.
- **Length.** At most 140 characters (a bubble of about three lines); replies at most 32.
- **Guards** (`CampaignGuards`), all must hold: `alive: <companion>`, `dead: <companion>`, `fact: <fact>`, `nofact: <fact>`, `custody: <crystal> = AtSite | RivalTransit | AtRivalAltar | Carried | Restored`, `restored: <0-4>` (exactly that many restored).
- **Tokens** (`CampaignTokens`): `{crystal.<id>.name}`, `{crystal.<id>.place}` (where the party believes it is), `{crystal.<id>.trueplace}` (the truth, for witnesses who know), `{crystal.<id>.tower}`, `{companion.<id>.name}`, `{role.<id>.name}`, `{volunteer.name}` ("nobody" when none), `{count.restored}`, `{count.alive}`.
- **Rules the validator enforces.** A line spoken by a companion has the guard `alive: <that companion>` (so no dead character speaks). A line that names a crystal and a place (a location or interior name, "pedestal", "altar") as fixed text must carry a `custody:` or `fact:` guard that makes it true; otherwise write the place as `{crystal.<id>.place}`. So no line ever reports an impossible crystal location.

## 9. Tuning

`Tuning` holds every number of section 5 (`CampaignTuning`): leg and action times, patience and waits, queue and report numbers, the rival charges, the tide. A pack may change any of them; unset fields keep their starting values.

Fields added by the race tuning (14 Nov 2026); each default keeps the earlier rule:

| Field | Default | Authored pack | Meaning |
|---|---|---|---|
| `PartyRunTicksPerStep` | 0 (never runs) | 7 | ticks per 5 m step when Mira runs: a surface leg of 4 steps or more toward a crystal believed on its pedestal while the newest word says Ash runners are heading there |
| `InteriorTicksPerStep` | 0 (= `PartyTicksPerStep`) | 30 | ticks per step inside an interior |
| `StairTicks` | 0 (one step at the leg's pace) | 120 | ticks to cross a stair, exit or door |
| `ThresholdWaitPresent` | -1 (= `ThresholdWait`) | 0 (no limit) | Mira's wait at a lethal threshold with the player present; `ThresholdWait` is then the away wait |
| `OpeningReportDelay` | -1 (= `ReportDelay`) | 0 | delay of the report of the expedition's opening step |
| `RetreatOnLooting` | 1 | 0 | 0: a looting of the home camp only charges the clock |
| `OverrunRetreatCooldown` | 0 | 12000 | an overrun turns the party home at most once in this many ticks |

The authored pack also changed these starting values (before, after): `PartyTicksPerStep` 7, 20; `ScoutTicksPerStep` 4, 7; `ReadTicks` 50, 200; `SearchTicks` 150, 300; `TakeTicks` 50, 150; `RestoreTicks` 100, 300; `LeadPatience` 450, 1200; `ChoicePatience` 300, 1200; `SoloDecide` 100, 900; `CampOverrunCharge` 1800, 600; `AshCampLostPause` 900, 300. Why, and what they do to the race, is in `PRD_CRYSTALS.md` A.6 and `Docs/CAMPAIGN_PROOF.md` section H. The skeleton pack keeps the starting values.

## 10. What the validator checks

`CampaignValidator.Validate(definition[, options])` returns problems, each with a `Code`, a `Sentence`, the `Id` it is about and `IsError`. Errors block `StartCampaign` (`invalid_definition` with the first error's sentence). `ValidationOptions.KnownProfiles` also checks the Ward profile ids.

| Group | Checks | Codes |
|---|---|---|
| Form | known version; parts present; unique ids; display names; every reference (markers, lines, crystals, choices, clues, events, speakers, services, guards' subjects, tokens' ids) exists; guards and tokens in the grammar; line lengths; one to three answers with exactly one correct; encounters have a profile, an opponent, and lethal ones a stage; one finale | `bad_version`, `missing`, `duplicate_id`, `no_name`, `dangling_reference`, `bad_guard`, `bad_token`, `line_too_long`, `bad_choice`, `bad_encounter` |
| Roles | the five roles bound; six companions; volunteer orders 1 to 6 | `role_unbound`, `companion_count`, `volunteer_order` |
| Crystals and towers | four crystals, one per element; each with its own tower of its element; every tower stages a duel and has a safe point; at least one underground, and the flag matches the site | `crystal_count`, `missing_tower`, `shared_tower`, `no_finale_stage`, `underground` |
| Rival | real crystals and altars, each crystal once, positive costs, stolen-at-start crystals have a step, the leader is a role, `OpenWith` crystals have a step, `MarchSpread` is 0 or more and smaller than `MarchCost` | `rival_plan` |
| Places | legal anchors (Meadow hex with no number; rim corner; one home camp), not shared, not a start corner, not on a landmark of the ages; site markers within 9 m; no marker on a plot; tower markers on the island (warning) | `bad_anchor` |
| Interiors | size and floor limits; legend use; all floor connected; links lead both ways; a way in from the surface; from every pedestal, altar, threshold and safe point a way back to the surface through doors whose facts can be known | `bad_interior`, `interior_disconnected`, `no_way_out` |
| Essential clues | a companion-held essential clue has a non-companion fallback; no fallback is a companion's | `companion_only_clue` |
| Routes (warnings on Partial packs) | every crystal's site is reached from the opening facts; also with no companion alive; for every planned theft, the trail from the empty pedestal reaches the altar, the keeper exists, is lethal and has a threshold and safe point; also with no companion alive; every restoration order of the routes reaches its tower with the crystal (guards `restored: n` are honoured); a finale bound to the last tower; both endings, and a lethal encounter so the defeat can happen | `unreachable_clue`, `companion_only_clue`, `no_recovery`, `finale_unreachable`, `no_ending` |
| Narrative | companions' lines guarded `alive`; no crystal place as fixed text | `dead_speaker`, `fixed_location_text` |
| Warnings | an interior reference filed under another location; an unused legend entry; a tower marker off the island | `interior_location`, `bad_interior`, `bad_anchor` |

Not yet checked (needs the runtime; package I4's proof and G2): the four scripted players of 6.5 ("Simulation"), and "no essential line is guarded on a companion".

**How the route checks reason.** The validator walks the story as the runtime would (section 7): from the surface every location can be walked to; interiors open through links whose facts are known; Mira visits a marker when an open lead has its goal there and the marker can be reached; events fire and clues are given as described; facts only grow, so it repeats until nothing changes. It does this for the opening (crystals at their sites), for each planned theft (that crystal on its altar), with all companions and with none, and once for each restoration order with the count of restored crystals set step by step.

## 11. Broken fixtures

`Assets/World/Tests/EditMode/Campaign/Validation/Fixtures/<code>.json` holds one fixture per 6.5 code. A fixture edits a shipped pack and must be rejected with exactly its code:

```json
{ "Why": "Two crystals sharing a tower", "Base": "skeleton_v1", "Expect": "shared_tower",
  "Patch": [ { "Op": "replace", "Path": "/Crystals/water/Tower", "Value": "tower_fire" } ] }
```

`Op` is `add`, `replace` or `remove`. A `Path` walks the merged pack: an object step is a key, a list step is the `Id` of an entry (or its index), and `-` appends. `ValidatorTests` runs every file in the folder.

## 12. The shipped packs

- `authored_v1` (`Partial`): the Slice 1 Earth route end to end, the whole cast, the four towers, the full rival plan, and placeholder locations for the Fire, Water and Air sites and altars (`pending_fire.json`, `pending_water.json`, `pending_air.json`). A Slice 2 route writer replaces their pending file with the route's own files, keeping the location ids `kiln`/`forge`, `causeway`/`cistern`, `shoulder`/`cliff` or updating the crystal's `Site` and the rival step's `Altar` in `campaign.json`. Validates with no error; its only warnings are the three unwritten routes, the missing finale and the two missing endings.
- `skeleton_v1` (`Complete`): four trivial routes (one underground with two levels) that exercise every part of the state machine: a witness clue with a fallback and a companion shortcut, a choice, a scout clue, a report clue, four keepers, the finale and both endings with survivor variants. Validates with no problem at all. The proof (I4) uses it beside the authored pack.

## 13. Writing a route, step by step

1. Add the route's locations (anchors from appendix C) and interiors (rows, legend, `Location`), and connections for the links.
2. Write the clue chain from an opening or witness fact to the site, with a fallback in the world for anything a companion knows.
3. Write one lead per place Mira must go, with reason lines.
4. At the pedestal: the take event and its two stolen twins; the trail facts; leads down the trail to the keeper's threshold.
5. The keeper encounter (lethal, profile, threshold, safe point, stage, `OnVictory` taking the crystal and naming the tower).
6. The tower lead and restore event.
7. Lines: every companion line guarded `alive`, every crystal place a token or guarded; journal lines for each step.
8. Run `dotnet test DotNet` (the pack tests validate the shipped packs) and read the problems.
