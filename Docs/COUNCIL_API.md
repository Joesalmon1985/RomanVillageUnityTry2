# Council API (village election and active listening)

Pure simulation for the council encounter: `Assets/World/Sim/Encounters/Council` (namespace `World.Sim.Encounters.Council`). Tests: `Assets/World/Tests/EditMode/Council`. Balance report: `Docs/COUNCIL_PROOF.md` (automated simulations only). The rules come from the owner's brief (section 3 for the election, section 5 for listening); the screen layout of section 4 and 5 is the Unity half's job.

## What the Unity half does and must not do

Does: send commands, read state to draw the scene, animate events, pace rival turns (call `CouncilAiStep` once per AI action, as fast or slow as it likes), measure listening response times and pass them in.

Must not:

- Write to `WorldState.Council` or any election field. Read it, send commands, animate events. (Like the rest of `Assets/World/Game`.)
- Show dice or invent values. The engine never exposes the four values; it exposes only trays. `CouncilPushMade` carries no values. Do not add any.
- Re-roll, retry or resubmit to "fix" a refused command. A refusal carries a reason code; show a gesture, not an error. One accepted command commits exactly one legal move, and repeated, stale or out-of-turn commands are refused.
- Let animation, camera turns, looking left or leaving change a roll. Listening has its own stream; the election's draws depend only on its seed and the number of pushes made.
- Apply effects to the economy from the election. The council adds no workers or stock; the winner is recorded as an `OfficeHolder`, separate from victory points. Crowds are presentation only.
- Name or reproduce the board game that inspired the rules, or copy the owner's brief into the repo.

Commands apply at the next tick boundary like all commands (`Submit` validates now). While an encounter is open the village clock is paused in the game layer, so after `Submit` step one tick to apply and read events. For preview use `sim.Check(command)`, which never mutates.

## State the view reads

`WorldState.Council` (`CouncilState`, saved with the world; older saves load with it empty, no version bump):

| Field | Meaning |
|---|---|
| `Settlements` | list of `SettlementCouncil`, sorted by settlement id |
| `Offices` | list of `OfficeHolder` (settlement, name, resident key, occupation, office title, election number, tick, seat). One per settlement |
| `ElectionsOffered` | counter |

Helpers in `CouncilApi`: `Get(state, settlementId)`, `StatusOf`, `CanOffer(state, id)` (eligible and no election yet; use it for the NPC approach), `OfficeHolderOf(state, id)`, `DisputeLine(...)`, `ListeningLineOf(id)`, `Banked(sc, seat, track)`.

`SettlementCouncil`:

