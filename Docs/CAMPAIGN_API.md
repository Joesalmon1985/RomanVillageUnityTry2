# Campaign API (namespace `World.Sim.Campaign`)

The simulation surface of the crystal campaign (`Docs/PRD_CRYSTALS.md` appendix A). Pure C#, integer state, deterministic. Landed by package I0 (Sim half, tag `cry-I0-sim`); pinned by `Assets/World/Tests/EditMode/Campaign/CampaignSurfaceTests.cs`. From that tag names only grow: every change is additive and logged at the end of this file.

`WorldState.Campaign` is null when no campaign runs. Then the campaign system does nothing, every command except `StartCampaign` is refused with `no_campaign`, and every read helper returns false, null, an empty list or "". A 20-minute game gives the same state JSON with the module as without it.

## Files and owners

| Path (under `Assets/World/Sim`) | What | Owner |
|---|---|---|
| `Campaign/CampaignDefinition.cs` | A.1: enums, the definition and its parts, `MarkerRef`, `MarkerDef`, `CampaignTuning` | I0, then J0 / G0 (additive) |
| `Campaign/CampaignState.cs` | A.2: `WorldState.Campaign`, run state classes | I0, then J0 (additive) |
| `Campaign/CampaignCommands.cs` | A.3: commands, `CampaignRefusals`, `DuelResults` | I0, then J0 |
| `Campaign/CampaignEvents.cs` | A.4: events, `RivalCharges` | I0, then J0 |
| `Campaign/CampaignViews.cs` | A.5: views, `CampaignRecord`, `HistoryState.CampaignRecord`, `Problem`, `ProblemCodes` | I0, then J0 |
| `Campaign/CampaignApi.cs` | A.5: read helpers | I0, then J0 |
| `Campaign/CampaignText.cs` | Guard and token grammar (6.1) | I0, then J0 |
| `Campaign/CampaignModule.cs` | Registration: system at `SystemOrder.Campaign` (1250), 15 handlers | I0 |
| `Campaign/InteriorMaps.cs` | Interiors as lattice regions | I0 |
| `Campaign/Runtime/CampaignRuntime.cs` | Command checks, effects, start, the outcome table, restoration, endings, the tick (the module forwards here) | **I1** (seeded by I0) |
| `Campaign/Runtime/CampaignRoutes.cs`, `CampaignRival.cs`, `CampaignLeads.cs`, `CampaignParty.cs` | Routes and legs; the expedition; facts, clues, leads and Mira's queue; the party's modes, choices, thresholds, scout, overrun link | **I1** |
| `Campaign/Runtime/CampaignRuntimeState.cs` | I1's partial fields of the run state | **I1** |
| `Campaign/Definition/CampaignLoader.cs` | `LoadFolder`, `Hash` | **I2** (seeded by I0) |
| `Campaign/Validation/CampaignValidator.cs` | `Validate` | **I2**, then J3, G2 (seeded by I0) |
| `Board/Lattice.cs` | `CellOf`, `Distance`, `Step`, `X`, `Z` | I0, then `PRD_AGES` M1 |
| `Encounters/Ward/WardOpponentProfile.cs`, `WardDuelSpec.cs`, `WardTuning.For`, `WardDuelSession.Create` | Opponents as data | **I3** (seeded by I0) |

Every definition and state class is `partial`: an owner adds fields from its own folder, never by editing the interface files.

## Registration and saves

`CampaignModule.Register(sim)` is idempotent. `WorldSimFactory.CreateNew` calls it, and so does `SaveCodec.CreateSim` (after `LedgerModule`, since sim-H1-cry-S1): a loaded save has the campaign system and handlers.

`CampaignState.FormatVersion` (1) versions the campaign's own shape; the world save version is not touched. The whole definition is saved inside the state, with `DefinitionHash` (SHA-256 of the canonical JSON).

## Commands (PlayerId 0) and refusals

