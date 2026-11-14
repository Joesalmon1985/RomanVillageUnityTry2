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
| `Campaign/Runtime/CampaignRuntime.cs` | Command checks, effects and the tick (the module forwards here) | **I1** (seeded by I0) |
| `Campaign/Definition/CampaignLoader.cs` | `LoadFolder`, `Hash` | **I2** (seeded by I0) |
| `Campaign/Validation/CampaignValidator.cs` | `Validate` | **I2**, then J3, G2 (seeded by I0) |
| `Board/Lattice.cs` | `CellOf`, `Distance`, `Step`, `X`, `Z` | I0, then `PRD_AGES` M1 |
| `Encounters/Ward/WardOpponentProfile.cs`, `WardDuelSpec.cs`, `WardTuning.For`, `WardDuelSession.Create` | Opponents as data | **I3** (seeded by I0) |

Every definition and state class is `partial`: an owner adds fields from its own folder, never by editing the interface files.

## Registration and saves

`CampaignModule.Register(sim)` is idempotent. `WorldSimFactory.CreateNew` calls it. `SaveCodec.CreateSim` (owned by `PRD_AGES`) does not yet: **until it does, a loaded save has no campaign handlers or system**, and tests that load a campaign save call `CampaignModule.Register(sim)` themselves. Requested: one line `Campaign.CampaignModule.Register(sim);` after `Ledger.LedgerModule.Register(sim)` in `SaveCodec.CreateSim`.

`CampaignState.FormatVersion` (1) versions the campaign's own shape; the world save version is not touched. The whole definition is saved inside the state, with `DefinitionHash` (SHA-256 of the canonical JSON).

## Commands (PlayerId 0) and refusals

| Command | Refused with |
|---|---|
| `StartCampaign { Definition }` | `campaign_running`, `invalid_definition` (first error of `CampaignValidator.Validate`) |
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

## First-version behaviour (I0)

Real: every refusal above; `StartCampaign` (crystals at their sites or, for `Rival.StolenAtStart`, on their altars with the Ash point paid; beliefs "at the site"; companions alive; party Idle at the first Camp location's safe point, player present; opening clues' facts; `ErasModule.EnterCampaign`; landmarks `lm.tower.<element>` at the tower location's rim corner, or the appendix C bearing corner when `Corner` is -1, and `lm.altar.<element>` at the hex of the location holding each plan step's altar; `CampaignStarted`); `SetCompany`; `CommitEncounter` (attempt id, volunteer = first living companion in volunteer order or "", seed from the campaign's `Rng`, mode InEncounter, `EncounterCommitted`); `ResolveEncounter` (the record, `EncounterResolved`, once only); `RestartAttempt` (new seed, `Restarts` + 1, `EncounterCommitted` again); the +1 per tick of `RivalProgress` while Running; `DevSetRivalProgress`, `DevSetCustody`, `DevSetCompanion`.

"I0 STUB" (accepted, no effect): `AcceptLead`, `DeclineLead`, `AdviseMira`, `SuggestScout`, `WithdrawFromEncounter`, `ConfirmRestore`, `DevRivalNextStep`; after a resolution only the party's return to Idle (the outcome table of 5.6 is I1's); the rest of the tick (rival plan, reports, Mira's brain, legs, acting, waits, scout, restoration, endings). The loader is a plain merge; the validator checks only a known format version and missing parts.

Shared steps for I1 in `CampaignRuntime`: `SetCustody(s, element, to, at, ev)` (one event, guarded by the state it starts from; `Restored` pays the player `crystal:<element>`, the first `AtRivalAltar` pays the Ash Clan the same key through `LedgerLogic.Award`, and records `EverOnAltar`), `Learn(c, fact, source, ev)` (sorted, once), `SetMode(s, c, mode, ev)`, `Start`, `Lower(element)`.

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