- `Status`: `None` (no record; `Get` returns null) / `Offered` / `InProgress` / `Decided`. `Decided` is final: the winner is recorded once (`Applied`, `WinnerSeat`, `DecidedTick`) and every further command is refused with `already_decided`.
- `Candidates` (4, index = seat, fixed seating order): `Name`, `Occupation`, `OccupationId`, `ResidentKey`, `Emblem` (id of the seat's emblem: acorn circle/green, arrowhead triangle/red, hearthstone square/blue, willow leaf diamond/gold; shape, name and colour are in the content file `Emblems`).
- `AskerSeat`: the candidate who approaches and asks for help.
- `Disputes` (11, index = track - 2): `Track`, `DisputeId`, `LeadName`, `LeadResidentKey` (null for a local delegate), `IsDelegate`, `LeadTitle` (occupation, or the delegate title), `Prop` (age-appropriate prop name). Bound to real residents where an occupation matches, otherwise local delegates; the facts never need a trade the village lacks.
- `Office` (title for this age: chief, reeve, mayor), `Era`.
- `Present` (false after `CouncilLeave`), `PlayerHelps`.
- `Election` (null until the offer is answered): `ElectionState`.

`ElectionState` (everything to draw the scene):

- `Active` seat, `Starter`, `PlayerSeat` (-1 when all four are AI), `Personality[seat]`, `Winner` (-1 until complete).
- `Claimed[track-2]` winner seat or -1 (closed to everyone when claimed); `ClaimsOf(seat)`.
- `Banked[seat * 11 + (track-2)]` or `BankedAt(seat, track)`: permanent place, 0 = none. Draw an emblem on a post at that place for each seat that has one; several seats may share a place.
- `Temp[track-2]`: the active candidate's catspaw place, 0 = no catspaw there. `CatspawsInPlay`, `CatspawsLeft` (3 at the start of each sitting). Draw a catspaw wearing the active emblem at `Temp`; it is temporary until banked.
- Track lengths: `CouncilRules.TrackLengths` = 3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3 for tracks 2 to 12.
- `Phase`: `Ready` (the hand is actionable; the seal is actionable when `HasAdvanced`), `Choosing` (trays are on the table), `BustPending` (no legal move and a petition is in hand: spend it or accept), `Complete`.
- `Pending`: the trays of the pending push (up to 3). A `Tray` has `Mode` and two `Sockets`; a socket has `Track` (0 = empty socket) and `Steps` (1 per carving). `SumA <= SumB` are the pair sums. `TrayMode.Both`: tapping the tray commits every carving (a double is two copies of the same track; an unusable half is an empty socket). `TrayMode.Either`: two separately tappable single carvings; tap one socket (`Socket` 0 or 1), the other is left behind. Equivalent moves are already merged. The example values 1, 3, 4, 5 give trays 4+9, 5+8, 6+7.
- `PushId` (quote it in `CouncilChoose`), `ActionSeq` (optional stale check), `HasAdvanced`, `SittingPushes`, `SittingAdvances`, `Sittings`, `Pushes`, `Advances`, `Banks`, `Busts`.
- Petition: `PetitionEarned`, `PetitionSpent`, `PetitionAvailable` (earned and unspent). The petition belongs to the player's candidate and can only be spent in `BustPending`. Show it beside the seal when `PetitionAvailable`.
- Listening: `Listening` (`ListeningState`, the burst in progress or the last finished one), `ListenBursts`, `ListenSkips`, `ListenAbandons`, `ListeningRules` (threshold, bonus, times, `MaxBursts` = 2).

`ListeningState`: `Phase` (`InProgress`/`Complete`/`Abandoned`), `LineIds[6]` (line 0 is the reference, line i+1 is judged by answer i), `Tags[6]`, `Index` (answers given), `Answers`, `Correct`, `ResponseMs`, `Result` (`ListeningResult`: `Correct`, `AccuracyPercent`, `SpeedBonusPercent`, `EffectivePercent`, `ThresholdPercent`, `Earned`). Look up text with `CouncilApi.ListeningLineOf(id)` (`Speaker`, `Text`, `Tag`). The line to show now is `LineIds[Index + 1]`; the comparison line is `LineIds[Index]`. Do not show the expected answer; the result of each answer arrives as an event for the aide's reaction.

## Commands

All derive from `CouncilCommand` (`Settlement` = settlement id; `PlayerId` as usual, the owner of the settlement). Refusals use `CommandResult.Reason` (below).

| Command | Effect |
|---|---|
| `CouncilOffer` | The NPC approach: records the offer, candidates and disputes. Needs four living, eligible residents who want to stand (`CanOffer`). Status becomes `Offered` |
| `CouncilRespond { Help }` | Accept (`true`: advise the asking candidate) or decline help (`false`: all four play themselves). Status becomes `InProgress`. Doing nothing leaves it `Offered` |
| `CouncilPush` | Press on: a fresh push. The hand tap. Starts the sitting's first push too |
| `CouncilChoose { PushId, Tray, Socket }` | Commit one tray (`Both`: `Socket` -1) or one carving (`Either`: `Socket` 0 or 1) |
| `CouncilBank` | The seal: banks every catspaw, claims any track whose top was reached, ends the sitting; the third claim wins |
| `CouncilAcceptBust` | Take the setback when `BustPending` |
| `CouncilSpendPetition` | When `BustPending` and `PetitionAvailable`: one fresh push, the sitting's progress kept |
| `CouncilAiStep` | One AI action for the active seat (refused on the player's turn). Call repeatedly to play a rival turn |
| `CouncilStartListening` | Starts a burst (needs a player seat, no petition earned yet, bursts left, none open) |
| `CouncilAnswerListening { Choice, ResponseMs }` | `Same` or `Different`; `ResponseMs` measured by the caller from when the line was readable and input ready |
| `CouncilAbandonListening` | Drops an unfinished burst |
| `CouncilSkipListening` | Records that the player chose not to listen (a counter; no other effect) |
| `CouncilLeave` / `CouncilResume` | Leave the council (drops an open burst) and come back. State is kept either way |

The player's action commands act for `PlayerSeat` only. Optional `ExpectedSeq` on action commands refuses the command if `ActionSeq` moved. A bust with no petition in hand (or on an AI turn) resolves at once inside the push: you get `CouncilBust` then `CouncilTurnPassed`.

Refusal reasons (`CouncilReasons`): `election_over`, `not_your_turn`, `wrong_phase`, `stale_action`, `stale_push`, `bad_tray`, `socket_required`, `bad_socket`, `nothing_to_bank`, `no_petition`, `no_player`, `petition_already_earned`, `listening_active`, `listening_none`, `listening_limit`, `bad_answer`, `not_eligible`, `wrong_status`, `unknown_settlement`, `not_your_settlement`, `already_decided`.

Gate inputs with `Phase`, `Active` and `PushId`: a held tap or a tap after a camera turn simply gets refused.

## Events

All derive from `CouncilEvent` (`Settlement`, `Seat`) and `SimEvent` (`Tick`), in emission order:

- `CouncilOffered`, `CouncilStarted { PlayerHelps, Starter }`, `CouncilLeft`, `CouncilResumed`
- `CouncilPushMade { PushId, FromPetition }` (no values), `CouncilOptionsOffered { PushId, Trays }` (empty = no legal move)
- `CouncilAdvanceMade { PushId, Tray, Socket, Steps[] }`: each step has `Track`, `From`, `To`, `NewCatspaw`, `ReachedTop`. Move the catspaws along these.
- `CouncilBanked { Places[] }`; `CouncilTrackClaimed { Track, RemovedFrom[] }` (raise the winner's banner at the lead NPC, add a seal to their bay, remove the listed markers)
- `CouncilBustPending { PushId }`; `CouncilBust { Lost[] }` (walk the catspaws back; banked emblems stay); `CouncilPetitionSpent`
- `CouncilTurnPassed { From, To, Sitting }`
- `CouncilElectionWon`, then `CouncilOfficeHandedOver { WinnerName, Office }` (the short handover; recorded once)
- Listening: `CouncilListeningStarted { Burst }`, `CouncilListeningAnswered { Index, Correct, ResponseMs, ReferenceTag, LineTag }`, `CouncilListeningFinished { Result }`, `CouncilPetitionEarned` (the aide carries it to the table), `CouncilListeningLeft { Abandoned }` (skip or abandon)

The sim also emits the standard `CommandApplied` / `CommandRejected`.

## Dialogue and content

`CouncilApi.DisputeLine(sc, track, kind, variant, candidateSeat, winnerSeat)` fills a line for a track; kinds are `CouncilContent.OpeningLine`, `ProgressLine`, `SetbackLine` (on a bust), `RevisitLine` (when the dispute is revisited), `AgreementLine` (when secured) and `AcknowledgementLine` (later council dialogue, naming the winner). Each kind has at least two variants; pick a variant by any presentation counter (`variant` wraps), never by the election's random streams. Tokens `{candidate}`, `{lead}`, `{prop}`, `{office}`, `{winner}` are filled for you.

Content is data: `Assets/StreamingAssets/world/council/council_content.json`, loaded with `CouncilContent.Parse(json)`; `CouncilContent.BuiltIn` is an exact built-in copy (a test pins that they match; edit the JSON, then regenerate `CouncilContentDefaults.cs` from it). Registering a custom table: `CouncilModule.Register(sim, content)` (default registration happens in `SaveCodec.CreateSim`, so any loaded or new sim already has it with `BuiltIn`).

Format (all keys PascalCase; the file is read with Newtonsoft):

- `Version`; `Offices`: `{ prehistoric, historic, modern, future }` titles.
- `Emblems[4]`: `Id`, `Name`, `Shape`, `Colour` (seat order).
- `Personalities[4+]`: `Id`, `Name`, `Description`, `BustTolerance` (percent: press on while the chance of a bust next push is at most this), `MinAdvances`, `MaxAdvances`, `BankAtTopPercent`, `TrailingBoost`, `LeadingCaution`, `PetitionBoost`, `Impulse`, tray weights `WeightStep`, `WeightProgress`, `WeightTop`, `WeightNewCatspaw`, `WeightCentral`, `WeightAdvanced`, `Noise`. The first four are dealt to the four seats by a seeded shuffle.
- `Listening`: `Responses` (5), `AccuracyThresholdPercent`, `SpeedBonusPercent`, `FastMs`, `SlowMs`, `MaxBursts`. Copied into each election when it starts, so tuning changes affect new elections only.
- `Fallbacks`: `DelegateTitle`, `DelegateNames`, `Note`. `Names`: the seeded list used for residents.
- `Disputes[11]`: `Id`, `Track` (2 to 12), `Pattern`, `Role`, `Occupations` (resident occupation ids who may lead it, `"*"` = anyone), `FallbackTitle` (local delegate title), `Props` (`prehistoric`, `historic`; later ages use `historic`), `Silhouette` (the drawn shape idea shared by the real object and its carving), `Lines` (`opening`, `progress`, `setback`, `revisit`, `agreement`, `acknowledgement`, each an array of at least two).
- `ListeningLines[60+]`: `Id`, `Tag` (`Complain`, `Encourage`, `Warn`, `Reassure`, `Question`), `Speaker`, `Text` (90 characters at most; speech-bubble sized). Evenly spread over the five intentions.

Occupation ids a resident can have come from the village's active buildings: woodcutter (log pile, woodcutter's hut), claydigger, gleaner, farmer, shepherd, quarrier (rock heap, quarry), sawyer, potter, baker, curer, stonecutter, warden; plus carrier (one per carrier living there) and storekeeper (always). No ferryman, healer, miner, well keeper or reeve exists in this economy, so those leads are local delegates (miner is filled by a quarrier when there is one).

## Rules summary the view can rely on

- Four seats in fixed order; the starter is a fair seeded choice; turns go seat 0, 1, 2, 3 (cyclic).
- A push draws four values 1 to 6 from the election's own stream (never exposed); three pairings; each pair sum is one step on that track; legality is resolved for you, so draw only what is in `Pending`.
- Up to three catspaws per sitting on distinct tracks; the first step puts a catspaw one place past the banked place (place 1 on a fresh track); a catspaw at the top cannot advance; claimed tracks are closed to all; two equal sums are one catspaw moving two places (one if the top comes first).
- After an advance: press on or bank. Reaching the top counts only once banked. A bust (no legal move) drops unbanked progress and passes the turn unless the petition is spent.
- First to three claimed tracks wins; the office holder is recorded once per village.
- Listening: a reference line then five lines, each answered Same or Different against the line before; at most one petition earned per election (accuracy plus a speed bonus reaches the threshold), spendable only after a bust, recorded once. Visiting, skipping or abandoning listening never changes the election's draws.

## Saving

Everything is plain data in `WorldState.Council` and is saved and loaded by `SaveCodec` with the world: a loaded election shows the same candidate, banked positions, in-progress sitting, pending push and trays, petition state and open listening burst. Save between ticks; queued but unapplied commands are not saved (as everywhere). Nothing was added to the save version; `SaveCodec.CreateSim` registers the council commands for loaded sims.