| Command | Refused with |
|---|---|
| `StartCampaign { Definition }` | `campaign_running`, `invalid_definition` (first error of `CampaignValidator.Validate`), `campaign_not_yet` (a story game before `AgesApi.CampaignMayStart`; not for a developer start or "Begin again") |
| `SetCompany { Present }` | `no_campaign`, `campaign_over` (as every command below) |
| `AcceptLead { LeadId }`, `DeclineLead { LeadId }` | `unknown_lead` |
| `AdviseMira { ChoiceId, Answer }` | `not_at_choice` (not AskingChoice, another choice, answer out of range) |
| `SuggestScout { LocationId }` | `unknown_place`, `scout_out`, `no_scout` |
| `CommitEncounter { EncounterId }` | `not_at_threshold` (also non-lethal or an attempt pending), `scout_out`, `player_needed` |
| `WithdrawFromEncounter { EncounterId }` | `not_at_threshold` |
| `ResolveEncounter { AttemptId, Result }` | `already_resolved` (even after the run ended), `unknown_attempt` |
| `RestartAttempt { AttemptId }` | `already_resolved`, `unknown_attempt` |
| `ConfirmRestore { Crystal }` | `not_carried`, `wrong_tower` (not at its tower's location on the surface, or travelling), `player_needed` |
| `DevSetRivalProgress`, `DevRivalNextStep`, `DevSetCustody`, `DevSetCompanion` | F1 tools and tests only |

Sentences: `CampaignRefusals.Sentence(code, arg)`; `arg` fills the first problem of `invalid_definition` and the name of `scout_out`.

## First-version behaviour (I0; history: I1 replaced every "I0 STUB", see "Runtime (I1)" below)

Real: every refusal above; `StartCampaign` (crystals at their sites or, for `Rival.StolenAtStart`, on their altars with the Ash point paid; beliefs "at the site"; companions alive; party Idle at the first Camp location's safe point, player present; opening clues' facts; `ErasModule.EnterCampaign`; landmarks `lm.tower.<element>` at the tower location's rim corner, or the appendix C bearing corner when `Corner` is -1, and `lm.altar.<element>` at the hex of the location holding each plan step's altar; `CampaignStarted`); `SetCompany`; `CommitEncounter` (attempt id, volunteer = first living companion in volunteer order or "", seed from the campaign's `Rng`, mode InEncounter, `EncounterCommitted`); `ResolveEncounter` (the record, `EncounterResolved`, once only); `RestartAttempt` (new seed, `Restarts` + 1, `EncounterCommitted` again); the +1 per tick of `RivalProgress` while Running; `DevSetRivalProgress`, `DevSetCustody`, `DevSetCompanion`.

"I0 STUB" (accepted, no effect): `AcceptLead`, `DeclineLead`, `AdviseMira`, `SuggestScout`, `WithdrawFromEncounter`, `ConfirmRestore`, `DevRivalNextStep`; after a resolution only the party's return to Idle (the outcome table of 5.6 is I1's); the rest of the tick (rival plan, reports, Mira's brain, legs, acting, waits, scout, restoration, endings). The loader is a plain merge; the validator checks only a known format version and missing parts.

Shared steps for I1 in `CampaignRuntime`: `SetCustody(s, element, to, at, ev)` (one event, guarded by the state it starts from; `Restored` pays the player `crystal:<element>`, the first `AtRivalAltar` pays the Ash Clan the same key through `LedgerLogic.Award`, and records `EverOnAltar`), `Learn(c, fact, source, ev)` (sorted, once), `SetMode(s, c, mode, ev)`, `Start`, `Lower(element)`.

## Runtime (I1)

Everything of `PRD_CRYSTALS` 5.1 to 5.9 that the Sim owns is real; nothing is marked "I0 STUB" any more. One tick of the campaign slot, in order: the clock's +1; `CampOverrun` events raised earlier in the tick; the party; the scout; the rival; reports reaching the party; Mira's own scouting; Mira's plan (every `PlanInterval`, or at once after any change). The party moves before the rival, so a party standing at a pedestal wins a tie.

**Places and travel.** A place is a marker. Surface cells are the location's anchor cell (`CampaignPlaces.TryAnchorCell`, the format package's rule) plus the marker offset. Each interior is its own space, keyed by interior id. Links follow `CAMPAIGN_FORMAT` 5: a connection's two ends, or an `Exit`, `Stairs` or `Door` marker and the marker its `To` names; a link works both ways and is open when every `OpenWhen` of its two markers and of every connection joining them is known; crossing costs one step. A route is Dijkstra over the start, the goal and the link ends; legs are walks within one space (`PartyTicksPerStep` per step; interior legs carry their `Cells`) or link crossings. One `PartySetOut` and one `PartyArrived` per leg. At every leg's end the `Arrive` quest events and the clues of the marker reached fire, and of every marker the leg passed over inside an interior; an event that asks a question or moves the party stops the route there. A leg or action of N ticks takes N ticks from the tick it began.

**Custody (5.2).** `SetCustody` is the one step (one event, guarded, ledger points). The party takes a crystal by standing at its pedestal (`Take`, `TakeTicks`) while it is `AtSite`; a keeper's victory (`OnVictory` `TakeCrystal`) or an alternate-route quest event takes it off an altar. The rival seizes only `AtSite` crystals the party is not standing at. `RivalTransit` keeps `At` = the site; `AtRivalAltar` `At` = the plan step's altar. Carried and Restored are never touched by the rival.

**The expedition (5.1, 5.3).** `RivalProgress` is +1 per tick plus the charges (`defeat` 900, also surrender; `draw` 600, or 300 while the companion with the `shield` service lives; `wrong_answer` 300, also the `RivalCharge` effect; `camp_overrun` 1800; `dev`). The rival spends progress (`CampaignState.RivalSpent`) through march, seize-and-carry or search, step by step, within the same tick; steps whose crystal is already on an altar or restored are skipped; a pause (`AshCampLostPause`) uses progress with no effect; with the plan done and three restored it goes `AtLastTower`. Every move is a `RivalReport` due `ReportDelay` later; when due: `RivalReported`, the belief (Seized: `RivalTransit` at the site; OnAltar: `AtRivalAltar` at the altar; never over something the party saw for itself since), `Report` clues, a journal line. `DevRivalNextStep` finishes the current step (charged as `dev`).

**Facts, clues, events.** Opening clues with requirements, living companions' clues and witness, marker or wizard clues whose `Ref` is a role id (not a marker) are given at each plan. Witness, marker and wizard clues with a marker `Ref` are heard when the party visits that marker (arrives or passes over it, or acts there); scout clues (`Ref` = location id) when the scout reports from there; report clues (`Ref` = crystal id or "") when word of a seizure or an altar arrives. Quest events fire in definition order on (place, action) with requirements, guards and `Once`; their `Sets`, effects and first speakable line (else a generic sentence) go to the journal.

**Leads and Mira's queue (5.4).** Open = facts known, `DoneWhen` not known, not declined (`DeclineCooldown`), not spent, its crystal not restored and not carried unless the lead goes to its tower, reachable. Score = `Priority` + 30 (latest known report on its crystal is Marching) + 20 (carried and the lead is its tower) - steps / 20; ties by id. Best `QueueLength`, ids stable. A lead acted on with no result is *spent* until something changes (a progress signature: facts, fired events, won encounters, open choices, living companions, custody). Mira goes only where a lead sends her (`CAMPAIGN_FORMAT` 7): the shipped packs run with only their own leads, plus `@choice:<choice>` (back to a question left open, never a new place). With `CampaignState.MakeOwnLeads` (off by default; on for hand-made definitions without a full story, such as the I0 sample in the runtime tests) the runtime also makes leads where the definition has no lead with that goal (ids start with `@`, never written into the definition, shown by `CampaignApi.Leads` with a title and reason): `@take:<crystal>` (the party believes it is on its pedestal), `@recover:<crystal>` (the party knows it is on an altar: to its keeper's threshold), `@restore:<crystal>` (carried: to its tower), `@clue:<clue>` (a witness, marker or wizard clue not yet heard: this keeps every essential clue reachable with no companion), `@choice:<choice>` (back to a question left open). Priorities: recover and restore 60, choice 55, take 50, clue 45.

**Mira and the party (5.5).** Idle: the top lead is taken after `LeadPatience` (player present) or `SoloDecide` (away), with a journal line when she goes alone. Arrival: `Arrive` events, the lead's action, then the pedestal (take it, or see it empty: the party's belief becomes the truth), then a threshold (an active lethal encounter here: `AtThreshold`, `EncounterThreshold` with the volunteer), a tower with its carried crystal (`WaitingForPlayer`), else Idle. Choices: the player is asked; with no answer in `ChoicePatience`, or away, she walks to `RevealedBy`, studies `SoloStudyTicks`, walks back and answers right (`ChoiceAnswered.ByPlayer` false) through the same step as `AdviseMira`; a wrong answer charges 300 and the choice stays open; with no `RevealedBy` she leaves it open and moves on. Thresholds: `ThresholdWait` (present or away), then she withdraws on foot to the safe point; away she takes another open lead (never the one back into that fight), or with nothing open returns and holds at the threshold with no time limit. Alone she waits at each tower once until the player is back. "I'm with you" (`SetCompany` true) ends the hold. She never commits a lethal encounter and never restores while the player is away (`player_needed`). Scout: `SuggestScout` or, away, a stale belief; out at `ScoutTicksPerStep` (surface anchor distance), `SearchTicks`, back; on return the beliefs of crystals at or believed at that place become the truth (`ScoutReported`), scout clues, `ScoutReturned`.

**Encounters (5.6).** Victory: `OnVictory` effects once, the encounter is complete. Defeat or Surrender: the attempt's volunteer (or the next living one) falls (`CompanionFell`, cause, place), the party is placed at the safe point, +900. No volunteer: `CampaignEnded { Won = false }`, the first `Lost` ending. Clash or Stalemate: placed at the safe point, nobody dies, 600 or 300. All in the one `ResolveEncounter` application, which clears the attempt first. Friendly and practice encounters never make an attempt.

**Restoration and the finale (5.7, 5.8).** `ConfirmRestore` acts `RestoreTicks`, then: `Restored` (the player's point), the order, `CrystalRestored`, the belief, the island (from the saved base, never stacked: `SevensPerDraw` = max(1, base + restored - on altars), `int.MaxValue` and every demon banished at four; Deepstone: Hills and Mountain `GrowTicks` x 100 / (100 + `BoonPercent`); Tideglass: Fields and Pasture; Skyshard: `CarrierTicksPerStep` - 1; Kilnheart: Kiln and Smokehouse `EconomyTuning.CycleBonusPermille` = base + 1000 x `BoonPercent` / (100 + `BoonPercent`)), the journal, `Restore` quest events. With three restored, arriving anywhere at the fourth crystal's tower carrying it is the finale's threshold (any of the 24 orders); `ConfirmRestore` of that crystal is refused with `not_at_threshold` until the finale is won; victory restores it at once and ends the run (`CampaignEnded { Won = true }`, the first `Won` ending, the record). A survivable loss retreats to that tower's safe point and the finale stays open. At `CampaignEnded{Won}` the Eras code copies the record into `HistoryState.CampaignRecord` and raises `ChapterReady{ch.modern.harbour}` in the same tick (advancing is refused with `chapter_not_built` until `PRD_AGES` M1-4).

**Overruns (5.9).** `CampOverrun` with the player as defender (either kind): +1800, and unless a duel is pending the party drops what it is doing and walks home (`PartyRetreated` `camp_overrun`); an uncommitted threshold is abandoned; an open choice stays open. Attacker the player, defender the Ash Clan: the expedition pauses (`RivalPaused`). Nobody dies.

**Saves.** All of it is saved state (`CampaignRuntimeState.cs`); a save at any tick continues identically (pinned by `CampaignSaveTests`). `CampaignState.FormatVersion` stays 1: I1 only added fields with safe defaults.

**Not yet.** The tide does not gate routes over the Low Causeway (`CausewayDry` is read-only; J1 with the Water pack).

## Read helpers (`CampaignApi`)

`IsRunning`, `Crystal` (truth), `Belief` (with `Stale` after `StaleTicks` and the "where to look" sentence: the last known place, then the living scout, then the wizard role), `Leads` (rank order; `Reason` is the filled reason line), `TopLead`, `Party`, `PartyPosition(s, tickFraction, out from, out to, out t)`, `LegCells`, `Companions` (volunteer order), `NextVolunteer`, `Attempt`, `RivalView` (reports whose `DueTick` has passed), `Journal`, `WhereIsMira`, `Line`, `LinesFor(s, speaker, topic)`, `IsOpen(connection)`, `CausewayDry` (true for the first `TideDry` ticks of each `TideCycle` from the start), `Check`, `Record` (`CampaignRecord`: ending, definition id, seed, restoration order, ever on an altar, survivors, the fallen in order with place name, tick and cause).

## Text: guards and tokens (6.1)

A guard is `kind: subject` or `kind: subject = value`: `alive: odo`, `dead: garrick`, `fact: earth.stolen_seen`, `nofact: earth.stair_open`, `custody: earth = AtRivalAltar`, `restored: 2` (exactly). All of a line's guards must hold; an unknown kind never holds. A line whose speaker is a dead companion is never returned.

Tokens: `{crystal.<id>.name}`, `{crystal.<id>.place}` (where the party believes it is), `{crystal.<id>.trueplace}` (the truth, for witnesses who know), `{crystal.<id>.tower}`, `{companion.<id>.name}`, `{role.<id>.name}`, `{volunteer.name}` ("nobody" when none), `{count.restored}`, `{count.alive}`. A place name is the interior's name inside an interior, else the location's. Unknown tokens are left as written for the validator to report.

## Places and interiors

`MarkerRef { Location, Interior, Marker }`; text form `location/marker` (surface) or `location/interior/marker`. Surface markers' `I, J` are lattice offsets from the location's anchor cell. An interior's rows are lattice cells in its own frame (column i, row j); `#` rock, `.` floor, a legend character is a floor cell with that marker. `InteriorMaps.Path` is a breadth-first search over the six steps in the fixed order (0,1), (1,0), (1,-1), (0,-1), (-1,0), (-1,1); a `Door` cell opens when its `OpenWhen` fact is known.

`Lattice` has the walker's cells: cell (i, j) at X = 5 · (√3/2) · i, Z = 5 · (i/2 + j), origin at the board centre; every hex centre is a cell.

## Ward opponents

`WardTuning.For(profile, base = Practice())` returns a copy with the profile's first-cast, think and mistake numbers in the `Mira*` fields. `WardDuelSession.Create(spec)` equals `new WardDuelSession(spec.Tuning, spec.Pool, spec.Seed)`, falling back to the practice tuning and first pool. `DuelResults.TryFrom(WardOutcome, out DuelResult)` maps a finished duel (`Forfeited` is `Surrender`).

## Change log

- **14 Nov 2026, I0 Sim half (`cry-I0-sim`).** First landing. Additions and fixes against appendix A are listed in `PRD_CRYSTALS.md` A.6.
- **14 Nov 2026, I2 (`cry/i2`).** The real loader, validator v1, `CampaignPlaces`, `InteriorDef.Location`, the authored (Partial, Earth) and skeleton (Complete) packs. All additive, from `Definition/**` and `Validation/**` (new files `Definition/CampaignPlaces.cs`, `Definition/DefinitionFormat.cs`, `Validation/ValidationRun.cs`, `Validation/ValidationRoutes.cs`). Details in `PRD_CRYSTALS.md` A.6 (I2 entry); the format and the validator's checks are in `Docs/CAMPAIGN_FORMAT.md`.
- **14 Nov 2026, I1 (branch `cry/i1`).** The runtime (section "Runtime (I1)"). Additive surface changes: `CampaignApi.Leads` also shows the runtime's own leads (`@` ids) through `CampaignRuntime.ImplicitLead`, and its `Reason` falls back to `LeadDef.ReasonText` (new, `[JsonIgnore]`, never in a pack, so hashes are unchanged); `CampaignApi.Record` takes a fallen companion's place from `CompanionState.FellPlace` (new) when set, so the finale's tower is named. New partial fields from `Runtime/CampaignRuntimeState.cs`: `CampaignState.RivalSpent`, `RivalResumePhase`, `ReportsRaised`, `Spent`, `OpenChoices`, `CompletedEncounters`, `ReplanNow`, `BaseGrowTicks`, `BaseCarrierTicksPerStep`; `PartyState.Route`, `Purpose`, `LeadDefId`, `LeadAction`, `Pending`, `ActingAction`, `ActingTicks`, `ActingProgress`, `Studying`, `ActingCrystal`, `WaitSinceTick`, `HoldAtThreshold`, `WithdrawnFrom`, `ChoiceAt`, `BeganTick`, `ArriveSignature`; `ScoutState.Phase`, `Timer`, `Found`; `CompanionState.FellPlace`; new types `RoutePurpose`, `SpentLead`, `OpenChoice`. After merging I2 (`cry/i0` with I2 and I3): `CampaignState.MakeOwnLeads` (new, default false: story-contract packs get no runtime-made leads but `@choice:`); links follow `CAMPAIGN_FORMAT` 5 (both ways, every `OpenWhen` on the link); `Arrive` events and visit clues fire at every leg's end and on markers passed inside interiors; role-id clue sources are given at once; anchors come from I2's `CampaignPlaces`; the runtime's routing file is `Runtime/CampaignRoutes.cs` (renamed from `CampaignPlaces.cs` to avoid two files of one name). Nothing renamed or removed. Two I0 tests in `CampaignSurfaceTests` pinned stub behaviour and were adjusted without loosening a check: `Start_BuildsTheRun...` now expects Odo's companion clue on the start tick beside the opening clue, and `Belief_GoesStale...` keeps Mira at camp (`LeadPatience` max) and the expedition out of the field (no plan steps) so its stale-belief and due-report checks stay exact.
- **14 Nov 2026, sim-H1-cry-S1 (`cry/i0` with `ages-H1-sim` merged).** Additive: refusal `CampaignRefusals.CampaignNotYet` `campaign_not_yet` and the start gate; `SaveCodec.CreateSim` registers the module (made here for `PRD_AGES`, logged in its A.9); the Kilnheart's boon through `EconomyTuning.CycleBonusPermille` with the new run-state field `CampaignState.BaseCycleBonusPermille`; the Eras link at `CampaignEnded{Won}` (`HistoryState.CampaignRecord`, `ChapterReady{ch.modern.harbour}`). Details in `PRD_CRYSTALS.md` A.6. Tests: `Campaign/CampaignSeamTests.cs`; the campaign proof `Docs/CAMPAIGN_PROOF.md` (`Campaign/Proof/**`).
